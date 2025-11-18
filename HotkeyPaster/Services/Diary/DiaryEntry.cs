using System;

namespace TalkKeys.Services.Diary
{
    /// <summary>
    /// Represents a single diary entry with timestamp and text
    /// </summary>
    public class DiaryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Text { get; set; }
        public int WordCount { get; set; }
        public string? Language { get; set; }

        public DiaryEntry(DateTime timestamp, string text, int wordCount = 0, string? language = null)
        {
            Timestamp = timestamp;
            Text = text ?? throw new ArgumentNullException(nameof(text));
            WordCount = wordCount;
            Language = language;
        }

        /// <summary>
        /// Gets the date portion of the timestamp (for grouping by day)
        /// </summary>
        public DateTime Date => Timestamp.Date;

        /// <summary>
        /// Formats the entry as markdown with timestamp header
        /// </summary>
        public string ToMarkdown()
        {
            return $"## {Timestamp:HH:mm:ss}\n{Text}\n";
        }
    }
}
