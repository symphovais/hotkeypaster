using System;
using System.Collections.Generic;
using System.Linq;
using TalkKeys.Logging;
using TalkKeys.Services.Pipeline.Configuration;

namespace TalkKeys.Services.Pipeline
{
    /// <summary>
    /// Factory for building pipelines from configurations
    /// </summary>
    public class PipelineFactory
    {
        private readonly Dictionary<string, IPipelineStageFactory> _stageFactories = new();
        private readonly ILogger? _logger;

        public PipelineFactory(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Register a stage factory
        /// </summary>
        public void RegisterStageFactory(IPipelineStageFactory factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            if (_stageFactories.ContainsKey(factory.StageType))
            {
                _logger?.Log($"Warning: Stage factory for type '{factory.StageType}' is being overwritten");
            }

            _stageFactories[factory.StageType] = factory;
            _logger?.Log($"Registered stage factory for type: {factory.StageType}");
        }

        /// <summary>
        /// Build a pipeline from configuration
        /// </summary>
        public Pipeline BuildPipeline(PipelineConfiguration config, PipelineBuildContext buildContext)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (buildContext == null)
                throw new ArgumentNullException(nameof(buildContext));

            _logger?.Log($"Building pipeline: {config.Name}");

            var stages = new List<IPipelineStage>();

            foreach (var stageConfig in config.Stages.Where(s => s.Enabled))
            {
                if (!_stageFactories.TryGetValue(stageConfig.Type, out var factory))
                {
                    throw new InvalidOperationException(
                        $"No factory registered for stage type: {stageConfig.Type}. " +
                        $"Available types: {string.Join(", ", _stageFactories.Keys)}");
                }

                var stage = factory.CreateStage(stageConfig, buildContext);
                stages.Add(stage);

                _logger?.Log($"  Added stage: {stage.Name} (type: {stageConfig.Type})");
            }

            if (stages.Count == 0)
            {
                throw new InvalidOperationException($"Pipeline '{config.Name}' has no enabled stages");
            }

            return new Pipeline(config.Name, stages, _logger);
        }

        /// <summary>
        /// Get all registered stage types
        /// </summary>
        public IEnumerable<string> GetRegisteredStageTypes()
        {
            return _stageFactories.Keys;
        }
    }
}
