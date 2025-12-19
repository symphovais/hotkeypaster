using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using TalkKeys.Logging;
using TalkKeys.Services.Resilience;
using TalkKeys.Services.Settings;

namespace TalkKeys.Services.Auth
{
    /// <summary>
    /// Usage information from the API
    /// </summary>
    public class UsageInfo
    {
        public int UsedSeconds { get; set; }
        public int LimitSeconds { get; set; }
        public int RemainingSeconds { get; set; }
        public string? ResetAt { get; set; }
    }

    /// <summary>
    /// Transcription result from the API
    /// </summary>
    public class TranscriptionResult
    {
        public bool Success { get; set; }
        public string? Text { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Text cleaning result from the API
    /// </summary>
    public class CleaningResult
    {
        public bool Success { get; set; }
        public string? CleanedText { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Plain English explanation result from the API
    /// </summary>
    public class ExplanationResult
    {
        public bool Success { get; set; }
        public string? Explanation { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Words analysis result from the API
    /// </summary>
    public class WordsAnalysisApiResult
    {
        public bool Success { get; set; }
        public List<string> Suggestions { get; set; } = new();
        public string? Error { get; set; }
    }

    /// <summary>
    /// Transcription pair for words analysis
    /// </summary>
    public class TranscriptionPair
    {
        public string Raw { get; set; } = string.Empty;
        public string Cleaned { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for making authenticated API calls to the TalkKeys backend
    /// </summary>
    public class TalkKeysApiService : IDisposable
    {
        private const string ApiBaseUrl = "https://talkkeys.symphonytek.dk";

        private readonly HttpClient _httpClient;
        private readonly SettingsService _settingsService;
        private readonly ILogger? _logger;

        public TalkKeysApiService(SettingsService settingsService, ILogger? logger = null)
        {
            _settingsService = settingsService;
            _logger = logger;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(ApiBaseUrl),
                Timeout = TimeSpan.FromMinutes(2) // Long timeout for audio uploads
            };
        }

        /// <summary>
        /// Get the current access token
        /// </summary>
        private string? GetAccessToken()
        {
            var settings = _settingsService.LoadSettings();
            return settings.TalkKeysAccessToken;
        }

        /// <summary>
        /// Set authorization header with current token
        /// </summary>
        private void SetAuthHeader(HttpRequestMessage request)
        {
            var token = GetAccessToken();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        /// <summary>
        /// Get current usage statistics
        /// </summary>
        public async Task<UsageInfo?> GetUsageAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/usage");
                SetAuthHeader(request);

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.Log($"[API] Usage request failed: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data))
                {
                    return new UsageInfo
                    {
                        UsedSeconds = data.TryGetProperty("used_seconds", out var used) ? used.GetInt32() : 0,
                        LimitSeconds = data.TryGetProperty("limit_seconds", out var limit) ? limit.GetInt32() : 600,
                        RemainingSeconds = data.TryGetProperty("remaining_seconds", out var remaining) ? remaining.GetInt32() : 0,
                        ResetAt = data.TryGetProperty("reset_at", out var reset) ? reset.GetString() : null
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[API] Usage request error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Transcribe audio using the TalkKeys proxy.
        /// Uses Polly resilience pipeline with retry and exponential backoff.
        /// </summary>
        public async Task<TranscriptionResult> TranscribeAsync(
            Stream audioStream,
            string fileName,
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Read stream into byte array to ensure proper Content-Disposition
                using var memoryStream = new MemoryStream();
                await audioStream.CopyToAsync(memoryStream, cancellationToken);
                var audioBytes = memoryStream.ToArray();

                _logger?.Log("[API] Sending transcription request...");

                // Use Polly resilience pipeline for transient error handling
                var response = await HttpResilience.ExecuteTranscriptionWithRetryAsync(
                    async ct =>
                    {
                        using var content = new MultipartFormDataContent();
                        var fileContent = new ByteArrayContent(audioBytes);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                        fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                        {
                            Name = "\"file\"",
                            FileName = $"\"{fileName}\""
                        };
                        content.Add(fileContent);

                        if (!string.IsNullOrEmpty(language))
                        {
                            content.Add(new StringContent(language), "language");
                        }

                        var request = new HttpRequestMessage(HttpMethod.Post, "/api/whisper")
                        {
                            Content = content
                        };
                        SetAuthHeader(request);

                        return await _httpClient.SendAsync(request, ct);
                    },
                    _logger,
                    cancellationToken);

                var responseJson = await response.Content.ReadAsStringAsync();

                _logger?.Log($"[API] Transcription response: {response.StatusCode}");

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

                if (success && root.TryGetProperty("data", out var data))
                {
                    var text = data.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
                    return new TranscriptionResult { Success = true, Text = text };
                }
                else
                {
                    var error = root.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : "Unknown error";
                    return new TranscriptionResult { Success = false, Error = error };
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[API] Transcription error: {ex.Message}");
                return new TranscriptionResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Clean/format text using the TalkKeys proxy.
        /// Uses Polly resilience pipeline with retry and exponential backoff.
        /// </summary>
        public async Task<CleaningResult> CleanTextAsync(
            string text,
            string? context = null,
            IReadOnlyList<string>? wordsList = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.Log("[API] Sending text cleaning request...");

                // Use Polly resilience pipeline for transient error handling
                var response = await HttpResilience.ExecuteWithRetryAsync(
                    async ct =>
                    {
                        var requestBody = new
                        {
                            text = text,
                            context = context,
                            wordsList = wordsList
                        };

                        var json = JsonSerializer.Serialize(requestBody);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var request = new HttpRequestMessage(HttpMethod.Post, "/api/clean")
                        {
                            Content = content
                        };
                        SetAuthHeader(request);

                        return await _httpClient.SendAsync(request, ct);
                    },
                    _logger,
                    cancellationToken);

                var responseJson = await response.Content.ReadAsStringAsync();

                _logger?.Log($"[API] Clean response: {response.StatusCode}");

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

                if (success && root.TryGetProperty("data", out var data))
                {
                    var cleanedText = data.TryGetProperty("cleaned_text", out var textProp) ? textProp.GetString() : text;
                    return new CleaningResult { Success = true, CleanedText = cleanedText };
                }
                else
                {
                    var error = root.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : "Unknown error";
                    return new CleaningResult { Success = false, Error = error };
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[API] Clean error: {ex.Message}");
                return new CleaningResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Get a plain English explanation of text using the TalkKeys proxy.
        /// Uses Polly resilience pipeline with retry and exponential backoff.
        /// </summary>
        public async Task<ExplanationResult> ExplainTextAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.Log("[API] Sending explanation request...");

                // Use Polly resilience pipeline for transient error handling
                var response = await HttpResilience.ExecuteWithRetryAsync(
                    async ct =>
                    {
                        var requestBody = new { text = text };
                        var json = JsonSerializer.Serialize(requestBody);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var request = new HttpRequestMessage(HttpMethod.Post, "/api/explain")
                        {
                            Content = content
                        };
                        SetAuthHeader(request);

                        return await _httpClient.SendAsync(request, ct);
                    },
                    _logger,
                    cancellationToken);

                var responseJson = await response.Content.ReadAsStringAsync();

                _logger?.Log($"[API] Explain response: {response.StatusCode}");

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

                if (success && root.TryGetProperty("data", out var data))
                {
                    var explanation = data.TryGetProperty("explanation", out var textProp) ? textProp.GetString() : null;
                    return new ExplanationResult { Success = true, Explanation = explanation };
                }
                else
                {
                    var error = root.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : "Unknown error";
                    return new ExplanationResult { Success = false, Error = error };
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[API] Explain error: {ex.Message}");
                return new ExplanationResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Analyze transcriptions to find suggested words.
        /// Uses Polly resilience pipeline with retry and exponential backoff.
        /// </summary>
        public async Task<WordsAnalysisApiResult> AnalyzeWordsAsync(
            IReadOnlyList<TranscriptionPair> transcriptions,
            IReadOnlyList<string>? existingWords = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.Log($"[API] Sending words analysis request ({transcriptions.Count} transcriptions)...");

                // Use Polly resilience pipeline for transient error handling
                var response = await HttpResilience.ExecuteWithRetryAsync(
                    async ct =>
                    {
                        var requestBody = new
                        {
                            transcriptions = transcriptions.Select(t => new { raw = t.Raw, cleaned = t.Cleaned }).ToArray(),
                            existingWords = existingWords
                        };

                        var json = JsonSerializer.Serialize(requestBody);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var request = new HttpRequestMessage(HttpMethod.Post, "/api/analyze-words")
                        {
                            Content = content
                        };
                        SetAuthHeader(request);

                        return await _httpClient.SendAsync(request, ct);
                    },
                    _logger,
                    cancellationToken);

                var responseJson = await response.Content.ReadAsStringAsync();

                _logger?.Log($"[API] Analyze words response: {response.StatusCode}");

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

                if (success && root.TryGetProperty("data", out var data))
                {
                    var suggestions = new List<string>();
                    if (data.TryGetProperty("suggestions", out var suggestionsArray))
                    {
                        foreach (var item in suggestionsArray.EnumerateArray())
                        {
                            var word = item.GetString();
                            if (!string.IsNullOrEmpty(word))
                            {
                                suggestions.Add(word);
                            }
                        }
                    }
                    return new WordsAnalysisApiResult { Success = true, Suggestions = suggestions };
                }
                else
                {
                    var error = root.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : "Unknown error";
                    return new WordsAnalysisApiResult { Success = false, Error = error };
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[API] Analyze words error: {ex.Message}");
                return new WordsAnalysisApiResult { Success = false, Error = ex.Message };
            }
        }

        /// <summary>
        /// Check if the API is reachable and user is authenticated
        /// </summary>
        public async Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var usage = await GetUsageAsync(cancellationToken);
                return usage != null;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
