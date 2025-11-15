using System;
using System.Threading.Tasks;
using TalkKeys.Services.Pipeline.Configuration;
using TalkKeys.Services.Transcription;

namespace TalkKeys.Services.Pipeline.Stages
{
    /// <summary>
    /// Transcription stage using OpenAI Whisper API
    /// </summary>
    public class OpenAIWhisperTranscriptionStage : IPipelineStage
    {
        private readonly OpenAIWhisperTranscriber _transcriber;

        public string Name { get; }
        public string StageType => "OpenAIWhisperTranscription";

        public OpenAIWhisperTranscriptionStage(string apiKey, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            _transcriber = new OpenAIWhisperTranscriber(apiKey);
            Name = name ?? "OpenAI Whisper Transcription";
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
                context.Progress?.Report(new ProgressEventArgs("Transcribing with OpenAI Whisper...", 30));

                // Transcribe
                var transcription = await _transcriber.TranscribeAsync(audioData);

                if (string.IsNullOrWhiteSpace(transcription))
                {
                    metrics.EndTime = DateTime.UtcNow;
                    return StageResult.Failure("Transcription returned empty result", metrics);
                }

                // Store result in context
                context.SetData("RawTranscription", transcription);

                // Calculate word count
                var wordCount = transcription.Split(new[] { ' ', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries).Length;

                // Add metrics
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Provider", "OpenAI");
                metrics.AddMetric("Model", "whisper-1");
                metrics.AddMetric("WordCount", wordCount);
                metrics.AddMetric("CharacterCount", transcription.Length);

                // Report progress
                context.Progress?.Report(new ProgressEventArgs($"Transcribed {wordCount} words", 50));

                return StageResult.Success(metrics);
            }
            catch (Exception ex)
            {
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Exception", ex.Message);
                return StageResult.Failure($"OpenAI Whisper transcription failed: {ex.Message}", metrics);
            }
        }
    }

    /// <summary>
    /// Factory for creating OpenAIWhisperTranscriptionStage instances
    /// </summary>
    public class OpenAIWhisperTranscriptionStageFactory : IPipelineStageFactory
    {
        public string StageType => "OpenAIWhisperTranscription";

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

            return new OpenAIWhisperTranscriptionStage(apiKey, config.Name);
        }
    }
}
