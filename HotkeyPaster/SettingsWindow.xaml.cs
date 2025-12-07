using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TalkKeys.Logging;
using TalkKeys.Services.Settings;
using TalkKeys.Services.Startup;
using TalkKeys.Services.Triggers;

namespace TalkKeys
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly ILogger _logger;
        private readonly Services.Audio.IAudioRecordingService _audioService;
        private readonly IStartupService _startupService;
        private readonly TriggerPluginManager? _triggerPluginManager;
        private AppSettings _currentSettings;
        private bool _isInitializing = true;

        // Plugin UI state
        private readonly Dictionary<string, RadioButton> _pluginTabButtons = new();
        private readonly Dictionary<string, FrameworkElement> _pluginSettingsPanels = new();
        private string? _selectedPluginId;

        public event EventHandler? SettingsChanged;

        public SettingsWindow(
            SettingsService settingsService,
            ILogger logger,
            Services.Audio.IAudioRecordingService audioService,
            TriggerPluginManager? triggerPluginManager = null)
        {
            InitializeComponent();

            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _startupService = new StartupService();
            _triggerPluginManager = triggerPluginManager;

            _currentSettings = _settingsService.LoadSettings();

            LoadSettings();
            LoadTriggerPlugins();

            _isInitializing = false;
        }

        private void LoadSettings()
        {
            // Load Transcription Provider
            LoadTranscriptionProvider();

            // Load API keys
            if (!string.IsNullOrEmpty(_currentSettings.OpenAIApiKey))
            {
                ApiKeyTextBox.Password = _currentSettings.OpenAIApiKey;
            }
            if (!string.IsNullOrEmpty(_currentSettings.GroqApiKey))
            {
                GroqApiKeyTextBox.Password = _currentSettings.GroqApiKey;
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

            // Load startup setting - check actual registry state
            StartWithWindowsCheckBox.IsChecked = _startupService.IsStartupEnabled;
        }

        private void LoadTriggerPlugins()
        {
            if (_triggerPluginManager == null) return;

            var plugins = _triggerPluginManager.GetPlugins();
            if (!plugins.Any()) return;

            TriggerPluginTabsPanel.Children.Clear();
            _pluginTabButtons.Clear();
            _pluginSettingsPanels.Clear();

            foreach (var plugin in plugins)
            {
                // Create tab button for this plugin
                var tabButton = new RadioButton
                {
                    Content = $"{plugin.Icon} {plugin.DisplayName}",
                    GroupName = "TriggerPluginTabs",
                    Tag = plugin.PluginId,
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Style = CreatePluginTabStyle()
                };

                tabButton.Checked += OnPluginTabSelected;
                TriggerPluginTabsPanel.Children.Add(tabButton);
                _pluginTabButtons[plugin.PluginId] = tabButton;

                // Create settings panel for this plugin
                var settingsPanel = plugin.CreateSettingsPanel();
                settingsPanel.Visibility = Visibility.Collapsed;
                _pluginSettingsPanels[plugin.PluginId] = settingsPanel;
            }

            // Select the first plugin by default
            if (_pluginTabButtons.Any())
            {
                var firstPlugin = _pluginTabButtons.First();
                firstPlugin.Value.IsChecked = true;
                _selectedPluginId = firstPlugin.Key;
            }
        }

        private Style CreatePluginTabStyle()
        {
            var style = new Style(typeof(RadioButton));

            var template = new ControlTemplate(typeof(RadioButton));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "TabBorder";
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(12, 8, 12, 8));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);

            template.VisualTree = borderFactory;

            // Triggers
            var checkedTrigger = new Trigger { Property = RadioButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1")), "TabBorder"));
            checkedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            template.Triggers.Add(checkedTrigger);

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")), "TabBorder"));
            template.Triggers.Add(hoverTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Medium));
            style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"))));

            return style;
        }

        private void OnPluginTabSelected(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton button && button.Tag is string pluginId)
            {
                _selectedPluginId = pluginId;
                UpdatePluginContentArea();
            }
        }

        private void UpdatePluginContentArea()
        {
            TriggerPluginContentArea.Child = null;

            if (_selectedPluginId != null && _pluginSettingsPanels.TryGetValue(_selectedPluginId, out var panel))
            {
                panel.Visibility = Visibility.Visible;
                TriggerPluginContentArea.Child = panel;
            }
        }

        private void Tab_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (GeneralTab.IsChecked == true)
            {
                GeneralTabContent.Visibility = Visibility.Visible;
                TriggersTabContent.Visibility = Visibility.Collapsed;
            }
            else if (TriggersTab.IsChecked == true)
            {
                GeneralTabContent.Visibility = Visibility.Collapsed;
                TriggersTabContent.Visibility = Visibility.Visible;
                UpdatePluginContentArea();
            }
        }

        private void LoadTranscriptionProvider()
        {
            var tag = _currentSettings.TranscriptionProvider.ToString();
            foreach (ComboBoxItem item in TranscriptionProviderComboBox.Items)
            {
                if (item.Tag?.ToString() == tag)
                {
                    TranscriptionProviderComboBox.SelectedItem = item;
                    break;
                }
            }

            // Update visibility of API key panels
            UpdateApiKeyPanelVisibility();
        }

        private void UpdateApiKeyPanelVisibility()
        {
            var provider = _currentSettings.TranscriptionProvider;

            // Show OpenAI panel only when OpenAI is selected
            OpenAIKeyPanel.Visibility = provider == TranscriptionProvider.OpenAI
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Show Groq panel only when Groq is selected
            GroqKeyPanel.Visibility = provider == TranscriptionProvider.Groq
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void TranscriptionProvider_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (TranscriptionProviderComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                var provider = tag switch
                {
                    "OpenAI" => TranscriptionProvider.OpenAI,
                    "Groq" => TranscriptionProvider.Groq,
                    _ => TranscriptionProvider.OpenAI
                };

                _currentSettings.TranscriptionProvider = provider;
                UpdateApiKeyPanelVisibility();
            }
        }

        private void GroqApiKey_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _currentSettings.GroqApiKey = GroqApiKeyTextBox.Password;
        }

        private void ApiKey_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _currentSettings.OpenAIApiKey = ApiKeyTextBox.Password;
        }

        private void AudioDevice_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var selectedIndex = AudioDeviceComboBox.SelectedIndex;
            _currentSettings.AudioDeviceIndex = selectedIndex;

            // Update the audio service immediately (hardware setting)
            _audioService.SetDevice(selectedIndex);
        }

        private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            // This is handled on save, not immediately
        }

        private void SaveClose_Click(object sender, RoutedEventArgs e)
        {
            // Validate API keys based on selected provider
            if (_currentSettings.TranscriptionProvider == TranscriptionProvider.OpenAI)
            {
                if (string.IsNullOrWhiteSpace(_currentSettings.OpenAIApiKey))
                {
                    MessageBox.Show(
                        "Please enter your OpenAI API key before saving.",
                        "API Key Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
            else if (_currentSettings.TranscriptionProvider == TranscriptionProvider.Groq)
            {
                if (string.IsNullOrWhiteSpace(_currentSettings.GroqApiKey))
                {
                    MessageBox.Show(
                        "Please enter your Groq API key before saving.",
                        "Groq API Key Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            // Save plugin configurations
            if (_triggerPluginManager != null)
            {
                _currentSettings.TriggerPlugins = _triggerPluginManager.GetAllConfigurations();
            }

            // Update startup registry setting
            var startWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            _startupService.SetStartupEnabled(startWithWindows);

            // Save settings to disk
            if (_settingsService.SaveSettings(_currentSettings))
            {
                _logger.Log($"[Settings] Settings saved. Provider: {_currentSettings.TranscriptionProvider}");
                SettingsChanged?.Invoke(this, EventArgs.Empty);
                Close();
            }
            else
            {
                MessageBox.Show(
                    "Failed to save settings. Please check that the application has write access to the settings folder.",
                    "Settings Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
