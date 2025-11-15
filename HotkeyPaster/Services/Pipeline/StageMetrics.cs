using System;
using System.Collections.Generic;

namespace HotkeyPaster.Services.Pipeline
{
    /// <summary>
    /// Metrics collected during a single pipeline stage execution
    /// </summary>
    public class StageMetrics
    {
        /// <summary>
        /// Name of the stage
        /// </summary>
        public string StageName { get; set; } = string.Empty;

        /// <summary>
        /// When the stage started
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When the stage completed
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Duration of stage execution
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// Duration in milliseconds (for easy display)
        /// </summary>
        public double DurationMs => Duration.TotalMilliseconds;

        /// <summary>
        /// Custom metrics specific to this stage
        /// Examples:
        /// - "WordCount": 150
        /// - "ModelUsed": "gpt-4.1-nano"
        /// - "ApiCalls": 2
        /// - "BytesProcessed": 125000
        /// - "NoiseReductionDb": 15.5
        /// - "TrimmedDurationSeconds": 2.3
        /// </summary>
        public Dictionary<string, object> CustomMetrics { get; } = new();

        /// <summary>
        /// Helper to add a custom metric
        /// </summary>
        public void AddMetric(string key, object value)
        {
            CustomMetrics[key] = value;
        }

        /// <summary>
        /// Helper to get a typed custom metric
        /// </summary>
        public T? GetMetric<T>(string key)
        {
            if (CustomMetrics.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return default;
        }
    }
}
