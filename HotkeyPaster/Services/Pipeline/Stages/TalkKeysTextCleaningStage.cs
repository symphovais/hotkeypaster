using System;
using System.Threading.Tasks;
using TalkKeys.Services.Auth;
using TalkKeys.Services.Pipeline.Configuration;

namespace TalkKeys.Services.Pipeline.Stages
{
    /// <summary>
    /// Text cleaning stage using TalkKeys API proxy (for free tier users)
    /// </summary>
    public class TalkKeysTextCleaningStage : IPipelineStage
    {
        private readonly TalkKeysApiService _apiService;

        public string Name { get; }
        public string StageType => "TalkKeysTextCleaning";
        public int RetryCount => 2;
        public TimeSpan RetryDelay => TimeSpan.FromSeconds(1);

        public TalkKeysTextCleaningStage(TalkKeysApiService apiService, string? name = null)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            Name = name ?? "TalkKeys Text Cleaning";
        }

        public async Task<StageResult> ExecuteAsync(PipelineContext context)
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
                    return StageResult.Failure("Raw transcription not found in context", metrics);
                }

                // Get window context (if available)
                var windowContext = context.GetData<Windowing.WindowContext>("WindowContext");
                var contextHint = windowContext?.ProcessName;

                // Report progress
                context.Progress?.Report(new ProgressEventArgs("Cleaning text with TalkKeys...", 70));

                // Calculate before word count
                var beforeWordCount = rawText.Split(new[] { ' ', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries).Length;

                // Clean text via API proxy
                var result = await _apiService.CleanTextAsync(rawText, contextHint);

                if (!result.Success || string.IsNullOrWhiteSpace(result.CleanedText))
                {
                    metrics.EndTime = DateTime.UtcNow;
                    var error = result.Error ?? "Text cleaning returned empty result";
                    return StageResult.Failure(error, metrics);
                }

                // Store result in context
                context.SetData("CleanedText", result.CleanedText);

                // Calculate after word count
                var afterWordCount = result.CleanedText.Split(new[] { ' ', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries).Length;

                // Add metrics
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Provider", "TalkKeys");
                metrics.AddMetric("BeforeWordCount", beforeWordCount);
                metrics.AddMetric("AfterWordCount", afterWordCount);
                metrics.AddMetric("WordCountChange", afterWordCount - beforeWordCount);
                metrics.AddMetric("BeforeLength", rawText.Length);
                metrics.AddMetric("AfterLength", result.CleanedText.Length);

                // Report progress
                context.Progress?.Report(new ProgressEventArgs($"Cleaned {afterWordCount} words", 90));

                return StageResult.Success(metrics);
            }
            catch (Exception ex)
            {
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Exception", ex.Message);
                return StageResult.Failure($"TalkKeys text cleaning failed: {ex.Message}", metrics);
            }
        }
    }

    /// <summary>
    /// Factory for creating TalkKeysTextCleaningStage instances
    /// </summary>
    public class TalkKeysTextCleaningStageFactory : IPipelineStageFactory
    {
        public string StageType => "TalkKeysTextCleaning";

        public IPipelineStage CreateStage(StageConfiguration config, PipelineBuildContext buildContext)
        {
            var apiService = buildContext.TalkKeysApiService;

            if (apiService == null)
            {
                throw new InvalidOperationException(
                    "TalkKeysApiService not found in build context");
            }

            return new TalkKeysTextCleaningStage(apiService, config.Name);
        }
    }
}
