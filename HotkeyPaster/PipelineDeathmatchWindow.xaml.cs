using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TalkKeys.Logging;
using TalkKeys.Services.Audio;
using TalkKeys.Services.Pipeline;
using TalkKeys.Services.Pipeline.Configuration;
using TalkKeys.Services.Pipeline.Stages;

namespace TalkKeys
{
    public partial class PipelineDeathmatchWindow : Window
    {
        private readonly IAudioRecordingService _audioService;
        private readonly ILogger _logger;
        private readonly PipelineFactory _pipelineFactory;
        private readonly PipelineBuildContext _buildContext;
        private string? _recordingPath;
        private bool _isRecording;
        private bool _measureAccuracy;
        private string? _referenceText;

        public Dictionary<string, DeathmatchResult>? BenchmarkResults { get; private set; }

        public PipelineDeathmatchWindow(
            IAudioRecordingService audioService,
            ILogger logger,
            PipelineFactory pipelineFactory,
            PipelineBuildContext buildContext)
        {
            InitializeComponent();
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
            _buildContext = buildContext ?? throw new ArgumentNullException(nameof(buildContext));
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RecordButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                LoadFileButton.IsEnabled = false;
                _isRecording = true;

                // Enable accuracy measurement for recordings
                _measureAccuracy = true;
                _referenceText = PassageText.Text;

                StatusText.Text = "🎤 Recording... Read the passage above";
                _logger.Log("Pipeline Benchmark: Started recording");

                // Create recording file path
                var tempPath = Path.GetTempPath();
                _recordingPath = Path.Combine(tempPath, $"deathmatch_{Guid.NewGuid()}.wav");

                // Start recording
                _audioService.StartRecording(_recordingPath);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to start recording: {ex.Message}");
                MessageBox.Show($"Failed to start recording: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RecordButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                LoadFileButton.IsEnabled = true;
                _isRecording = false;
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording || string.IsNullOrEmpty(_recordingPath))
                return;

            try
            {
                StopButton.IsEnabled = false;
                StatusText.Text = "⏹ Stopping recording...";

                // Stop recording
                _audioService.StopRecording();
                _isRecording = false;

                _logger.Log($"Pipeline Benchmark: Stopped recording, file: {_recordingPath}");

                // Wait a bit for the file to be released
                await Task.Delay(500);

                // Read audio data with retry logic
                byte[]? audioData = null;
                int retries = 5;
                while (retries > 0)
                {
                    try
                    {
                        if (!File.Exists(_recordingPath))
                        {
                            throw new FileNotFoundException("Recording file not found", _recordingPath);
                        }

                        audioData = await File.ReadAllBytesAsync(_recordingPath);
                        break;
                    }
                    catch (IOException) when (retries > 1)
                    {
                        // File is still locked, wait and retry
                        _logger.Log($"File locked, retrying... ({retries} attempts left)");
                        await Task.Delay(200);
                        retries--;
                        continue;
                    }
                }

                if (audioData == null)
                {
                    throw new IOException("Failed to read recording file after multiple retries");
                }

                // Process through different pipelines
                await RunDeathmatchAsync(audioData);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to process recording: {ex.Message}");
                MessageBox.Show($"Failed to process recording: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RecordButton.IsEnabled = true;
                LoadFileButton.IsEnabled = true;
                StatusText.Text = "Ready to record or load a file";
            }
        }

        private async Task RunDeathmatchAsync(byte[] audioData)
        {
            StatusText.Text = "🧪 Running pipeline benchmark...";
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = true;
            ProgressText.Visibility = Visibility.Visible;
            ProgressText.Text = "Creating test pipelines...";

            try
            {
                // Create test pipeline configurations
                var testPipelines = CreateTestPipelines();
                _logger.Log($"Created {testPipelines.Count} test pipelines");

                var results = new List<DeathmatchResult>();

                for (int i = 0; i < testPipelines.Count; i++)
                {
                    var (name, pipeline) = testPipelines[i];
                    ProgressText.Text = $"Testing pipeline {i + 1}/{testPipelines.Count}: {name}";
                    _logger.Log($"Testing pipeline: {name}");

                    try
                    {
                        var context = new PipelineContext
                        {
                            CancellationToken = default
                        };
                        context.SetData("AudioData", audioData);

                        var result = await pipeline.ExecuteAsync(context);

                        var deathmatchResult = new DeathmatchResult
                        {
                            PipelineName = name,
                            Success = result.IsSuccess,
                            DurationMs = result.Metrics.TotalDurationMs ?? 0.0,
                            TranscriptionText = result.Text ?? "",
                            WordCount = result.WordCount,
                            NoiseReductionDb = GetMetricValue(result, "NoiseReductionDB") ?? 0.0,
                            SilenceRemovedSeconds = GetMetricValue(result, "SilenceRemovedSeconds") ?? 0.0,
                            ErrorMessage = result.ErrorMessage
                        };

                        // Calculate accuracy if we're measuring it
                        if (_measureAccuracy && !string.IsNullOrEmpty(_referenceText) && !string.IsNullOrEmpty(result.Text))
                        {
                            double accuracy = CalculateAccuracy(_referenceText, result.Text);
                            deathmatchResult.AccuracyPercent = accuracy;
                        }

                        results.Add(deathmatchResult);
                        _logger.Log($"Pipeline '{name}' completed: {result.IsSuccess}, Duration: {result.Metrics.TotalDurationMs:F0}ms, Words: {result.WordCount}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Pipeline '{name}' failed: {ex.Message}");
                        results.Add(new DeathmatchResult
                        {
                            PipelineName = name,
                            Success = false,
                            ErrorMessage = ex.Message,
                            TranscriptionText = $"ERROR: {ex.Message}"
                        });
                    }
                }

                // Get successful results
                var successfulResults = results.Where(r => r.Success).ToList();
                var failedResults = results.Where(r => !r.Success).ToList();

                // Calculate composite scores and sort
                List<DeathmatchResult> sortedResults;

                // Check if we have accuracy data
                bool hasAccuracyData = successfulResults.Any(r => r.AccuracyPercent.HasValue);

                if (hasAccuracyData)
                {
                    // Calculate composite scores: accuracy (80%) + speed (20%)
                    // Speed component: faster is better, so invert duration
                    var maxDuration = successfulResults.Max(r => r.DurationMs);
                    var minDuration = successfulResults.Min(r => r.DurationMs);
                    var durationRange = maxDuration - minDuration;

                    foreach (var result in successfulResults)
                    {
                        double accuracyScore = result.AccuracyPercent ?? 0.0;

                        // Normalize duration: 100 for fastest, 0 for slowest
                        double normalizedSpeed = durationRange > 0
                            ? ((maxDuration - result.DurationMs) / durationRange) * 100.0
                            : 100.0;

                        // Composite score: 80% accuracy, 20% speed
                        result.CompositeScore = (accuracyScore * 0.8) + (normalizedSpeed * 0.2);
                    }

                    // Sort by composite score (descending - higher is better)
                    sortedResults = successfulResults.OrderByDescending(r => r.CompositeScore).ToList();
                }
                else
                {
                    // No accuracy data, fall back to duration-only sorting
                    sortedResults = successfulResults.OrderBy(r => r.DurationMs).ToList();
                }

                // Mark the winner (first in sorted results)
                if (sortedResults.Count > 0)
                {
                    sortedResults[0].IsWinner = true;
                }

                // Assign ranks and visual properties to successful results
                for (int i = 0; i < sortedResults.Count; i++)
                {
                    var result = sortedResults[i];
                    int rank = i + 1;

                    // Rank display and color
                    if (rank == 1)
                    {
                        result.Rank = result.IsWinner ? "🏆" : "🥇";
                        result.RankColor = "#fbbf24"; // Gold
                        result.DurationBadgeColor = "#10b981"; // Green
                    }
                    else if (rank == 2)
                    {
                        result.Rank = "🥈";
                        result.RankColor = "#9ca3af"; // Silver
                        result.DurationBadgeColor = "#3b82f6"; // Blue
                    }
                    else if (rank == 3)
                    {
                        result.Rank = "🥉";
                        result.RankColor = "#cd7f32"; // Bronze
                        result.DurationBadgeColor = "#6366f1"; // Indigo
                    }
                    else
                    {
                        result.Rank = $"#{rank}";
                        result.RankColor = "#6b7280";
                        result.DurationBadgeColor = "#4b5563"; // Gray
                    }

                    // Duration display
                    result.DurationDisplay = $"{result.DurationMs:F0} ms";

                    // Status icon
                    result.StatusIcon = "✓";

                    // Accuracy display
                    if (result.AccuracyPercent.HasValue)
                    {
                        double accuracy = result.AccuracyPercent.Value;
                        result.AccuracyDisplay = $"{accuracy:F1}%";

                        // Color based on accuracy
                        if (accuracy >= 90)
                            result.AccuracyColor = "#10b981"; // Green
                        else if (accuracy >= 75)
                            result.AccuracyColor = "#3b82f6"; // Blue
                        else if (accuracy >= 60)
                            result.AccuracyColor = "#f59e0b"; // Orange
                        else
                            result.AccuracyColor = "#ef4444"; // Red
                    }
                    else
                    {
                        result.AccuracyDisplay = "N/A";
                        result.AccuracyColor = "#6b7280"; // Gray
                    }
                }

                // Process failed results
                foreach (var result in failedResults)
                {
                    result.Rank = "❌";
                    result.RankColor = "#ef4444";
                    result.StatusIcon = "✗";
                    result.DurationBadgeColor = "#ef4444"; // Red
                    result.DurationDisplay = "Failed";
                    result.TranscriptionText = $"ERROR: {result.ErrorMessage ?? "Unknown error"}";
                    result.AccuracyDisplay = "N/A";
                    result.AccuracyColor = "#ef4444";
                }

                // Store results in Dictionary
                var allResults = sortedResults.Concat(failedResults).ToList();
                BenchmarkResults = allResults.ToDictionary(r => r.PipelineName, r => r);

                // Comment out DataGrid display - results will be shown in settings window
                // ResultsGrid.ItemsSource = allResults;

                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressText.Visibility = Visibility.Collapsed;
                ProgressPanel.Visibility = Visibility.Collapsed;

                // Log the winner
                var winner = sortedResults.FirstOrDefault(r => r.IsWinner);
                if (winner != null && hasAccuracyData)
                {
                    _logger.Log($"Benchmark complete. Winner: {winner.PipelineName} (Composite Score: {winner.CompositeScore:F1}, Accuracy: {winner.AccuracyPercent:F1}%, Duration: {winner.DurationMs:F0}ms)");
                }
                else if (winner != null)
                {
                    _logger.Log($"Benchmark complete. Fastest: {winner.PipelineName} (Duration: {winner.DurationMs:F0}ms)");
                }
                else
                {
                    _logger.Log($"Benchmark complete. {testPipelines.Count} pipelines tested");
                }

                // Close window and return results
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger.Log($"Benchmark failed: {ex.Message}");
                MessageBox.Show($"Benchmark failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressText.Visibility = Visibility.Collapsed;
                StatusText.Text = "❌ Benchmark failed";
            }
        }

        private List<(string name, Pipeline pipeline)> CreateTestPipelines()
        {
            var pipelines = new List<(string, Pipeline)>();

            // 1. No preprocessing (baseline)
            pipelines.Add(("Baseline (No Preprocessing)", CreatePipeline(
                useNoise: false,
                useVAD: false,
                useCloud: true
            )));

            // 2. Noise reduction only
            pipelines.Add(("RNNoise Only", CreatePipeline(
                useNoise: true,
                useVAD: false,
                useCloud: true
            )));

            // 3. VAD only
            pipelines.Add(("VAD Only", CreatePipeline(
                useNoise: false,
                useVAD: true,
                useCloud: true
            )));

            // 4. Both preprocessing (recommended)
            pipelines.Add(("RNNoise + VAD (Cloud)", CreatePipeline(
                useNoise: true,
                useVAD: true,
                useCloud: true
            )));

            // 5. Local transcription with preprocessing
            if (!string.IsNullOrEmpty(_buildContext.LocalModelPath))
            {
                pipelines.Add(("RNNoise + VAD (Local)", CreatePipeline(
                    useNoise: true,
                    useVAD: true,
                    useCloud: false
                )));

                pipelines.Add(("Local (No Preprocessing)", CreatePipeline(
                    useNoise: false,
                    useVAD: false,
                    useCloud: false
                )));
            }

            return pipelines;
        }

        private Pipeline CreatePipeline(bool useNoise, bool useVAD, bool useCloud)
        {
            var stages = new List<IPipelineStage>();

            // Always validate
            stages.Add(new AudioValidationStage());

            // Optional noise reduction
            if (useNoise)
            {
                stages.Add(new RNNoiseStage());
            }

            // Optional VAD
            if (useVAD)
            {
                stages.Add(new SileroVADStage());
            }

            // Transcription
            if (useCloud)
            {
                stages.Add(new OpenAIWhisperTranscriptionStage(_buildContext.OpenAIApiKey ?? ""));
            }
            else
            {
                stages.Add(new LocalWhisperTranscriptionStage(_buildContext.LocalModelPath ?? ""));
            }

            // Text cleaning (always use pass-through for fair comparison)
            stages.Add(new PassThroughCleaningStage());

            return new Pipeline("Test", stages);
        }

        private double? GetMetricValue(PipelineResult result, string metricName)
        {
            foreach (var stageMetric in result.Metrics.StageMetrics)
            {
                if (stageMetric.CustomMetrics.TryGetValue(metricName, out var value))
                {
                    return Convert.ToDouble(value);
                }
            }
            return null;
        }

        private double CalculateAccuracy(string reference, string transcription)
        {
            if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(transcription))
                return 0.0;

            // Normalize text: lowercase and split into words
            var referenceWords = reference.ToLower()
                .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
            var transcriptionWords = transcription.ToLower()
                .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (referenceWords.Length == 0)
                return 0.0;

            // Calculate word-level accuracy using longest common subsequence
            int matchingWords = 0;
            var transcriptionSet = new HashSet<string>(transcriptionWords);

            foreach (var word in referenceWords)
            {
                if (transcriptionSet.Contains(word))
                {
                    matchingWords++;
                }
            }

            // Accuracy = (matching words / total reference words) * 100
            // Penalize for extra words
            double baseAccuracy = (double)matchingWords / referenceWords.Length;
            double extraWordsPenalty = Math.Max(0, (transcriptionWords.Length - referenceWords.Length)) / (double)referenceWords.Length;
            double accuracy = Math.Max(0, baseAccuracy - (extraWordsPenalty * 0.1)); // Small penalty for extra words

            return Math.Min(100.0, accuracy * 100.0);
        }

        private async void LoadFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open file dialog
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select WAV File",
                    Filter = "WAV Files (*.wav)|*.wav|All Files (*.*)|*.*",
                    FilterIndex = 1,
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() != true)
                    return;

                var filePath = openFileDialog.FileName;
                _logger.Log($"Pipeline Benchmark: Loading file: {filePath}");

                // Disable accuracy measurement for loaded files (we don't know what was said)
                _measureAccuracy = false;
                _referenceText = null;

                // Disable buttons during processing
                RecordButton.IsEnabled = false;
                StopButton.IsEnabled = false;
                LoadFileButton.IsEnabled = false;

                StatusText.Text = $"📂 Loading file: {Path.GetFileName(filePath)}";

                // Read audio data
                byte[] audioData;
                try
                {
                    audioData = await File.ReadAllBytesAsync(filePath);
                    _logger.Log($"Loaded {audioData.Length} bytes from {filePath}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Failed to read file: {ex.Message}");
                    MessageBox.Show($"Failed to read file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Validate it's a WAV file
                if (audioData.Length < 44 ||
                    audioData[0] != 'R' || audioData[1] != 'I' || audioData[2] != 'F' || audioData[3] != 'F' ||
                    audioData[8] != 'W' || audioData[9] != 'A' || audioData[10] != 'V' || audioData[11] != 'E')
                {
                    _logger.Log("Invalid WAV file format");
                    MessageBox.Show("The selected file is not a valid WAV file.", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "🥊 Processing loaded file...";

                // Process through pipelines
                await RunDeathmatchAsync(audioData);
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to load file: {ex.Message}");
                MessageBox.Show($"Failed to load file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RecordButton.IsEnabled = true;
                LoadFileButton.IsEnabled = true;
                StatusText.Text = "Ready to record or load a file";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class DeathmatchResult
    {
        public string PipelineName { get; set; } = "";
        public bool Success { get; set; }
        public double DurationMs { get; set; }
        public string TranscriptionText { get; set; } = "";
        public int WordCount { get; set; }
        public double NoiseReductionDb { get; set; }
        public double SilenceRemovedSeconds { get; set; }
        public string? ErrorMessage { get; set; }

        // Accuracy metrics
        public double? AccuracyPercent { get; set; }
        public string AccuracyDisplay { get; set; } = "";
        public string AccuracyColor { get; set; } = "#6b7280";

        // Winner tracking
        public bool IsWinner { get; set; }
        public double CompositeScore { get; set; }

        // UI Properties
        public string Rank { get; set; } = "";
        public string RankColor { get; set; } = "#9ca3af";
        public string StatusIcon { get; set; } = "✓";
        public string DurationBadgeColor { get; set; } = "#4b5563";
        public string DurationDisplay { get; set; } = "";
    }
}
