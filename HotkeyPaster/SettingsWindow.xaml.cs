using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TalkKeys.Logging;
using TalkKeys.Services.Audio;
using TalkKeys.Services.Pipeline;
using TalkKeys.Services.Settings;
using TalkKeys.Services.Transcription;

namespace TalkKeys
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly IAudioRecordingService _audioService;
        private readonly ILogger _logger;
        private readonly PipelineFactory _pipelineFactory;
        private readonly PipelineBuildContext _buildContext;
        private AppSettings _currentSettings;
        private bool _isInitializing = true;
        private Dictionary<string, BenchmarkResult>? _benchmarkResults;

        public event EventHandler? SettingsChanged;

        public SettingsWindow(
            SettingsService settingsService,
            IAudioRecordingService audioService,
            ILogger logger,
            PipelineFactory pipelineFactory,
            PipelineBuildContext buildContext)
        {
            InitializeComponent();

            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
            _buildContext = buildContext ?? throw new ArgumentNullException(nameof(buildContext));

            _currentSettings = _settingsService.LoadSettings();

            LoadSettings();
            PopulateLocalModels();

            _isInitializing = false;
        }

        private void LoadSettings()
        {
            // Load pipeline preset
            switch (_currentSettings.SelectedPipeline)
            {
                case PipelinePresets.MaximumQuality:
                    MaximumQualityRadio.IsChecked = true;
                    LocalModelPanel.Visibility = Visibility.Collapsed;
                    break;
                case PipelinePresets.BalancedQuality:
                    BalancedQualityRadio.IsChecked = true;
                    LocalModelPanel.Visibility = Visibility.Collapsed;
                    break;
                case PipelinePresets.FastCloud:
                    FastCloudRadio.IsChecked = true;
                    LocalModelPanel.Visibility = Visibility.Collapsed;
                    break;
                case PipelinePresets.MaximumPrivacy:
                    MaximumPrivacyRadio.IsChecked = true;
                    LocalModelPanel.Visibility = Visibility.Visible;
                    break;
                case PipelinePresets.FastLocal:
                    FastLocalRadio.IsChecked = true;
                    LocalModelPanel.Visibility = Visibility.Visible;
                    break;
                default:
                    // Default to BalancedQuality if no valid preset
                    BalancedQualityRadio.IsChecked = true;
                    LocalModelPanel.Visibility = Visibility.Collapsed;
                    break;
            }

            // Load API key
            if (!string.IsNullOrEmpty(_currentSettings.OpenAIApiKey))
            {
                ApiKeyTextBox.Text = _currentSettings.OpenAIApiKey;
            }
            else
            {
                // Try to load from environment variable
                var envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (!string.IsNullOrEmpty(envKey))
                {
                    ApiKeyTextBox.Text = envKey;
                    _currentSettings.OpenAIApiKey = envKey;
                }
            }
        }

        private void PopulateLocalModels()
        {
            LocalModelComboBox.Items.Clear();

            var modelsDir = WhisperModelManager.GetModelsDirectory();
            
            if (!Directory.Exists(modelsDir))
            {
                LocalModelComboBox.Items.Add("No models found - run download script");
                LocalModelComboBox.SelectedIndex = 0;
                LocalModelComboBox.IsEnabled = false;
                return;
            }

            var modelFiles = Directory.GetFiles(modelsDir, "ggml-*.bin");
            
            if (modelFiles.Length == 0)
            {
                LocalModelComboBox.Items.Add("No models found - run download script");
                LocalModelComboBox.SelectedIndex = 0;
                LocalModelComboBox.IsEnabled = false;
                return;
            }

            foreach (var modelFile in modelFiles.OrderBy(f => f))
            {
                var fileName = Path.GetFileName(modelFile);
                var fileSize = new FileInfo(modelFile).Length / (1024.0 * 1024.0);
                var displayName = $"{fileName} ({fileSize:F0} MB)";
                
                LocalModelComboBox.Items.Add(new ModelItem 
                { 
                    DisplayName = displayName,
                    FilePath = modelFile 
                });
            }

            // Select the previously selected model or the first one
            if (!string.IsNullOrEmpty(_currentSettings.LocalModelPath))
            {
                var matchingItem = LocalModelComboBox.Items.Cast<ModelItem>()
                    .FirstOrDefault(m => m.FilePath == _currentSettings.LocalModelPath);
                
                if (matchingItem != null)
                {
                    LocalModelComboBox.SelectedItem = matchingItem;
                }
                else
                {
                    LocalModelComboBox.SelectedIndex = 0;
                }
            }
            else
            {
                LocalModelComboBox.SelectedIndex = 0;
            }

            LocalModelComboBox.IsEnabled = true;
        }

        private void DownloadModels_Click(object sender, RoutedEventArgs e)
        {
            var downloadWindow = new ModelDownloadWindow(_logger);
            downloadWindow.ShowDialog();

            // Refresh model list if any models were downloaded
            if (downloadWindow.ModelsDownloaded)
            {
                _logger.Log("Models were downloaded, refreshing model list...");

                // Clear and repopulate the model list
                PopulateLocalModels();

                _logger.Log($"Model list refreshed. Total models: {LocalModelComboBox.Items.Count}");

                // Force UI update
                LocalModelComboBox.Items.Refresh();
            }
        }

        private void PipelinePreset_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (MaximumQualityRadio.IsChecked == true)
            {
                _currentSettings.SelectedPipeline = PipelinePresets.MaximumQuality;
                LocalModelPanel.Visibility = Visibility.Collapsed;
            }
            else if (BalancedQualityRadio.IsChecked == true)
            {
                _currentSettings.SelectedPipeline = PipelinePresets.BalancedQuality;
                LocalModelPanel.Visibility = Visibility.Collapsed;
            }
            else if (FastCloudRadio.IsChecked == true)
            {
                _currentSettings.SelectedPipeline = PipelinePresets.FastCloud;
                LocalModelPanel.Visibility = Visibility.Collapsed;
            }
            else if (MaximumPrivacyRadio.IsChecked == true)
            {
                _currentSettings.SelectedPipeline = PipelinePresets.MaximumPrivacy;
                LocalModelPanel.Visibility = Visibility.Visible;
            }
            else if (FastLocalRadio.IsChecked == true)
            {
                _currentSettings.SelectedPipeline = PipelinePresets.FastLocal;
                LocalModelPanel.Visibility = Visibility.Visible;
            }

            AutoSave();
        }

        private void LocalModel_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (LocalModelComboBox.SelectedItem is ModelItem modelItem)
            {
                _currentSettings.LocalModelPath = modelItem.FilePath;
                
                var fileInfo = new FileInfo(modelItem.FilePath);
                var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
                ModelInfoText.Text = $"Selected: {Path.GetFileName(modelItem.FilePath)} ({sizeMB:F0} MB)";
            }
        }

        private void ApiKey_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isInitializing) return;

            _currentSettings.OpenAIApiKey = ApiKeyTextBox.Text;
            AutoSave();
        }

        private void AutoSave()
        {
            _settingsService.SaveSettings(_currentSettings);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Deathmatch_Click(object sender, RoutedEventArgs e)
        {
            // Validate that we have an API key for cloud pipelines
            if (string.IsNullOrWhiteSpace(_currentSettings.OpenAIApiKey))
            {
                MessageBox.Show(
                    "OpenAI API Key is required to run the benchmark.\n\n" +
                    "Please enter your API key in the settings above before running the benchmark.",
                    "API Key Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Check if local model is needed for benchmark
            if (string.IsNullOrEmpty(_currentSettings.LocalModelPath) ||
                !File.Exists(_currentSettings.LocalModelPath))
            {
                // Check if there are any models available but just not selected
                var modelsDir = WhisperModelManager.GetModelsDirectory();
                var availableModels = Directory.Exists(modelsDir)
                    ? Directory.GetFiles(modelsDir, "ggml-*.bin")
                    : Array.Empty<string>();

                if (availableModels.Length > 0)
                {
                    // Models exist but none is selected - prompt to select
                    var selectResult = MessageBox.Show(
                        $"You have {availableModels.Length} local model(s) downloaded but none is selected.\n\n" +
                        "Would you like to select a local model for complete benchmark testing?\n\n" +
                        "• Click 'Yes' to select a model from the Local Model section\n" +
                        "• Click 'No' to run cloud-only benchmark\n" +
                        "• Click 'Cancel' to return to settings",
                        "Select Local Model",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (selectResult == MessageBoxResult.Yes)
                    {
                        // Switch to the local pipeline tab to make model selection visible
                        MaximumPrivacyRadio.IsChecked = true;

                        MessageBox.Show(
                            "Please select a local model from the dropdown above, then run the benchmark again.",
                            "Select Model",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }
                    else if (selectResult == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                    // If No, continue with cloud-only benchmark
                }
                else
                {
                    // No models available - offer to download
                    var downloadResult = MessageBox.Show(
                        "No local Whisper models are downloaded.\n\n" +
                        "Would you like to download a local model now?\n\n" +
                        "• Click 'Yes' to download a model for complete benchmark testing\n" +
                        "• Click 'No' to run cloud-only benchmark\n" +
                        "• Click 'Cancel' to return to settings",
                        "Download Local Model",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (downloadResult == MessageBoxResult.Yes)
                    {
                        // Open download window
                        var downloadWindow = new ModelDownloadWindow(_logger);
                        downloadWindow.ShowDialog();

                        // Refresh model list if any models were downloaded
                        if (downloadWindow.ModelsDownloaded)
                        {
                            _logger.Log("Models were downloaded, refreshing model list...");
                            PopulateLocalModels();
                            LocalModelComboBox.Items.Refresh();
                            _logger.Log($"Model list refreshed. Total models: {LocalModelComboBox.Items.Count}");
                        }

                        // Check again if we have a model now
                        if (string.IsNullOrEmpty(_currentSettings.LocalModelPath) ||
                            !File.Exists(_currentSettings.LocalModelPath))
                        {
                            var continueResult = MessageBox.Show(
                                "No local model was selected.\n\n" +
                                "Continue with cloud-only benchmark?",
                                "Continue Benchmark?",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (continueResult == MessageBoxResult.No)
                            {
                                return;
                            }
                        }
                    }
                    else if (downloadResult == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                    // If No, continue with cloud-only benchmark
                }
            }

            // Create a new build context with current settings for the benchmark
            var benchmarkContext = new PipelineBuildContext
            {
                OpenAIApiKey = _currentSettings.OpenAIApiKey,
                LocalModelPath = _currentSettings.LocalModelPath,
                Logger = _buildContext.Logger,
                AppSettings = _currentSettings
            };

            var deathmatchWindow = new PipelineDeathmatchWindow(_audioService, _logger, _pipelineFactory, benchmarkContext);
            var dialogResult = deathmatchWindow.ShowDialog();

            if (dialogResult == true && deathmatchWindow.BenchmarkResults != null)
            {
                // Map DeathmatchResult to BenchmarkResult and store
                _benchmarkResults = deathmatchWindow.BenchmarkResults
                    .ToDictionary(
                        kvp => MapPipelineNameToPreset(kvp.Key),
                        kvp => new BenchmarkResult
                        {
                            AccuracyPercent = kvp.Value.AccuracyPercent,
                            DurationMs = kvp.Value.DurationMs,
                            IsWinner = kvp.Value.IsWinner
                        }
                    );

                UpdateBenchmarkResultsDisplay();
                _logger.Log($"Benchmark completed. Results stored for {_benchmarkResults.Count} pipelines");
            }
        }

        private string MapPipelineNameToPreset(string pipelineName)
        {
            // Map benchmark pipeline names to preset constants
            return pipelineName switch
            {
                "RNNoise + VAD (Cloud)" => PipelinePresets.MaximumQuality,
                "RNNoise Only" => PipelinePresets.BalancedQuality,
                "Baseline (No Preprocessing)" => PipelinePresets.FastCloud,
                "RNNoise + VAD (Local)" => PipelinePresets.MaximumPrivacy,
                "Local (No Preprocessing)" => PipelinePresets.FastLocal,
                _ => pipelineName
            };
        }

        private void UpdateBenchmarkResultsDisplay()
        {
            if (_benchmarkResults == null) return;

            // Update each pipeline option with its results
            UpdatePipelineResult(MaximumQualityResult, PipelinePresets.MaximumQuality);
            UpdatePipelineResult(BalancedQualityResult, PipelinePresets.BalancedQuality);
            UpdatePipelineResult(FastCloudResult, PipelinePresets.FastCloud);
            UpdatePipelineResult(MaximumPrivacyResult, PipelinePresets.MaximumPrivacy);
            UpdatePipelineResult(FastLocalResult, PipelinePresets.FastLocal);
        }

        private void UpdatePipelineResult(System.Windows.Controls.TextBlock resultTextBlock, string presetName)
        {
            if (_benchmarkResults != null && _benchmarkResults.TryGetValue(presetName, out var result))
            {
                var parts = new List<string>();

                // Show accuracy if available
                if (result.AccuracyPercent.HasValue)
                {
                    parts.Add($"{result.AccuracyPercent.Value:F1}% accuracy");
                }

                // Show duration if available
                if (result.DurationMs > 0)
                {
                    parts.Add($"{result.DurationMs:F0}ms");
                }

                // Combine both metrics
                string resultText = parts.Count > 0 ? string.Join(" • ", parts) : "No data";

                resultTextBlock.Text = resultText;
                resultTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                resultTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private async void SaveClose_Click(object sender, RoutedEventArgs e)
        {
            // Validate local model if selected pipeline requires it
            bool requiresLocalModel = _currentSettings.SelectedPipeline == PipelinePresets.MaximumPrivacy ||
                                     _currentSettings.SelectedPipeline == PipelinePresets.FastLocal;

            if (requiresLocalModel)
            {
                if (string.IsNullOrEmpty(_currentSettings.LocalModelPath) || !File.Exists(_currentSettings.LocalModelPath))
                {
                    MessageBox.Show(
                        "Please select a valid local model before saving.",
                        "Model Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Try to load the model to validate it works
                try
                {
                    // Disable UI during loading
                    SaveCloseButton.IsEnabled = false;
                    CloseButton.IsEnabled = false;
                    LocalModelComboBox.IsEnabled = false;
                    ModelInfoText.Text = "Loading model, please wait...";
                    
                    // Load model on background thread to avoid blocking UI
                    await Task.Run(() =>
                    {
                        // This will throw if model is invalid
                        using var testTranscriber = new LocalWhisperTranscriber(_currentSettings.LocalModelPath);
                    });
                    
                    ModelInfoText.Text = $"✓ Model loaded: {Path.GetFileName(_currentSettings.LocalModelPath)}";
                }
                catch (Exception ex)
                {
                    // Re-enable UI
                    SaveCloseButton.IsEnabled = true;
                    CloseButton.IsEnabled = true;
                    LocalModelComboBox.IsEnabled = true;
                    ModelInfoText.Text = "Failed to load model";
                    
                    MessageBox.Show(
                        $"Failed to load the selected model:\n\n{ex.Message}\n\nPlease select a different model or re-download the model file.",
                        "Model Load Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
            }
            
            // Save settings
            _settingsService.SaveSettings(_currentSettings);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            
            // Close without showing success message
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private class ModelItem
        {
            public required string DisplayName { get; init; }
            public required string FilePath { get; init; }

            public override string ToString() => DisplayName;
        }
    }

    public class BenchmarkResult
    {
        public double? AccuracyPercent { get; set; }
        public double DurationMs { get; set; }
        public bool IsWinner { get; set; }
    }
}
