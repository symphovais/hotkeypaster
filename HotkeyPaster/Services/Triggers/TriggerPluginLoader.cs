using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TalkKeys.Logging;

namespace TalkKeys.Services.Triggers
{
    /// <summary>
    /// Loads trigger plugins from DLL files in the plugins directory.
    /// </summary>
    public class TriggerPluginLoader
    {
        private readonly ILogger? _logger;
        private readonly string _pluginsDirectory;
        private readonly List<Assembly> _loadedAssemblies = new();

        /// <summary>
        /// Gets the plugins directory path.
        /// </summary>
        public string PluginsDirectory => _pluginsDirectory;

        public TriggerPluginLoader(ILogger? logger = null)
        {
            _logger = logger;

            // Default plugins directory: %APPDATA%\TalkKeys\Plugins
            _pluginsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TalkKeys",
                "Plugins"
            );
        }

        public TriggerPluginLoader(string pluginsDirectory, ILogger? logger = null)
        {
            _logger = logger;
            _pluginsDirectory = pluginsDirectory;
        }

        /// <summary>
        /// Ensures the plugins directory exists.
        /// </summary>
        public void EnsurePluginsDirectoryExists()
        {
            if (!Directory.Exists(_pluginsDirectory))
            {
                Directory.CreateDirectory(_pluginsDirectory);
                _logger?.Log($"[PluginLoader] Created plugins directory: {_pluginsDirectory}");
            }
        }

        /// <summary>
        /// Discovers and loads all trigger plugins from the plugins directories.
        /// Searches both the AppData plugins folder and the app's local Plugins folder.
        /// </summary>
        /// <returns>List of instantiated plugin instances.</returns>
        public List<ITriggerPlugin> LoadPlugins()
        {
            var plugins = new List<ITriggerPlugin>();
            var searchPaths = new List<string>();

            // Primary location: %APPDATA%\TalkKeys\Plugins
            EnsurePluginsDirectoryExists();
            searchPaths.Add(_pluginsDirectory);

            // Secondary location: App directory\Plugins (for development/portable installs)
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var localPluginsDir = Path.Combine(appDir, "Plugins");
            if (Directory.Exists(localPluginsDir) && localPluginsDir != _pluginsDirectory)
            {
                searchPaths.Add(localPluginsDir);
            }

            foreach (var searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath)) continue;

                // Find all DLL files in the plugins directory
                var dllFiles = Directory.GetFiles(searchPath, "*.dll", SearchOption.TopDirectoryOnly);

                _logger?.Log($"[PluginLoader] Found {dllFiles.Length} DLL files in {searchPath}");

                foreach (var dllPath in dllFiles)
                {
                    // Skip the SDK DLL
                    if (Path.GetFileName(dllPath).Equals("TalkKeys.PluginSdk.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        var loadedPlugins = LoadPluginsFromAssembly(dllPath);
                        plugins.AddRange(loadedPlugins);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"[PluginLoader] Failed to load plugins from {Path.GetFileName(dllPath)}: {ex.Message}");
                    }
                }
            }

            _logger?.Log($"[PluginLoader] Loaded {plugins.Count} plugins from external assemblies");

            return plugins;
        }

        /// <summary>
        /// Loads plugins from a specific assembly file.
        /// </summary>
        private List<ITriggerPlugin> LoadPluginsFromAssembly(string assemblyPath)
        {
            var plugins = new List<ITriggerPlugin>();

            _logger?.Log($"[PluginLoader] Loading assembly: {Path.GetFileName(assemblyPath)}");

            // Load the assembly
            var assembly = LoadAssemblyWithDependencies(assemblyPath);
            if (assembly == null)
            {
                return plugins;
            }

            _loadedAssemblies.Add(assembly);

            // Find all types that implement ITriggerPlugin
            var pluginTypes = FindPluginTypes(assembly);

            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    var plugin = CreatePluginInstance(pluginType);
                    if (plugin != null)
                    {
                        plugins.Add(plugin);
                        _logger?.Log($"[PluginLoader] Loaded plugin: {plugin.DisplayName} ({plugin.PluginId})");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[PluginLoader] Failed to instantiate plugin {pluginType.Name}: {ex.Message}");
                }
            }

            return plugins;
        }

        /// <summary>
        /// Loads an assembly and sets up dependency resolution.
        /// </summary>
        private Assembly? LoadAssemblyWithDependencies(string assemblyPath)
        {
            try
            {
                // Use LoadFrom to allow loading dependencies from the same directory
                var assembly = Assembly.LoadFrom(assemblyPath);

                // Set up assembly resolution for dependencies in the same folder
                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                    {
                        var assemblyName = new AssemblyName(args.Name).Name;
                        var dependencyPath = Path.Combine(assemblyDir, assemblyName + ".dll");

                        if (File.Exists(dependencyPath))
                        {
                            return Assembly.LoadFrom(dependencyPath);
                        }

                        return null;
                    };
                }

                return assembly;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[PluginLoader] Failed to load assembly {Path.GetFileName(assemblyPath)}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds all types in an assembly that implement ITriggerPlugin.
        /// </summary>
        private IEnumerable<Type> FindPluginTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes()
                    .Where(t => typeof(ITriggerPlugin).IsAssignableFrom(t)
                                && !t.IsInterface
                                && !t.IsAbstract
                                && t.IsPublic);
            }
            catch (ReflectionTypeLoadException ex)
            {
                _logger?.Log($"[PluginLoader] Type load exception for assembly {assembly.GetName().Name}");
                foreach (var loaderException in ex.LoaderExceptions)
                {
                    if (loaderException != null)
                    {
                        _logger?.Log($"[PluginLoader]   - {loaderException.Message}");
                    }
                }

                // Return the types that did load successfully
                return ex.Types.Where(t => t != null
                                           && typeof(ITriggerPlugin).IsAssignableFrom(t)
                                           && !t.IsInterface
                                           && !t.IsAbstract
                                           && t.IsPublic)!;
            }
        }

        /// <summary>
        /// Creates an instance of a plugin type.
        /// </summary>
        private ITriggerPlugin? CreatePluginInstance(Type pluginType)
        {
            // Try to find a constructor that takes ILogger
            var loggerConstructor = pluginType.GetConstructor(new[] { typeof(ILogger) });
            if (loggerConstructor != null)
            {
                return (ITriggerPlugin?)loggerConstructor.Invoke(new object?[] { _logger });
            }

            // Try parameterless constructor
            var defaultConstructor = pluginType.GetConstructor(Type.EmptyTypes);
            if (defaultConstructor != null)
            {
                return (ITriggerPlugin?)defaultConstructor.Invoke(null);
            }

            _logger?.Log($"[PluginLoader] No suitable constructor found for {pluginType.Name}");
            return null;
        }

        /// <summary>
        /// Gets information about loaded assemblies.
        /// </summary>
        public IReadOnlyList<PluginAssemblyInfo> GetLoadedAssemblyInfo()
        {
            return _loadedAssemblies.Select(a => new PluginAssemblyInfo
            {
                Name = a.GetName().Name ?? "Unknown",
                Version = a.GetName().Version?.ToString() ?? "Unknown",
                Location = a.Location
            }).ToList();
        }
    }

    /// <summary>
    /// Information about a loaded plugin assembly.
    /// </summary>
    public class PluginAssemblyInfo
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Location { get; set; } = "";
    }
}
