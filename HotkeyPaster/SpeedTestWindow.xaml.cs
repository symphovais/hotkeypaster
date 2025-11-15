using System;
using System.Threading.Tasks;
using System.Windows;
using TalkKeys.Logging;
using TalkKeys.Services.Audio;
using TalkKeys.Services.Transcription;

namespace TalkKeys
{
    public partial class SpeedTestWindow : Window
    {
        private readonly IAudioRecordingService _audioService;
        private readonly ILogger _logger;
        private bool _isRunning;

        public SpeedTestWindow(IAudioRecordingService audioService, ILogger logger)
        {
            InitializeComponent();
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;

            try
            {
                _isRunning = true;
                StartButton.IsEnabled = false;
                
                // Hide result cards
                LocalCard.Visibility = Visibility.Collapsed;
                CloudCard.Visibility = Visibility.Collapsed;
                ComparisonCard.Visibility = Visibility.Collapsed;
                
                // Show progress
                ProgressCard.Visibility = Visibility.Visible;
                
                await RunSpeedTestAsync();
            }
            catch (Exception ex)
            {
                _logger.Log($"Speed test error: {ex}");
                MessageBox.Show($"Speed test failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isRunning = false;
                StartButton.IsEnabled = true;
                ProgressCard.Visibility = Visibility.Collapsed;
            }
        }

        private async Task RunSpeedTestAsync()
        {
            // Check for API key
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show(
                    "OPENAI_API_KEY environment variable is required to run the speed test.\n\n" +
                    "Set the environment variable and restart the application.",
                    "Configuration Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Ensure local model is downloaded
            UpdateStatus("Checking local model...");
            var modelType = Whisper.net.Ggml.GgmlType.Base;
            if (!WhisperModelManager.IsModelDownloaded(modelType))
            {
                UpdateStatus("Downloading local model... This may take a few minutes.");
                await WhisperModelManager.EnsureModelDownloadedAsync(modelType);
            }

            // Start recording
            UpdateStatus("Recording 10 seconds of audio... Speak clearly into your microphone!");
            
            var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), 
                $"SpeedTest_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
            
            _audioService.StartRecording(tempFile);
            
            // Countdown
            for (int i = 10; i > 0; i--)
            {
                UpdateStatus($"Recording... {i} seconds remaining");
                await Task.Delay(1000);
            }
            
            _audioService.StopRecording();
            UpdateStatus("Recording complete. Processing...");
            
            // Wait for file to be written
            await Task.Delay(500);

            if (!System.IO.File.Exists(tempFile))
            {
                MessageBox.Show("Recording failed - file not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var audioData = await System.IO.File.ReadAllBytesAsync(tempFile);

            // Create transcribers
            UpdateStatus("Running local transcription...");
            var localTranscriber = new LocalWhisperTranscriber(WhisperModelManager.GetModelPath(modelType));
            var cloudTranscriber = new OpenAIWhisperTranscriber(apiKey);
            var textCleaner = new OpenAIGPTTextCleaner(apiKey);

            // Run speed test
            var speedTestService = new TranscriptionSpeedTestService(
                localTranscriber, 
                cloudTranscriber, 
                textCleaner);

            UpdateStatus("Running transcription tests...");
            var (localResult, cloudResult) = await speedTestService.RunSpeedTestAsync(audioData);

            // Display results
            DisplayResults(localResult, cloudResult);

            // Log results
            var results = TranscriptionSpeedTestService.FormatResults(localResult, cloudResult);
            _logger.Log("Speed Test Results:\n" + results);

            // Clean up
            try { System.IO.File.Delete(tempFile); } catch { }
            if (localTranscriber is IDisposable ld) ld.Dispose();
            if (cloudTranscriber is IDisposable cd) cd.Dispose();
            if (textCleaner is IDisposable tc) tc.Dispose();

            UpdateStatus("Test complete!");
        }

        private void DisplayResults(SpeedTestResult local, SpeedTestResult cloud)
        {
            // Show result cards
            LocalCard.Visibility = Visibility.Visible;
            CloudCard.Visibility = Visibility.Visible;
            ComparisonCard.Visibility = Visibility.Visible;

            // Local results
            if (local.Success)
            {
                LocalTime.Text = $"{local.Duration.TotalSeconds:F2}s";
                LocalWords.Text = local.WordCount.ToString();
                LocalSpeed.Text = $"{local.WordsPerSecond:F1} w/s";
                LocalText.Text = string.IsNullOrEmpty(local.TranscribedText) ? "(empty)" : local.TranscribedText;
            }
            else
            {
                LocalTime.Text = "Failed";
                LocalWords.Text = "--";
                LocalSpeed.Text = "--";
                LocalText.Text = $"Error: {local.ErrorMessage}";
            }

            // Cloud results
            if (cloud.Success)
            {
                CloudTime.Text = $"{cloud.Duration.TotalSeconds:F2}s";
                CloudWords.Text = cloud.WordCount.ToString();
                CloudSpeed.Text = $"{cloud.WordsPerSecond:F1} w/s";
                CloudText.Text = string.IsNullOrEmpty(cloud.TranscribedText) ? "(empty)" : cloud.TranscribedText;
            }
            else
            {
                CloudTime.Text = "Failed";
                CloudWords.Text = "--";
                CloudSpeed.Text = "--";
                CloudText.Text = $"Error: {cloud.ErrorMessage}";
            }

            // Comparison
            if (local.Success && cloud.Success)
            {
                var faster = local.Duration < cloud.Duration ? local : cloud;
                var slower = local.Duration < cloud.Duration ? cloud : local;
                var speedup = slower.Duration.TotalSeconds / faster.Duration.TotalSeconds;
                var winner = local.Duration < cloud.Duration ? "Local (Whisper.net)" : "Cloud (OpenAI)";
                var winnerEmoji = local.Duration < cloud.Duration ? "🏆 📍" : "🏆 ☁️";

                WinnerText.Text = $"{winnerEmoji} Winner: {winner}";
                SpeedupText.Text = $"{faster.Name} is {speedup:F2}x faster than {slower.Name}";
                DifferenceText.Text = $"Time difference: {Math.Abs((local.Duration - cloud.Duration).TotalSeconds):F2} seconds";
            }
            else
            {
                WinnerText.Text = "Unable to compare - one or both tests failed";
                SpeedupText.Text = "";
                DifferenceText.Text = "";
            }
        }

        private void UpdateStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                ProgressText.Text = message;
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
