using System;
using System.Threading.Tasks;
using TalkKeys.Services.Pipeline.Configuration;
using TalkKeys.Services.Transcription;

namespace TalkKeys.Services.Pipeline.Stages
{
    /// <summary>
    /// Text cleaning stage using Groq with Llama models (very fast inference)
    /// </summary>
    public class GroqTextCleaningStage : IPipelineStage
    {
        private readonly GroqTextCleaner _cleaner;

        public string Name { get; }
        public string StageType => "GroqTextCleaning";
        public int RetryCount => 2;
        public TimeSpan RetryDelay => TimeSpan.FromSeconds(1);

        public GroqTextCleaningStage(string apiKey, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            _cleaner = new GroqTextCleaner(apiKey);
            Name = name ?? "Groq Text Cleaning";
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

                // Report progress
                context.Progress?.Report(new ProgressEventArgs("Cleaning text with Groq...", 70));

                // Calculate before word count
                var beforeWordCount = rawText.Split(new[] { ' ', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries).Length;

                // Clean text with streaming progress
                var cleanedText = await _cleaner.CleanAsync(rawText, partialText =>
                {
                    // Update progress with partial cleaned text
                    var currentWordCount = partialText.Split(new[] { ' ', '\n', '\r', '\t' },
                        StringSplitOptions.RemoveEmptyEntries).Length;

                    context.Progress?.Report(new ProgressEventArgs(
                        $"Cleaning... {currentWordCount} words",
                        80));
                }, windowContext);

                if (string.IsNullOrWhiteSpace(cleanedText))
                {
                    metrics.EndTime = DateTime.UtcNow;
                    return StageResult.Failure("Text cleaning returned empty result", metrics);
                }

                // Store result in context
                context.SetData("CleanedText", cleanedText);

                // Calculate after word count
                var afterWordCount = cleanedText.Split(new[] { ' ', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries).Length;

                // Add metrics
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Provider", "Groq");
                metrics.AddMetric("Model", "llama-3.1-8b-instant");
                metrics.AddMetric("BeforeWordCount", beforeWordCount);
                metrics.AddMetric("AfterWordCount", afterWordCount);
                metrics.AddMetric("WordCountChange", afterWordCount - beforeWordCount);
                metrics.AddMetric("BeforeLength", rawText.Length);
                metrics.AddMetric("AfterLength", cleanedText.Length);

                // Report progress
                context.Progress?.Report(new ProgressEventArgs($"Cleaned {afterWordCount} words", 90));

                return StageResult.Success(metrics);
            }
            catch (Exception ex)
            {
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Exception", ex.Message);
                return StageResult.Failure($"Groq text cleaning failed: {ex.Message}", metrics);
            }
        }
    }

    /// <summary>
    /// Factory for creating GroqTextCleaningStage instances
    /// </summary>
    public class GroqTextCleaningStageFactory : IPipelineStageFactory
    {
        public string StageType => "GroqTextCleaning";

        public IPipelineStage CreateStage(StageConfiguration config, PipelineBuildContext buildContext)
        {
            // Get API key from stage settings or build context
            var apiKey = SettingsHelper.GetString(config.Settings, "ApiKey") ?? buildContext.GroqApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Groq API key not found in stage settings or build context");
            }

            return new GroqTextCleaningStage(apiKey, config.Name);
        }
    }
}
