using System;
using System.Threading.Tasks;
using HotkeyPaster.Services.Pipeline.Configuration;
using HotkeyPaster.Services.Transcription;

namespace HotkeyPaster.Services.Pipeline.Stages
{
    /// <summary>
    /// Transcription stage using local Whisper.net model
    /// </summary>
    public class LocalWhisperTranscriptionStage : IPipelineStage
    {
        private readonly LocalWhisperTranscriber _transcriber;

        public string Name { get; }
        public string StageType => "LocalWhisperTranscription";

        public LocalWhisperTranscriptionStage(string modelPath, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("Model path cannot be null or empty", nameof(modelPath));

            _transcriber = new LocalWhisperTranscriber(modelPath);
            Name = name ?? "Local Whisper Transcription";
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
                context.Progress?.Report(new ProgressEventArgs("Transcribing with local Whisper model...", 30));

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
                metrics.AddMetric("Provider", "Local");
                metrics.AddMetric("Model", "Whisper.net");
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
                return StageResult.Failure($"Local Whisper transcription failed: {ex.Message}", metrics);
            }
        }
    }

    /// <summary>
    /// Factory for creating LocalWhisperTranscriptionStage instances
    /// </summary>
    public class LocalWhisperTranscriptionStageFactory : IPipelineStageFactory
    {
        public string StageType => "LocalWhisperTranscription";

        public IPipelineStage CreateStage(StageConfiguration config, PipelineBuildContext buildContext)
        {
            // Get model path from stage settings or build context
            var modelPath = config.Settings.TryGetValue("ModelPath", out var pathObj) && pathObj is string path
                ? path
                : buildContext.LocalModelPath;

            if (string.IsNullOrWhiteSpace(modelPath))
            {
                throw new InvalidOperationException(
                    "Local Whisper model path not found in stage settings or build context");
            }

            return new LocalWhisperTranscriptionStage(modelPath, config.Name);
        }
    }
}
