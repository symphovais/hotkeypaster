namespace HotkeyPaster.Services.Pipeline
{
    /// <summary>
    /// Result of a pipeline stage execution
    /// </summary>
    public class StageResult
    {
        /// <summary>
        /// Whether the stage completed successfully
        /// </summary>
        public bool IsSuccess { get; init; }

        /// <summary>
        /// Error message if stage failed
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Metrics collected during stage execution
        /// </summary>
        public StageMetrics Metrics { get; init; } = new();

        /// <summary>
        /// Create a success result
        /// </summary>
        public static StageResult Success(StageMetrics metrics)
        {
            return new StageResult
            {
                IsSuccess = true,
                Metrics = metrics
            };
        }

        /// <summary>
        /// Create a failure result
        /// </summary>
        public static StageResult Failure(string errorMessage, StageMetrics metrics)
        {
            return new StageResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Metrics = metrics
            };
        }
    }
}
