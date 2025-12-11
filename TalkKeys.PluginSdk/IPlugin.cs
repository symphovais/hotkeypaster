using System;
using System.Windows;

namespace TalkKeys.PluginSdk
{
    /// <summary>
    /// Base interface for all general-purpose plugins.
    /// Unlike ITriggerPlugin (which is for recording triggers), IPlugin is for utility plugins
    /// like focus timers, statistics, integrations, etc.
    /// </summary>
    public interface IPlugin : IDisposable
    {
        /// <summary>
        /// Unique identifier for this plugin (e.g., "focus-timer", "stats").
        /// Must be unique across all plugins.
        /// </summary>
        string PluginId { get; }

        /// <summary>
        /// Display name shown in settings UI.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Brief description of what this plugin does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Icon character or emoji for display (e.g., "⏱️", "📊").
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// Plugin version.
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// Initialize the plugin with configuration loaded from settings.
        /// </summary>
        /// <param name="configuration">The saved configuration, or default if none exists.</param>
        void Initialize(PluginConfiguration configuration);

        /// <summary>
        /// Activate the plugin (start running).
        /// Called when the application starts or when the plugin is enabled.
        /// </summary>
        void Activate();

        /// <summary>
        /// Deactivate the plugin (stop running).
        /// Called when the application exits or when the plugin is disabled.
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Get the current configuration.
        /// </summary>
        PluginConfiguration GetConfiguration();

        /// <summary>
        /// Get the default configuration for this plugin.
        /// Called when no saved configuration exists.
        /// </summary>
        PluginConfiguration GetDefaultConfiguration();

        /// <summary>
        /// Create the settings UI panel for this plugin.
        /// This panel is displayed in Settings → Plugins when the user selects this plugin.
        /// </summary>
        /// <returns>A WPF FrameworkElement (typically a StackPanel) containing the settings UI, or null if no settings.</returns>
        FrameworkElement? CreateSettingsPanel();
    }
}
