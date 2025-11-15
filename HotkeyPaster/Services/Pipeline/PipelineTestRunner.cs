using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HotkeyPaster.Logging;

namespace HotkeyPaster.Services.Pipeline
{
    /// <summary>
    /// Runs multiple pipeline configurations on the same audio for comparison
    /// </summary>
    public class PipelineTestRunner
    {
        private readonly ILogger? _logger;

        public PipelineTestRunner(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Run multiple pipelines on the same audio and compare results
        /// </summary>
        public async Task<PipelineComparisonResult> RunComparisonAsync(
            byte[] audioData,
            params Pipeline[] pipelines)
        {
            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));

            if (pipelines == null || pipelines.Length == 0)
                throw new ArgumentException("At least one pipeline must be provided", nameof(pipelines));

            _logger?.Log($"Starting pipeline comparison with {pipelines.Length} pipelines on {audioData.Length} bytes of audio");

            var results = new List<PipelineResult>();
            var stopwatch = Stopwatch.StartNew();

            foreach (var pipeline in pipelines)
            {
                _logger?.Log($"Running pipeline: {pipeline.Name}");

                // Create fresh context for each pipeline
                var context = new PipelineContext();
                context.SetData("AudioData", audioData);

                try
                {
                    var result = await pipeline.ExecuteAsync(context);
                    results.Add(result);

                    if (result.IsSuccess)
                    {
                        _logger?.Log($"Pipeline '{pipeline.Name}' completed: {result.WordCount} words in {result.Metrics.TotalDurationMs:F2}ms");
                    }
                    else
                    {
                        _logger?.Log($"Pipeline '{pipeline.Name}' failed: {result.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"Pipeline '{pipeline.Name}' threw exception: {ex.Message}");

                    // Create error result
                    results.Add(new PipelineResult
                    {
                        IsSuccess = false,
                        ErrorMessage = ex.Message,
                        Metrics = new PipelineMetrics { PipelineName = pipeline.Name }
                    });
                }
            }

            stopwatch.Stop();

            var comparison = new PipelineComparisonResult
            {
                Results = results,
                TotalComparisonDurationMs = stopwatch.Elapsed.TotalMilliseconds
            };

            _logger?.Log($"Pipeline comparison completed in {comparison.TotalComparisonDurationMs:F2}ms");
            _logger?.Log(comparison.GetSummary());

            return comparison;
        }
    }

    /// <summary>
    /// Results from comparing multiple pipeline runs
    /// </summary>
    public class PipelineComparisonResult
    {
        /// <summary>
        /// Results from each pipeline
        /// </summary>
        public List<PipelineResult> Results { get; init; } = new();

        /// <summary>
        /// Total time for all comparisons
        /// </summary>
        public double TotalComparisonDurationMs { get; init; }

        /// <summary>
        /// Get the fastest successful pipeline
        /// </summary>
        public PipelineResult? FastestPipeline =>
            Results.Where(r => r.IsSuccess)
                   .OrderBy(r => r.Metrics.TotalDurationMs)
                   .FirstOrDefault();

        /// <summary>
        /// Get the slowest successful pipeline
        /// </summary>
        public PipelineResult? SlowestPipeline =>
            Results.Where(r => r.IsSuccess)
                   .OrderByDescending(r => r.Metrics.TotalDurationMs)
                   .FirstOrDefault();

        /// <summary>
        /// Get summary statistics
        /// </summary>
        public string GetSummary()
        {
            var summary = $"Pipeline Comparison Summary\n";
            summary += $"===========================\n";
            summary += $"Total Comparison Duration: {TotalComparisonDurationMs:F2}ms\n";
            summary += $"Pipelines Tested: {Results.Count}\n";
            summary += $"Successful: {Results.Count(r => r.IsSuccess)}\n";
            summary += $"Failed: {Results.Count(r => !r.IsSuccess)}\n\n";

            foreach (var result in Results.OrderBy(r => r.Metrics.TotalDurationMs))
            {
                summary += $"Pipeline: {result.Metrics.PipelineName}\n";
                summary += $"  Status: {(result.IsSuccess ? "SUCCESS" : "FAILED")}\n";

                if (result.IsSuccess)
                {
                    summary += $"  Duration: {result.Metrics.TotalDurationMs:F2}ms\n";
                    summary += $"  Word Count: {result.WordCount}\n";
                    summary += $"  Stages: {result.Metrics.StageMetrics.Count}\n";

                    // Stage breakdown
                    foreach (var stage in result.Metrics.StageMetrics)
                    {
                        summary += $"    - {stage.StageName}: {stage.DurationMs:F2}ms";
                        if (stage.CustomMetrics.Any())
                        {
                            var metrics = string.Join(", ", stage.CustomMetrics.Select(kv => $"{kv.Key}={kv.Value}"));
                            summary += $" ({metrics})";
                        }
                        summary += "\n";
                    }
                }
                else
                {
                    summary += $"  Error: {result.ErrorMessage}\n";
                    summary += $"  Failed Stage: {result.FailedStageName}\n";
                }

                summary += "\n";
            }

            // Comparison statistics
            var successful = Results.Where(r => r.IsSuccess).ToList();
            if (successful.Count > 1)
            {
                var fastest = FastestPipeline;
                var slowest = SlowestPipeline;

                if (fastest != null && slowest != null)
                {
                    var speedup = slowest.Metrics.TotalDurationMs / fastest.Metrics.TotalDurationMs;
                    summary += $"Performance:\n";
                    summary += $"  Fastest: {fastest.Metrics.PipelineName} ({fastest.Metrics.TotalDurationMs:F2}ms)\n";
                    summary += $"  Slowest: {slowest.Metrics.PipelineName} ({slowest.Metrics.TotalDurationMs:F2}ms)\n";
                    summary += $"  Speedup: {speedup:F2}x\n";
                }
            }

            return summary;
        }
    }
}
