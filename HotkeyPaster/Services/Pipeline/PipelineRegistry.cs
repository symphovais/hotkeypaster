using System;
using System.Collections.Generic;
using System.Linq;
using HotkeyPaster.Logging;
using HotkeyPaster.Services.Pipeline.Configuration;

namespace HotkeyPaster.Services.Pipeline
{
    /// <summary>
    /// Registry for managing multiple pipeline configurations
    /// </summary>
    public class PipelineRegistry
    {
        private readonly PipelineFactory _factory;
        private readonly PipelineConfigurationLoader _configLoader;
        private readonly PipelineBuildContext _buildContext;
        private readonly ILogger? _logger;

        private readonly Dictionary<string, PipelineConfiguration> _configurations = new();
        private string? _defaultPipelineName;

        public PipelineRegistry(
            PipelineFactory factory,
            PipelineConfigurationLoader configLoader,
            PipelineBuildContext buildContext,
            ILogger? logger = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _buildContext = buildContext ?? throw new ArgumentNullException(nameof(buildContext));
            _logger = logger;
        }

        /// <summary>
        /// Load all pipeline configurations
        /// </summary>
        public void LoadConfigurations()
        {
            _configurations.Clear();

            var configs = _configLoader.LoadAll();

            foreach (var config in configs.Where(c => c.Enabled))
            {
                _configurations[config.Name] = config;
                _logger?.Log($"Loaded pipeline configuration: {config.Name}");
            }

            // Set first enabled pipeline as default if none set
            if (_defaultPipelineName == null && _configurations.Any())
            {
                _defaultPipelineName = _configurations.Keys.First();
                _logger?.Log($"Set default pipeline: {_defaultPipelineName}");
            }

            _logger?.Log($"Loaded {_configurations.Count} pipeline configurations");
        }

        /// <summary>
        /// Get a pipeline by name
        /// </summary>
        public Pipeline? GetPipeline(string name)
        {
            if (!_configurations.TryGetValue(name, out var config))
            {
                _logger?.Log($"Pipeline configuration not found: {name}");
                return null;
            }

            try
            {
                return _factory.BuildPipeline(config, _buildContext);
            }
            catch (Exception ex)
            {
                _logger?.Log($"Failed to build pipeline '{name}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the default pipeline
        /// </summary>
        public Pipeline? GetDefaultPipeline()
        {
            if (_defaultPipelineName == null)
            {
                _logger?.Log("No default pipeline set");
                return null;
            }

            return GetPipeline(_defaultPipelineName);
        }

        /// <summary>
        /// Set the default pipeline
        /// </summary>
        public void SetDefaultPipeline(string name)
        {
            if (!_configurations.ContainsKey(name))
            {
                throw new ArgumentException($"Pipeline configuration not found: {name}", nameof(name));
            }

            _defaultPipelineName = name;
            _logger?.Log($"Set default pipeline: {name}");
        }

        /// <summary>
        /// Get all available pipeline names
        /// </summary>
        public IEnumerable<string> GetAvailablePipelineNames()
        {
            return _configurations.Keys;
        }

        /// <summary>
        /// Get all pipeline configurations
        /// </summary>
        public IEnumerable<PipelineConfiguration> GetAllConfigurations()
        {
            return _configurations.Values;
        }

        /// <summary>
        /// Reload configurations from disk
        /// </summary>
        public void Reload()
        {
            LoadConfigurations();
        }
    }
}
