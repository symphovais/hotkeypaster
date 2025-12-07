using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
        private string[] _audioDevices = Array.Empty<string>();
        private int _selectedAudioDeviceIndex = 0;

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
            UpdateVersion();

            _isInitializing = false;
        }

        private void UpdateVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = $"TalkKeys v{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        private void LoadSettings()
        {
            // Load provider hint
            UpdateProviderHint();

            // Load API key hint
            UpdateApiKeyHint();

            // Load Audio Devices
            _audioDevices = _audioService.GetAvailableDevices();
            _selectedAudioDeviceIndex = _currentSettings.AudioDeviceIndex;

            if (_selectedAudioDeviceIndex >= 0 && _selectedAudioDeviceIndex < _audioDevices.Length)
            {
                MicrophoneHint.Text = _audioDevices[_selectedAudioDeviceIndex];
            }
            else if (_audioDevices.Length > 0)
            {
                _selectedAudioDeviceIndex = 0;
                MicrophoneHint.Text = _audioDevices[0];
            }

            // Load startup setting
            StartWithWindowsCheckBox.IsChecked = _startupService.IsStartupEnabled;
        }

        private void UpdateProviderHint()
        {
            TranscriptionProviderHint.Text = _currentSettings.TranscriptionProvider switch
            {
                TranscriptionProvider.OpenAI => "OpenAI Whisper",
                TranscriptionProvider.Groq => "Groq Whisper (Faster)",
                _ => "OpenAI Whisper"
            };
        }

        private void UpdateApiKeyHint()
        {
            if (_currentSettings.TranscriptionProvider == TranscriptionProvider.Groq)
            {
                ApiKeyLabel.Text = "Groq API Key";
                ApiKeyHint.Text = string.IsNullOrEmpty(_currentSettings.GroqApiKey)
                    ? "Required for Groq transcription"
                    : "Configured";
            }
            else
            {
                ApiKeyLabel.Text = "OpenAI API Key";
                ApiKeyHint.Text = string.IsNullOrEmpty(_currentSettings.OpenAIApiKey)
                    ? "Required for transcription"
                    : "Configured";
            }
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
                var tabButton = new RadioButton
                {
                    Content = $"{plugin.Icon} {plugin.DisplayName}",
                    GroupName = "TriggerPluginTabs",
                    Tag = plugin.PluginId,
                    Padding = new Thickness(16, 10, 16, 10),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Style = CreatePluginTabStyle()
                };

                tabButton.Checked += OnPluginTabSelected;
                TriggerPluginTabsPanel.Children.Add(tabButton);
                _pluginTabButtons[plugin.PluginId] = tabButton;

                var settingsPanel = plugin.CreateSettingsPanel();
                settingsPanel.Visibility = Visibility.Collapsed;
                _pluginSettingsPanels[plugin.PluginId] = settingsPanel;
            }

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
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(16, 10, 16, 10));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);

            template.VisualTree = borderFactory;

            var checkedTrigger = new Trigger { Property = RadioButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED")), "TabBorder"));
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

        private void ChangeProvider_Click(object sender, RoutedEventArgs e)
        {
            ShowProviderDialog();
        }

        private void ShowProviderDialog()
        {
            var dialog = CreateModalDialog("Transcription Provider");

            var content = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };

            var options = new[]
            {
                ("OpenAI", "OpenAI Whisper", "High quality transcription"),
                ("Groq", "Groq Whisper", "Faster transcription with Llama")
            };

            RadioButton? selectedOption = null;

            foreach (var (tag, name, description) in options)
            {
                var isSelected = (_currentSettings.TranscriptionProvider == TranscriptionProvider.OpenAI && tag == "OpenAI") ||
                                 (_currentSettings.TranscriptionProvider == TranscriptionProvider.Groq && tag == "Groq");

                var option = CreateRadioOption(tag, name, description, isSelected, "ProviderGroup");
                if (isSelected) selectedOption = option;
                content.Children.Add(option);
            }

            var outerGrid = (Grid)dialog.Content;
            var border = (Border)outerGrid.Children[0];
            var dialogContent = (StackPanel)border.Child;
            dialogContent.Children.Add(content);

            dialog.ShowDialog();

            // Find selected option
            foreach (RadioButton rb in content.Children)
            {
                if (rb.IsChecked == true && rb.Tag is string tag)
                {
                    _currentSettings.TranscriptionProvider = tag switch
                    {
                        "OpenAI" => TranscriptionProvider.OpenAI,
                        "Groq" => TranscriptionProvider.Groq,
                        _ => TranscriptionProvider.OpenAI
                    };
                    UpdateProviderHint();
                    UpdateApiKeyHint();
                    break;
                }
            }
        }

        private void ChangeApiKey_Click(object sender, RoutedEventArgs e)
        {
            ShowApiKeyDialog();
        }

        private void ShowApiKeyDialog()
        {
            var isGroq = _currentSettings.TranscriptionProvider == TranscriptionProvider.Groq;
            var title = isGroq ? "Groq API Key" : "OpenAI API Key";
            var currentKey = isGroq ? _currentSettings.GroqApiKey : _currentSettings.OpenAIApiKey;
            var helpText = isGroq
                ? "Get your key from console.groq.com"
                : "Get your key from platform.openai.com";

            var dialog = CreateModalDialog(title);

            var content = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };

            var passwordBox = new PasswordBox
            {
                Password = currentKey ?? "",
                Height = 44,
                Padding = new Thickness(12, 12, 12, 12),
                FontSize = 14,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1)
            };

            var helpLabel = new TextBlock
            {
                Text = helpText,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 8, 0, 0)
            };

            content.Children.Add(passwordBox);
            content.Children.Add(helpLabel);

            // Add save button
            var saveButton = new Button
            {
                Content = "Save",
                Height = 38,
                Margin = new Thickness(0, 20, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED")),
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            saveButton.Click += (s, e) => dialog.Close();

            content.Children.Add(saveButton);

            var outerGrid = (Grid)dialog.Content;
            var border = (Border)outerGrid.Children[0];
            var dialogContent = (StackPanel)border.Child;
            dialogContent.Children.Add(content);

            dialog.ShowDialog();

            // Save the key
            if (isGroq)
            {
                _currentSettings.GroqApiKey = passwordBox.Password;
            }
            else
            {
                _currentSettings.OpenAIApiKey = passwordBox.Password;
            }
            UpdateApiKeyHint();
        }

        private void ChangeMicrophone_Click(object sender, RoutedEventArgs e)
        {
            ShowMicrophoneDialog();
        }

        private void ShowMicrophoneDialog()
        {
            var dialog = CreateModalDialog("Microphone");

            var content = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };

            for (int i = 0; i < _audioDevices.Length; i++)
            {
                var isSelected = i == _selectedAudioDeviceIndex;
                var option = CreateRadioOption(
                    i.ToString(),
                    _audioDevices[i],
                    null,
                    isSelected,
                    "MicrophoneGroup");
                content.Children.Add(option);
            }

            var outerGrid = (Grid)dialog.Content;
            var border = (Border)outerGrid.Children[0];
            var dialogContent = (StackPanel)border.Child;
            dialogContent.Children.Add(content);

            dialog.ShowDialog();

            // Find selected option
            foreach (RadioButton rb in content.Children)
            {
                if (rb.IsChecked == true && rb.Tag is string tag && int.TryParse(tag, out int index))
                {
                    _selectedAudioDeviceIndex = index;
                    _currentSettings.AudioDeviceIndex = index;
                    MicrophoneHint.Text = _audioDevices[index];
                    _audioService.SetDevice(index);
                    break;
                }
            }
        }

        private Window CreateModalDialog(string title)
        {
            var dialog = new Window
            {
                Title = title,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), // Semi-transparent backdrop
                Width = this.Width,
                Height = this.Height,
                Left = this.Left,
                Top = this.Top
            };

            // Outer grid to center the dialog content
            var outerGrid = new Grid();

            // Click on backdrop to close
            outerGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (e.Source == outerGrid)
                    dialog.Close();
            };

            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(24),
                Width = 400,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 40,
                    ShadowDepth = 12,
                    Opacity = 0.3
                }
            };

            var mainStack = new StackPanel();

            // Header with title and close button
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);
            header.Children.Add(titleBlock);

            var closeButton = new Button
            {
                Content = "\xE711",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Width = 32,
                Height = 32,
                FontSize = 10,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeButton.Click += (s, e) => dialog.Close();
            Grid.SetColumn(closeButton, 1);
            header.Children.Add(closeButton);

            mainStack.Children.Add(header);

            border.Child = mainStack;
            outerGrid.Children.Add(border);
            dialog.Content = outerGrid;

            // Allow dragging the dialog card
            border.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true; // Prevent backdrop click handler
            };

            return dialog;
        }

        private RadioButton CreateRadioOption(string tag, string label, string? description, bool isChecked, string groupName)
        {
            var radioButton = new RadioButton
            {
                Tag = tag,
                GroupName = groupName,
                IsChecked = isChecked,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var template = new ControlTemplate(typeof(RadioButton));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "OptionBorder";
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA")));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(16, 14, 16, 14));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            borderFactory.SetValue(Border.BorderBrushProperty, Brushes.Transparent);

            var gridFactory = new FrameworkElementFactory(typeof(Grid));

            var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);

            gridFactory.AppendChild(col1);
            gridFactory.AppendChild(col2);

            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(Grid.ColumnProperty, 0);
            stackFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            var labelFactory = new FrameworkElementFactory(typeof(TextBlock));
            labelFactory.SetValue(TextBlock.TextProperty, label);
            labelFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
            labelFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            labelFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")));
            stackFactory.AppendChild(labelFactory);

            if (!string.IsNullOrEmpty(description))
            {
                var descFactory = new FrameworkElementFactory(typeof(TextBlock));
                descFactory.SetValue(TextBlock.TextProperty, description);
                descFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
                descFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")));
                descFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 4, 0, 0));
                stackFactory.AppendChild(descFactory);
            }

            gridFactory.AppendChild(stackFactory);

            // Check indicator (hamburger style lines)
            var indicatorFactory = new FrameworkElementFactory(typeof(StackPanel));
            indicatorFactory.Name = "Indicator";
            indicatorFactory.SetValue(Grid.ColumnProperty, 1);
            indicatorFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            indicatorFactory.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            indicatorFactory.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);

            for (int i = 0; i < 3; i++)
            {
                var lineFactory = new FrameworkElementFactory(typeof(Border));
                lineFactory.SetValue(Border.WidthProperty, 16.0);
                lineFactory.SetValue(Border.HeightProperty, 2.0);
                lineFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")));
                lineFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(1));
                lineFactory.SetValue(Border.MarginProperty, new Thickness(0, i == 0 ? 0 : 3, 0, 0));
                indicatorFactory.AppendChild(lineFactory);
            }

            gridFactory.AppendChild(indicatorFactory);
            borderFactory.AppendChild(gridFactory);

            template.VisualTree = borderFactory;

            // Checked trigger
            var checkedTrigger = new Trigger { Property = RadioButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3E8FF")), "OptionBorder"));
            checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED")), "OptionBorder"));
            checkedTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "Indicator"));
            template.Triggers.Add(checkedTrigger);

            // Hover trigger
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")), "OptionBorder"));
            template.Triggers.Add(hoverTrigger);

            radioButton.Template = template;
            return radioButton;
        }

        private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            // This is handled on save
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
