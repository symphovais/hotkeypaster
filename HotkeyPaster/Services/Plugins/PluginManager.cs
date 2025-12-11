using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TalkKeys.Logging;
using TalkKeys.PluginSdk;
using TalkKeys.Services.Windowing;

namespace TalkKeys.Services.Plugins
{
    /// <summary>
    /// Manages lifecycle of all general-purpose plugins.
    /// Separate from TriggerPluginManager which handles recording triggers.
    /// </summary>
    public class PluginManager : IDisposable
    {
        private readonly ILogger? _logger;
        private readonly IWindowPositionService? _positionService;
        private readonly List<IPlugin> _plugins = new();
        private readonly Dictionary<string, PluginConfiguration> _configurations = new();
        private readonly Dictionary<string, Window> _pluginWindows = new();
        private bool _disposed;

        /// <summary>
        /// Event raised when a plugin's widget position changes.
        /// </summary>
        public event EventHandler<PluginWidgetPositionChangedEventArgs>? PluginWidgetPositionChanged;

        /// <summary>
        /// Event raised when a plugin's widget visibility changes.
        /// </summary>
        public event EventHandler<PluginWidgetVisibilityChangedEventArgs>? PluginWidgetVisibilityChanged;

        /// <summary>
        /// Event raised when a plugin's tray menu items need refreshing.
        /// </summary>
        public event EventHandler<PluginTrayMenuChangedEventArgs>? PluginTrayMenuChanged;

        public PluginManager(ILogger? logger = null, IWindowPositionService? positionService = null)
        {
            _logger = logger;
            _positionService = positionService;
        }

        /// <summary>
        /// Register a built-in plugin.
        /// </summary>
        public void RegisterPlugin(IPlugin plugin)
        {
            if (_plugins.Any(p => p.PluginId == plugin.PluginId))
            {
                _logger?.Log($"[PluginManager] Plugin '{plugin.PluginId}' already registered, skipping");
                return;
            }

            _plugins.Add(plugin);

            // Subscribe to widget events if applicable
            if (plugin is IWidgetPlugin widgetPlugin)
            {
                widgetPlugin.WidgetPositionChanged += OnWidgetPositionChanged;
                widgetPlugin.WidgetVisibilityChanged += OnWidgetVisibilityChanged;
            }

            // Subscribe to tray menu events if applicable
            if (plugin is ITrayMenuPlugin trayPlugin)
            {
                trayPlugin.TrayMenuItemsChanged += OnTrayMenuItemsChanged;
            }

            _logger?.Log($"[PluginManager] Registered plugin: {plugin.PluginId} ({plugin.DisplayName})");
        }

        /// <summary>
        /// Initialize all plugins with configurations.
        /// </summary>
        public void Initialize(Dictionary<string, PluginConfiguration>? configurations)
        {
            _configurations.Clear();

            _logger?.Log($"[PluginManager] Initialize called with {configurations?.Count ?? 0} saved configurations");

            if (configurations != null)
            {
                foreach (var kvp in configurations)
                {
                    _configurations[kvp.Key] = kvp.Value;
                    _logger?.Log($"[PluginManager] Loaded saved config for '{kvp.Key}': Enabled={kvp.Value.Enabled}, WidgetVisible={kvp.Value.WidgetVisible}, X={kvp.Value.WidgetX}, Y={kvp.Value.WidgetY}");
                }
            }

            foreach (var plugin in _plugins)
            {
                var hadSavedConfig = _configurations.TryGetValue(plugin.PluginId, out var config);

                if (!hadSavedConfig)
                {
                    config = plugin.GetDefaultConfiguration();
                    _configurations[plugin.PluginId] = config;
                    _logger?.Log($"[PluginManager] Using DEFAULT config for '{plugin.PluginId}': Enabled={config.Enabled}, WidgetVisible={config.WidgetVisible}");
                }

                try
                {
                    plugin.Initialize(config!);
                    _logger?.Log($"[PluginManager] Initialized plugin: {plugin.PluginId}");
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[PluginManager] Failed to initialize plugin '{plugin.PluginId}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Activate all enabled plugins.
        /// </summary>
        public void ActivateAll()
        {
            _logger?.Log($"[PluginManager] ActivateAll called. {_plugins.Count} plugins registered.");

            foreach (var plugin in _plugins)
            {
                var hasConfig = _configurations.TryGetValue(plugin.PluginId, out var config);
                _logger?.Log($"[PluginManager] Plugin '{plugin.PluginId}': hasConfig={hasConfig}, enabled={config?.Enabled}, widgetVisible={config?.WidgetVisible}");

                if (hasConfig && config!.Enabled)
                {
                    try
                    {
                        plugin.Activate();
                        _logger?.Log($"[PluginManager] Activated plugin: {plugin.PluginId}");

                        // Show widget if applicable and configured to be visible
                        if (plugin is IWidgetPlugin widgetPlugin && config.WidgetVisible)
                        {
                            _logger?.Log($"[PluginManager] Showing widget for '{plugin.PluginId}' at ({config.WidgetX}, {config.WidgetY})");
                            widgetPlugin.ShowWidget();

                            // Use position service if available, otherwise let plugin handle positioning
                            if (_positionService != null && widgetPlugin.IsWidgetVisible)
                            {
                                // Get the widget window and position it
                                var window = GetWidgetWindow(widgetPlugin);
                                if (window != null)
                                {
                                    _positionService.PositionAt(
                                        window,
                                        config.WidgetX >= 0 ? config.WidgetX : null,
                                        config.WidgetY >= 0 ? config.WidgetY : null,
                                        DefaultWindowPosition.TopRight);
                                    _pluginWindows[plugin.PluginId] = window;
                                }
                            }
                            else
                            {
                                // Fallback to plugin's own positioning
                                widgetPlugin.PositionWidget(
                                    config.WidgetX >= 0 ? config.WidgetX : null,
                                    config.WidgetY >= 0 ? config.WidgetY : null);
                            }

                            _logger?.Log($"[PluginManager] Widget shown for '{plugin.PluginId}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"[PluginManager] Failed to activate plugin '{plugin.PluginId}': {ex.Message}");
                        _logger?.Log($"[PluginManager] Stack trace: {ex.StackTrace}");
                    }
                }
            }
        }

        private Window? GetWidgetWindow(IWidgetPlugin widgetPlugin)
        {
            // Use reflection to get the widget window - this is a bit hacky but necessary
            // since IWidgetPlugin doesn't expose the window directly
            try
            {
                var pluginType = widgetPlugin.GetType();
                var widgetField = pluginType.GetField("_widget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return widgetField?.GetValue(widgetPlugin) as Window;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Deactivate all plugins.
        /// </summary>
        public void DeactivateAll()
        {
            foreach (var plugin in _plugins)
            {
                try
                {
                    plugin.Deactivate();

                    if (plugin is IWidgetPlugin widgetPlugin)
                        widgetPlugin.HideWidget();

                    _logger?.Log($"[PluginManager] Deactivated plugin: {plugin.PluginId}");
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[PluginManager] Error deactivating plugin '{plugin.PluginId}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get all registered plugins.
        /// </summary>
        public IReadOnlyList<IPlugin> GetPlugins() => _plugins.AsReadOnly();

        /// <summary>
        /// Get a plugin by ID.
        /// </summary>
        public IPlugin? GetPlugin(string pluginId) => _plugins.FirstOrDefault(p => p.PluginId == pluginId);

        /// <summary>
        /// Get all current configurations.
        /// </summary>
        public Dictionary<string, PluginConfiguration> GetAllConfigurations()
        {
            var configs = new Dictionary<string, PluginConfiguration>();
            foreach (var plugin in _plugins)
            {
                configs[plugin.PluginId] = plugin.GetConfiguration();
            }
            return configs;
        }

        /// <summary>
        /// Update a plugin's configuration.
        /// </summary>
        public void UpdatePluginConfiguration(string pluginId, PluginConfiguration configuration)
        {
            _configurations[pluginId] = configuration;

            var plugin = GetPlugin(pluginId);
            if (plugin != null)
            {
                plugin.Initialize(configuration);

                // Handle enable/disable
                if (configuration.Enabled)
                {
                    plugin.Activate();
                    if (plugin is IWidgetPlugin widgetPlugin && configuration.WidgetVisible)
                    {
                        widgetPlugin.ShowWidget();

                        // Position using service if available
                        if (_positionService != null && widgetPlugin.IsWidgetVisible)
                        {
                            var window = GetWidgetWindow(widgetPlugin);
                            if (window != null)
                            {
                                _positionService.PositionAt(
                                    window,
                                    configuration.WidgetX >= 0 ? configuration.WidgetX : null,
                                    configuration.WidgetY >= 0 ? configuration.WidgetY : null,
                                    DefaultWindowPosition.TopRight);
                                _pluginWindows[pluginId] = window;
                            }
                        }
                    }
                }
                else
                {
                    plugin.Deactivate();
                    if (plugin is IWidgetPlugin widgetPlugin)
                    {
                        widgetPlugin.HideWidget();
                    }
                    _pluginWindows.Remove(pluginId);
                }
            }
        }

        /// <summary>
        /// Get tray menu items from all enabled ITrayMenuPlugin plugins.
        /// </summary>
        public IReadOnlyList<PluginMenuItem> GetAllTrayMenuItems()
        {
            var items = new List<PluginMenuItem>();

            foreach (var plugin in _plugins.OfType<ITrayMenuPlugin>())
            {
                if (_configurations.TryGetValue(plugin.PluginId, out var config) && config.Enabled)
                {
                    try
                    {
                        var pluginItems = plugin.GetTrayMenuItems();
                        if (pluginItems != null && pluginItems.Count > 0)
                        {
                            items.AddRange(pluginItems);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"[PluginManager] Error getting tray menu from '{plugin.PluginId}': {ex.Message}");
                    }
                }
            }

            return items;
        }

        private void OnWidgetPositionChanged(object? sender, WidgetPositionChangedEventArgs e)
        {
            if (sender is IPlugin plugin)
            {
                if (_configurations.TryGetValue(plugin.PluginId, out var config))
                {
                    config.WidgetX = e.X;
                    config.WidgetY = e.Y;
                }
                PluginWidgetPositionChanged?.Invoke(this, new PluginWidgetPositionChangedEventArgs(plugin.PluginId, e.X, e.Y));
            }
        }

        private void OnWidgetVisibilityChanged(object? sender, WidgetVisibilityChangedEventArgs e)
        {
            if (sender is IPlugin plugin)
            {
                if (_configurations.TryGetValue(plugin.PluginId, out var config))
                {
                    config.WidgetVisible = e.IsVisible;
                }
                PluginWidgetVisibilityChanged?.Invoke(this, new PluginWidgetVisibilityChangedEventArgs(plugin.PluginId, e.IsVisible));
            }
        }

        private void OnTrayMenuItemsChanged(object? sender, EventArgs e)
        {
            if (sender is IPlugin plugin)
            {
                PluginTrayMenuChanged?.Invoke(this, new PluginTrayMenuChangedEventArgs(plugin.PluginId));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            DeactivateAll();

            foreach (var plugin in _plugins)
            {
                try
                {
                    // Unsubscribe from events
                    if (plugin is IWidgetPlugin widgetPlugin)
                    {
                        widgetPlugin.WidgetPositionChanged -= OnWidgetPositionChanged;
                        widgetPlugin.WidgetVisibilityChanged -= OnWidgetVisibilityChanged;
                    }
                    if (plugin is ITrayMenuPlugin trayPlugin)
                    {
                        trayPlugin.TrayMenuItemsChanged -= OnTrayMenuItemsChanged;
                    }

                    plugin.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[PluginManager] Error disposing plugin '{plugin.PluginId}': {ex.Message}");
                }
            }

            _plugins.Clear();
            _configurations.Clear();
        }
    }

    /// <summary>
    /// Event args for plugin widget position changes.
    /// </summary>
    public class PluginWidgetPositionChangedEventArgs : EventArgs
    {
        public string PluginId { get; }
        public double X { get; }
        public double Y { get; }

        public PluginWidgetPositionChangedEventArgs(string pluginId, double x, double y)
        {
            PluginId = pluginId;
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Event args for plugin widget visibility changes.
    /// </summary>
    public class PluginWidgetVisibilityChangedEventArgs : EventArgs
    {
        public string PluginId { get; }
        public bool IsVisible { get; }

        public PluginWidgetVisibilityChangedEventArgs(string pluginId, bool isVisible)
        {
            PluginId = pluginId;
            IsVisible = isVisible;
        }
    }

    /// <summary>
    /// Event args for plugin tray menu changes.
    /// </summary>
    public class PluginTrayMenuChangedEventArgs : EventArgs
    {
        public string PluginId { get; }

        public PluginTrayMenuChangedEventArgs(string pluginId)
        {
            PluginId = pluginId;
        }
    }
}
