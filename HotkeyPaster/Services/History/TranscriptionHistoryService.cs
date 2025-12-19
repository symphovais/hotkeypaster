using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TalkKeys.Services.History
{
    /// <summary>
    /// Service for storing and retrieving recent transcription history.
    /// History is stored locally in a JSON file for privacy.
    /// </summary>
    public class TranscriptionHistoryService : ITranscriptionHistoryService
    {
        private static readonly string DefaultHistoryDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TalkKeys"
        );
        private static readonly string DefaultHistoryPath = Path.Combine(DefaultHistoryDir, "history.json");

        private readonly string _historyPath;
        private readonly int _maxEntries;
        private readonly object _lock = new();
        private List<TranscriptionRecord>? _cachedHistory;

        /// <summary>
        /// Creates a new TranscriptionHistoryService with default path.
        /// </summary>
        /// <param name="maxEntries">Maximum number of transcriptions to keep (default 20)</param>
        public TranscriptionHistoryService(int maxEntries = 20)
            : this(DefaultHistoryPath, maxEntries)
        {
        }

        /// <summary>
        /// Creates a new TranscriptionHistoryService with custom path (for testing).
        /// </summary>
        /// <param name="historyPath">Path to the history JSON file</param>
        /// <param name="maxEntries">Maximum number of transcriptions to keep</param>
        public TranscriptionHistoryService(string historyPath, int maxEntries)
        {
            _historyPath = historyPath ?? throw new ArgumentNullException(nameof(historyPath));
            _maxEntries = maxEntries;
        }

        /// <inheritdoc/>
        public void AddTranscription(string rawText, string cleanedText, double durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(rawText) && string.IsNullOrWhiteSpace(cleanedText))
                return;

            lock (_lock)
            {
                var history = LoadHistory();

                var record = new TranscriptionRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow,
                    RawText = rawText ?? string.Empty,
                    CleanedText = cleanedText ?? string.Empty,
                    DurationSeconds = durationSeconds
                };

                history.Add(record);

                // Trim to max entries (keep most recent)
                if (history.Count > _maxEntries)
                {
                    history = history
                        .OrderByDescending(r => r.Timestamp)
                        .Take(_maxEntries)
                        .ToList();
                }

                SaveHistory(history);
            }
        }

        /// <inheritdoc/>
        public List<TranscriptionRecord> GetHistory()
        {
            lock (_lock)
            {
                return LoadHistory()
                    .OrderByDescending(r => r.Timestamp)
                    .ToList();
            }
        }

        /// <inheritdoc/>
        public int GetCount()
        {
            lock (_lock)
            {
                return LoadHistory().Count;
            }
        }

        /// <inheritdoc/>
        public void ClearHistory()
        {
            lock (_lock)
            {
                _cachedHistory = new List<TranscriptionRecord>();
                SaveHistory(_cachedHistory);
            }
        }

        /// <inheritdoc/>
        public string GetHistoryPath() => _historyPath;

        /// <inheritdoc/>
        public void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedHistory = null;
            }
        }

        private List<TranscriptionRecord> LoadHistory()
        {
            if (_cachedHistory != null)
                return _cachedHistory;

            try
            {
                if (File.Exists(_historyPath))
                {
                    var json = File.ReadAllText(_historyPath);
                    var history = JsonSerializer.Deserialize<List<TranscriptionRecord>>(json);
                    if (history != null)
                    {
                        _cachedHistory = history;
                        return history;
                    }
                }
            }
            catch
            {
                // If loading fails, return empty list
            }

            _cachedHistory = new List<TranscriptionRecord>();
            return _cachedHistory;
        }

        private void SaveHistory(List<TranscriptionRecord> history)
        {
            try
            {
                var dir = Path.GetDirectoryName(_historyPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_historyPath, json);
                _cachedHistory = history;
            }
            catch
            {
                // Silently fail if we can't save history
            }
        }
    }
}
