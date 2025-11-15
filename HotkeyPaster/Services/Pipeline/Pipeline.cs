using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TalkKeys.Logging;

namespace TalkKeys.Services.Pipeline
{
    /// <summary>
    /// Pipeline executor that runs a series of stages sequentially.
    /// Each stage processes the shared context and adds metrics.
    /// </summary>
    public class Pipeline
    {
        private readonly List<IPipelineStage> _stages;
        private readonly ILogger? _logger;

        public string Name { get; }

        public Pipeline(string name, List<IPipelineStage> stages, ILogger? logger = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _stages = stages ?? throw new ArgumentNullException(nameof(stages));
            _logger = logger;
        }

        /// <summary>
        /// Execute all stages in sequence
        /// </summary>
        public async Task<PipelineResult> ExecuteAsync(PipelineContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.Metrics.PipelineName = Name;
            context.Metrics.StartTime = DateTime.UtcNow;

            _logger?.Log($"Pipeline '{Name}' starting with {_stages.Count} stages");

            try
            {
                // Execute each stage in order
                for (int i = 0; i < _stages.Count; i++)
                {
                    var stage = _stages[i];

                    // Check for cancellation
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        _logger?.Log($"Pipeline '{Name}' cancelled at stage {i + 1}/{_stages.Count} ({stage.Name})");
                        return CreateCancelledResult(context, stage.Name);
                    }

                    // Report progress
                    context.Progress?.Report(new ProgressEventArgs(
                        $"Stage {i + 1}/{_stages.Count}: {stage.Name}...",
                        (int)((i / (double)_stages.Count) * 100)
                    ));

                    _logger?.Log($"Pipeline '{Name}' executing stage {i + 1}/{_stages.Count}: {stage.Name}");

                    // Execute the stage
                    StageResult result;
                    try
                    {
                        result = await stage.ExecuteAsync(context);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"Pipeline '{Name}' stage '{stage.Name}' threw exception: {ex}");

                        // Create error metrics
                        var errorMetrics = new StageMetrics
                        {
                            StageName = stage.Name,
                            StartTime = DateTime.UtcNow,
                            EndTime = DateTime.UtcNow
                        };
                        errorMetrics.AddMetric("Exception", ex.Message);

                        result = StageResult.Failure($"Exception in {stage.Name}: {ex.Message}", errorMetrics);
                    }

                    // Add stage metrics
                    context.Metrics.AddStageMetrics(result.Metrics);

                    // Check if stage failed
                    if (!result.IsSuccess)
                    {
                        _logger?.Log($"Pipeline '{Name}' failed at stage '{stage.Name}': {result.ErrorMessage}");
                        context.Metrics.EndTime = DateTime.UtcNow;

                        return new PipelineResult
                        {
                            IsSuccess = false,
                            ErrorMessage = result.ErrorMessage,
                            FailedStageName = stage.Name,
                            Metrics = context.Metrics,
                            Context = context
                        };
                    }

                    _logger?.Log($"Pipeline '{Name}' stage '{stage.Name}' completed in {result.Metrics.DurationMs:F2}ms");
                }

                // All stages succeeded
                context.Metrics.EndTime = DateTime.UtcNow;
                context.Progress?.Report(new ProgressEventArgs("Complete", 100));

                _logger?.Log($"Pipeline '{Name}' completed successfully in {context.Metrics.TotalDurationMs:F2}ms");

                // Extract final result from context
                var finalText = context.GetData<string>("CleanedText")
                              ?? context.GetData<string>("RawTranscription")
                              ?? string.Empty;

                var wordCount = 0;
                if (!string.IsNullOrWhiteSpace(finalText))
                {
                    wordCount = finalText.Split(new[] { ' ', '\n', '\r', '\t' },
                        StringSplitOptions.RemoveEmptyEntries).Length;
                }

                context.Metrics.SetGlobalMetric("TotalWordCount", wordCount);

                return new PipelineResult
                {
                    IsSuccess = true,
                    Text = finalText,
                    Metrics = context.Metrics,
                    Context = context,
                    Language = context.GetData<string>("Language"),
                    DurationSeconds = context.GetData<double?>("AudioDuration"),
                    WordCount = wordCount
                };
            }
            catch (Exception ex)
            {
                _logger?.Log($"Pipeline '{Name}' failed with unexpected exception: {ex}");
                context.Metrics.EndTime = DateTime.UtcNow;

                return new PipelineResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Pipeline execution failed: {ex.Message}",
                    Metrics = context.Metrics,
                    Context = context
                };
            }
        }

        private PipelineResult CreateCancelledResult(PipelineContext context, string stageName)
        {
            context.Metrics.EndTime = DateTime.UtcNow;

            return new PipelineResult
            {
                IsSuccess = false,
                ErrorMessage = "Pipeline execution was cancelled",
                FailedStageName = stageName,
                Metrics = context.Metrics,
                Context = context
            };
        }
    }
}
