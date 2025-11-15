using System;
using System.Threading.Tasks;
using TalkKeys.Services.Pipeline.Configuration;

namespace TalkKeys.Services.Pipeline.Stages
{
    /// <summary>
    /// Pass-through stage that copies raw transcription to cleaned text without modifications
    /// </summary>
    public class PassThroughCleaningStage : IPipelineStage
    {
        public string Name { get; }
        public string StageType => "PassThroughCleaning";

        public PassThroughCleaningStage(string? name = null)
        {
            Name = name ?? "Pass Through (No Cleaning)";
        }

        public Task<StageResult> ExecuteAsync(PipelineContext context)
        {
            var startTime = DateTime.UtcNow;
            var metrics = new StageMetrics
            {
                StageName = Name,
                StartTime = startTime
            };

            try
            {
                // Get raw transcription from context
                var rawText = context.GetData<string>("RawTranscription");

                if (string.IsNullOrWhiteSpace(rawText))
                {
                    metrics.EndTime = DateTime.UtcNow;
                    return Task.FromResult(StageResult.Failure("Raw transcription not found in context", metrics));
                }

                // Simply copy raw to cleaned (no processing)
                context.SetData("CleanedText", rawText);

                // Calculate word count
                var wordCount = rawText.Split(new[] { ' ', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries).Length;

                // Add metrics
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("WordCount", wordCount);
                metrics.AddMetric("CharacterCount", rawText.Length);
                metrics.AddMetric("Modified", false);

                // Report progress
                context.Progress?.Report(new ProgressEventArgs("Text ready (no cleaning)", 90));

                return Task.FromResult(StageResult.Success(metrics));
            }
            catch (Exception ex)
            {
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Exception", ex.Message);
                return Task.FromResult(StageResult.Failure($"Pass-through stage failed: {ex.Message}", metrics));
            }
        }
    }

    /// <summary>
    /// Factory for creating PassThroughCleaningStage instances
    /// </summary>
    public class PassThroughCleaningStageFactory : IPipelineStageFactory
    {
        public string StageType => "PassThroughCleaning";

        public IPipelineStage CreateStage(StageConfiguration config, PipelineBuildContext buildContext)
        {
            return new PassThroughCleaningStage(config.Name);
        }
    }
}
