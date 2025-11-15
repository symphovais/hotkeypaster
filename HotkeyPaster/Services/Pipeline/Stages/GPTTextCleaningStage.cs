using System;
using System.Threading.Tasks;
using HotkeyPaster.Services.Pipeline.Configuration;
using HotkeyPaster.Services.Transcription;

namespace HotkeyPaster.Services.Pipeline.Stages
{
    /// <summary>
    /// Text cleaning stage using OpenAI GPT
    /// </summary>
    public class GPTTextCleaningStage : IPipelineStage
    {
        private readonly OpenAIGPTTextCleaner _cleaner;

        public string Name { get; }
        public string StageType => "GPTTextCleaning";

        public GPTTextCleaningStage(string apiKey, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            _cleaner = new OpenAIGPTTextCleaner(apiKey);
            Name = name ?? "GPT Text Cleaning";
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
                context.Progress?.Report(new ProgressEventArgs("Cleaning text with GPT...", 70));

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
                metrics.AddMetric("Model", "gpt-4.1-nano");
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
                return StageResult.Failure($"GPT text cleaning failed: {ex.Message}", metrics);
            }
        }
    }

    /// <summary>
    /// Factory for creating GPTTextCleaningStage instances
    /// </summary>
    public class GPTTextCleaningStageFactory : IPipelineStageFactory
    {
        public string StageType => "GPTTextCleaning";

        public IPipelineStage CreateStage(StageConfiguration config, PipelineBuildContext buildContext)
        {
            // Get API key from stage settings or build context
            var apiKey = config.Settings.TryGetValue("ApiKey", out var keyObj) && keyObj is string key
                ? key
                : buildContext.OpenAIApiKey;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "OpenAI API key not found in stage settings or build context");
            }

            return new GPTTextCleaningStage(apiKey, config.Name);
        }
    }
}
