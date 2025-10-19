using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace HotkeyPaster.Services.Transcription
{
    /// <summary>
    /// OpenAI GPT-based implementation of text cleaning and formatting.
    /// </summary>
    public class OpenAIGPTTextCleaner : ITextCleaner, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string ChatUrl = "https://api.openai.com/v1/chat/completions";
        private const string CleanupModel = "gpt-4.1-nano-2025-04-14";
        
        // Cleanup prompt for GPT-4.1-nano - only fix issues, don't add content
        private const string CleanupPrompt = 
            "Clean up the following transcribed text. " +
            "ONLY fix issues - do NOT add new content or information. " +
            "Remove filler words (um, uh, like, you know, I mean). " +
            "Remove or replace profanity and inappropriate language. " +
            "Fix grammar errors and add proper punctuation. " +
            "Ensure proper capitalization. " +
            "Make it professional and workplace-appropriate. " +
            "Keep the original meaning and length - just clean it up.";

        public OpenAIGPTTextCleaner(string apiKey)
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

        public OpenAIGPTTextCleaner(string apiKey, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            _apiKey = apiKey;
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> CleanAsync(string rawText, Action<string>? onProgressUpdate = null)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                throw new ArgumentException("Raw text cannot be null or empty", nameof(rawText));

            // Build JSON request for GPT-4.1-nano with streaming
            var requestBody = new
            {
                model = CleanupModel,
                messages = new[]
                {
                    new { role = "system", content = CleanupPrompt },
                    new { role = "user", content = rawText }
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
            
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            // Handle errors
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"GPT cleanup error ({response.StatusCode}): {errorContent}");
            }

            // Read streaming response
            var cleanedText = new System.Text.StringBuilder();
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new System.IO.StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                        continue;
                    
                    var data = line.Substring(6); // Remove "data: " prefix
                    
                    if (data == "[DONE]")
                        break;
                    
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
                throw new InvalidOperationException("GPT returned empty cleaned text");

            return result;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
