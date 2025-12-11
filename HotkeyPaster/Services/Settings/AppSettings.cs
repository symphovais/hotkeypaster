using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TalkKeys.PluginSdk;

namespace TalkKeys.Services.Settings
{
    /// <summary>
    /// Action types for Jabra buttons
    /// </summary>
    public enum JabraButtonAction
    {
        Disabled = 0,
        TalkKeysToggle = 1,
        KeyboardShortcut = 2,
        PushToTalk = 3
    }

    /// <summary>
    /// Recording mode for keyboard shortcut
    /// </summary>
    public enum RecordingMode
    {
        Toggle = 0,
        PushToTalk = 1
    }

    /// <summary>
    /// Authentication mode - how the app connects to transcription services
    /// </summary>
    public enum AuthMode
    {
        /// <summary>
        /// Use TalkKeys account (free tier with limits)
        /// </summary>
        TalkKeysAccount = 0,

        /// <summary>
        /// Use user's own Groq API key (unlimited)
        /// </summary>
        OwnApiKey = 1
    }

    /// <summary>
    /// Application settings that can be persisted.
    /// </summary>
    public class AppSettings
    {
        // Authentication Mode
        public AuthMode AuthMode { get; set; } = AuthMode.TalkKeysAccount;

        // TalkKeys Account (when AuthMode = TalkKeysAccount)
        public string? TalkKeysAccessToken { get; set; }
        public string? TalkKeysRefreshToken { get; set; }
        public string? TalkKeysUserEmail { get; set; }
        public string? TalkKeysUserName { get; set; }

        // Own API Key (when AuthMode = OwnApiKey)
        public string? GroqApiKey { get; set; }

        // Text Processing
        public bool EnableTextCleaning { get; set; } = true;

        // Audio Configuration
        public int AudioDeviceIndex { get; set; } = 0;

        // Floating Widget Configuration
        public bool FloatingWidgetVisible { get; set; } = true; // Visible on startup
        public double FloatingWidgetX { get; set; } = -1; // -1 = not set
        public double FloatingWidgetY { get; set; } = -1;

        // Startup Configuration
        public bool StartWithWindows { get; set; } = true; // Default to true for installer

        // Recording Mode Configuration
        public RecordingMode RecordingMode { get; set; } = RecordingMode.Toggle;

        // Jabra Headset Configuration
        public bool JabraEnabled { get; set; } = true;
        public bool JabraAutoSelectDevice { get; set; } = true;

        // Three Dot Button (⋮) Configuration
        public JabraButtonAction JabraThreeDotAction { get; set; } = JabraButtonAction.TalkKeysToggle;
        public string? JabraThreeDotShortcut { get; set; }

        // Hook Button Configuration (Legacy - kept for backwards compatibility)
        public JabraButtonAction JabraHookAction { get; set; } = JabraButtonAction.Disabled;
        public string? JabraHookShortcut { get; set; }

        // Trigger Plugin Configurations (new system)
        public Dictionary<string, TriggerPluginConfiguration> TriggerPlugins { get; set; } = new();

        // General Plugin Configurations (utility plugins like Focus Timer)
        public Dictionary<string, PluginConfiguration> Plugins { get; set; } = new();
    }

    /// <summary>
    /// Service for persisting and loading application settings.
    /// Uses in-memory caching to avoid repeated disk reads.
    /// </summary>
    public class SettingsService
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TalkKeys"
        );
        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

        private AppSettings? _cachedSettings;
        private readonly object _lock = new();

        /// <summary>
        /// Event raised when settings fail to save.
        /// </summary>
        public event EventHandler<SettingsSaveErrorEventArgs>? SaveError;

        /// <summary>
        /// Loads settings from disk, or returns defaults if not found.
        /// Uses caching to avoid repeated disk reads.
        /// </summary>
        public AppSettings LoadSettings()
        {
            lock (_lock)
            {
                if (_cachedSettings != null)
                {
                    return _cachedSettings;
                }

                try
                {
                    if (File.Exists(SettingsPath))
                    {
                        var json = File.ReadAllText(SettingsPath);
                        var settings = JsonSerializer.Deserialize<AppSettings>(json);
                        if (settings != null)
                        {
                            _cachedSettings = settings;
                            return settings;
                        }
                    }
                }
                catch
                {
                    // If loading fails, return defaults
                }

                _cachedSettings = new AppSettings();
                return _cachedSettings;
            }
        }

        /// <summary>
        /// Saves settings to disk and updates the cache.
        /// </summary>
        /// <returns>True if save succeeded, false otherwise.</returns>
        public bool SaveSettings(AppSettings settings)
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(SettingsDir);
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    File.WriteAllText(SettingsPath, json);
                    _cachedSettings = settings;
                    return true;
                }
                catch (Exception ex)
                {
                    SaveError?.Invoke(this, new SettingsSaveErrorEventArgs(ex.Message));
                    return false;
                }
            }
        }

        /// <summary>
        /// Invalidates the cache, forcing a reload from disk on next access.
        /// </summary>
        public void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedSettings = null;
            }
        }

        /// <summary>
        /// Gets the path where settings are stored.
        /// </summary>
        public string GetSettingsPath() => SettingsPath;
    }

    /// <summary>
    /// Event args for settings save errors.
    /// </summary>
    public class SettingsSaveErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; }

        public SettingsSaveErrorEventArgs(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }
}
