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
        private const double ProgressMultiplier = 100.0;
        private const int BaseRetryCount = 1;
        
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
                        (int)((i / (double)_stages.Count) * ProgressMultiplier)
                    ));

                    _logger?.Log($"Pipeline '{Name}' executing stage {i + 1}/{_stages.Count}: {stage.Name}");

                    // Execute the stage with retries
                    StageResult? result = null;
                    int attempts = 0;
                    int maxAttempts = BaseRetryCount + stage.RetryCount;

                    while (attempts < maxAttempts)
                    {
                        attempts++;
                        try
                        {
                            if (attempts > 1)
                            {
                                _logger?.Log($"Retry attempt {attempts}/{maxAttempts} for stage '{stage.Name}' after {stage.RetryDelay.TotalMilliseconds}ms delay");
                                await Task.Delay(stage.RetryDelay, context.CancellationToken);
                            }

                            result = await stage.ExecuteAsync(context);

                            if (result.IsSuccess)
                            {
                                break;
                            }
                            else
                            {
                                _logger?.Log($"Stage '{stage.Name}' failed (attempt {attempts}/{maxAttempts}): {result.ErrorMessage}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.Log($"Pipeline '{Name}' stage '{stage.Name}' threw exception (attempt {attempts}/{maxAttempts}): {ex}");

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
                    }

                    // Add stage metrics (from last attempt)
                    if (result != null)
                    {
                        context.Metrics.AddStageMetrics(result.Metrics);
                    }
                    else
                    {
                        // Should not happen, but safety check
                        result = StageResult.Failure($"Stage {stage.Name} failed to execute", new StageMetrics { StageName = stage.Name });
                    }

                    // Check if stage failed after all retries
                    if (!result.IsSuccess)
                    {
                        _logger?.Log($"Pipeline '{Name}' failed at stage '{stage.Name}' after {attempts} attempts: {result.ErrorMessage}");
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
                context.Progress?.Report(new ProgressEventArgs("Complete", (int)ProgressMultiplier));

                _logger?.Log($"Pipeline '{Name}' completed successfully in {context.Metrics.TotalDurationMs:F2}ms");

                // Extract final result from context
                var finalText = ExtractFinalTextFromContext(context);

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

        /// <summary>
        /// Extracts the final text result from pipeline context with clear precedence rules.
        /// </summary>
        private static string ExtractFinalTextFromContext(PipelineContext context)
        {
            // Try cleaned text first (highest priority)
            var cleanedText = context.GetData<string>("CleanedText");
            if (!string.IsNullOrEmpty(cleanedText))
            {
                return cleanedText;
            }

            // Fall back to raw transcription
            var rawTranscription = context.GetData<string>("RawTranscription");
            if (!string.IsNullOrEmpty(rawTranscription))
            {
                return rawTranscription;
            }

            // Default to empty string
            return string.Empty;
        }
    }
}
