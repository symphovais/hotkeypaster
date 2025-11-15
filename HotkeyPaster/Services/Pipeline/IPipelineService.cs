using System;
using System.Threading;
using System.Threading.Tasks;
using TalkKeys.Services.Windowing;

namespace TalkKeys.Services.Pipeline
{
    /// <summary>
    /// Service interface for executing pipelines.
    /// Replaces IAudioTranscriptionService with pipeline-based architecture.
    /// </summary>
    public interface IPipelineService
    {
        /// <summary>
        /// Execute the default pipeline on audio data
        /// </summary>
        Task<PipelineResult> ExecuteAsync(
            byte[] audioData,
            WindowContext? windowContext = null,
            IProgress<ProgressEventArgs>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute a specific pipeline by name
        /// </summary>
        Task<PipelineResult> ExecuteAsync(
            string pipelineName,
            byte[] audioData,
            WindowContext? windowContext = null,
            IProgress<ProgressEventArgs>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the name of the currently active default pipeline
        /// </summary>
        string? GetDefaultPipelineName();

        /// <summary>
        /// Set the default pipeline
        /// </summary>
        void SetDefaultPipeline(string pipelineName);

        /// <summary>
        /// Get all available pipeline names
        /// </summary>
        string[] GetAvailablePipelines();

        /// <summary>
        /// Reload pipeline configurations from disk
        /// </summary>
        void ReloadConfigurations();
    }
}
