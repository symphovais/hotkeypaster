using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TalkKeys.Services.History;
using TalkKeys.Services.Settings;
using Xunit;

namespace TalkKeys.Tests
{
    /// <summary>
    /// Integration tests for Words List feature including transcription history,
    /// words list settings, and import/export validation.
    /// </summary>
    public class WordsListTests : IDisposable
    {
        private readonly string _testHistoryPath;
        private readonly string _testSettingsPath;

        public WordsListTests()
        {
            var testId = Guid.NewGuid().ToString();
            _testHistoryPath = Path.Combine(Path.GetTempPath(), $"TalkKeysTestHistory_{testId}.json");
            _testSettingsPath = Path.Combine(Path.GetTempPath(), $"TalkKeysTestSettings_{testId}.json");
        }

        public void Dispose()
        {
            try { if (File.Exists(_testHistoryPath)) File.Delete(_testHistoryPath); } catch { }
            try { if (File.Exists(_testSettingsPath)) File.Delete(_testSettingsPath); } catch { }
        }

        #region AppSettings WordsList Tests

        [Fact]
        public void AppSettings_WordsList_DefaultsToEmpty()
        {
            var settings = new AppSettings();

            Assert.NotNull(settings.WordsList);
            Assert.Empty(settings.WordsList);
        }

        [Fact]
        public void AppSettings_WordsList_CanAddWords()
        {
            var settings = new AppSettings();
            settings.WordsList.Add("Claude Code");
            settings.WordsList.Add("Anthropic");

            Assert.Equal(2, settings.WordsList.Count);
            Assert.Contains("Claude Code", settings.WordsList);
            Assert.Contains("Anthropic", settings.WordsList);
        }

        [Fact]
        public void AppSettings_WordsList_SerializesAndDeserializes()
        {
            var original = new AppSettings();
            original.WordsList = new List<string>
            {
                "Claude Code",
                "Anthropic",
                "Kubernetes",
                "TalkKeys"
            };

            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testSettingsPath, json);

            var loadedJson = File.ReadAllText(_testSettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(loadedJson);

            Assert.NotNull(loaded);
            Assert.Equal(4, loaded.WordsList.Count);
            Assert.Contains("Claude Code", loaded.WordsList);
            Assert.Contains("Anthropic", loaded.WordsList);
            Assert.Contains("Kubernetes", loaded.WordsList);
            Assert.Contains("TalkKeys", loaded.WordsList);
        }

        [Fact]
        public void AppSettings_WordsList_PreservedWithOtherSettings()
        {
            var original = new AppSettings
            {
                AuthMode = AuthMode.OwnApiKey,
                GroqApiKey = "test-key",
                WordsList = new List<string> { "TestWord", "AnotherWord" }
            };

            var json = JsonSerializer.Serialize(original);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(loaded);
            Assert.Equal(AuthMode.OwnApiKey, loaded.AuthMode);
            Assert.Equal("test-key", loaded.GroqApiKey);
            Assert.Equal(2, loaded.WordsList.Count);
        }

        [Fact]
        public void AppSettings_TranscriptionHistoryLimit_DefaultsTo20()
        {
            var settings = new AppSettings();

            Assert.Equal(20, settings.TranscriptionHistoryLimit);
        }

        [Fact]
        public void AppSettings_TranscriptionHistoryLimit_CanBeChanged()
        {
            var settings = new AppSettings
            {
                TranscriptionHistoryLimit = 50
            };

            var json = JsonSerializer.Serialize(settings);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(loaded);
            Assert.Equal(50, loaded.TranscriptionHistoryLimit);
        }

        #endregion

        #region TranscriptionHistoryService Tests

        [Fact]
        public void TranscriptionHistoryService_NewService_HasEmptyHistory()
        {
            var service = new TranscriptionHistoryService(_testHistoryPath, 20);

            Assert.Equal(0, service.GetCount());
            Assert.Empty(service.GetHistory());
        }

        [Fact]
        public void TranscriptionHistoryService_AddTranscription_IncreasesCount()
        {
            var service = new TranscriptionHistoryService(_testHistoryPath, 20);

            service.AddTranscription("raw text", "cleaned text", 5.5);

            Assert.Equal(1, service.GetCount());
        }

        [Fact]
        public void TranscriptionHistoryService_AddTranscription_StoresCorrectData()
        {
            var service = new TranscriptionHistoryService(_testHistoryPath, 20);

            service.AddTranscription("hello world", "Hello, World!", 3.2);
            var history = service.GetHistory();

            Assert.Single(history);
            var record = history[0];
            Assert.Equal("hello world", record.RawText);
            Assert.Equal("Hello, World!", record.CleanedText);
            Assert.Equal(3.2, record.DurationSeconds);
            Assert.NotEqual(default, record.Timestamp);
            Assert.NotNull(record.Id);
        }

        [Fact]
        public void TranscriptionHistoryService_RespectsLimit()
        {
            var service = new TranscriptionHistoryService(_testHistoryPath, 3);

            service.AddTranscription("first", "first", 1.0);
            service.AddTranscription("second", "second", 2.0);
            service.AddTranscription("third", "third", 3.0);
            service.AddTranscription("fourth", "fourth", 4.0);

            var history = service.GetHistory();

            Assert.Equal(3, history.Count);
            // Most recent should be first
            Assert.Equal("fourth", history[0].RawText);
            Assert.Equal("third", history[1].RawText);
            Assert.Equal("second", history[2].RawText);
        }

        [Fact]
        public void TranscriptionHistoryService_ClearHistory_RemovesAllRecords()
        {
            var service = new TranscriptionHistoryService(_testHistoryPath, 20);

            service.AddTranscription("test1", "test1", 1.0);
            service.AddTranscription("test2", "test2", 2.0);

            Assert.Equal(2, service.GetCount());

            service.ClearHistory();

            Assert.Equal(0, service.GetCount());
            Assert.Empty(service.GetHistory());
        }

        [Fact]
        public void TranscriptionHistoryService_PersistsToFile()
        {
            // Create service and add data
            var service1 = new TranscriptionHistoryService(_testHistoryPath, 20);
            service1.AddTranscription("persistent", "Persistent text", 5.0);

            // Create new service pointing to same file
            var service2 = new TranscriptionHistoryService(_testHistoryPath, 20);
            var history = service2.GetHistory();

            Assert.Single(history);
            Assert.Equal("persistent", history[0].RawText);
        }

        [Fact]
        public void TranscriptionHistoryService_GetHistoryPath_ReturnsCorrectPath()
        {
            var service = new TranscriptionHistoryService(_testHistoryPath, 20);

            Assert.Equal(_testHistoryPath, service.GetHistoryPath());
        }

        [Fact]
        public void TranscriptionHistoryService_EmptyTranscription_IsSkipped()
        {
            var service = new TranscriptionHistoryService(_testHistoryPath, 20);

            service.AddTranscription("", "", 0.0);

            // Empty transcriptions are skipped (intentional behavior)
            Assert.Equal(0, service.GetCount());
        }

        [Fact]
        public void TranscriptionHistoryService_PartiallyEmptyTranscription_IsSaved()
        {
            var service = new TranscriptionHistoryService(_testHistoryPath, 20);

            // Has raw text but no cleaned text - should still be saved
            service.AddTranscription("some text", "", 1.0);

            Assert.Equal(1, service.GetCount());
        }

        [Fact]
        public void TranscriptionHistoryService_InvalidateCache_ReloadsFromFile()
        {
            var service = new TranscriptionHistoryService(_testHistoryPath, 20);
            service.AddTranscription("cached", "Cached", 1.0);

            // Manually modify the file
            var records = new List<TranscriptionRecord>
            {
                new TranscriptionRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    RawText = "modified",
                    CleanedText = "Modified",
                    DurationSeconds = 2.0,
                    Timestamp = DateTime.UtcNow
                }
            };
            File.WriteAllText(_testHistoryPath, JsonSerializer.Serialize(records));

            // Invalidate cache
            service.InvalidateCache();

            var history = service.GetHistory();
            Assert.Single(history);
            Assert.Equal("modified", history[0].RawText);
        }

        #endregion

        #region TranscriptionRecord Tests

        [Fact]
        public void TranscriptionRecord_DefaultId_IsGenerated()
        {
            var record = new TranscriptionRecord();

            Assert.NotNull(record.Id);
            Assert.NotEmpty(record.Id);
        }

        [Fact]
        public void TranscriptionRecord_DefaultTimestamp_IsUtcNow()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var record = new TranscriptionRecord();
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.True(record.Timestamp >= before);
            Assert.True(record.Timestamp <= after);
        }

        [Fact]
        public void TranscriptionRecord_SerializesAndDeserializes()
        {
            var original = new TranscriptionRecord
            {
                Id = "test-id",
                RawText = "raw text here",
                CleanedText = "Cleaned text here",
                DurationSeconds = 10.5,
                Timestamp = new DateTime(2024, 1, 15, 12, 30, 0, DateTimeKind.Utc)
            };

            var json = JsonSerializer.Serialize(original);
            var loaded = JsonSerializer.Deserialize<TranscriptionRecord>(json);

            Assert.NotNull(loaded);
            Assert.Equal(original.Id, loaded.Id);
            Assert.Equal(original.RawText, loaded.RawText);
            Assert.Equal(original.CleanedText, loaded.CleanedText);
            Assert.Equal(original.DurationSeconds, loaded.DurationSeconds);
            Assert.Equal(original.Timestamp, loaded.Timestamp);
        }

        #endregion

        #region Import Validation Tests

        [Fact]
        public void ImportValidation_ValidTextFile_Parses()
        {
            var content = "Claude Code\nAnthropic\nKubernetes\n";
            var lines = ParseImportContent(content);

            Assert.Equal(3, lines.Count);
            Assert.Contains("Claude Code", lines);
            Assert.Contains("Anthropic", lines);
            Assert.Contains("Kubernetes", lines);
        }

        [Fact]
        public void ImportValidation_EmptyLines_AreSkipped()
        {
            var content = "Word1\n\n\nWord2\n\nWord3";
            var lines = ParseImportContent(content);

            Assert.Equal(3, lines.Count);
        }

        [Fact]
        public void ImportValidation_WhitespaceLines_AreTrimmed()
        {
            var content = "  Word1  \n\tWord2\t\n  Word3  ";
            var lines = ParseImportContent(content);

            Assert.Equal(3, lines.Count);
            Assert.Contains("Word1", lines);
            Assert.Contains("Word2", lines);
            Assert.Contains("Word3", lines);
        }

        [Fact]
        public void ImportValidation_Duplicates_AreRemoved()
        {
            var content = "Word\nWord\nWORD\nword";
            var lines = ParseImportContent(content);

            // Case-insensitive dedup should give us just one entry
            Assert.Single(lines);
        }

        [Fact]
        public void ImportValidation_MixedLineEndings_AreHandled()
        {
            var content = "Word1\r\nWord2\rWord3\nWord4";
            var lines = ParseImportContent(content);

            Assert.Equal(4, lines.Count);
        }

        [Fact]
        public void ImportValidation_BinaryContent_Detected()
        {
            var content = "Word1\n\0Word2\n"; // Contains null character
            var hasBinary = HasBinaryContent(content);

            Assert.True(hasBinary);
        }

        [Fact]
        public void ImportValidation_TextWithTabs_NotDetectedAsBinary()
        {
            var content = "Word1\tNote\nWord2\tAnother";
            var hasBinary = HasBinaryContent(content);

            Assert.False(hasBinary);
        }

        // Helper methods that mirror the import validation logic
        private static List<string> ParseImportContent(string content)
        {
            return content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(l => l.Trim())
                         .Where(l => !string.IsNullOrEmpty(l))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .ToList();
        }

        private static bool HasBinaryContent(string content)
        {
            return content.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');
        }

        #endregion

        #region Export Format Tests

        [Fact]
        public void ExportFormat_WordsList_OneWordPerLine()
        {
            var words = new List<string> { "Claude Code", "Anthropic", "Kubernetes" };
            var exported = string.Join(Environment.NewLine, words);

            Assert.Contains("Claude Code", exported);
            Assert.Contains("Anthropic", exported);
            Assert.Contains("Kubernetes", exported);

            // Should be able to reimport
            var reimported = ParseImportContent(exported);
            Assert.Equal(3, reimported.Count);
        }

        [Fact]
        public void ExportFormat_EmptyList_ReturnsEmpty()
        {
            var words = new List<string>();
            var exported = string.Join(Environment.NewLine, words);

            Assert.Equal(string.Empty, exported);
        }

        #endregion
    }
}
