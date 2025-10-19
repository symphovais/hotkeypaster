using System;
using System.IO;
using System.Text.Json;

namespace HotkeyPaster.Services.Settings
{
    /// <summary>
    /// Application settings that can be persisted.
    /// </summary>
    public class AppSettings
    {
        public TranscriptionMode TranscriptionMode { get; set; } = TranscriptionMode.Cloud;
        public string? LocalModelPath { get; set; }
        public bool EnableTextCleaning { get; set; } = true;
        public string? OpenAIApiKey { get; set; }
    }

    /// <summary>
    /// Transcription mode selection.
    /// </summary>
    public enum TranscriptionMode
    {
        Cloud,
        Local
    }

    /// <summary>
    /// Service for persisting and loading application settings.
    /// </summary>
    public class SettingsService
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HotkeyPaster"
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
