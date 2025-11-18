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
        private readonly PipelineBuildContextFactory _buildContextFactory;
        private AppSettings _currentSettings;
        private bool _isInitializing = true;
        private Dictionary<string, BenchmarkResult>? _benchmarkResults;

        public event EventHandler? SettingsChanged;

        public SettingsWindow(
            SettingsService settingsService,
            IAudioRecordingService audioService,
            ILogger logger,
            PipelineFactory pipelineFactory,
            PipelineBuildContext buildContext,
            PipelineBuildContextFactory buildContextFactory)
        {
            InitializeComponent();

            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
            _buildContext = buildContext ?? throw new ArgumentNullException(nameof(buildContext));
            _buildContextFactory = buildContextFactory ?? throw new ArgumentNullException(nameof(buildContextFactory));

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

            // Load API key from settings file or build context
            string? apiKeyToDisplay = null;

            // 1. Check settings file first
            if (!string.IsNullOrEmpty(_currentSettings.OpenAIApiKey))
            {
                apiKeyToDisplay = _currentSettings.OpenAIApiKey;
            }
            // 2. Check build context (the API key actually being used by the system)
            else if (!string.IsNullOrEmpty(_buildContext?.OpenAIApiKey))
            {
                apiKeyToDisplay = _buildContext.OpenAIApiKey;
                _currentSettings.OpenAIApiKey = _buildContext.OpenAIApiKey;
            }

            // Set the textbox if we found an API key
            if (!string.IsNullOrEmpty(apiKeyToDisplay))
            {
                ApiKeyTextBox.Text = apiKeyToDisplay;
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
            _logger.Log($"[Settings] ApiKey_Changed fired. IsInitializing={_isInitializing}");
            if (_isInitializing) return;

            var newKey = ApiKeyTextBox.Text;
            _logger.Log($"[Settings] Saving API key change (length: {newKey?.Length ?? 0})");
            _currentSettings.OpenAIApiKey = newKey;
            AutoSave();
        }

        private void AutoSave()
        {
            _logger.Log($"[Settings] AutoSave called. API Key length: {_currentSettings.OpenAIApiKey?.Length ?? 0}");
            _settingsService.SaveSettings(_currentSettings);
            _logger.Log("[Settings] Settings saved to disk");
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
                    // Auto-select the first available model
                    _currentSettings.LocalModelPath = availableModels[0];
                    _settingsService.SaveSettings(_currentSettings);
                    PopulateLocalModels();

                    _logger.Log($"Auto-selected local model for benchmark: {Path.GetFileName(availableModels[0])}");
                }
                else
                {
                    // No models available - simple prompt
                    var downloadResult = MessageBox.Show(
                        "Local models are needed to benchmark privacy-focused pipelines.\n\n" +
                        "Download a local model now?",
                        "Download Local Model?",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (downloadResult == MessageBoxResult.Yes)
                    {
                        var downloadWindow = new ModelDownloadWindow(_logger);
                        downloadWindow.ShowDialog();

                        if (downloadWindow.ModelsDownloaded)
                        {
                            PopulateLocalModels();

                            // Auto-select first downloaded model
                            var newModels = Directory.Exists(modelsDir)
                                ? Directory.GetFiles(modelsDir, "ggml-*.bin")
                                : Array.Empty<string>();

                            if (newModels.Length > 0)
                            {
                                _currentSettings.LocalModelPath = newModels[0];
                                _settingsService.SaveSettings(_currentSettings);
                                _logger.Log($"Auto-selected downloaded model: {Path.GetFileName(newModels[0])}");
                            }
                        }
                    }
                }
            }

            // Create a new build context with current settings for the benchmark
            var benchmarkContext = _buildContextFactory.Create(_currentSettings);

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
            UpdatePipelineResult(
                MaximumQualityResult, MaximumQualityAccuracy, MaximumQualitySpeed,
                PipelinePresets.MaximumQuality);
            UpdatePipelineResult(
                BalancedQualityResult, BalancedQualityAccuracy, BalancedQualitySpeed,
                PipelinePresets.BalancedQuality);
            UpdatePipelineResult(
                FastCloudResult, FastCloudAccuracy, FastCloudSpeed,
                PipelinePresets.FastCloud);
            UpdatePipelineResult(
                MaximumPrivacyResult, MaximumPrivacyAccuracy, MaximumPrivacySpeed,
                PipelinePresets.MaximumPrivacy);
            UpdatePipelineResult(
                FastLocalResult, FastLocalAccuracy, FastLocalSpeed,
                PipelinePresets.FastLocal);
        }

        private void UpdatePipelineResult(
            System.Windows.Controls.Border resultBorder,
            System.Windows.Controls.TextBlock accuracyText,
            System.Windows.Controls.TextBlock speedText,
            string presetName)
        {
            if (_benchmarkResults != null && _benchmarkResults.TryGetValue(presetName, out var result))
            {
                // Show accuracy
                if (result.AccuracyPercent.HasValue)
                {
                    accuracyText.Text = $"{result.AccuracyPercent.Value:F1}%";
                }
                else
                {
                    accuracyText.Text = "N/A";
                }

                // Show duration
                if (result.DurationMs > 0)
                {
                    speedText.Text = $"{result.DurationMs:F0}ms";
                }
                else
                {
                    speedText.Text = "N/A";
                }

                resultBorder.Visibility = Visibility.Visible;
            }
            else
            {
                resultBorder.Visibility = Visibility.Collapsed;
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
