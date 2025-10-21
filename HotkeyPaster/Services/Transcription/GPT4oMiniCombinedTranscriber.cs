using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HotkeyPaster.Services.Windowing;

namespace HotkeyPaster.Services.Transcription
{
    /// <summary>
    /// Optimized transcription service using GPT-4o-mini for both audio transcription and text cleaning in a single API call.
    /// This eliminates the need for separate Whisper API + GPT cleaning calls, significantly improving performance.
    /// </summary>
    public class GPT4oMiniCombinedTranscriber : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string ChatUrl = "https://api.openai.com/v1/chat/completions";
        private const string Model = "gpt-4o-audio-preview"; // Using gpt-4o-audio-preview instead

        // Combined prompt for transcription + cleaning
        private const string SystemPrompt =
            "You are a voice transcription assistant. The user will provide an audio recording. " +
            "Your job is to transcribe the audio and return clean, formatted text ready for use.\n\n" +
            "RULES:\n" +
            "1. Transcribe the spoken audio accurately\n" +
            "2. Remove filler words (um, uh, like, you know, I mean, sort of, kind of)\n" +
            "3. Fix grammar errors and add proper punctuation\n" +
            "4. Ensure proper capitalization\n" +
            "5. Remove or replace profanity and inappropriate language\n" +
            "6. Keep the original meaning and approximate length\n" +
            "7. If context about the target application is provided, adjust tone and formality accordingly\n" +
            "8. Return ONLY the cleaned transcribed text, no explanations or meta-commentary";

        public GPT4oMiniCombinedTranscriber(string apiKey)
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

        public GPT4oMiniCombinedTranscriber(string apiKey, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            _apiKey = apiKey;
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> TranscribeAndCleanAsync(
            byte[] audioData,
            Action<string>? onProgressUpdate = null,
            WindowContext? windowContext = null)
        {
            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));

            if (audioData.Length > 26_214_400) // 25MB OpenAI limit
                throw new ArgumentException("Audio file exceeds 25MB limit", nameof(audioData));

            // Build system prompt with optional context
            var systemPrompt = SystemPrompt;
            if (windowContext != null && windowContext.IsValid)
            {
                var contextInfo = windowContext.GetContextPrompt();
                if (!string.IsNullOrEmpty(contextInfo))
                {
                    systemPrompt = $"{SystemPrompt}\n\n{contextInfo}";
                }
            }

            // Convert audio bytes to base64 for GPT-4o-audio-preview
            var base64Audio = Convert.ToBase64String(audioData);

            // Build JSON request with audio input using correct format
            var requestBody = new
            {
                model = Model,
                modalities = new[] { "text", "audio" },
                audio = new { voice = "alloy", format = "wav" },
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "input_audio",
                                input_audio = new
                                {
                                    data = base64Audio,
                                    format = "wav"
                                }
                            }
                        }
                    }
                },
                temperature = 0.3,
                max_tokens = 500,
                stream = true
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
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
                    $"GPT-4o-mini transcription error ({response.StatusCode}): {errorContent}");
            }

            // Read streaming response with optimized parsing
            var transcribedText = new StringBuilder();
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();

                    // Optimization: Pre-filter before parsing JSON
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
                                    transcribedText.Append(chunk);

                                    // Invoke progress callback with current accumulated text
                                    onProgressUpdate?.Invoke(transcribedText.ToString());
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

            var result = transcribedText.ToString().Trim();

            if (string.IsNullOrWhiteSpace(result))
                throw new InvalidOperationException("GPT-4o-mini returned empty transcription");

            return result;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
