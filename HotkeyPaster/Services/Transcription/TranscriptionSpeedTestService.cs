using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TalkKeys.Services.Transcription
{
    /// <summary>
    /// Result of a transcription speed test.
    /// </summary>
    public class SpeedTestResult
    {
        public required string Name { get; init; }
        public required string TranscribedText { get; init; }
        public required TimeSpan Duration { get; init; }
        public required int WordCount { get; init; }
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }

        public double WordsPerSecond => Duration.TotalSeconds > 0 ? WordCount / Duration.TotalSeconds : 0;
    }

    /// <summary>
    /// Service for comparing transcription speeds between different implementations.
    /// </summary>
    public class TranscriptionSpeedTestService
    {
        private readonly ITranscriber _localTranscriber;
        private readonly ITranscriber _cloudTranscriber;
        private readonly ITextCleaner _textCleaner;

        public TranscriptionSpeedTestService(
            ITranscriber localTranscriber,
            ITranscriber cloudTranscriber,
            ITextCleaner textCleaner)
        {
            _localTranscriber = localTranscriber ?? throw new ArgumentNullException(nameof(localTranscriber));
            _cloudTranscriber = cloudTranscriber ?? throw new ArgumentNullException(nameof(cloudTranscriber));
            _textCleaner = textCleaner ?? throw new ArgumentNullException(nameof(textCleaner));
        }

        /// <summary>
        /// Runs a speed test comparing local and cloud transcription.
        /// </summary>
        public async Task<(SpeedTestResult Local, SpeedTestResult Cloud)> RunSpeedTestAsync(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));

            // Test local transcription
            var localResult = await TestTranscriberAsync("Local (Whisper.net)", _localTranscriber, audioData);

            // Test cloud transcription
            var cloudResult = await TestTranscriberAsync("Cloud (OpenAI)", _cloudTranscriber, audioData);

            return (localResult, cloudResult);
        }

        private async Task<SpeedTestResult> TestTranscriberAsync(string name, ITranscriber transcriber, byte[] audioData)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Transcribe
                var rawText = await transcriber.TranscribeAsync(audioData);
                
                // Clean (optional, but included for fair comparison)
                var cleanedText = await _textCleaner.CleanAsync(rawText);
                
                stopwatch.Stop();

                var wordCount = cleanedText.Split(new[] { ' ', '\n', '\r', '\t' }, 
                    StringSplitOptions.RemoveEmptyEntries).Length;

                return new SpeedTestResult
                {
                    Name = name,
                    TranscribedText = cleanedText,
                    Duration = stopwatch.Elapsed,
                    WordCount = wordCount,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                return new SpeedTestResult
                {
                    Name = name,
                    TranscribedText = string.Empty,
                    Duration = stopwatch.Elapsed,
                    WordCount = 0,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Formats the speed test results as a readable string.
        /// </summary>
        public static string FormatResults(SpeedTestResult local, SpeedTestResult cloud)
        {
            var result = "=== Transcription Speed Test Results ===\n\n";

            // Local results
            result += $"📍 {local.Name}\n";
            if (local.Success)
            {
                result += $"   Time: {local.Duration.TotalSeconds:F2}s\n";
                result += $"   Words: {local.WordCount}\n";
                result += $"   Speed: {local.WordsPerSecond:F1} words/sec\n";
                result += $"   Text: {TruncateText(local.TranscribedText, 100)}\n";
            }
            else
            {
                result += $"   ❌ Failed: {local.ErrorMessage}\n";
            }

            result += "\n";

            // Cloud results
            result += $"☁️ {cloud.Name}\n";
            if (cloud.Success)
            {
                result += $"   Time: {cloud.Duration.TotalSeconds:F2}s\n";
                result += $"   Words: {cloud.WordCount}\n";
                result += $"   Speed: {cloud.WordsPerSecond:F1} words/sec\n";
                result += $"   Text: {TruncateText(cloud.TranscribedText, 100)}\n";
            }
            else
            {
                result += $"   ❌ Failed: {cloud.ErrorMessage}\n";
            }

            result += "\n";

            // Comparison
            if (local.Success && cloud.Success)
            {
                var faster = local.Duration < cloud.Duration ? local : cloud;
                var slower = local.Duration < cloud.Duration ? cloud : local;
                var speedup = slower.Duration.TotalSeconds / faster.Duration.TotalSeconds;

                result += "📊 Comparison\n";
                result += $"   Winner: {faster.Name}\n";
                result += $"   {faster.Name} is {speedup:F2}x faster than {slower.Name}\n";
                result += $"   Time difference: {Math.Abs((local.Duration - cloud.Duration).TotalSeconds):F2}s\n";
            }

            return result;
        }

        private static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "(empty)";
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "...";
        }
    }
}
