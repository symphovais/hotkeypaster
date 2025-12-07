using System;
using System.Collections.Generic;
using System.Windows;

namespace TalkKeys.PluginSdk
{
    /// <summary>
    /// Action to perform when a trigger fires.
    /// </summary>
    public enum RecordingTriggerAction
    {
        /// <summary>Trigger is disabled.</summary>
        Disabled = 0,

        /// <summary>Toggle recording on/off.</summary>
        ToggleRecording = 1,

        /// <summary>Push-to-talk mode (record while held, stop on release).</summary>
        PushToTalk = 2,

        /// <summary>Send a keyboard shortcut.</summary>
        KeyboardShortcut = 3
    }

    /// <summary>
    /// Event arguments for trigger events.
    /// </summary>
    public class TriggerEventArgs : EventArgs
    {
        /// <summary>
        /// Unique identifier for this trigger (e.g., "keyboard:hotkey", "jabra:three-dot").
        /// </summary>
        public string TriggerId { get; }

        /// <summary>
        /// The action configured for this trigger.
        /// </summary>
        public RecordingTriggerAction Action { get; }

        /// <summary>
        /// Optional keyboard shortcut to send (when Action is KeyboardShortcut).
        /// </summary>
        public string? KeyboardShortcut { get; }

        /// <summary>
        /// When the trigger was activated.
        /// </summary>
        public DateTime Timestamp { get; }

        public TriggerEventArgs(string triggerId, RecordingTriggerAction action, string? keyboardShortcut = null)
        {
            TriggerId = triggerId;
            Action = action;
            KeyboardShortcut = keyboardShortcut;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Configuration for a single trigger within a plugin.
    /// </summary>
    public class TriggerConfiguration
    {
        /// <summary>
        /// Unique identifier for this trigger.
        /// </summary>
        public string TriggerId { get; set; } = "";

        /// <summary>
        /// Display name for this trigger.
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Whether this trigger is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Action to perform when triggered.
        /// </summary>
        public RecordingTriggerAction Action { get; set; } = RecordingTriggerAction.ToggleRecording;

        /// <summary>
        /// Keyboard shortcut to send (when Action is KeyboardShortcut).
        /// </summary>
        public string? KeyboardShortcut { get; set; }

        /// <summary>
        /// Plugin-specific settings.
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    /// <summary>
    /// Configuration for an entire trigger plugin.
    /// </summary>
    public class TriggerPluginConfiguration
    {
        /// <summary>
        /// Plugin identifier.
        /// </summary>
        public string PluginId { get; set; } = "";

        /// <summary>
        /// Whether the plugin is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Configuration for each trigger in the plugin.
        /// </summary>
        public List<TriggerConfiguration> Triggers { get; set; } = new();

        /// <summary>
        /// Plugin-wide settings.
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    /// <summary>
    /// Interface for trigger plugins that can start/stop recording.
    /// Implement this interface to create a custom trigger plugin for TalkKeys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To create a plugin:
    /// 1. Create a class library project targeting net8.0-windows
    /// 2. Reference TalkKeys.PluginSdk
    /// 3. Implement ITriggerPlugin
    /// 4. Build and copy the DLL to %APPDATA%\TalkKeys\Plugins
    /// </para>
    /// </remarks>
    public interface ITriggerPlugin : IDisposable
    {
        /// <summary>
        /// Unique identifier for this plugin (e.g., "keyboard", "jabra", "foot-pedal").
        /// Must be unique across all plugins.
        /// </summary>
        string PluginId { get; }

        /// <summary>
        /// Display name for the plugin (shown in settings UI).
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Description of what this plugin does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Icon character or emoji for the plugin (for tab display).
        /// Examples: "⌨️", "🎧", "🦶"
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// Whether the plugin's hardware/software is available.
        /// For example, a headset plugin would return false if the headset is not connected.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Status message (e.g., "Connected", "Device not found", "Ready").
        /// </summary>
        string StatusMessage { get; }

        /// <summary>
        /// Gets information about the triggers this plugin provides.
        /// </summary>
        IReadOnlyList<TriggerInfo> GetAvailableTriggers();

        /// <summary>
        /// Initialize the plugin with configuration loaded from settings.
        /// </summary>
        /// <param name="configuration">The saved configuration, or null if no configuration exists.</param>
        void Initialize(TriggerPluginConfiguration configuration);

        /// <summary>
        /// Start listening for triggers.
        /// Called when the application starts or when the plugin is enabled.
        /// </summary>
        void Start();

        /// <summary>
        /// Stop listening for triggers.
        /// Called when the application exits or when the plugin is disabled.
        /// </summary>
        void Stop();

        /// <summary>
        /// Update configuration while running.
        /// Called when the user changes settings.
        /// </summary>
        void UpdateConfiguration(TriggerPluginConfiguration configuration);

        /// <summary>
        /// Get the current configuration.
        /// </summary>
        TriggerPluginConfiguration GetConfiguration();

        /// <summary>
        /// Get the default configuration for this plugin.
        /// Called when no saved configuration exists.
        /// </summary>
        TriggerPluginConfiguration GetDefaultConfiguration();

        /// <summary>
        /// Create the settings UI panel for this plugin.
        /// This panel is displayed when the user selects this plugin in Settings → Triggers.
        /// </summary>
        /// <returns>A WPF FrameworkElement (typically a StackPanel) containing the settings UI.</returns>
        FrameworkElement CreateSettingsPanel();

        /// <summary>
        /// Event raised when a trigger is activated (pressed/started).
        /// </summary>
        event EventHandler<TriggerEventArgs>? TriggerActivated;

        /// <summary>
        /// Event raised when a trigger is deactivated (released/stopped).
        /// Only needed for push-to-talk style triggers.
        /// </summary>
        event EventHandler<TriggerEventArgs>? TriggerDeactivated;

        /// <summary>
        /// Event raised when the plugin's availability status changes.
        /// For example, when a device is connected or disconnected.
        /// </summary>
        event EventHandler<EventArgs>? AvailabilityChanged;
    }

    /// <summary>
    /// Information about a single trigger within a plugin.
    /// </summary>
    public class TriggerInfo
    {
        /// <summary>
        /// Unique identifier for this trigger within the plugin.
        /// Convention: "pluginid:triggername" (e.g., "jabra:three-dot")
        /// </summary>
        public string TriggerId { get; set; } = "";

        /// <summary>
        /// Display name for this trigger.
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Description of this trigger.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Whether this trigger supports push-to-talk (release detection).
        /// </summary>
        public bool SupportsPushToTalk { get; set; }

        /// <summary>
        /// Whether this trigger can send keyboard shortcuts.
        /// </summary>
        public bool SupportsKeyboardShortcut { get; set; }
    }

    /// <summary>
    /// Simple logger interface for plugins.
    /// </summary>
    public interface IPluginLogger
    {
        void Log(string message);
    }
}
