using System.Collections.Generic;

namespace TalkKeys.Services.History
{
    /// <summary>
    /// Record of a single transcription for history tracking.
    /// </summary>
    public class TranscriptionRecord
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public System.DateTime Timestamp { get; set; } = System.DateTime.UtcNow;
        public string RawText { get; set; } = string.Empty;
        public string CleanedText { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
    }

    /// <summary>
    /// Interface for storing and retrieving recent transcription history.
    /// </summary>
    public interface ITranscriptionHistoryService
    {
        /// <summary>
        /// Adds a new transcription to the history.
        /// </summary>
        void AddTranscription(string rawText, string cleanedText, double durationSeconds);

        /// <summary>
        /// Gets all transcription records, most recent first.
        /// </summary>
        List<TranscriptionRecord> GetHistory();

        /// <summary>
        /// Gets the count of stored transcriptions.
        /// </summary>
        int GetCount();

        /// <summary>
        /// Clears all transcription history.
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// Gets the path where history is stored.
        /// </summary>
        string GetHistoryPath();

        /// <summary>
        /// Invalidates the cache, forcing a reload from disk.
        /// </summary>
        void InvalidateCache();
    }
}
