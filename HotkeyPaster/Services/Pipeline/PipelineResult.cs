namespace HotkeyPaster.Services.Pipeline
{
    /// <summary>
    /// Final result of a complete pipeline execution
    /// </summary>
    public class PipelineResult
    {
        /// <summary>
        /// Whether the entire pipeline succeeded
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// Final output text (cleaned and ready to paste)
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Error message if pipeline failed
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Name of the stage that failed (if any)
        /// </summary>
        public string? FailedStageName { get; init; }

        /// <summary>
        /// Complete metrics for the pipeline run
        /// </summary>
        public PipelineMetrics Metrics { get; init; } = new();

        /// <summary>
        /// The pipeline context (contains all intermediate data)
        /// </summary>
        public PipelineContext Context { get; init; } = new();

        /// <summary>
        /// Detected language (if available)
        /// </summary>
        public string? Language { get; init; }

        /// <summary>
        /// Audio duration in seconds
        /// </summary>
        public double? DurationSeconds { get; init; }

        /// <summary>
        /// Final word count
        /// </summary>
        public int WordCount { get; init; }
    }
}
