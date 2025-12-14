using System;
using System.IO;
using System.Text.Json;
using TalkKeys.Services.Settings;
using Xunit;

namespace TalkKeys.Tests
{
    /// <summary>
    /// Integration tests for AppSettings and SettingsService.
    /// </summary>
    public class SettingsServiceTests : IDisposable
    {
        private readonly string _testSettingsPath;

        public SettingsServiceTests()
        {
            _testSettingsPath = Path.Combine(Path.GetTempPath(), $"TalkKeysTestSettings_{Guid.NewGuid()}.json");
        }

        public void Dispose()
        {
            if (File.Exists(_testSettingsPath))
            {
                try { File.Delete(_testSettingsPath); } catch { }
            }
        }

        [Fact]
        public void AppSettings_DefaultValues_AreCorrect()
        {
            var settings = new AppSettings();

            Assert.Equal(AuthMode.TalkKeysAccount, settings.AuthMode);
            Assert.Null(settings.GroqApiKey);
            Assert.Equal(0, settings.AudioDeviceIndex);
            Assert.True(settings.EnableTextCleaning);
            Assert.True(settings.FloatingWidgetVisible);
            Assert.Equal(RecordingMode.Toggle, settings.RecordingMode);
        }

        [Fact]
        public void AppSettings_CanSerializeAndDeserialize()
        {
            var original = new AppSettings
            {
                AuthMode = AuthMode.OwnApiKey,
                GroqApiKey = "test-api-key",
                AudioDeviceIndex = 2,
                FloatingWidgetX = 100.5,
                FloatingWidgetY = 200.5,
                EnableTextCleaning = false,
                RecordingMode = RecordingMode.PushToTalk
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(original.AuthMode, deserialized.AuthMode);
            Assert.Equal(original.GroqApiKey, deserialized.GroqApiKey);
            Assert.Equal(original.AudioDeviceIndex, deserialized.AudioDeviceIndex);
            Assert.Equal(original.FloatingWidgetX, deserialized.FloatingWidgetX);
            Assert.Equal(original.FloatingWidgetY, deserialized.FloatingWidgetY);
            Assert.Equal(original.EnableTextCleaning, deserialized.EnableTextCleaning);
            Assert.Equal(original.RecordingMode, deserialized.RecordingMode);
        }

        [Fact]
        public void AppSettings_SaveAndLoad_PreservesValues()
        {
            var original = new AppSettings
            {
                AuthMode = AuthMode.OwnApiKey,
                GroqApiKey = "test-key-123",
                AudioDeviceIndex = 1,
                RecordingMode = RecordingMode.PushToTalk
            };

            // Save
            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testSettingsPath, json);

            // Load
            var loadedJson = File.ReadAllText(_testSettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(loadedJson);

            Assert.NotNull(loaded);
            Assert.Equal(original.AuthMode, loaded.AuthMode);
            Assert.Equal(original.GroqApiKey, loaded.GroqApiKey);
            Assert.Equal(original.AudioDeviceIndex, loaded.AudioDeviceIndex);
            Assert.Equal(original.RecordingMode, loaded.RecordingMode);
        }

        [Fact]
        public void AppSettings_LoadingCorruptedJson_Throws()
        {
            File.WriteAllText(_testSettingsPath, "{ invalid json }");

            Assert.Throws<JsonException>(() =>
            {
                var json = File.ReadAllText(_testSettingsPath);
                JsonSerializer.Deserialize<AppSettings>(json);
            });
        }

        [Fact]
        public void AppSettings_LoadingPartialJson_UsesDefaultsForMissing()
        {
            // JSON with only some properties
            var partialJson = @"{ ""GroqApiKey"": ""partial-key"" }";
            File.WriteAllText(_testSettingsPath, partialJson);

            var json = File.ReadAllText(_testSettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(loaded);
            Assert.Equal("partial-key", loaded.GroqApiKey);
            // Other properties should have defaults
            Assert.Equal(0, loaded.AudioDeviceIndex);
            Assert.Equal(AuthMode.TalkKeysAccount, loaded.AuthMode);
        }

        [Fact]
        public void SettingsService_LoadSettings_ReturnsDefaultsWhenNoFile()
        {
            var service = new SettingsService();
            var settings = service.LoadSettings();

            Assert.NotNull(settings);
            Assert.Equal(AuthMode.TalkKeysAccount, settings.AuthMode);
        }

        [Fact]
        public void SettingsService_GetSettingsPath_ReturnsValidPath()
        {
            var service = new SettingsService();
            var path = service.GetSettingsPath();

            Assert.NotNull(path);
            Assert.NotEmpty(path);
            Assert.EndsWith("settings.json", path);
        }

        [Fact]
        public void AuthMode_EnumValues_AreCorrect()
        {
            Assert.Equal(0, (int)AuthMode.TalkKeysAccount);
            Assert.Equal(1, (int)AuthMode.OwnApiKey);
        }

        [Fact]
        public void RecordingMode_EnumValues_AreCorrect()
        {
            Assert.Equal(0, (int)RecordingMode.Toggle);
            Assert.Equal(1, (int)RecordingMode.PushToTalk);
        }

        [Fact]
        public void AppSettings_LastSeenVersion_DefaultsToNull()
        {
            var settings = new AppSettings();
            Assert.Null(settings.LastSeenVersion);
        }

        [Fact]
        public void AppSettings_LastSeenVersion_CanSerializeAndDeserialize()
        {
            var original = new AppSettings
            {
                LastSeenVersion = "1.1.0"
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("1.1.0", deserialized.LastSeenVersion);
        }

        [Fact]
        public void AppSettings_LastSeenVersion_PreservedOnSaveAndLoad()
        {
            var original = new AppSettings
            {
                LastSeenVersion = "1.0.8",
                GroqApiKey = "test-key"
            };

            // Save
            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testSettingsPath, json);

            // Load
            var loadedJson = File.ReadAllText(_testSettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(loadedJson);

            Assert.NotNull(loaded);
            Assert.Equal("1.0.8", loaded.LastSeenVersion);
            Assert.Equal("test-key", loaded.GroqApiKey);
        }

        [Fact]
        public void AppSettings_LegacySettingsWithoutLastSeenVersion_LoadsAsNull()
        {
            // Simulate loading settings from before LastSeenVersion was added
            var legacyJson = @"{ ""GroqApiKey"": ""legacy-key"", ""AudioDeviceIndex"": 1 }";
            File.WriteAllText(_testSettingsPath, legacyJson);

            var json = File.ReadAllText(_testSettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(loaded);
            Assert.Null(loaded.LastSeenVersion); // Should be null for legacy settings
            Assert.Equal("legacy-key", loaded.GroqApiKey);
        }
    }
}
