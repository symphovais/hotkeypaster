using System;
using System.Threading.Tasks;
using TalkKeys.Services;
using TalkKeys.Services.Windowing;

namespace TalkKeys.Services.Transcription
{
    /// <summary>
    /// Composable audio transcription service that uses injected transcriber and text cleaner.
    /// </summary>
    public class OpenAITranscriptionService : IAudioTranscriptionService, IReportProgress
    {
        private readonly ITranscriber _transcriber;
        private readonly ITextCleaner _textCleaner;

        public event EventHandler<ProgressEventArgs>? ProgressChanged;

        public OpenAITranscriptionService(ITranscriber transcriber, ITextCleaner textCleaner)
        {
            _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
            _textCleaner = textCleaner ?? throw new ArgumentNullException(nameof(textCleaner));
        }

        protected virtual void OnProgressChanged(string message, int? percentComplete = null)
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs(message, percentComplete));
        }

        public async Task<TranscriptionResult> TranscribeAsync(byte[] audioData, WindowContext? windowContext = null)
        {
            // Use streaming version without progress callback
            return await TranscribeStreamingAsync(audioData, null, windowContext);
        }

        public async Task<TranscriptionResult> TranscribeStreamingAsync(byte[] audioData, Action<string>? onProgressUpdate, WindowContext? windowContext = null)
        {
            // Validate input
            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));

            if (audioData.Length > 26_214_400) // 25MB OpenAI limit
                throw new ArgumentException("Audio file exceeds 25MB limit", nameof(audioData));

            try
            {
                // Step 1: Transcribe audio to raw text
                OnProgressChanged("Transcribing audio...", 0);
                string rawTranscription = await _transcriber.TranscribeAsync(audioData);
                
                // Step 2: Clean up text (with streaming progress and context)
                OnProgressChanged("Cleaning up text...", 50);
                string cleanedText = await _textCleaner.CleanAsync(rawTranscription, onProgressUpdate, windowContext);
                
                OnProgressChanged("Complete", 100);
                
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
