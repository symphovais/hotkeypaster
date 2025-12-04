using System;
using System.IO;
using System.Text.Json;

namespace TalkKeys.Services.Settings
{
    /// <summary>
    /// Application settings that can be persisted.
    /// </summary>
    public class AppSettings
    {
        // API Configuration
        public string? OpenAIApiKey { get; set; }

        // Text Processing
        public bool EnableTextCleaning { get; set; } = true;

        // Audio Configuration
        public int AudioDeviceIndex { get; set; } = 0;

        // Floating Widget Configuration
        public bool FloatingWidgetVisible { get; set; } = true; // Visible on startup
        public double FloatingWidgetX { get; set; } = -1; // -1 = not set
        public double FloatingWidgetY { get; set; } = -1;
    }

    /// <summary>
    /// Service for persisting and loading application settings.
    /// </summary>
    public class SettingsService
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TalkKeys"
        );
        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

        /// <summary>
        /// Loads settings from disk, or returns defaults if not found.
        /// </summary>
        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch
            {
                // If loading fails, return defaults
            }

            return new AppSettings();
        }

        /// <summary>
        /// Saves settings to disk.
        /// </summary>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Silently fail - settings won't persist
            }
        }

        /// <summary>
        /// Gets the path where settings are stored.
        /// </summary>
        public string GetSettingsPath() => SettingsPath;
    }
}
