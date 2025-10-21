using System;
using System.Diagnostics;
using System.Threading.Tasks;
using HotkeyPaster.Services.Windowing;

namespace HotkeyPaster.Services.Transcription
{
    /// <summary>
    /// Result of a single transcription test run.
    /// </summary>
    public class TranscriptionTestResult
    {
        public required string Name { get; init; }
        public required string TranscribedText { get; init; }
        public required TimeSpan TotalDuration { get; init; }
        public TimeSpan? TranscriptionDuration { get; init; }
        public TimeSpan? CleaningDuration { get; init; }
        public required int WordCount { get; init; }
        public int ApiCalls { get; init; }
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }

        public double WordsPerSecond => TotalDuration.TotalSeconds > 0 ? WordCount / TotalDuration.TotalSeconds : 0;
    }

    /// <summary>
    /// Service for comparing OLD (Whisper + GPT) vs NEW (GPT-4o-mini combined) transcription approaches.
    /// </summary>
    public class OptimizationComparisonService
    {
        private readonly string _apiKey;

        public OptimizationComparisonService(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        }

        /// <summary>
        /// Runs a comprehensive comparison test between old and optimized approaches.
        /// </summary>
        public async Task<(TranscriptionTestResult Old, TranscriptionTestResult Optimized)> RunComparisonAsync(
            byte[] audioData,
            WindowContext? windowContext = null)
        {
            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));

            Console.WriteLine("🔬 Starting Transcription Optimization Comparison Test\n");
            Console.WriteLine($"Audio size: {audioData.Length:N0} bytes ({audioData.Length / 1024.0:F2} KB)\n");

            // Test OLD approach: Whisper API + GPT cleaning (2 API calls)
            Console.WriteLine("Testing OLD approach (Whisper + GPT-4.1-nano)...");
            var oldResult = await TestOldApproachAsync(audioData, windowContext);

            Console.WriteLine("\nTesting OPTIMIZED approach (GPT-4o-mini combined)...");
            var optimizedResult = await TestOptimizedApproachAsync(audioData, windowContext);

            return (oldResult, optimizedResult);
        }

        private async Task<TranscriptionTestResult> TestOldApproachAsync(byte[] audioData, WindowContext? windowContext)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var transcriptionStopwatch = new Stopwatch();
            var cleaningStopwatch = new Stopwatch();

            try
            {
                // Step 1: Whisper API transcription
                Console.WriteLine("  → Calling Whisper API...");
                transcriptionStopwatch.Start();
                var transcriber = new OpenAIWhisperTranscriber(_apiKey);
                var rawText = await transcriber.TranscribeAsync(audioData);
                transcriptionStopwatch.Stop();
                Console.WriteLine($"  ✓ Whisper completed in {transcriptionStopwatch.Elapsed.TotalSeconds:F2}s");

                // Step 2: GPT cleaning
                Console.WriteLine("  → Calling GPT-4.1-nano for cleaning...");
                cleaningStopwatch.Start();
                var textCleaner = new OpenAIGPTTextCleaner(_apiKey);
                var cleanedText = await textCleaner.CleanAsync(rawText, null, windowContext);
                cleaningStopwatch.Stop();
                Console.WriteLine($"  ✓ GPT cleaning completed in {cleaningStopwatch.Elapsed.TotalSeconds:F2}s");

                totalStopwatch.Stop();

                var wordCount = cleanedText.Split(new[] { ' ', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries).Length;

                Console.WriteLine($"  ✓ Total time: {totalStopwatch.Elapsed.TotalSeconds:F2}s | Words: {wordCount}");

                return new TranscriptionTestResult
                {
                    Name = "OLD (Whisper + GPT-4.1-nano)",
                    TranscribedText = cleanedText,
                    TotalDuration = totalStopwatch.Elapsed,
                    TranscriptionDuration = transcriptionStopwatch.Elapsed,
                    CleaningDuration = cleaningStopwatch.Elapsed,
                    WordCount = wordCount,
                    ApiCalls = 2,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                Console.WriteLine($"  ✗ Failed: {ex.Message}");

                return new TranscriptionTestResult
                {
                    Name = "OLD (Whisper + GPT-4.1-nano)",
                    TranscribedText = string.Empty,
                    TotalDuration = totalStopwatch.Elapsed,
                    WordCount = 0,
                    ApiCalls = 2,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<TranscriptionTestResult> TestOptimizedApproachAsync(byte[] audioData, WindowContext? windowContext)
        {
            var totalStopwatch = Stopwatch.StartNew();

            try
            {
                // Single step: GPT-4o-mini combined transcription + cleaning
                Console.WriteLine("  → Calling GPT-4o-mini (combined)...");
                var combinedTranscriber = new GPT4oMiniCombinedTranscriber(_apiKey);
                var cleanedText = await combinedTranscriber.TranscribeAndCleanAsync(audioData, null, windowContext);

                totalStopwatch.Stop();

                var wordCount = cleanedText.Split(new[] { ' ', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries).Length;

                Console.WriteLine($"  ✓ Total time: {totalStopwatch.Elapsed.TotalSeconds:F2}s | Words: {wordCount}");

                return new TranscriptionTestResult
                {
                    Name = "OPTIMIZED (GPT-4o-mini combined)",
                    TranscribedText = cleanedText,
                    TotalDuration = totalStopwatch.Elapsed,
                    TranscriptionDuration = null, // Not separated
                    CleaningDuration = null,      // Not separated
                    WordCount = wordCount,
                    ApiCalls = 1,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                Console.WriteLine($"  ✗ Failed: {ex.Message}");

                return new TranscriptionTestResult
                {
                    Name = "OPTIMIZED (GPT-4o-mini combined)",
                    TranscribedText = string.Empty,
                    TotalDuration = totalStopwatch.Elapsed,
                    WordCount = 0,
                    ApiCalls = 1,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Formats the comparison results as a detailed report.
        /// </summary>
        public static string FormatComparisonReport(TranscriptionTestResult oldResult, TranscriptionTestResult optimizedResult)
        {
            var report = "\n╔════════════════════════════════════════════════════════════════════════════╗\n";
            report += "║          TRANSCRIPTION OPTIMIZATION COMPARISON REPORT                     ║\n";
            report += "╚════════════════════════════════════════════════════════════════════════════╝\n\n";

            // OLD Approach Results
            report += "📊 OLD APPROACH (Whisper API + GPT-4.1-nano)\n";
            report += "────────────────────────────────────────────────────────────────────────────\n";
            if (oldResult.Success)
            {
                report += $"  API Calls:           {oldResult.ApiCalls}\n";
                report += $"  Total Time:          {oldResult.TotalDuration.TotalSeconds:F3}s\n";
                if (oldResult.TranscriptionDuration.HasValue)
                    report += $"  - Transcription:     {oldResult.TranscriptionDuration.Value.TotalSeconds:F3}s\n";
                if (oldResult.CleaningDuration.HasValue)
                    report += $"  - Text Cleaning:     {oldResult.CleaningDuration.Value.TotalSeconds:F3}s\n";
                report += $"  Word Count:          {oldResult.WordCount}\n";
                report += $"  Processing Speed:    {oldResult.WordsPerSecond:F1} words/sec\n";
                report += $"  Result Preview:      {TruncateText(oldResult.TranscribedText, 80)}\n";
            }
            else
            {
                report += $"  ❌ FAILED: {oldResult.ErrorMessage}\n";
            }

            report += "\n";

            // OPTIMIZED Approach Results
            report += "🚀 OPTIMIZED APPROACH (GPT-4o-mini Combined)\n";
            report += "────────────────────────────────────────────────────────────────────────────\n";
            if (optimizedResult.Success)
            {
                report += $"  API Calls:           {optimizedResult.ApiCalls} (single combined call)\n";
                report += $"  Total Time:          {optimizedResult.TotalDuration.TotalSeconds:F3}s\n";
                report += $"  Word Count:          {optimizedResult.WordCount}\n";
                report += $"  Processing Speed:    {optimizedResult.WordsPerSecond:F1} words/sec\n";
                report += $"  Result Preview:      {TruncateText(optimizedResult.TranscribedText, 80)}\n";
            }
            else
            {
                report += $"  ❌ FAILED: {optimizedResult.ErrorMessage}\n";
            }

            report += "\n";

            // Performance Comparison
            if (oldResult.Success && optimizedResult.Success)
            {
                var timeSaved = oldResult.TotalDuration - optimizedResult.TotalDuration;
                var percentImprovement = (timeSaved.TotalSeconds / oldResult.TotalDuration.TotalSeconds) * 100;
                var speedupFactor = oldResult.TotalDuration.TotalSeconds / optimizedResult.TotalDuration.TotalSeconds;

                report += "📈 PERFORMANCE IMPROVEMENT\n";
                report += "════════════════════════════════════════════════════════════════════════════\n";

                if (timeSaved.TotalSeconds > 0)
                {
                    report += $"  Time Saved:          {timeSaved.TotalSeconds:F3}s ({percentImprovement:F1}% faster)\n";
                    report += $"  Speedup Factor:      {speedupFactor:F2}x\n";
                    report += $"  API Calls Reduced:   {oldResult.ApiCalls - optimizedResult.ApiCalls} fewer call(s)\n";
                    report += $"\n  ✅ OPTIMIZED approach is {speedupFactor:F2}x FASTER!\n";
                }
                else if (timeSaved.TotalSeconds < 0)
                {
                    var slower = Math.Abs(timeSaved.TotalSeconds);
                    report += $"  Time Difference:     +{slower:F3}s ({Math.Abs(percentImprovement):F1}% slower)\n";
                    report += $"  ⚠️  OLD approach was faster by {slower:F3}s\n";
                }
                else
                {
                    report += $"  Time Difference:     ~0s (approximately equal)\n";
                }

                report += "\n";

                // Cost Comparison (estimated)
                report += "💰 ESTIMATED COST COMPARISON\n";
                report += "────────────────────────────────────────────────────────────────────────────\n";
                report += "  OLD:        Whisper ($0.006/min) + GPT-4.1-nano (~$0.0001/request)\n";
                report += "  OPTIMIZED:  GPT-4o-mini audio (~$0.00015/min) + text generation\n";
                report += "  Note: Actual costs depend on audio length and text output tokens\n\n";
            }

            // Recommendations
            report += "💡 RECOMMENDATIONS\n";
            report += "────────────────────────────────────────────────────────────────────────────\n";
            if (oldResult.Success && optimizedResult.Success)
            {
                if (optimizedResult.TotalDuration < oldResult.TotalDuration)
                {
                    report += "  ✅ Use OPTIMIZED approach for:\n";
                    report += "     • Faster response times\n";
                    report += "     • Reduced API complexity\n";
                    report += "     • Better user experience\n";
                }
                else
                {
                    report += "  ⚠️  Consider factors beyond speed:\n";
                    report += "     • Transcription accuracy\n";
                    report += "     • Cost per transcription\n";
                    report += "     • API rate limits\n";
                }
            }

            report += "\n════════════════════════════════════════════════════════════════════════════\n";

            return report;
        }

        private static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "(empty)";
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "...";
        }
    }
}
