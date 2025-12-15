using System;
using System.IO;
using System.Text.Json;
using TalkKeys.PluginSdk;
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

        #region Plugin Configuration Tests

        [Fact]
        public void PluginConfiguration_DefaultValues_AreCorrect()
        {
            var config = new PluginConfiguration();

            Assert.Equal("", config.PluginId);
            Assert.True(config.Enabled);
            Assert.True(config.WidgetVisible);
            Assert.Equal(-1, config.WidgetX);
            Assert.Equal(-1, config.WidgetY);
            Assert.NotNull(config.Settings);
            Assert.Empty(config.Settings);
        }

        [Fact]
        public void PluginConfiguration_GetSetting_ReturnsDefaultWhenKeyNotFound()
        {
            var config = new PluginConfiguration();

            var stringResult = config.GetSetting("nonexistent", "default");
            var intResult = config.GetSetting("nonexistent", 42);
            var boolResult = config.GetSetting("nonexistent", true);

            Assert.Equal("default", stringResult);
            Assert.Equal(42, intResult);
            Assert.True(boolResult);
        }

        [Fact]
        public void PluginConfiguration_SetAndGetSetting_StringValue()
        {
            var config = new PluginConfiguration();

            config.SetSetting("Hotkey", "Ctrl+Win+E");
            var result = config.GetSetting("Hotkey", "default");

            Assert.Equal("Ctrl+Win+E", result);
        }

        [Fact]
        public void PluginConfiguration_SetAndGetSetting_IntValue()
        {
            var config = new PluginConfiguration();

            config.SetSetting("AutoDismissSeconds", 20);
            var result = config.GetSetting("AutoDismissSeconds", 8);

            Assert.Equal(20, result);
        }

        [Fact]
        public void PluginConfiguration_SetAndGetSetting_BoolValue()
        {
            var config = new PluginConfiguration();

            config.SetSetting("ShowPopup", false);
            var result = config.GetSetting("ShowPopup", true);

            Assert.False(result);
        }

        [Fact]
        public void PluginConfiguration_SetSetting_NullRemovesKey()
        {
            var config = new PluginConfiguration();
            config.SetSetting("Key", "value");

            Assert.True(config.Settings.ContainsKey("Key"));

            config.SetSetting<string?>("Key", null);

            Assert.False(config.Settings.ContainsKey("Key"));
        }

        [Fact]
        public void PluginConfiguration_CanSerializeAndDeserialize()
        {
            var original = new PluginConfiguration
            {
                PluginId = "explainer",
                Enabled = true,
                WidgetVisible = false,
                WidgetX = 100.5,
                WidgetY = 200.5
            };
            original.SetSetting("Hotkey", "Ctrl+Win+E");
            original.SetSetting("AutoDismissSeconds", 20);

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<PluginConfiguration>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(original.PluginId, deserialized.PluginId);
            Assert.Equal(original.Enabled, deserialized.Enabled);
            Assert.Equal(original.WidgetVisible, deserialized.WidgetVisible);
            Assert.Equal(original.WidgetX, deserialized.WidgetX);
            Assert.Equal(original.WidgetY, deserialized.WidgetY);
        }

        [Fact]
        public void PluginConfiguration_GetSetting_HandlesJsonElementAfterDeserialization()
        {
            var original = new PluginConfiguration { PluginId = "test" };
            original.SetSetting("StringValue", "hello");
            original.SetSetting("IntValue", 42);
            original.SetSetting("BoolValue", true);
            original.SetSetting("DoubleValue", 3.14);

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<PluginConfiguration>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("hello", deserialized.GetSetting("StringValue", "default"));
            Assert.Equal(42, deserialized.GetSetting("IntValue", 0));
            Assert.True(deserialized.GetSetting("BoolValue", false));
            Assert.Equal(3.14, deserialized.GetSetting("DoubleValue", 0.0), 2);
        }

        [Fact]
        public void AppSettings_PluginsDictionary_CanStoreAndRetrieveConfiguration()
        {
            var settings = new AppSettings();

            var explainerConfig = new PluginConfiguration
            {
                PluginId = "explainer",
                Enabled = true
            };
            explainerConfig.SetSetting("Hotkey", "Ctrl+Win+E");

            settings.Plugins["explainer"] = explainerConfig;

            Assert.True(settings.Plugins.ContainsKey("explainer"));
            Assert.Equal("Ctrl+Win+E", settings.Plugins["explainer"].GetSetting("Hotkey", ""));
        }

        [Fact]
        public void AppSettings_PluginsDictionary_SerializesAndDeserializesCorrectly()
        {
            var original = new AppSettings();
            var explainerConfig = new PluginConfiguration
            {
                PluginId = "explainer",
                Enabled = true
            };
            explainerConfig.SetSetting("Hotkey", "Ctrl+Shift+E");
            explainerConfig.SetSetting("AutoDismissSeconds", 15);
            original.Plugins["explainer"] = explainerConfig;

            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testSettingsPath, json);

            var loadedJson = File.ReadAllText(_testSettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(loadedJson);

            Assert.NotNull(loaded);
            Assert.True(loaded.Plugins.ContainsKey("explainer"));

            var loadedConfig = loaded.Plugins["explainer"];
            Assert.Equal("explainer", loadedConfig.PluginId);
            Assert.True(loadedConfig.Enabled);
            Assert.Equal("Ctrl+Shift+E", loadedConfig.GetSetting("Hotkey", ""));
            Assert.Equal(15, loadedConfig.GetSetting("AutoDismissSeconds", 8));
        }

        #endregion

        #region TalkKeys Account Tests

        [Fact]
        public void AppSettings_TalkKeysAccount_DefaultsToEmptyCredentials()
        {
            var settings = new AppSettings();

            Assert.Null(settings.TalkKeysAccessToken);
            Assert.Null(settings.TalkKeysRefreshToken);
            Assert.Null(settings.TalkKeysUserEmail);
            Assert.Null(settings.TalkKeysUserName);
        }

        [Fact]
        public void AppSettings_TalkKeysAccount_CanSetAndRetrieveCredentials()
        {
            var settings = new AppSettings
            {
                AuthMode = AuthMode.TalkKeysAccount,
                TalkKeysAccessToken = "test-access-token",
                TalkKeysRefreshToken = "test-refresh-token",
                TalkKeysUserEmail = "test@example.com",
                TalkKeysUserName = "Test User"
            };

            Assert.Equal(AuthMode.TalkKeysAccount, settings.AuthMode);
            Assert.Equal("test-access-token", settings.TalkKeysAccessToken);
            Assert.Equal("test-refresh-token", settings.TalkKeysRefreshToken);
            Assert.Equal("test@example.com", settings.TalkKeysUserEmail);
            Assert.Equal("Test User", settings.TalkKeysUserName);
        }

        [Fact]
        public void AppSettings_TalkKeysAccount_SerializesAndDeserializesCorrectly()
        {
            var original = new AppSettings
            {
                AuthMode = AuthMode.TalkKeysAccount,
                TalkKeysAccessToken = "access-token-123",
                TalkKeysRefreshToken = "refresh-token-456",
                TalkKeysUserEmail = "user@talkkeys.com",
                TalkKeysUserName = "John Doe"
            };

            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testSettingsPath, json);

            var loadedJson = File.ReadAllText(_testSettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(loadedJson);

            Assert.NotNull(loaded);
            Assert.Equal(AuthMode.TalkKeysAccount, loaded.AuthMode);
            Assert.Equal("access-token-123", loaded.TalkKeysAccessToken);
            Assert.Equal("refresh-token-456", loaded.TalkKeysRefreshToken);
            Assert.Equal("user@talkkeys.com", loaded.TalkKeysUserEmail);
            Assert.Equal("John Doe", loaded.TalkKeysUserName);
        }

        [Fact]
        public void AppSettings_TalkKeysAccount_ClearingCredentialsSetsToNull()
        {
            var settings = new AppSettings
            {
                TalkKeysAccessToken = "token",
                TalkKeysUserEmail = "email",
                TalkKeysUserName = "name"
            };

            // Simulate sign-out
            settings.TalkKeysAccessToken = null;
            settings.TalkKeysRefreshToken = null;
            settings.TalkKeysUserEmail = null;
            settings.TalkKeysUserName = null;

            Assert.Null(settings.TalkKeysAccessToken);
            Assert.Null(settings.TalkKeysRefreshToken);
            Assert.Null(settings.TalkKeysUserEmail);
            Assert.Null(settings.TalkKeysUserName);
        }

        [Fact]
        public void AppSettings_AuthMode_SwitchingModes_PreservesOtherSettings()
        {
            var settings = new AppSettings
            {
                AuthMode = AuthMode.TalkKeysAccount,
                TalkKeysAccessToken = "token",
                TalkKeysUserEmail = "email@test.com",
                GroqApiKey = "groq-key",
                AudioDeviceIndex = 2
            };

            // Switch to OwnApiKey mode
            settings.AuthMode = AuthMode.OwnApiKey;

            // TalkKeys credentials should still be there (not cleared automatically)
            Assert.Equal(AuthMode.OwnApiKey, settings.AuthMode);
            Assert.Equal("token", settings.TalkKeysAccessToken);
            Assert.Equal("groq-key", settings.GroqApiKey);
            Assert.Equal(2, settings.AudioDeviceIndex);
        }

        #endregion

        #region Comprehensive Settings Tests

        [Fact]
        public void AppSettings_FullConfiguration_SavesAndLoadsAllProperties()
        {
            var original = new AppSettings
            {
                // Auth
                AuthMode = AuthMode.TalkKeysAccount,
                GroqApiKey = "groq-api-key",
                TalkKeysAccessToken = "access-token",
                TalkKeysRefreshToken = "refresh-token",
                TalkKeysUserEmail = "user@example.com",
                TalkKeysUserName = "Test User",

                // Audio
                AudioDeviceIndex = 2,
                EnableTextCleaning = false,

                // Recording
                RecordingMode = RecordingMode.PushToTalk,

                // Widget
                FloatingWidgetVisible = false,
                FloatingWidgetX = 150.5,
                FloatingWidgetY = 250.5,

                // Version
                LastSeenVersion = "1.0.8"
            };

            // Add plugin configuration
            var explainerConfig = new PluginConfiguration
            {
                PluginId = "explainer",
                Enabled = true
            };
            explainerConfig.SetSetting("Hotkey", "Ctrl+Alt+E");
            explainerConfig.SetSetting("AutoDismissSeconds", 25);
            original.Plugins["explainer"] = explainerConfig;

            // Save
            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testSettingsPath, json);

            // Load
            var loadedJson = File.ReadAllText(_testSettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(loadedJson);

            Assert.NotNull(loaded);

            // Verify all properties
            Assert.Equal(AuthMode.TalkKeysAccount, loaded.AuthMode);
            Assert.Equal("groq-api-key", loaded.GroqApiKey);
            Assert.Equal("access-token", loaded.TalkKeysAccessToken);
            Assert.Equal("refresh-token", loaded.TalkKeysRefreshToken);
            Assert.Equal("user@example.com", loaded.TalkKeysUserEmail);
            Assert.Equal("Test User", loaded.TalkKeysUserName);
            Assert.Equal(2, loaded.AudioDeviceIndex);
            Assert.False(loaded.EnableTextCleaning);
            Assert.Equal(RecordingMode.PushToTalk, loaded.RecordingMode);
            Assert.False(loaded.FloatingWidgetVisible);
            Assert.Equal(150.5, loaded.FloatingWidgetX);
            Assert.Equal(250.5, loaded.FloatingWidgetY);
            Assert.Equal("1.0.8", loaded.LastSeenVersion);

            // Verify plugin config
            Assert.True(loaded.Plugins.ContainsKey("explainer"));
            var loadedPlugin = loaded.Plugins["explainer"];
            Assert.Equal("Ctrl+Alt+E", loadedPlugin.GetSetting("Hotkey", ""));
            Assert.Equal(25, loadedPlugin.GetSetting("AutoDismissSeconds", 0));
        }

        [Fact]
        public void AppSettings_EmptyPluginsDictionary_DeserializesCorrectly()
        {
            var json = @"{ ""Plugins"": {} }";
            File.WriteAllText(_testSettingsPath, json);

            var loadedJson = File.ReadAllText(_testSettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(loadedJson);

            Assert.NotNull(loaded);
            Assert.NotNull(loaded.Plugins);
            Assert.Empty(loaded.Plugins);
        }

        [Fact]
        public void AppSettings_MultiplePlugins_AllSerializeCorrectly()
        {
            var original = new AppSettings();

            var plugin1 = new PluginConfiguration { PluginId = "plugin1", Enabled = true };
            plugin1.SetSetting("Setting1", "value1");

            var plugin2 = new PluginConfiguration { PluginId = "plugin2", Enabled = false };
            plugin2.SetSetting("Setting2", 100);

            original.Plugins["plugin1"] = plugin1;
            original.Plugins["plugin2"] = plugin2;

            var json = JsonSerializer.Serialize(original);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(loaded);
            Assert.Equal(2, loaded.Plugins.Count);
            Assert.True(loaded.Plugins["plugin1"].Enabled);
            Assert.False(loaded.Plugins["plugin2"].Enabled);
            Assert.Equal("value1", loaded.Plugins["plugin1"].GetSetting("Setting1", ""));
            Assert.Equal(100, loaded.Plugins["plugin2"].GetSetting("Setting2", 0));
        }

        #endregion
    }
}
