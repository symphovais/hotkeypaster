using TalkKeys.Services.Pipeline.Configuration;

namespace TalkKeys.Services.Pipeline
{
    /// <summary>
    /// Factory interface for creating pipeline stages from configuration
    /// </summary>
    public interface IPipelineStageFactory
    {
        /// <summary>
        /// Stage type this factory creates (matches StageConfiguration.Type)
        /// </summary>
        string StageType { get; }

        /// <summary>
        /// Create a stage instance from configuration
        /// </summary>
        IPipelineStage CreateStage(StageConfiguration config, PipelineBuildContext buildContext);
    }
}
