using System;
using System.Collections.Generic;
using System.Threading;
using TalkKeys.Services.Windowing;

namespace TalkKeys.Services.Pipeline
{
    /// <summary>
    /// Context object passed between pipeline stages.
    /// Contains shared data, metrics, and configuration.
    /// </summary>
    public class PipelineContext
    {
        /// <summary>
        /// Shared data dictionary for passing data between stages.
        /// Common keys:
        /// - "AudioData" (byte[]): Raw audio input
        /// - "AudioDuration" (double): Duration in seconds
        /// - "RawTranscription" (string): Unprocessed transcription
        /// - "CleanedText" (string): Final cleaned text
        /// - "WindowContext" (WindowContext): User's active window info
        /// </summary>
        public Dictionary<string, object> Data { get; } = new();

        /// <summary>
        /// Metrics collection for the entire pipeline run
        /// </summary>
        public PipelineMetrics Metrics { get; } = new();

        /// <summary>
        /// Cancellation token for stopping pipeline execution
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Progress reporter for UI updates
        /// </summary>
        public IProgress<ProgressEventArgs>? Progress { get; set; }

        /// <summary>
        /// Pipeline-wide settings from configuration
        /// </summary>
        public Dictionary<string, object> Settings { get; } = new();

        /// <summary>
        /// Helper to get typed data from context
        /// </summary>
        public T? GetData<T>(string key)
        {
            if (Data.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return default;
        }

        /// <summary>
        /// Helper to set data in context
        /// </summary>
        public void SetData(string key, object value)
        {
            Data[key] = value;
        }

        /// <summary>
        /// Helper to get typed setting
        /// </summary>
        public T? GetSetting<T>(string key)
        {
            if (Settings.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return default;
        }
    }
}
