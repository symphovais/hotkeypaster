using System;
using System.Collections.Generic;
using System.Linq;
using TalkKeys.Logging;

namespace TalkKeys.Services.Triggers
{
    /// <summary>
    /// Manages all trigger plugins and coordinates their lifecycle.
    /// </summary>
    public class TriggerPluginManager : IDisposable
    {
        private readonly ILogger? _logger;
        private readonly TriggerPluginLoader _pluginLoader;
        private readonly List<ITriggerPlugin> _plugins = new();
        private readonly Dictionary<string, TriggerPluginConfiguration> _configurations = new();
        private bool _isRunning;
        private bool _disposed;

        /// <summary>
        /// Event raised when any trigger is activated.
        /// </summary>
        public event EventHandler<TriggerEventArgs>? TriggerActivated;

        /// <summary>
        /// Event raised when any trigger is deactivated.
        /// </summary>
        public event EventHandler<TriggerEventArgs>? TriggerDeactivated;

        /// <summary>
        /// Event raised when a plugin's availability changes.
        /// </summary>
        public event EventHandler<PluginAvailabilityChangedEventArgs>? PluginAvailabilityChanged;

        public TriggerPluginManager(ILogger? logger = null)
        {
            _logger = logger;
            _pluginLoader = new TriggerPluginLoader(logger);
        }

        /// <summary>
        /// Gets the plugins directory path.
        /// </summary>
        public string PluginsDirectory => _pluginLoader.PluginsDirectory;

        /// <summary>
        /// Register a trigger plugin (for built-in plugins).
        /// </summary>
        public void RegisterPlugin(ITriggerPlugin plugin)
        {
            if (_plugins.Any(p => p.PluginId == plugin.PluginId))
            {
                _logger?.Log($"[TriggerManager] Plugin {plugin.PluginId} already registered, skipping");
                return;
            }

            _plugins.Add(plugin);
            plugin.TriggerActivated += OnPluginTriggerActivated;
            plugin.TriggerDeactivated += OnPluginTriggerDeactivated;
            plugin.AvailabilityChanged += OnPluginAvailabilityChanged;

            _logger?.Log($"[TriggerManager] Registered built-in plugin: {plugin.PluginId} ({plugin.DisplayName})");
        }

        /// <summary>
        /// Discovers and loads plugins from the plugins directory.
        /// Call this after registering built-in plugins.
        /// </summary>
        public void DiscoverExternalPlugins()
        {
            _logger?.Log($"[TriggerManager] Discovering external plugins from: {_pluginLoader.PluginsDirectory}");

            var externalPlugins = _pluginLoader.LoadPlugins();

            foreach (var plugin in externalPlugins)
            {
                if (_plugins.Any(p => p.PluginId == plugin.PluginId))
                {
                    _logger?.Log($"[TriggerManager] External plugin {plugin.PluginId} conflicts with built-in plugin, skipping");
                    plugin.Dispose();
                    continue;
                }

                _plugins.Add(plugin);
                plugin.TriggerActivated += OnPluginTriggerActivated;
                plugin.TriggerDeactivated += OnPluginTriggerDeactivated;
                plugin.AvailabilityChanged += OnPluginAvailabilityChanged;

                _logger?.Log($"[TriggerManager] Registered external plugin: {plugin.PluginId} ({plugin.DisplayName})");
            }

            _logger?.Log($"[TriggerManager] Total plugins registered: {_plugins.Count}");
        }

        /// <summary>
        /// Gets information about loaded external plugin assemblies.
        /// </summary>
        public IReadOnlyList<PluginAssemblyInfo> GetLoadedAssemblyInfo()
        {
            return _pluginLoader.GetLoadedAssemblyInfo();
        }

        /// <summary>
        /// Get all registered plugins.
        /// </summary>
        public IReadOnlyList<ITriggerPlugin> GetPlugins() => _plugins.AsReadOnly();

        /// <summary>
        /// Get a plugin by ID.
        /// </summary>
        public ITriggerPlugin? GetPlugin(string pluginId)
        {
            return _plugins.FirstOrDefault(p => p.PluginId == pluginId);
        }

        /// <summary>
        /// Initialize all plugins with their configurations.
        /// </summary>
        public void Initialize(Dictionary<string, TriggerPluginConfiguration> configurations)
        {
            _configurations.Clear();
            foreach (var kvp in configurations)
            {
                _configurations[kvp.Key] = kvp.Value;
            }

            foreach (var plugin in _plugins)
            {
                if (_configurations.TryGetValue(plugin.PluginId, out var config))
                {
                    plugin.Initialize(config);
                    _logger?.Log($"[TriggerManager] Initialized plugin {plugin.PluginId} with stored configuration");
                }
                else
                {
                    // Use default configuration
                    var defaultConfig = plugin.GetDefaultConfiguration();
                    _configurations[plugin.PluginId] = defaultConfig;
                    plugin.Initialize(defaultConfig);
                    _logger?.Log($"[TriggerManager] Initialized plugin {plugin.PluginId} with default configuration");
                }
            }
        }

        /// <summary>
        /// Start all enabled plugins.
        /// </summary>
        public void StartAll()
        {
            if (_isRunning) return;

            foreach (var plugin in _plugins)
            {
                if (_configurations.TryGetValue(plugin.PluginId, out var config) && config.Enabled)
                {
                    try
                    {
                        plugin.Start();
                        _logger?.Log($"[TriggerManager] Started plugin: {plugin.PluginId}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"[TriggerManager] Failed to start plugin {plugin.PluginId}: {ex.Message}");
                    }
                }
            }

            _isRunning = true;
        }

        /// <summary>
        /// Stop all plugins.
        /// </summary>
        public void StopAll()
        {
            if (!_isRunning) return;

            foreach (var plugin in _plugins)
            {
                try
                {
                    plugin.Stop();
                    _logger?.Log($"[TriggerManager] Stopped plugin: {plugin.PluginId}");
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[TriggerManager] Error stopping plugin {plugin.PluginId}: {ex.Message}");
                }
            }

            _isRunning = false;
        }

        /// <summary>
        /// Update configuration for a specific plugin.
        /// </summary>
        public void UpdatePluginConfiguration(string pluginId, TriggerPluginConfiguration configuration)
        {
            _configurations[pluginId] = configuration;

            var plugin = GetPlugin(pluginId);
            if (plugin != null)
            {
                plugin.UpdateConfiguration(configuration);
                _logger?.Log($"[TriggerManager] Updated configuration for plugin: {pluginId}");
            }
        }

        /// <summary>
        /// Get all plugin configurations.
        /// </summary>
        public Dictionary<string, TriggerPluginConfiguration> GetAllConfigurations()
        {
            // Return current configurations from plugins (in case they were modified)
            var configs = new Dictionary<string, TriggerPluginConfiguration>();
            foreach (var plugin in _plugins)
            {
                configs[plugin.PluginId] = plugin.GetConfiguration();
            }
            return configs;
        }

        /// <summary>
        /// Get configuration for a specific plugin.
        /// </summary>
        public TriggerPluginConfiguration? GetPluginConfiguration(string pluginId)
        {
            return GetPlugin(pluginId)?.GetConfiguration();
        }

        private void OnPluginTriggerActivated(object? sender, TriggerEventArgs e)
        {
            _logger?.Log($"[TriggerManager] Trigger activated: {e.TriggerId}, Action: {e.Action}");
            TriggerActivated?.Invoke(this, e);
        }

        private void OnPluginTriggerDeactivated(object? sender, TriggerEventArgs e)
        {
            _logger?.Log($"[TriggerManager] Trigger deactivated: {e.TriggerId}");
            TriggerDeactivated?.Invoke(this, e);
        }

        private void OnPluginAvailabilityChanged(object? sender, EventArgs e)
        {
            if (sender is ITriggerPlugin plugin)
            {
                _logger?.Log($"[TriggerManager] Plugin availability changed: {plugin.PluginId} -> {plugin.IsAvailable}");
                PluginAvailabilityChanged?.Invoke(this, new PluginAvailabilityChangedEventArgs(plugin.PluginId, plugin.IsAvailable));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopAll();

            foreach (var plugin in _plugins)
            {
                plugin.TriggerActivated -= OnPluginTriggerActivated;
                plugin.TriggerDeactivated -= OnPluginTriggerDeactivated;
                plugin.AvailabilityChanged -= OnPluginAvailabilityChanged;
                plugin.Dispose();
            }

            _plugins.Clear();
            _configurations.Clear();
        }
    }

    /// <summary>
    /// Event args for plugin availability changes.
    /// </summary>
    public class PluginAvailabilityChangedEventArgs : EventArgs
    {
        public string PluginId { get; }
        public bool IsAvailable { get; }

        public PluginAvailabilityChangedEventArgs(string pluginId, bool isAvailable)
        {
            PluginId = pluginId;
            IsAvailable = isAvailable;
        }
    }
}
