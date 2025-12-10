using System;
using System.IO;
using System.Threading.Tasks;
using TalkKeys.Services.Auth;
using TalkKeys.Services.Pipeline.Configuration;

namespace TalkKeys.Services.Pipeline.Stages
{
    /// <summary>
    /// Transcription stage using TalkKeys API proxy (for free tier users)
    /// </summary>
    public class TalkKeysTranscriptionStage : IPipelineStage
    {
        private readonly TalkKeysApiService _apiService;

        public string Name { get; }
        public string StageType => "TalkKeysTranscription";
        public int RetryCount => 2;
        public TimeSpan RetryDelay => TimeSpan.FromSeconds(1);

        public TalkKeysTranscriptionStage(TalkKeysApiService apiService, string? name = null)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            Name = name ?? "TalkKeys Transcription";
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
                // Get audio data from context
                var audioData = context.GetData<byte[]>("AudioData");

                if (audioData == null || audioData.Length == 0)
                {
                    metrics.EndTime = DateTime.UtcNow;
                    return StageResult.Failure("Audio data not found in context", metrics);
                }

                // Report progress
                context.Progress?.Report(new ProgressEventArgs("Transcribing with TalkKeys...", 30));

                // Transcribe via API proxy
                using var audioStream = new MemoryStream(audioData);
                var result = await _apiService.TranscribeAsync(audioStream, "audio.wav");

                if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
                {
                    metrics.EndTime = DateTime.UtcNow;
                    var error = result.Error ?? "Transcription returned empty result";
                    return StageResult.Failure(error, metrics);
                }

                // Store result in context
                context.SetData("RawTranscription", result.Text);

                // Calculate word count
                var wordCount = result.Text.Split(new[] { ' ', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries).Length;

                // Add metrics
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Provider", "TalkKeys");
                metrics.AddMetric("WordCount", wordCount);
                metrics.AddMetric("CharacterCount", result.Text.Length);

                // Report progress
                context.Progress?.Report(new ProgressEventArgs($"Transcribed {wordCount} words", 50));

                return StageResult.Success(metrics);
            }
            catch (Exception ex)
            {
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Exception", ex.Message);
                return StageResult.Failure($"TalkKeys transcription failed: {ex.Message}", metrics);
            }
        }
    }

    /// <summary>
    /// Factory for creating TalkKeysTranscriptionStage instances
    /// </summary>
    public class TalkKeysTranscriptionStageFactory : IPipelineStageFactory
    {
        public string StageType => "TalkKeysTranscription";

        public IPipelineStage CreateStage(StageConfiguration config, PipelineBuildContext buildContext)
        {
            var apiService = buildContext.TalkKeysApiService;

            if (apiService == null)
            {
                throw new InvalidOperationException(
                    "TalkKeysApiService not found in build context");
            }

            return new TalkKeysTranscriptionStage(apiService, config.Name);
        }
    }
}
