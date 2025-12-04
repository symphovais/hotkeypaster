using System;
using System.Windows;
using TalkKeys.Logging;
using TalkKeys.Services.Settings;

namespace TalkKeys
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly ILogger _logger;
        private readonly Services.Audio.IAudioRecordingService _audioService;
        private AppSettings _currentSettings;
        private bool _isInitializing = true;

        public event EventHandler? SettingsChanged;

        public SettingsWindow(SettingsService settingsService, ILogger logger, Services.Audio.IAudioRecordingService audioService)
        {
            InitializeComponent();

            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));

            _currentSettings = _settingsService.LoadSettings();

            LoadSettings();

            _isInitializing = false;
        }

        private void LoadSettings()
        {
            // Load API key
            if (!string.IsNullOrEmpty(_currentSettings.OpenAIApiKey))
            {
                ApiKeyTextBox.Text = _currentSettings.OpenAIApiKey;
            }

            // Load Audio Devices
            var devices = _audioService.GetAvailableDevices();
            AudioDeviceComboBox.ItemsSource = devices;
            
            if (_currentSettings.AudioDeviceIndex >= 0 && _currentSettings.AudioDeviceIndex < devices.Length)
            {
                AudioDeviceComboBox.SelectedIndex = _currentSettings.AudioDeviceIndex;
            }
            else if (devices.Length > 0)
            {
                AudioDeviceComboBox.SelectedIndex = 0;
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

        private void AudioDevice_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var selectedIndex = AudioDeviceComboBox.SelectedIndex;
            _logger.Log($"[Settings] Audio device changed to index: {selectedIndex}");
            _currentSettings.AudioDeviceIndex = selectedIndex;
            
            // Update the service immediately
            _audioService.SetDevice(selectedIndex);
            
            AutoSave();
        }

        private void AutoSave()
        {
            _logger.Log($"[Settings] AutoSave called. API Key length: {_currentSettings.OpenAIApiKey?.Length ?? 0}");
            _settingsService.SaveSettings(_currentSettings);
            _logger.Log("[Settings] Settings saved to disk");
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SaveClose_Click(object sender, RoutedEventArgs e)
        {
            // Validate API key
            if (string.IsNullOrWhiteSpace(_currentSettings.OpenAIApiKey))
            {
                MessageBox.Show(
                    "Please enter your OpenAI API key before saving.",
                    "API Key Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Save settings
            _settingsService.SaveSettings(_currentSettings);
            SettingsChanged?.Invoke(this, EventArgs.Empty);

            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
