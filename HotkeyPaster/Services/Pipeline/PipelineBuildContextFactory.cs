using TalkKeys.Logging;
using TalkKeys.Services.Settings;

namespace TalkKeys.Services.Pipeline
{
    /// <summary>
    /// Factory for creating PipelineBuildContext instances with consistent configuration
    /// </summary>
    public class PipelineBuildContextFactory
    {
        private readonly ILogger _logger;

        public PipelineBuildContextFactory(ILogger logger)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new PipelineBuildContext from application settings
        /// </summary>
        public PipelineBuildContext Create(AppSettings settings)
        {
            if (settings == null)
                throw new System.ArgumentNullException(nameof(settings));

            return new PipelineBuildContext
            {
                Logger = _logger,
                AppSettings = settings,
                OpenAIApiKey = settings.OpenAIApiKey,
                LocalModelPath = settings.LocalModelPath
            };
        }
    }
}
