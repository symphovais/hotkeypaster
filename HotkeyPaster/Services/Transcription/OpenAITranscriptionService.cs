using System;
using System.Threading.Tasks;

namespace HotkeyPaster.Services.Transcription
{
    /// <summary>
    /// Composable audio transcription service that uses injected transcriber and text cleaner.
    /// </summary>
    public class OpenAITranscriptionService : IAudioTranscriptionService
    {
        private readonly ITranscriber _transcriber;
        private readonly ITextCleaner _textCleaner;

        public OpenAITranscriptionService(ITranscriber transcriber, ITextCleaner textCleaner)
        {
            _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
            _textCleaner = textCleaner ?? throw new ArgumentNullException(nameof(textCleaner));
        }

        public async Task<TranscriptionResult> TranscribeAsync(byte[] audioData)
        {
            // Use streaming version without progress callback
            return await TranscribeStreamingAsync(audioData, null);
        }

        public async Task<TranscriptionResult> TranscribeStreamingAsync(byte[] audioData, Action<string>? onProgressUpdate)
        {
            // Validate input
            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));

            if (audioData.Length > 26_214_400) // 25MB OpenAI limit
                throw new ArgumentException("Audio file exceeds 25MB limit", nameof(audioData));

            try
            {
                // Step 1: Transcribe audio to raw text
                string rawTranscription = await _transcriber.TranscribeAsync(audioData);
                
                // Step 2: Clean up text (with streaming progress)
                string cleanedText = await _textCleaner.CleanAsync(rawTranscription, onProgressUpdate);
                
                // Calculate metadata
                int wordCount = cleanedText.Split(new[] { ' ', '\n', '\r', '\t' }, 
                    StringSplitOptions.RemoveEmptyEntries).Length;

                return new TranscriptionResult
                {
                    Text = cleanedText,
                    Language = "en", // Whisper doesn't return language in basic mode
                    DurationSeconds = null,
                    WordCount = wordCount,
                    CompletedAt = DateTime.UtcNow,
                    IsSuccess = true
                };
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException("Transcription request timed out after 5 minutes");
            }
        }

    }
}
