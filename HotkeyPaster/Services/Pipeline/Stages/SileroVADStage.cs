using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TalkKeys.Services.Pipeline.Configuration;
using NAudio.Wave;
using SileroVad;

namespace TalkKeys.Services.Pipeline.Stages
{
    /// <summary>
    /// Applies Silero VAD to detect voice activity and trim silence
    /// </summary>
    public class SileroVADStage : IPipelineStage
    {
        private readonly float _threshold;
        private readonly int _minSpeechDurationMs;
        private readonly int _minSilenceDurationMs;

        public string Name { get; }
        public string StageType => "SileroVAD";
        public int RetryCount => 0;
        public TimeSpan RetryDelay => TimeSpan.Zero;

        public SileroVADStage(
            string? name = null,
            float threshold = 0.5f,
            int minSpeechDurationMs = 250,
            int minSilenceDurationMs = 100)
        {
            Name = name ?? "Silero VAD Trimming";
            _threshold = threshold;
            _minSpeechDurationMs = minSpeechDurationMs;
            _minSilenceDurationMs = minSilenceDurationMs;
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
                context.Progress?.Report(new ProgressEventArgs("Detecting voice activity", 25));

                // Get original duration
                var originalDuration = context.GetData<double>("AudioDuration");

                // Process audio with Silero VAD
                var (trimmedAudio, trimmedDuration, speechSegments) = await Task.Run(() =>
                    ProcessAudioWithVAD(audioData));

                // Calculate trimming metrics
                var originalSize = audioData.Length;
                var trimmedSize = trimmedAudio.Length;
                var sizeReductionBytes = originalSize - trimmedSize;
                var sizeReductionPercentage = (1.0 - (double)trimmedSize / originalSize) * 100.0;
                var durationReductionSeconds = originalDuration - trimmedDuration;
                var durationReductionPercentage = (1.0 - trimmedDuration / originalDuration) * 100.0;

                // Update context with trimmed audio
                context.SetData("AudioData", trimmedAudio);
                context.SetData("AudioDuration", trimmedDuration);

                // Add metrics
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("OriginalSizeBytes", originalSize);
                metrics.AddMetric("TrimmedSizeBytes", trimmedSize);
                metrics.AddMetric("SizeReductionBytes", sizeReductionBytes);
                metrics.AddMetric("SizeReductionPercentage", sizeReductionPercentage);
                metrics.AddMetric("OriginalDurationSeconds", originalDuration);
                metrics.AddMetric("TrimmedDurationSeconds", trimmedDuration);
                metrics.AddMetric("DurationReductionSeconds", durationReductionSeconds);
                metrics.AddMetric("DurationReductionPercentage", durationReductionPercentage);
                metrics.AddMetric("SilenceRemovedSeconds", durationReductionSeconds);
                metrics.AddMetric("SpeechSegmentsDetected", speechSegments);
                metrics.AddMetric("VADThreshold", _threshold);

                // Report progress
                context.Progress?.Report(new ProgressEventArgs($"Trimmed {durationReductionSeconds:F1}s silence ({durationReductionPercentage:F1}% removed)", 30));

                return StageResult.Success(metrics);
            }
            catch (Exception ex)
            {
                metrics.EndTime = DateTime.UtcNow;
                metrics.AddMetric("Exception", ex.Message);
                return StageResult.Failure($"VAD processing failed: {ex.Message}", metrics);
            }
        }

        private (byte[] trimmedAudio, double trimmedDuration, int speechSegments) ProcessAudioWithVAD(byte[] wavData)
        {
            using var inputStream = new MemoryStream(wavData);
            using var reader = new WaveFileReader(inputStream);

            var waveFormat = reader.WaveFormat;

            if (waveFormat.Channels != 1)
            {
                throw new InvalidOperationException("VAD requires mono audio");
            }

            // Silero VAD expects 16kHz (which matches our audio format)
            int sampleRate = waveFormat.SampleRate;

            // Read all samples
            var samples = new float[reader.SampleCount];
            var sampleProvider = reader.ToSampleProvider();
            int totalSamples = sampleProvider.Read(samples, 0, samples.Length);

            // Create VAD detector
            using var vad = new Vad();

            // Get speech timestamps using Silero VAD
            var vadSpeeches = vad.GetSpeechTimestamps(
                samples,
                threshold: _threshold,
                min_speech_duration_ms: _minSpeechDurationMs,
                min_silence_duration_ms: _minSilenceDurationMs
            );

            // Convert VAD timestamps to sample indices
            var speechSegments = new List<(int start, int end)>();
            foreach (var speech in vadSpeeches)
            {
                // VadSpeech contains Start and End in samples
                speechSegments.Add((speech.Start, speech.End));
            }

            // If no speech detected, return original audio
            if (speechSegments.Count == 0)
            {
                double originalDuration = (double)totalSamples / sampleRate;
                return (wavData, originalDuration, 0);
            }

            // Concatenate speech segments
            var trimmedSamples = new List<float>();
            foreach (var (start, end) in speechSegments)
            {
                int segmentLength = end - start;
                var segment = new float[segmentLength];
                Array.Copy(samples, start, segment, 0, segmentLength);
                trimmedSamples.AddRange(segment);
            }

            // Calculate trimmed duration
            double trimmedDuration = (double)trimmedSamples.Count / sampleRate;

            // Convert back to WAV format
            using var outputStream = new MemoryStream();
            using var writer = new WaveFileWriter(outputStream, waveFormat);

            writer.WriteSamples(trimmedSamples.ToArray(), 0, trimmedSamples.Count);
            writer.Flush();

            return (outputStream.ToArray(), trimmedDuration, speechSegments.Count);
        }
    }

    /// <summary>
    /// Factory for creating SileroVADStage instances
    /// </summary>
    public class SileroVADStageFactory : IPipelineStageFactory
    {
        public string StageType => "SileroVAD";

        public IPipelineStage CreateStage(StageConfiguration config, PipelineBuildContext buildContext)
        {
            // Extract settings
            float threshold = 0.5f;
            int minSpeechDurationMs = 250;
            int minSilenceDurationMs = 100;

            if (config.Settings.TryGetValue("Threshold", out var thresholdValue))
            {
                threshold = thresholdValue is JsonElement jsonThreshold
                    ? (float)jsonThreshold.GetDouble()
                    : Convert.ToSingle(thresholdValue);
            }

            if (config.Settings.TryGetValue("MinSpeechDurationMs", out var minSpeechValue))
            {
                minSpeechDurationMs = minSpeechValue is JsonElement jsonMinSpeech
                    ? jsonMinSpeech.GetInt32()
                    : Convert.ToInt32(minSpeechValue);
            }

            if (config.Settings.TryGetValue("MinSilenceDurationMs", out var minSilenceValue))
            {
                minSilenceDurationMs = minSilenceValue is JsonElement jsonMinSilence
                    ? jsonMinSilence.GetInt32()
                    : Convert.ToInt32(minSilenceValue);
            }

            return new SileroVADStage(config.Name, threshold, minSpeechDurationMs, minSilenceDurationMs);
        }
    }
}
