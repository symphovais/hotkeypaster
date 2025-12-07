using TalkKeys.Logging;
using TalkKeys.Services.Settings;

namespace TalkKeys.Services.Pipeline
{
    /// <summary>
    /// Context for building pipelines, provides access to services and settings
    /// </summary>
    public class PipelineBuildContext
    {
        /// <summary>
        /// Logger for pipeline stages
        /// </summary>
        public ILogger? Logger { get; init; }

        /// <summary>
        /// Application settings
        /// </summary>
        public AppSettings? AppSettings { get; init; }

        /// <summary>
        /// OpenAI API key (if available)
        /// </summary>
        public string? OpenAIApiKey { get; init; }

        /// <summary>
        /// Groq API key (if available)
        /// </summary>
        public string? GroqApiKey { get; init; }

        /// <summary>
        /// Local Whisper model path (if available)
        /// </summary>
        public string? LocalModelPath { get; init; }
    }
}
