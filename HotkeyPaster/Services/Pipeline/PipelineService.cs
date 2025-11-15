using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HotkeyPaster.Logging;
using HotkeyPaster.Services.Windowing;

namespace HotkeyPaster.Services.Pipeline
{
    /// <summary>
    /// Main service for executing pipelines
    /// </summary>
    public class PipelineService : IPipelineService
    {
        private readonly PipelineRegistry _registry;
        private readonly ILogger? _logger;

        public PipelineService(PipelineRegistry registry, ILogger? logger = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _logger = logger;
        }

        public async Task<PipelineResult> ExecuteAsync(
            byte[] audioData,
            WindowContext? windowContext = null,
            IProgress<ProgressEventArgs>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var pipeline = _registry.GetDefaultPipeline();

            if (pipeline == null)
            {
                _logger?.Log("No default pipeline configured");
                return new PipelineResult
                {
                    IsSuccess = false,
                    ErrorMessage = "No default pipeline configured. Please configure a pipeline in settings.",
                    Metrics = new PipelineMetrics()
                };
            }

            return await ExecutePipelineAsync(pipeline, audioData, windowContext, progress, cancellationToken);
        }

        public async Task<PipelineResult> ExecuteAsync(
            string pipelineName,
            byte[] audioData,
            WindowContext? windowContext = null,
            IProgress<ProgressEventArgs>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var pipeline = _registry.GetPipeline(pipelineName);

            if (pipeline == null)
            {
                _logger?.Log($"Pipeline not found: {pipelineName}");
                return new PipelineResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Pipeline '{pipelineName}' not found",
                    Metrics = new PipelineMetrics()
                };
            }

            return await ExecutePipelineAsync(pipeline, audioData, windowContext, progress, cancellationToken);
        }

        private async Task<PipelineResult> ExecutePipelineAsync(
            Pipeline pipeline,
            byte[] audioData,
            WindowContext? windowContext,
            IProgress<ProgressEventArgs>? progress,
            CancellationToken cancellationToken)
        {
            _logger?.Log($"Executing pipeline: {pipeline.Name}");

            // Create pipeline context
            var context = new PipelineContext
            {
                CancellationToken = cancellationToken,
                Progress = progress
            };

            // Set audio data
            context.SetData("AudioData", audioData);

            // Set window context if available
            if (windowContext != null && windowContext.IsValid)
            {
                context.SetData("WindowContext", windowContext);
            }

            // Execute pipeline
            var result = await pipeline.ExecuteAsync(context);

            _logger?.Log($"Pipeline '{pipeline.Name}' completed: Success={result.IsSuccess}, Duration={result.Metrics.TotalDurationMs:F2}ms");

            return result;
        }

        public string? GetDefaultPipelineName()
        {
            var pipeline = _registry.GetDefaultPipeline();
            return pipeline?.Name;
        }

        public void SetDefaultPipeline(string pipelineName)
        {
            _registry.SetDefaultPipeline(pipelineName);
            _logger?.Log($"Default pipeline set to: {pipelineName}");
        }

        public string[] GetAvailablePipelines()
        {
            return _registry.GetAvailablePipelineNames().ToArray();
        }

        public void ReloadConfigurations()
        {
            _registry.Reload();
            _logger?.Log("Pipeline configurations reloaded");
        }
    }
}
