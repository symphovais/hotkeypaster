using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace TalkKeys.Services.Transcription
{
    /// <summary>
    /// Groq Whisper implementation of audio transcription.
    /// Groq offers very fast Whisper inference.
    /// </summary>
    public class GroqWhisperTranscriber : ITranscriber, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string TranscriptionUrl = "https://api.groq.com/openai/v1/audio/transcriptions";
        private const string WhisperModel = "whisper-large-v3-turbo"; // Groq's fastest Whisper model

        public GroqWhisperTranscriber(string apiKey)
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

        public GroqWhisperTranscriber(string apiKey, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            _apiKey = apiKey;
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> TranscribeAsync(byte[] audioData)
        {
            // Validate input
            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));

            if (audioData.Length > 26_214_400) // 25MB limit
                throw new ArgumentException("Audio file exceeds 25MB limit", nameof(audioData));

            // Build multipart form request for Whisper
            using var form = new MultipartFormDataContent();

            // Add audio file
            var audioContent = new ByteArrayContent(audioData);
            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
            form.Add(audioContent, "file", "audio.wav");

            // Add model
            form.Add(new StringContent(WhisperModel), "model");

            // Use text format for fastest response
            form.Add(new StringContent("text"), "response_format");

            // Send request
            var response = await _httpClient.PostAsync(TranscriptionUrl, form);

            // Handle errors
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Groq Whisper API error ({response.StatusCode}): {errorContent}");
            }

            // Parse response (text format returns plain text)
            var transcription = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(transcription))
                throw new InvalidOperationException("Groq Whisper returned empty transcription");

            return transcription;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
