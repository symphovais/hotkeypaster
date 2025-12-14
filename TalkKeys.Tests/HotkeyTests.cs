using System;
using System.Collections.Generic;
using System.Text.Json;
using TalkKeys.PluginSdk;
using TalkKeys.Services.Settings;
using Xunit;

namespace TalkKeys.Tests
{
    /// <summary>
    /// Tests for hotkey parsing and configuration handling.
    /// Includes regression test for the JsonElement deserialization issue.
    /// </summary>
    public class HotkeyTests
    {
        [Fact]
        public void TriggerConfiguration_WithStringHotkey_WorksCorrectly()
        {
            var config = new TriggerConfiguration
            {
                TriggerId = "keyboard:hotkey",
                Enabled = true,
                Settings = new Dictionary<string, object>
                {
                    { "Hotkey", "Ctrl+Shift+Space" }
                }
            };

            Assert.True(config.Settings.TryGetValue("Hotkey", out var value));
            Assert.IsType<string>(value);
            Assert.Equal("Ctrl+Shift+Space", value);
        }

        /// <summary>
        /// REGRESSION TEST: This tests the fix for hotkeys resetting on app restart.
        /// When settings are deserialized from JSON, the hotkey value comes as JsonElement,
        /// not as a string. The fix handles both types.
        /// </summary>
        [Fact]
        public void TriggerConfiguration_AfterJsonDeserialization_HotkeyIsJsonElement()
        {
            // Simulate what happens when settings are loaded from file
            var json = @"{
                ""TriggerId"": ""keyboard:hotkey"",
                ""DisplayName"": ""Keyboard Hotkey"",
                ""Enabled"": true,
                ""Action"": 1,
                ""Settings"": {
                    ""Hotkey"": ""Alt+Space"",
                    ""Mode"": ""Toggle""
                }
            }";

            var config = JsonSerializer.Deserialize<TriggerConfiguration>(json);

            Assert.NotNull(config);
            Assert.Equal("keyboard:hotkey", config.TriggerId);
            Assert.True(config.Settings.TryGetValue("Hotkey", out var hotkeyValue));

            // After JSON deserialization, the value is a JsonElement, not a string
            Assert.IsType<JsonElement>(hotkeyValue);

            // The fix: Handle JsonElement and extract the string value
            string? hotkey = null;
            if (hotkeyValue is string hotkeyStr)
            {
                hotkey = hotkeyStr;
            }
            else if (hotkeyValue is JsonElement jsonElement &&
                     jsonElement.ValueKind == JsonValueKind.String)
            {
                hotkey = jsonElement.GetString();
            }

            Assert.Equal("Alt+Space", hotkey);
        }

        [Fact]
        public void TriggerConfiguration_JsonRoundTrip_PreservesSettings()
        {
            var original = new TriggerConfiguration
            {
                TriggerId = "keyboard:hotkey",
                DisplayName = "Keyboard Hotkey",
                Enabled = true,
                Action = RecordingTriggerAction.ToggleRecording,
                Settings = new Dictionary<string, object>
                {
                    { "Hotkey", "Ctrl+Alt+R" },
                    { "Mode", "Toggle" }
                }
            };

            var json = JsonSerializer.Serialize(original);
            var loaded = JsonSerializer.Deserialize<TriggerConfiguration>(json);

            Assert.NotNull(loaded);
            Assert.Equal(original.TriggerId, loaded.TriggerId);
            Assert.Equal(original.Enabled, loaded.Enabled);
            Assert.True(loaded.Settings.TryGetValue("Hotkey", out var hotkeyValue));

            // Extract hotkey handling both string and JsonElement
            string? hotkey = ExtractStringValue(hotkeyValue);
            Assert.Equal("Ctrl+Alt+R", hotkey);
        }

        [Fact]
        public void TriggerPluginConfiguration_JsonRoundTrip_PreservesAllSettings()
        {
            var original = new TriggerPluginConfiguration
            {
                PluginId = "keyboard",
                Enabled = true,
                Triggers = new List<TriggerConfiguration>
                {
                    new TriggerConfiguration
                    {
                        TriggerId = "keyboard:hotkey",
                        DisplayName = "Keyboard Hotkey",
                        Enabled = true,
                        Action = RecordingTriggerAction.ToggleRecording,
                        Settings = new Dictionary<string, object>
                        {
                            { "Hotkey", "Ctrl+Shift+Space" }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            var loaded = JsonSerializer.Deserialize<TriggerPluginConfiguration>(json);

            Assert.NotNull(loaded);
            Assert.Equal(original.PluginId, loaded.PluginId);
            Assert.Equal(original.Enabled, loaded.Enabled);
            Assert.Single(loaded.Triggers);

            var trigger = loaded.Triggers[0];
            Assert.Equal("keyboard:hotkey", trigger.TriggerId);
            Assert.True(trigger.Settings.TryGetValue("Hotkey", out var hotkeyValue));

            string? hotkey = ExtractStringValue(hotkeyValue);
            Assert.Equal("Ctrl+Shift+Space", hotkey);
        }

        [Fact]
        public void AppSettings_WithTriggerPlugins_SerializesCorrectly()
        {
            var original = new AppSettings
            {
                AuthMode = AuthMode.OwnApiKey,
                GroqApiKey = "test-key",
                AudioDeviceIndex = 1,
                FloatingWidgetX = 100.5,
                FloatingWidgetY = 200.5
            };

            // Add a keyboard trigger plugin configuration
            original.TriggerPlugins["keyboard"] = new TriggerPluginConfiguration
            {
                PluginId = "keyboard",
                Enabled = true,
                Triggers = new List<TriggerConfiguration>
                {
                    new TriggerConfiguration
                    {
                        TriggerId = "keyboard:hotkey",
                        DisplayName = "Keyboard Hotkey",
                        Enabled = true,
                        Action = RecordingTriggerAction.ToggleRecording,
                        Settings = new Dictionary<string, object>
                        {
                            { "Hotkey", "Ctrl+Space" }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions { WriteIndented = true });
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(loaded);
            Assert.Equal(original.AuthMode, loaded.AuthMode);
            Assert.Equal(original.GroqApiKey, loaded.GroqApiKey);
            Assert.Equal(original.AudioDeviceIndex, loaded.AudioDeviceIndex);
            Assert.Equal(original.FloatingWidgetX, loaded.FloatingWidgetX);
            Assert.Equal(original.FloatingWidgetY, loaded.FloatingWidgetY);
            Assert.True(loaded.TriggerPlugins.ContainsKey("keyboard"));

            var keyboardPlugin = loaded.TriggerPlugins["keyboard"];
            Assert.NotNull(keyboardPlugin);
            Assert.Equal("keyboard", keyboardPlugin.PluginId);
            Assert.Single(keyboardPlugin.Triggers);
        }

        [Fact]
        public void RecordingTriggerAction_EnumValues_AreCorrect()
        {
            Assert.Equal(0, (int)RecordingTriggerAction.Disabled);
            Assert.Equal(1, (int)RecordingTriggerAction.ToggleRecording);
            Assert.Equal(2, (int)RecordingTriggerAction.PushToTalk);
            Assert.Equal(3, (int)RecordingTriggerAction.KeyboardShortcut);
        }

        [Fact]
        public void ExtractStringValue_HandlesString()
        {
            object value = "test string";
            var result = ExtractStringValue(value);
            Assert.Equal("test string", result);
        }

        [Fact]
        public void ExtractStringValue_HandlesJsonElement()
        {
            var json = "\"test value\"";
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            var result = ExtractStringValue(element);
            Assert.Equal("test value", result);
        }

        [Fact]
        public void ExtractStringValue_ReturnsNullForNonString()
        {
            object value = 123;
            var result = ExtractStringValue(value);
            Assert.Null(result);
        }

        /// <summary>
        /// Helper method that mirrors the fix in KeyboardTriggerPlugin.ApplyConfiguration()
        /// </summary>
        private static string? ExtractStringValue(object? value)
        {
            if (value is string str)
            {
                return str;
            }
            else if (value is JsonElement jsonElement &&
                     jsonElement.ValueKind == JsonValueKind.String)
            {
                return jsonElement.GetString();
            }
            return null;
        }
    }
}
