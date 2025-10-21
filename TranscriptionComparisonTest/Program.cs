using System;
using System.IO;
using System.Threading.Tasks;
using HotkeyPaster.Services.Transcription;
using HotkeyPaster.Services.Windowing;

namespace TranscriptionComparisonTest
{
    /// <summary>
    /// Test program to compare OLD vs OPTIMIZED transcription approaches.
    /// Run this to measure the actual performance improvement.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("════════════════════════════════════════════════════════════════");
            Console.WriteLine("  HotkeyPaster Transcription Optimization Test");
            Console.WriteLine("════════════════════════════════════════════════════════════════\n");

            // Get API key from environment or args
            string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (args.Length > 0)
            {
                apiKey = args[0];
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("❌ Error: OpenAI API key not provided.");
                Console.WriteLine("\nUsage:");
                Console.WriteLine("  dotnet run --project TranscriptionComparisonTest <OPENAI_API_KEY> [audio_file.wav]");
                Console.WriteLine("  OR set OPENAI_API_KEY environment variable");
                return;
            }

            // Get audio file path
            string? audioFilePath = null;
            if (args.Length > 1)
            {
                audioFilePath = args[1];
            }
            else
            {
                // Try to find a test audio file in temp directory
                var tempFiles = Directory.GetFiles(Path.GetTempPath(), "HotkeyPaster_*.wav");
                if (tempFiles.Length > 0)
                {
                    // Get most recent file
                    Array.Sort(tempFiles);
                    audioFilePath = tempFiles[tempFiles.Length - 1];
                    Console.WriteLine($"ℹ️  No audio file specified, using most recent recording:");
                    Console.WriteLine($"   {audioFilePath}\n");
                }
            }

            if (string.IsNullOrWhiteSpace(audioFilePath) || !File.Exists(audioFilePath))
            {
                Console.WriteLine("❌ Error: Audio file not found.");
                Console.WriteLine("\nPlease provide a WAV file path as the second argument:");
                Console.WriteLine("  dotnet run --project TranscriptionComparisonTest <API_KEY> <path_to_audio.wav>");
                Console.WriteLine("\nOr record audio using HotkeyPaster app first (files saved to temp directory).");

                // List available recordings
                var tempDir = Path.GetTempPath();
                var recordings = Directory.GetFiles(tempDir, "HotkeyPaster_*.wav");
                if (recordings.Length > 0)
                {
                    Console.WriteLine($"\n📁 Found {recordings.Length} recording(s) in temp directory:");
                    foreach (var rec in recordings)
                    {
                        var fileInfo = new FileInfo(rec);
                        Console.WriteLine($"   {Path.GetFileName(rec)} ({fileInfo.Length / 1024.0:F1} KB, {fileInfo.LastWriteTime:g})");
                    }
                }

                return;
            }

            try
            {
                // Load audio data
                Console.WriteLine($"📂 Loading audio file: {Path.GetFileName(audioFilePath)}");
                byte[] audioData = await File.ReadAllBytesAsync(audioFilePath);
                Console.WriteLine($"   Size: {audioData.Length:N0} bytes ({audioData.Length / 1024.0:F2} KB)\n");

                // Create comparison service
                var comparisonService = new OptimizationComparisonService(apiKey);

                // Optional: Create window context for testing context-aware cleaning
                WindowContext? context = null;
                // Uncomment to test with context:
                // context = new WindowContext("OUTLOOK", "Inbox - Microsoft Outlook", IntPtr.Zero);

                // Run comparison
                Console.WriteLine("🚀 Starting comparison test...\n");
                var (oldResult, optimizedResult) = await comparisonService.RunComparisonAsync(audioData, context);

                // Display results
                var report = OptimizationComparisonService.FormatComparisonReport(oldResult, optimizedResult);
                Console.WriteLine(report);

                // Display full transcriptions for comparison
                Console.WriteLine("\n📝 FULL TRANSCRIPTION COMPARISON\n");
                Console.WriteLine("OLD Approach Result:");
                Console.WriteLine("────────────────────────────────────────────────────────────────");
                Console.WriteLine(oldResult.TranscribedText);
                Console.WriteLine("\n");

                Console.WriteLine("OPTIMIZED Approach Result:");
                Console.WriteLine("────────────────────────────────────────────────────────────────");
                Console.WriteLine(optimizedResult.TranscribedText);
                Console.WriteLine("\n");

                // Save results to file
                var resultFileName = $"transcription_comparison_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var resultFilePath = Path.Combine(Path.GetTempPath(), resultFileName);

                var fullReport = report +
                    "\n\n" +
                    "═══════════════════════════════════════════════════════════════════════════\n" +
                    "                        FULL TRANSCRIPTION RESULTS\n" +
                    "═══════════════════════════════════════════════════════════════════════════\n\n" +
                    "OLD APPROACH (Whisper + GPT-4.1-nano):\n" +
                    "───────────────────────────────────────────────────────────────────────────\n" +
                    oldResult.TranscribedText +
                    "\n\n" +
                    "OPTIMIZED APPROACH (GPT-4o-mini combined):\n" +
                    "───────────────────────────────────────────────────────────────────────────\n" +
                    optimizedResult.TranscribedText;

                await File.WriteAllTextAsync(resultFilePath, fullReport);
                Console.WriteLine($"💾 Full report saved to: {resultFilePath}\n");

                Console.WriteLine("✅ Test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Test failed with error:");
                Console.WriteLine($"   {ex.Message}");
                Console.WriteLine($"\nStack trace:");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
