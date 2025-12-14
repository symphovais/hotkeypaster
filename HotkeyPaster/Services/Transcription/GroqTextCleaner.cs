using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TalkKeys.Services.Resilience;
using TalkKeys.Services.Windowing;

namespace TalkKeys.Services.Transcription
{
    /// <summary>
    /// Groq-based implementation of text cleaning and formatting using Llama models.
    /// Groq offers very fast LLM inference.
    /// </summary>
    public class GroqTextCleaner : ITextCleaner, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string ChatUrl = "https://api.groq.com/openai/v1/chat/completions";
        private const string CleanupModel = "openai/gpt-oss-20b"; // Fast OpenAI open model on Groq

        // Cleanup prompt for text cleaning
        private const string CleanupPrompt =
            "You are a text cleaning assistant for a voice-to-text transcription application called TalkKeys. " +
            "The user speaks into their microphone, and the audio is transcribed to text. Your job is to clean up the raw transcription. " +
            "\n\nRULES:\n" +
            "1. ONLY fix issues - do NOT add new content or information that wasn't spoken\n" +
            "2. Remove filler words (um, uh, like, you know, I mean, sort of, kind of)\n" +
            "3. Fix grammar errors and add proper punctuation\n" +
            "4. Ensure proper capitalization\n" +
            "5. Remove or replace profanity and inappropriate language\n" +
            "6. Keep the original meaning and approximate length\n" +
            "7. If context about the target application is provided, adjust tone and formality accordingly\n" +
            "8. Return ONLY the cleaned text, no explanations or meta-commentary";

        public GroqTextCleaner(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            _apiKey = apiKey;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public GroqTextCleaner(string apiKey, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            _apiKey = apiKey;
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> CleanAsync(string rawText, Action<string>? onProgressUpdate = null, WindowContext? windowContext = null)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                throw new ArgumentException("Raw text cannot be null or empty", nameof(rawText));

            // Build system prompt with optional context
            var systemPrompt = CleanupPrompt;
            if (windowContext != null && windowContext.IsValid)
            {
                var contextInfo = windowContext.GetContextPrompt();
                if (!string.IsNullOrEmpty(contextInfo))
                {
                    systemPrompt = $"{CleanupPrompt}\n\n{contextInfo}";
                }
            }

            // Use Polly resilience pipeline for transient error handling
            var response = await HttpResilience.ExecuteWithRetryAsync(
                async ct =>
                {
                    // Build JSON request for Groq with streaming (must be inside retry lambda - HttpContent is single-use)
                    var requestBody = new
                    {
                        model = CleanupModel,
                        messages = new[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = $"Clean this transcription:\n\n{rawText}" }
                        },
                        temperature = 0.3,
                        max_tokens = 500,
                        stream = true
                    };

                    var jsonContent = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );

                    // Send request with streaming
                    var request = new HttpRequestMessage(HttpMethod.Post, ChatUrl)
                    {
                        Content = jsonContent
                    };

                    return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                },
                logger: null,
                CancellationToken.None);

            // Handle errors (non-transient errors that weren't retried)
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Groq text cleanup error ({response.StatusCode}): {errorContent}");
            }

            // Read streaming response with optimized parsing
            var cleanedText = new System.Text.StringBuilder();
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new System.IO.StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();

                    // Optimization: Pre-filter before attempting JSON parsing
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                        continue;

                    var data = line.Substring(6); // Remove "data: " prefix

                    if (data == "[DONE]")
                        break;

                    // Optimization: Only parse if it looks like valid JSON (starts with '{')
                    if (!data.StartsWith("{"))
                        continue;

                    try
                    {
                        using var jsonDoc = JsonDocument.Parse(data);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var delta = choices[0].GetProperty("delta");
                            if (delta.TryGetProperty("content", out var content))
                            {
                                var chunk = content.GetString();
                                if (!string.IsNullOrEmpty(chunk))
                                {
                                    cleanedText.Append(chunk);

                                    // Invoke progress callback with current accumulated text
                                    onProgressUpdate?.Invoke(cleanedText.ToString());
                                }
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Skip malformed JSON chunks
                        continue;
                    }
                }
            }

            var result = cleanedText.ToString().Trim();

            if (string.IsNullOrWhiteSpace(result))
                throw new InvalidOperationException("Groq returned empty cleaned text");

            return result;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
