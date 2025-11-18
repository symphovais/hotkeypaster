using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TalkKeys.Logging;

namespace TalkKeys.Services.Diary
{
    /// <summary>
    /// Service for managing diary entries in daily markdown files
    /// File format: diary_YYYY-MM-DD.md in %APPDATA%\TalkKeys\Diary\
    /// </summary>
    public class DiaryService : IDiaryService
    {
        private readonly ILogger _logger;
        private readonly string _diaryDirectory;
        private const string DateFormat = "yyyy-MM-dd";
        private const string TimeFormat = "HH:mm:ss";

        public DiaryService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Default diary directory: %APPDATA%\TalkKeys\Diary\
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _diaryDirectory = Path.Combine(appData, "TalkKeys", "Diary");

            // Ensure directory exists
            Directory.CreateDirectory(_diaryDirectory);
            _logger.Log($"Diary directory: {_diaryDirectory}");
        }

        public async Task<DiaryEntry> AddEntryAsync(string text, int wordCount = 0, string? language = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Diary entry text cannot be empty", nameof(text));

            var now = DateTime.Now;
            var entry = new DiaryEntry(now, text, wordCount, language);

            var filePath = GetDailyFilePath(now);
            var isNewFile = !File.Exists(filePath);

            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                // Append to daily file
                var content = new StringBuilder();

                // Add header if new file
                if (isNewFile)
                {
                    content.AppendLine($"# Diary - {now:yyyy-MM-dd}");
                    content.AppendLine();
                }

                // Add entry
                content.Append(entry.ToMarkdown());
                content.AppendLine();

                await File.AppendAllTextAsync(filePath, content.ToString(), Encoding.UTF8);

                _logger.Log($"Added diary entry: {filePath} ({wordCount} words)");
                return entry;
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to save diary entry: {ex}");
                throw new InvalidOperationException($"Failed to save diary entry: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<DiaryEntry>> GetEntriesAsync(DateTime date)
        {
            var filePath = GetDailyFilePath(date);

            if (!File.Exists(filePath))
                return Enumerable.Empty<DiaryEntry>();

            try
            {
                var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                return ParseDiaryFile(content, date);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to read diary entries for {date:yyyy-MM-dd}: {ex}");
                return Enumerable.Empty<DiaryEntry>();
            }
        }

        public async Task<IEnumerable<DiaryEntry>> SearchEntriesAsync(string query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<DiaryEntry>();

            var from = fromDate ?? DateTime.MinValue;
            var to = (toDate ?? DateTime.Now).Date.AddDays(1).AddTicks(-1); // End of day

            var allEntries = new List<DiaryEntry>();

            try
            {
                // Get all diary files in date range
                var files = Directory.GetFiles(_diaryDirectory, "diary_*.md")
                    .Select(f => new
                    {
                        Path = f,
                        Date = ParseDateFromFilename(Path.GetFileNameWithoutExtension(f))
                    })
                    .Where(f => f.Date >= from && f.Date <= to)
                    .OrderBy(f => f.Date);

                foreach (var file in files)
                {
                    var content = await File.ReadAllTextAsync(file.Path, Encoding.UTF8);
                    var entries = ParseDiaryFile(content, file.Date);

                    // Filter by query (case-insensitive)
                    var matchingEntries = entries.Where(e =>
                        e.Text.Contains(query, StringComparison.OrdinalIgnoreCase));

                    allEntries.AddRange(matchingEntries);
                }

                return allEntries.OrderBy(e => e.Timestamp);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to search diary entries: {ex}");
                return Enumerable.Empty<DiaryEntry>();
            }
        }

        public string GetDiaryDirectory() => _diaryDirectory;

        public async Task<IEnumerable<DateTime>> GetDatesWithEntriesAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    return Directory.GetFiles(_diaryDirectory, "diary_*.md")
                        .Select(f => ParseDateFromFilename(Path.GetFileNameWithoutExtension(f)))
                        .Where(d => d != DateTime.MinValue)
                        .OrderByDescending(d => d)
                        .ToList();
                });
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to get dates with entries: {ex}");
                return Enumerable.Empty<DateTime>();
            }
        }

        // Private helper methods

        private string GetDailyFilePath(DateTime date)
        {
            var filename = $"diary_{date:yyyy-MM-dd}.md";
            return Path.Combine(_diaryDirectory, filename);
        }

        private DateTime ParseDateFromFilename(string filename)
        {
            // Extract date from "diary_YYYY-MM-DD"
            var match = Regex.Match(filename, @"diary_(\d{4}-\d{2}-\d{2})");
            if (match.Success && DateTime.TryParseExact(match.Groups[1].Value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
            return DateTime.MinValue;
        }

        private IEnumerable<DiaryEntry> ParseDiaryFile(string content, DateTime date)
        {
            var entries = new List<DiaryEntry>();

            // Parse markdown entries: ## HH:mm:ss followed by text until next ##
            var entryPattern = @"##\s+(\d{2}:\d{2}:\d{2})\s*\n(.*?)(?=\n##|\z)";
            var matches = Regex.Matches(content, entryPattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var timeStr = match.Groups[1].Value;
                var text = match.Groups[2].Value.Trim();

                if (TimeSpan.TryParseExact(timeStr, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var time))
                {
                    var timestamp = date.Date.Add(time);
                    var wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

                    entries.Add(new DiaryEntry(timestamp, text, wordCount));
                }
            }

            return entries;
        }
    }
}
