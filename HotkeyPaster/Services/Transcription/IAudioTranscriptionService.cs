using System;
using System.Threading.Tasks;
using TalkKeys.Services.Windowing;

namespace TalkKeys.Services.Transcription
{
    /// <summary>
    /// Service for transcribing audio to clean, formatted text.
    /// </summary>
    public interface IAudioTranscriptionService
    {
        /// <summary>
        /// Transcribes audio data to formatted text.
        /// </summary>
        /// <param name="audioData">Audio data in WAV, MP3, or other supported format</param>
        /// <param name="windowContext">Optional context about where the transcription will be pasted</param>
        /// <returns>Transcription result with text and metadata</returns>
        Task<TranscriptionResult> TranscribeAsync(byte[] audioData, WindowContext? windowContext = null);

        /// <summary>
        /// Transcribes audio data with real-time progress updates.
        /// </summary>
        /// <param name="audioData">Audio data in WAV, MP3, or other supported format</param>
        /// <param name="onProgressUpdate">Callback invoked with partial text as it's being processed</param>
        /// <param name="windowContext">Optional context about where the transcription will be pasted</param>
        /// <returns>Transcription result with text and metadata</returns>
        Task<TranscriptionResult> TranscribeStreamingAsync(byte[] audioData, Action<string> onProgressUpdate, WindowContext? windowContext = null);
    }

    /// <summary>
    /// Result of audio transcription operation.
    /// </summary>
    public class TranscriptionResult
    {
        /// <summary>
        /// The transcribed and formatted text.
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// Detected language of the audio (e.g., "en", "es", "fr").
        /// </summary>
        public string? Language { get; init; }

        /// <summary>
        /// Duration of the audio in seconds.
        /// </summary>
        public double? DurationSeconds { get; init; }

        /// <summary>
        /// Number of words in the transcription.
        /// </summary>
        public int WordCount { get; init; }

        /// <summary>
        /// When the transcription was completed (UTC).
        /// </summary>
        public DateTime CompletedAt { get; init; }

        /// <summary>
        /// Whether the transcription was successful.
        /// </summary>
        public bool IsSuccess { get; init; }
    }
}
