using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HotkeyPaster.Logging;
using HotkeyPaster.Services.Audio;
using HotkeyPaster.Services.Settings;
using HotkeyPaster.Services.Transcription;

namespace HotkeyPaster
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly IAudioRecordingService _audioService;
        private readonly ILogger _logger;
        private AppSettings _currentSettings;
        private bool _isInitializing = true;

        public event EventHandler? SettingsChanged;

        public SettingsWindow(
            SettingsService settingsService,
            IAudioRecordingService audioService,
            ILogger logger)
        {
            InitializeComponent();
            
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _currentSettings = _settingsService.LoadSettings();
            
            LoadSettings();
            PopulateLocalModels();
            
            _isInitializing = false;
        }

        private void LoadSettings()
        {
            // Load transcription mode
            if (_currentSettings.TranscriptionMode == TranscriptionMode.Cloud)
            {
                CloudModeRadio.IsChecked = true;
            }
            else
            {
                LocalModeRadio.IsChecked = true;
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

            // Load text cleaning preference
            EnableCleaningCheckBox.IsChecked = _currentSettings.EnableTextCleaning;
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

        private void TranscriptionMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (CloudModeRadio.IsChecked == true)
            {
                _currentSettings.TranscriptionMode = TranscriptionMode.Cloud;
                LocalModelPanel.Visibility = Visibility.Collapsed;
            }
            else if (LocalModeRadio.IsChecked == true)
            {
                _currentSettings.TranscriptionMode = TranscriptionMode.Local;
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

        private void TextCleaning_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            _currentSettings.EnableTextCleaning = EnableCleaningCheckBox.IsChecked == true;
            AutoSave();
        }

        private void AutoSave()
        {
            _settingsService.SaveSettings(_currentSettings);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SpeedTest_Click(object sender, RoutedEventArgs e)
        {
            // Validate settings before running speed test
            if (string.IsNullOrWhiteSpace(_currentSettings.OpenAIApiKey))
            {
                MessageBox.Show(
                    "OpenAI API Key is required to run the speed test.\n\n" +
                    "Please enter your API key in the settings above.",
                    "API Key Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_currentSettings.TranscriptionMode == TranscriptionMode.Local &&
                string.IsNullOrEmpty(_currentSettings.LocalModelPath))
            {
                MessageBox.Show(
                    "No local model selected.\n\n" +
                    "Please select a local model or download one using the download script.",
                    "Model Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var speedTestWindow = new SpeedTestWindow(_audioService, _logger);
            speedTestWindow.Show();
        }

        private async void SaveClose_Click(object sender, RoutedEventArgs e)
        {
            // Validate local model if in local mode
            if (_currentSettings.TranscriptionMode == TranscriptionMode.Local)
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
}
