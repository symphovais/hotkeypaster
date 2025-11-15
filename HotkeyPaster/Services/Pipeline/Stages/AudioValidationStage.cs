using System;
using System.Threading.Tasks;
using HotkeyPaster.Services.Pipeline.Configuration;

namespace HotkeyPaster.Services.Pipeline.Stages
{
    /// <summary>
    /// Validates audio data and extracts metadata
    /// </summary>
    public class AudioValidationStage : IPipelineStage
    {
        public string Name { get; }
        public string StageType => "AudioValidation";

        public AudioValidationStage(string? name = null)
        {
            Name = name ?? "Audio Validation";
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
                // Get audio data from context
                var audioData = context.GetData<byte[]>("AudioData");

                if (audioData == null || audioData.Length == 0)
                {
                    return Task.FromResult(StageResult.Failure(
                        "Audio data is null or empty",
                        new StageMetrics
                        {
                            StageName = Name,
                            StartTime = startTime,
                            EndTime = DateTime.UtcNow
                        }));
                }

                // Validate size (25MB OpenAI limit)
                const int maxSize = 26_214_400; // 25MB
                if (audioData.Length > maxSize)
                {
                    return Task.FromResult(StageResult.Failure(
                        $"Audio file exceeds 25MB limit (size: {audioData.Length / 1_048_576.0:F2}MB)",
                        new StageMetrics
                        {
                            StageName = Name,
                            StartTime = startTime,
                            EndTime = DateTime.UtcNow
                        }));
                }

                // Calculate audio duration from WAV format
                // WAV format: 16kHz, 16-bit (2 bytes), mono (1 channel)
                double? durationSeconds = null;
                try
                {
                    const int wavHeaderSize = 44;
                    if (audioData.Length > wavHeaderSize)
                    {
                        int audioBytes = audioData.Length - wavHeaderSize;
                        const int sampleRate = 16000;
                        const int bytesPerSample = 2; // 16-bit
                        const int channels = 1; // mono
                        durationSeconds = (double)audioBytes / (sampleRate * bytesPerSample * channels);

                        // Store in context for other stages
                        context.SetData("AudioDuration", durationSeconds.Value);
                    }
                }
                catch
                {
                    // Duration calculation is optional
                }

                // Add metrics
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("AudioSizeBytes", audioData.Length);
                metrics.AddMetric("AudioSizeMB", audioData.Length / 1_048_576.0);

                if (durationSeconds.HasValue)
                {
                    metrics.AddMetric("AudioDurationSeconds", durationSeconds.Value);
                }

                // Report progress
                context.Progress?.Report(new ProgressEventArgs("Audio validated", 10));

                return Task.FromResult(StageResult.Success(metrics));
            }
            catch (Exception ex)
            {
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Exception", ex.Message);
                return Task.FromResult(StageResult.Failure($"Audio validation failed: {ex.Message}", metrics));
            }
        }
    }

    /// <summary>
    /// Factory for creating AudioValidationStage instances
    /// </summary>
    public class AudioValidationStageFactory : IPipelineStageFactory
    {
        public string StageType => "AudioValidation";

        public IPipelineStage CreateStage(StageConfiguration config, PipelineBuildContext buildContext)
        {
            return new AudioValidationStage(config.Name);
        }
    }
}
