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
        // Pipeline Selection
        public string SelectedPipeline { get; set; } = "MaximumQuality"; // Default to best quality

        // API & Model Configuration
        public string? LocalModelPath { get; set; }
        public string? OpenAIApiKey { get; set; }

        // Text Processing
        public bool EnableTextCleaning { get; set; } = true;
    }

    /// <summary>
    /// Available pipeline presets.
    /// </summary>
    public static class PipelinePresets
    {
        public const string MaximumQuality = "MaximumQuality";      // RNNoise + VAD + Cloud
        public const string BalancedQuality = "BalancedQuality";    // RNNoise only + Cloud
        public const string FastCloud = "FastCloud";                // Cloud only
        public const string MaximumPrivacy = "MaximumPrivacy";      // RNNoise + VAD + Local
        public const string FastLocal = "FastLocal";                // Local only
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
