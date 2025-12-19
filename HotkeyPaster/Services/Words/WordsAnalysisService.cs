using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TalkKeys.Logging;
using TalkKeys.Services.Auth;
using TalkKeys.Services.History;
using TalkKeys.Services.Resilience;

namespace TalkKeys.Services.Words
{
    /// <summary>
    /// Result of analyzing transcriptions for word suggestions
    /// </summary>
    public class WordsAnalysisResult
    {
        public bool Success { get; set; }
        public List<string> SuggestedWords { get; set; } = new();
        public string? Error { get; set; }
    }

    /// <summary>
    /// Service for analyzing transcription history to find words that might need correct spellings
    /// </summary>
    public class WordsAnalysisService
    {
        private const string GroqChatUrl = "https://api.groq.com/openai/v1/chat/completions";

        private readonly string? _groqApiKey;
        private readonly TalkKeysApiService? _talkKeysApiService;
        private readonly HttpClient _httpClient;
        private readonly ILogger? _logger;

        /// <summary>
        /// Create service for direct Groq API access (own API key mode)
        /// </summary>
        public WordsAnalysisService(string groqApiKey, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(groqApiKey))
                throw new ArgumentException("Groq API key cannot be null or empty", nameof(groqApiKey));

            _groqApiKey = groqApiKey;
            _logger = logger;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
        }

        /// <summary>
        /// Create service for TalkKeys API proxy access (free tier mode)
        /// </summary>
        public WordsAnalysisService(TalkKeysApiService talkKeysApiService, ILogger? logger = null)
        {
            _talkKeysApiService = talkKeysApiService ?? throw new ArgumentNullException(nameof(talkKeysApiService));
            _logger = logger;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
        }

        /// <summary>
        /// Analyze transcription history to find words that might need correct spellings
        /// </summary>
        public async Task<WordsAnalysisResult> AnalyzeAsync(
            IReadOnlyList<TranscriptionRecord> history,
            IReadOnlyList<string>? existingWords = null,
            CancellationToken cancellationToken = default)
        {
            if (history == null || history.Count == 0)
            {
                return new WordsAnalysisResult
                {
                    Success = false,
                    Error = "No transcription history to analyze"
                };
            }

            try
            {
                _logger?.Log($"[Words] Analyzing {history.Count} transcriptions...");

                // Build transcription pairs for analysis
                var transcriptionPairs = history
                    .Where(r => !string.IsNullOrWhiteSpace(r.RawText) || !string.IsNullOrWhiteSpace(r.CleanedText))
                    .Select((r, i) => $"#{i + 1}\nRaw: {r.RawText}\nCleaned: {r.CleanedText}")
                    .ToList();

                if (transcriptionPairs.Count == 0)
                {
                    return new WordsAnalysisResult
                    {
                        Success = false,
                        Error = "No valid transcriptions to analyze"
                    };
                }

                var existingWordsNote = existingWords?.Count > 0
                    ? $"\n\nThe user already has these words in their list (don't suggest these again):\n{string.Join(", ", existingWords)}"
                    : "";

                var systemPrompt = @"Analyze these voice transcriptions to identify words that may need correct spellings added to the user's words list.

For each transcription, you're given:
- Raw: What Whisper heard (speech-to-text result)
- Cleaned: What the AI cleaned it to

Look for:
1. Proper nouns that might be spelled inconsistently (company names, people, products)
2. Technical terms that could be misheard (programming terms, acronyms)
3. Words that don't quite make sense in context and might be mishearings
4. Names or terms that appear multiple times with different spellings
5. Domain-specific terminology the user frequently uses" + existingWordsNote + @"

Return a JSON array of correctly-spelled words the user should add:
[""Claude Code"", ""Anthropic"", ""Kubernetes""]

IMPORTANT:
- Only suggest words you're confident the user intended to say
- Return the CORRECT spelling (what they meant, not what was transcribed)
- Return an empty array [] if no issues found
- Maximum 10 suggestions per analysis
- Output ONLY the JSON array, nothing else";

                var userPrompt = $"TRANSCRIPTIONS:\n\n{string.Join("\n\n", transcriptionPairs)}";

                if (_groqApiKey != null)
                {
                    return await AnalyzeWithGroqAsync(systemPrompt, userPrompt, cancellationToken);
                }
                else if (_talkKeysApiService != null)
                {
                    return await AnalyzeWithTalkKeysAsync(history, existingWords, cancellationToken);
                }
                else
                {
                    return new WordsAnalysisResult
                    {
                        Success = false,
                        Error = "No API configured for word analysis"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Words] Analysis error: {ex.Message}");
                return new WordsAnalysisResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private async Task<WordsAnalysisResult> AnalyzeWithGroqAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = "llama-3.1-8b-instant",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                max_tokens = 500
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, GroqChatUrl)
            {
                Content = content
            };
            request.Headers.Add("Authorization", $"Bearer {_groqApiKey}");

            var response = await HttpResilience.ExecuteWithRetryAsync(
                async ct =>
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, GroqChatUrl)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    req.Headers.Add("Authorization", $"Bearer {_groqApiKey}");
                    return await _httpClient.SendAsync(req, ct);
                },
                _logger,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger?.Log($"[Words] Groq API error: {response.StatusCode} - {error}");
                return new WordsAnalysisResult
                {
                    Success = false,
                    Error = $"API error: {response.StatusCode}"
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return ParseAnalysisResponse(responseJson);
        }

        private async Task<WordsAnalysisResult> AnalyzeWithTalkKeysAsync(
            IReadOnlyList<TranscriptionRecord> history,
            IReadOnlyList<string>? existingWords,
            CancellationToken cancellationToken)
        {
            if (_talkKeysApiService == null)
            {
                return new WordsAnalysisResult
                {
                    Success = false,
                    Error = "TalkKeys API service not configured"
                };
            }

            // Convert history to TranscriptionPair format for the API
            var transcriptionPairs = history
                .Where(r => !string.IsNullOrWhiteSpace(r.RawText) || !string.IsNullOrWhiteSpace(r.CleanedText))
                .Select(r => new TranscriptionPair
                {
                    Raw = r.RawText ?? string.Empty,
                    Cleaned = r.CleanedText ?? string.Empty
                })
                .ToList();

            if (transcriptionPairs.Count == 0)
            {
                return new WordsAnalysisResult
                {
                    Success = false,
                    Error = "No valid transcriptions to analyze"
                };
            }

            _logger?.Log($"[Words] Calling TalkKeys API to analyze {transcriptionPairs.Count} transcriptions...");

            var result = await _talkKeysApiService.AnalyzeWordsAsync(
                transcriptionPairs,
                existingWords,
                cancellationToken);

            if (result.Success)
            {
                _logger?.Log($"[Words] TalkKeys API returned {result.Suggestions.Count} suggestions");
                return new WordsAnalysisResult
                {
                    Success = true,
                    SuggestedWords = result.Suggestions
                };
            }
            else
            {
                _logger?.Log($"[Words] TalkKeys API error: {result.Error}");
                return new WordsAnalysisResult
                {
                    Success = false,
                    Error = result.Error
                };
            }
        }

        private WordsAnalysisResult ParseAnalysisResponse(string responseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var contentElement))
                    {
                        var content = contentElement.GetString()?.Trim() ?? "[]";

                        // Extract JSON array from response (handle potential markdown code blocks)
                        content = ExtractJsonArray(content);

                        var suggestions = JsonSerializer.Deserialize<List<string>>(content) ?? new List<string>();

                        _logger?.Log($"[Words] Found {suggestions.Count} suggestions");

                        return new WordsAnalysisResult
                        {
                            Success = true,
                            SuggestedWords = suggestions.Take(10).ToList()
                        };
                    }
                }

                return new WordsAnalysisResult
                {
                    Success = false,
                    Error = "Invalid response format from API"
                };
            }
            catch (JsonException ex)
            {
                _logger?.Log($"[Words] Failed to parse response: {ex.Message}");
                return new WordsAnalysisResult
                {
                    Success = false,
                    Error = "Failed to parse API response"
                };
            }
        }

        private static string ExtractJsonArray(string content)
        {
            // Remove markdown code blocks if present
            if (content.Contains("```"))
            {
                var start = content.IndexOf('[');
                var end = content.LastIndexOf(']');
                if (start >= 0 && end > start)
                {
                    content = content.Substring(start, end - start + 1);
                }
            }

            // Ensure we have a valid JSON array
            content = content.Trim();
            if (!content.StartsWith("["))
            {
                var start = content.IndexOf('[');
                if (start >= 0)
                {
                    content = content.Substring(start);
                }
            }
            if (!content.EndsWith("]"))
            {
                var end = content.LastIndexOf(']');
                if (end >= 0)
                {
                    content = content.Substring(0, end + 1);
                }
            }

            return content;
        }
    }
}
