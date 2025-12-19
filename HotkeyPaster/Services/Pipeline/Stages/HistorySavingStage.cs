using System;
using System.Threading.Tasks;
using TalkKeys.Services.History;
using TalkKeys.Services.Pipeline.Configuration;

namespace TalkKeys.Services.Pipeline.Stages
{
    /// <summary>
    /// Pipeline stage that saves transcription results to history.
    /// Should be placed at the end of the pipeline, after text cleaning.
    /// </summary>
    public class HistorySavingStage : IPipelineStage
    {
        private readonly ITranscriptionHistoryService _historyService;

        public string Name { get; }
        public string StageType => "HistorySaving";
        public int RetryCount => 0; // No retries for history saving
        public TimeSpan RetryDelay => TimeSpan.Zero;

        public HistorySavingStage(ITranscriptionHistoryService historyService, string? name = null)
        {
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            Name = name ?? "Save to History";
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
                // Get transcription data from context
                var rawText = context.GetData<string>("RawTranscription") ?? string.Empty;
                var cleanedText = context.GetData<string>("CleanedText") ?? context.GetData<string>("RawTranscription") ?? string.Empty;
                var duration = context.GetData<double?>("AudioDuration") ?? 0;

                // Only save if we have actual text
                if (!string.IsNullOrWhiteSpace(rawText) || !string.IsNullOrWhiteSpace(cleanedText))
                {
                    _historyService.AddTranscription(rawText, cleanedText, duration);
                    metrics.AddMetric("Saved", true);
                    metrics.AddMetric("RawLength", rawText.Length);
                    metrics.AddMetric("CleanedLength", cleanedText.Length);
                }
                else
                {
                    metrics.AddMetric("Saved", false);
                    metrics.AddMetric("Reason", "No text to save");
                }

                metrics.EndTime = DateTime.UtcNow;
                return Task.FromResult(StageResult.Success(metrics));
            }
            catch (Exception ex)
            {
                // History saving failures should not fail the pipeline
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Error", ex.Message);

                // Return success anyway - we don't want history issues to break transcription
                return Task.FromResult(StageResult.Success(metrics));
            }
        }
    }

    /// <summary>
    /// Factory for creating HistorySavingStage instances
    /// </summary>
    public class HistorySavingStageFactory : IPipelineStageFactory
    {
        public string StageType => "HistorySaving";

        public IPipelineStage CreateStage(StageConfiguration config, PipelineBuildContext buildContext)
        {
            var historyService = buildContext.HistoryService;

            if (historyService == null)
            {
                throw new InvalidOperationException(
                    "ITranscriptionHistoryService not found in build context");
            }

            return new HistorySavingStage(historyService, config.Name);
        }
    }
}
