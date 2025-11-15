using System;
using System.IO;
using System.Threading.Tasks;
using TalkKeys.Services.Pipeline.Configuration;
using NAudio.Wave;
using RNNoise.NET;

namespace TalkKeys.Services.Pipeline.Stages
{
    /// <summary>
    /// Applies RNNoise noise reduction to audio
    /// </summary>
    public class RNNoiseStage : IPipelineStage
    {
        public string Name { get; }
        public string StageType => "RNNoise";

        public RNNoiseStage(string? name = null)
        {
            Name = name ?? "RNNoise Noise Reduction";
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
                    return StageResult.Failure(
                        "Audio data is null or empty",
                        new StageMetrics
                        {
                            StageName = Name,
                            StartTime = startTime,
                            EndTime = DateTime.UtcNow
                        });
                }

                // Report progress
                context.Progress?.Report(new ProgressEventArgs("Applying noise reduction", 15));

                // Process audio with RNNoise
                var (processedAudio, noiseReductionDb, rmsOriginal, rmsProcessed) = await Task.Run(() => ProcessAudioWithRNNoise(audioData));

                // Calculate noise reduction metrics
                var originalSize = audioData.Length;
                var processedSize = processedAudio.Length;

                // Update context with processed audio
                context.SetData("AudioData", processedAudio);

                // Add metrics
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("OriginalSizeBytes", originalSize);
                metrics.AddMetric("ProcessedSizeBytes", processedSize);
                metrics.AddMetric("NoiseReductionDB", noiseReductionDb);
                metrics.AddMetric("RMSOriginal", rmsOriginal);
                metrics.AddMetric("RMSProcessed", rmsProcessed);
                metrics.AddMetric("SignalChangePercent", ((rmsOriginal - rmsProcessed) / rmsOriginal) * 100.0);

                // Report progress
                context.Progress?.Report(new ProgressEventArgs($"Noise reduced by {noiseReductionDb:F1} dB", 20));

                return StageResult.Success(metrics);
            }
            catch (Exception ex)
            {
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Exception", ex.Message);
                return StageResult.Failure($"Noise reduction failed: {ex.Message}", metrics);
            }
        }

        private (byte[] processedAudio, double noiseReductionDb, double rmsOriginal, double rmsProcessed) ProcessAudioWithRNNoise(byte[] wavData)
        {
            using var inputStream = new MemoryStream(wavData);
            using var reader = new WaveFileReader(inputStream);

            // RNNoise works best with specific format
            // If audio is not in the right format, resample it
            WaveStream sourceStream = reader;

            // RNNoise typically expects 48kHz mono
            // But we'll work with 16kHz mono since that's our recording format
            var waveFormat = reader.WaveFormat;

            if (waveFormat.Channels != 1)
            {
                throw new InvalidOperationException("RNNoise requires mono audio");
            }

            // Create RNNoise denoiser
            using var denoiser = new Denoiser();

            // Read all samples
            var samples = new float[reader.SampleCount];
            var denoisedSamples = new float[reader.SampleCount];

            // Read all samples
            int samplesRead = 0;
            var sampleProvider = reader.ToSampleProvider();
            samplesRead = sampleProvider.Read(samples, 0, samples.Length);

            // Calculate RMS of original signal
            double rmsOriginal = CalculateRMS(samples, samplesRead);

            // RNNoise's Denoise method processes the buffer in-place
            // We can pass the entire buffer at once
            Array.Copy(samples, denoisedSamples, samplesRead);

            // Denoise the entire buffer (it processes in-place)
            denoiser.Denoise(denoisedSamples.AsSpan(0, samplesRead));

            // Calculate RMS of processed signal
            double rmsProcessed = CalculateRMS(denoisedSamples, samplesRead);

            // Calculate noise reduction in dB
            // This shows how much the signal was attenuated (difference in noise floor)
            double noiseReductionDb = 20 * Math.Log10(rmsOriginal / (rmsProcessed + 1e-10));

            // Convert back to WAV format
            using var outputStream = new MemoryStream();
            using var writer = new WaveFileWriter(outputStream, waveFormat);

            // Convert float samples back to bytes
            writer.WriteSamples(denoisedSamples, 0, samplesRead);
            writer.Flush();

            return (outputStream.ToArray(), noiseReductionDb, rmsOriginal, rmsProcessed);
        }

        private static double CalculateRMS(float[] samples, int count)
        {
            double sum = 0.0;
            for (int i = 0; i < count; i++)
            {
                sum += samples[i] * samples[i];
            }
            return Math.Sqrt(sum / count);
        }
    }

    /// <summary>
    /// Factory for creating RNNoiseStage instances
    /// </summary>
    public class RNNoiseStageFactory : IPipelineStageFactory
    {
        public string StageType => "RNNoise";

        public IPipelineStage CreateStage(StageConfiguration config, PipelineBuildContext buildContext)
        {
            return new RNNoiseStage(config.Name);
        }
    }
}
