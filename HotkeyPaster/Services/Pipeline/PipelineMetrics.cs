using System;
using System.Collections.Generic;
using System.Linq;

namespace HotkeyPaster.Services.Pipeline
{
    /// <summary>
    /// Aggregated metrics for an entire pipeline execution
    /// </summary>
    public class PipelineMetrics
    {
        /// <summary>
        /// Name of the pipeline configuration
        /// </summary>
        public string PipelineName { get; set; } = string.Empty;

        /// <summary>
        /// When the pipeline started
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When the pipeline completed
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Total duration of pipeline execution
        /// </summary>
        public TimeSpan? TotalDuration => EndTime.HasValue ? EndTime.Value - StartTime : null;

        /// <summary>
        /// Total duration in milliseconds
        /// </summary>
        public double? TotalDurationMs => TotalDuration?.TotalMilliseconds;

        /// <summary>
        /// Metrics from each stage in execution order
        /// </summary>
        public List<StageMetrics> StageMetrics { get; } = new();

        /// <summary>
        /// Global metrics for the entire pipeline
        /// Examples:
        /// - "TotalWordCount": 150
        /// - "AudioDurationSeconds": 12.5
        /// - "EstimatedCost": 0.05
        /// - "Language": "en"
        /// </summary>
        public Dictionary<string, object> GlobalMetrics { get; } = new();

        /// <summary>
        /// Add a stage's metrics to the collection
        /// </summary>
        public void AddStageMetrics(StageMetrics metrics)
        {
            StageMetrics.Add(metrics);
        }

        /// <summary>
        /// Set a global metric
        /// </summary>
        public void SetGlobalMetric(string key, object value)
        {
            GlobalMetrics[key] = value;
        }

        /// <summary>
        /// Get a typed global metric
        /// </summary>
        public T? GetGlobalMetric<T>(string key)
        {
            if (GlobalMetrics.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return default;
        }

        /// <summary>
        /// Get summary statistics
        /// </summary>
        public string GetSummary()
        {
            var summary = $"Pipeline: {PipelineName}\n";
            summary += $"Total Duration: {TotalDurationMs:F2}ms\n";
            summary += $"Stages: {StageMetrics.Count}\n";

            foreach (var stage in StageMetrics)
            {
                summary += $"  - {stage.StageName}: {stage.DurationMs:F2}ms";
                if (stage.CustomMetrics.Any())
                {
                    var metrics = string.Join(", ", stage.CustomMetrics.Select(kv => $"{kv.Key}={kv.Value}"));
                    summary += $" ({metrics})";
                }
                summary += "\n";
            }

            if (GlobalMetrics.Any())
            {
                summary += "Global Metrics:\n";
                foreach (var metric in GlobalMetrics)
                {
                    summary += $"  - {metric.Key}: {metric.Value}\n";
                }
            }

            return summary;
        }
    }
}
