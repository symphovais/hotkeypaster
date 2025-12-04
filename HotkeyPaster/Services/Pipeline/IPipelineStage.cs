using System.Threading.Tasks;

namespace TalkKeys.Services.Pipeline
{
    /// <summary>
    /// Base interface for all pipeline stages.
    /// Each stage transforms input to output and collects metrics.
    /// </summary>
    public interface IPipelineStage
    {
        /// <summary>
        /// Unique name of this stage (e.g., "AudioValidation", "WhisperTranscription")
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Stage type identifier for configuration matching
        /// </summary>
        string StageType { get; }

        /// <summary>
        /// Execute the stage with the given context
        /// </summary>
        Task<StageResult> ExecuteAsync(PipelineContext context);

        /// <summary>
        /// Number of times to retry this stage if it fails
        /// </summary>
        int RetryCount { get; }

        /// <summary>
        /// Delay between retries
        /// </summary>
        System.TimeSpan RetryDelay { get; }
    }
}
