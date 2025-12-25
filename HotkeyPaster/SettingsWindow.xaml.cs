using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using TalkKeys.Logging;
using TalkKeys.Services.Auth;
using TalkKeys.Services.History;
using TalkKeys.Services.Plugins;
using TalkKeys.Services.Settings;
using TalkKeys.Services.Startup;
using TalkKeys.Services.Triggers;
using TalkKeys.Services.Words;
using TalkKeys.PluginSdk;
using TalkKeys.Plugins.Explainer;

namespace TalkKeys
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly ILogger _logger;
        private readonly Services.Audio.IAudioRecordingService _audioService;
        private readonly IStartupService _startupService;
        private readonly TriggerPluginManager? _triggerPluginManager;
        private readonly PluginManager? _pluginManager;
        private AppSettings _currentSettings;
        private bool _isInitializing = true;
        private string[] _audioDevices = Array.Empty<string>();
        private int _selectedAudioDeviceIndex = 0;

        // Plugin UI state
        private readonly Dictionary<string, RadioButton> _pluginTabButtons = new();
        private readonly Dictionary<string, FrameworkElement> _pluginSettingsPanels = new();
        private string? _selectedPluginId;

        // Explainer hotkey state
        private string _explainerHotkey = "Ctrl+Win+E";

        // Words list state
        private readonly ITranscriptionHistoryService? _historyService;
        private ObservableCollection<string> _wordsList = new();
        private ObservableCollection<string> _suggestions = new();

        public event EventHandler? SettingsChanged;

        public SettingsWindow(
            SettingsService settingsService,
            ILogger logger,
            Services.Audio.IAudioRecordingService audioService,
            TriggerPluginManager? triggerPluginManager = null,
            PluginManager? pluginManager = null,
            ITranscriptionHistoryService? historyService = null)
        {
            InitializeComponent();

            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _startupService = new StartupService();
            _triggerPluginManager = triggerPluginManager;
            _pluginManager = pluginManager;
            _historyService = historyService;

            _currentSettings = _settingsService.LoadSettings();

            LoadSettings();
            LoadTriggerPlugins();
            LoadExplainerSettings();
            LoadWordsSettings();
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
            // Load API key hint
            UpdateApiKeyHint();

            // Load account card display
            UpdateAccountCard();

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

            // Load experimental settings
            ExperimentalFeaturesEnabledCheckBox.IsChecked = _currentSettings.ExperimentalFeaturesEnabled;
            PostPasteSuggestionsEnabledCheckBox.IsChecked = _currentSettings.PostPasteSuggestionsEnabled;
            UpdateExperimentalControls();
        }

        private void UpdateExperimentalControls()
        {
            var enabled = _currentSettings.ExperimentalFeaturesEnabled;

            PostPasteSuggestionsEnabledCheckBox.IsEnabled = enabled;

            if (!enabled)
            {
                _currentSettings.PostPasteSuggestionsEnabled = false;
                PostPasteSuggestionsEnabledCheckBox.IsChecked = false;
            }
        }

        private void ExperimentalFeatures_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            _currentSettings.ExperimentalFeaturesEnabled = ExperimentalFeaturesEnabledCheckBox.IsChecked == true;
            UpdateExperimentalControls();
        }

        private void PostPasteSuggestions_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            _currentSettings.PostPasteSuggestionsEnabled = PostPasteSuggestionsEnabledCheckBox.IsChecked == true;
        }

        private void LoadExplainerSettings()
        {
            // Load explainer hotkey from settings
            if (_currentSettings.Plugins.TryGetValue("explainer", out var config))
            {
                _explainerHotkey = config.GetSetting(ExplainerPlugin.SettingHotkey, "Ctrl+Win+E");
            }
            ExplainerHotkeyTextBox.Text = _explainerHotkey;
        }

        private void UpdateApiKeyHint()
        {
            if (_currentSettings.AuthMode == AuthMode.TalkKeysAccount)
            {
                // Show TalkKeys account info
                ApiKeyLabel.Text = "TalkKeys Account";
                if (!string.IsNullOrEmpty(_currentSettings.TalkKeysUserEmail))
                {
                    ApiKeyHint.Text = $"Signed in as {_currentSettings.TalkKeysUserEmail}";
                }
                else
                {
                    ApiKeyHint.Text = "Free account (10 min/day)";
                }
            }
            else
            {
                // Show API key status
                ApiKeyLabel.Text = "Groq API Key";
                ApiKeyHint.Text = string.IsNullOrEmpty(_currentSettings.GroqApiKey)
                    ? "Required for transcription"
                    : "Configured (unlimited)";
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
                    Padding = new Thickness(12, 8, 12, 8),
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
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(12, 8, 12, 8));

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
            style.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
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

            // Hide all tabs first
            TalkKeysTabContent.Visibility = Visibility.Collapsed;
            WordsTabContent.Visibility = Visibility.Collapsed;
            ExplainerTabContent.Visibility = Visibility.Collapsed;
            GeneralTabContent.Visibility = Visibility.Collapsed;

            if (TalkKeysTab.IsChecked == true)
            {
                TalkKeysTabContent.Visibility = Visibility.Visible;
                UpdatePluginContentArea();
            }
            else if (WordsTab.IsChecked == true)
            {
                WordsTabContent.Visibility = Visibility.Visible;
                UpdateWordsUI();
            }
            else if (ExplainerTab.IsChecked == true)
            {
                ExplainerTabContent.Visibility = Visibility.Visible;
            }
            else if (GeneralTab.IsChecked == true)
            {
                GeneralTabContent.Visibility = Visibility.Visible;
            }
        }

        #region Explainer Hotkey Handling

        private void ExplainerHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            var modifiers = Keyboard.Modifiers;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Skip if only modifiers pressed
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin ||
                key == Key.DeadCharProcessed)
                return;

            // Add Windows modifier if Win key is pressed
            var finalModifiers = modifiers;
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            {
                finalModifiers |= ModifierKeys.Windows;
            }

            _explainerHotkey = FormatHotkey(finalModifiers, key);
            ExplainerHotkeyTextBox.Text = _explainerHotkey;
        }

        private void ExplainerHotkey_GotFocus(object sender, RoutedEventArgs e)
        {
            ExplainerHotkeyTextBox.Text = "Press keys...";
        }

        private void ExplainerHotkey_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ExplainerHotkeyTextBox.Text == "Press keys...")
            {
                ExplainerHotkeyTextBox.Text = _explainerHotkey;
            }
        }

        private static string FormatHotkey(ModifierKeys modifiers, Key key)
        {
            var parts = new List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }

        #endregion

        private void ChangeApiKey_Click(object sender, RoutedEventArgs e)
        {
            ShowApiKeyDialog();
        }

        private void ShowApiKeyDialog()
        {
            if (_currentSettings.AuthMode == AuthMode.TalkKeysAccount)
            {
                // Show account options dialog
                ShowAccountOptionsDialog();
            }
            else
            {
                // Show API key entry dialog
                ShowGroqApiKeyDialog();
            }
        }

        private void ShowAccountOptionsDialog()
        {
            var isSignedIn = !string.IsNullOrEmpty(_currentSettings.TalkKeysAccessToken);
            var dialog = CreateModalDialog("Account");

            var content = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };

            if (isSignedIn)
            {
                // Current account info
                var accountInfo = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3E8FF")),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 16)
                };

                var accountStack = new StackPanel();
                accountStack.Children.Add(new TextBlock
                {
                    Text = _currentSettings.TalkKeysUserName ?? "TalkKeys Account",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED"))
                });
                accountStack.Children.Add(new TextBlock
                {
                    Text = _currentSettings.TalkKeysUserEmail ?? "Signed in",
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                    Margin = new Thickness(0, 4, 0, 0)
                });
                accountStack.Children.Add(new TextBlock
                {
                    Text = "Free tier: 10 minutes/day",
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                    Margin = new Thickness(0, 4, 0, 0)
                });
                accountInfo.Child = accountStack;
                content.Children.Add(accountInfo);

                // Sign out button
                var signOutButton = new Button
                {
                    Content = "Sign Out",
                    Height = 38,
                    Margin = new Thickness(0, 0, 0, 12),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                    FontSize = 14,
                    FontWeight = FontWeights.Medium,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                signOutButton.Click += (s, e) =>
                {
                    if (MessageBox.Show("Are you sure you want to sign out?", "Sign Out",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        // Clear TalkKeys credentials
                        _currentSettings.TalkKeysAccessToken = null;
                        _currentSettings.TalkKeysRefreshToken = null;
                        _currentSettings.TalkKeysUserEmail = null;
                        _currentSettings.TalkKeysUserName = null;
                        dialog.Close();
                        UpdateApiKeyHint();
                        UpdateAccountCard();

                        // Show account dialog again with sign-in option
                        ShowAccountOptionsDialog();
                    }
                };
                content.Children.Add(signOutButton);
            }
            else
            {
                // Not signed in - show sign-in option
                var infoText = new TextBlock
                {
                    Text = "Sign in with Google to get 10 minutes of free transcription daily.",
                    FontSize = 13,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 16)
                };
                content.Children.Add(infoText);

                // Sign in button
                var signInButton = new Button
                {
                    Content = "Sign in with Google",
                    Height = 44,
                    Margin = new Thickness(0, 0, 0, 12),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED")),
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                signInButton.Click += async (s, e) =>
                {
                    signInButton.IsEnabled = false;
                    signInButton.Content = "Opening browser...";

                    try
                    {
                        var authService = new TalkKeysAuthService(_settingsService, _logger);
                        var result = await authService.LoginAsync();

                        if (result != null)
                        {
                            _currentSettings = _settingsService.LoadSettings();
                            dialog.Close();
                            UpdateApiKeyHint();
                            UpdateAccountCard();
                            SettingsChanged?.Invoke(this, EventArgs.Empty);
                            MessageBox.Show($"Welcome, {result.Name ?? result.Email}!", "Signed In",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            signInButton.IsEnabled = true;
                            signInButton.Content = "Sign in with Google";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Sign-in error: {ex.Message}");
                        signInButton.IsEnabled = true;
                        signInButton.Content = "Sign in with Google";
                        MessageBox.Show($"Sign-in failed: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                content.Children.Add(signInButton);
            }

            // Switch to own API key
            var switchButton = new Button
            {
                Content = "Use my own Groq API key instead",
                Height = 38,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                FontSize = 13,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            switchButton.Click += (s, e) =>
            {
                dialog.Close();
                _currentSettings.AuthMode = AuthMode.OwnApiKey;
                ShowGroqApiKeyDialog();
            };
            content.Children.Add(switchButton);

            var outerGrid = (Grid)dialog.Content;
            var border = (Border)outerGrid.Children[0];
            var dialogContent = (StackPanel)border.Child;
            dialogContent.Children.Add(content);

            dialog.ShowDialog();
        }

        private void ShowGroqApiKeyDialog()
        {
            var dialog = CreateModalDialog("Groq API Key");

            var content = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };

            var passwordBox = new PasswordBox
            {
                Password = _currentSettings.GroqApiKey ?? "",
                Height = 44,
                Padding = new Thickness(12, 12, 12, 12),
                FontSize = 14,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1)
            };

            var helpLabel = new TextBlock
            {
                Text = "Get your key from console.groq.com",
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

            // Save the key if entered
            if (!string.IsNullOrWhiteSpace(passwordBox.Password))
            {
                _currentSettings.AuthMode = AuthMode.OwnApiKey;
                _currentSettings.GroqApiKey = passwordBox.Password;
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
            // Validate authentication based on mode
            if (_currentSettings.AuthMode == AuthMode.OwnApiKey && string.IsNullOrWhiteSpace(_currentSettings.GroqApiKey))
            {
                MessageBox.Show(
                    "Please enter your Groq API key before saving.",
                    "Groq API Key Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_currentSettings.AuthMode == AuthMode.TalkKeysAccount && string.IsNullOrWhiteSpace(_currentSettings.TalkKeysAccessToken))
            {
                MessageBox.Show(
                    "Please sign in with your TalkKeys account before saving.",
                    "Sign In Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Save trigger plugin configurations
            if (_triggerPluginManager != null)
            {
                _currentSettings.TriggerPlugins = _triggerPluginManager.GetAllConfigurations();
            }

            // Save explainer hotkey
            if (!_currentSettings.Plugins.TryGetValue("explainer", out var explainerConfig))
            {
                explainerConfig = new PluginConfiguration { PluginId = "explainer", Enabled = true };
                _currentSettings.Plugins["explainer"] = explainerConfig;
            }
            explainerConfig.SetSetting(ExplainerPlugin.SettingHotkey, _explainerHotkey);

            // Update startup registry setting
            var startWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            _startupService.SetStartupEnabled(startWithWindows);

            // Save settings to disk
            if (_settingsService.SaveSettings(_currentSettings))
            {
                _logger.Log("[Settings] Settings saved");
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

        private void AccountCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ShowAccountOptionsDialog();
        }

        private void UpdateAccountCard()
        {
            if (_currentSettings.AuthMode == AuthMode.TalkKeysAccount &&
                !string.IsNullOrEmpty(_currentSettings.TalkKeysAccessToken))
            {
                // User is signed in
                var name = _currentSettings.TalkKeysUserName ?? "TalkKeys User";
                var email = _currentSettings.TalkKeysUserEmail ?? "Free tier";

                AccountName.Text = name;
                AccountEmail.Text = email;
                AccountInitial.Text = name.Length > 0 ? name[0].ToString().ToUpper() : "T";
            }
            else
            {
                // Not signed in
                AccountName.Text = "TalkKeys Account";
                AccountEmail.Text = "Click to sign in";
                AccountInitial.Text = "T";
            }
        }

        #region Words List Management

        private void LoadWordsSettings()
        {
            // Load words from settings
            _wordsList = new ObservableCollection<string>(_currentSettings.WordsList ?? new List<string>());
            WordsList.ItemsSource = _wordsList;

            // Update analyze button text with history count
            UpdateAnalyzeButtonText();
            UpdateWordsCount();
        }

        private void UpdateWordsUI()
        {
            UpdateAnalyzeButtonText();
            UpdateWordsCount();
        }

        private void UpdateAnalyzeButtonText()
        {
            var historyCount = _historyService?.GetCount() ?? 0;
            AnalyzeButtonText.Text = historyCount > 0
                ? $"Analyze {historyCount} Transcriptions"
                : "Analyze Transcriptions";

            AnalyzeButton.IsEnabled = historyCount > 0;
        }

        private void UpdateWordsCount()
        {
            WordsCountText.Text = _wordsList.Count == 1 ? "1 word" : $"{_wordsList.Count} words";
        }

        private async void AnalyzeTranscriptions_Click(object sender, RoutedEventArgs e)
        {
            if (_historyService == null)
            {
                MessageBox.Show("Transcription history is not available.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var history = _historyService.GetHistory();
            if (history.Count == 0)
            {
                MessageBox.Show("No transcription history to analyze. Use voice-to-text first to build up history.",
                    "No History", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Disable button and show analyzing state
            AnalyzeButton.IsEnabled = false;
            AnalyzeButtonText.Text = "Analyzing...";

            try
            {
                // Create analysis service based on auth mode
                WordsAnalysisService analysisService;
                if (_currentSettings.AuthMode == AuthMode.OwnApiKey && !string.IsNullOrEmpty(_currentSettings.GroqApiKey))
                {
                    analysisService = new WordsAnalysisService(_currentSettings.GroqApiKey, _logger);
                }
                else if (_currentSettings.AuthMode == AuthMode.TalkKeysAccount && !string.IsNullOrEmpty(_currentSettings.TalkKeysAccessToken))
                {
                    // Use TalkKeys API for free tier users
                    var apiService = new TalkKeysApiService(_settingsService, _logger);
                    analysisService = new WordsAnalysisService(apiService, _logger);
                }
                else
                {
                    // Not authenticated
                    MessageBox.Show("Please sign in to use word analysis, or configure your own Groq API key.",
                        "Sign In Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = await analysisService.AnalyzeAsync(history, _wordsList.ToList());

                if (result.Success && result.SuggestedWords.Count > 0)
                {
                    _suggestions = new ObservableCollection<string>(result.SuggestedWords);
                    SuggestionsList.ItemsSource = _suggestions;
                    SuggestionsCard.Visibility = Visibility.Visible;
                }
                else if (result.Success)
                {
                    MessageBox.Show("No new word suggestions found. Your transcriptions look good!",
                        "Analysis Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Analysis failed: {result.Error}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[Words] Analysis error: {ex.Message}");
                MessageBox.Show($"Analysis failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                UpdateAnalyzeButtonText();
            }
        }

        private void AcceptSuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string word)
            {
                if (!_wordsList.Contains(word, StringComparer.OrdinalIgnoreCase))
                {
                    _wordsList.Add(word);
                    _currentSettings.WordsList = _wordsList.ToList();
                    UpdateWordsCount();
                }
                _suggestions.Remove(word);

                if (_suggestions.Count == 0)
                {
                    SuggestionsCard.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void DismissSuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string word)
            {
                _suggestions.Remove(word);

                if (_suggestions.Count == 0)
                {
                    SuggestionsCard.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void DeleteWord_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string word)
            {
                _wordsList.Remove(word);
                _currentSettings.WordsList = _wordsList.ToList();
                UpdateWordsCount();
            }
        }

        private void AddWord_Click(object sender, RoutedEventArgs e)
        {
            var dialog = CreateModalDialog("Add Word");

            var content = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };

            var textBox = new TextBox
            {
                Height = 44,
                Padding = new Thickness(12, 12, 12, 12),
                FontSize = 14,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1)
            };

            var helpLabel = new TextBlock
            {
                Text = "Enter the correct spelling of the word or phrase",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 8, 0, 0)
            };

            content.Children.Add(textBox);
            content.Children.Add(helpLabel);

            var addButton = new Button
            {
                Content = "Add",
                Height = 38,
                Margin = new Thickness(0, 20, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED")),
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            addButton.Click += (s, ev) => dialog.Close();

            content.Children.Add(addButton);

            var outerGrid = (Grid)dialog.Content;
            var border = (Border)outerGrid.Children[0];
            var dialogContent = (StackPanel)border.Child;
            dialogContent.Children.Add(content);

            dialog.ShowDialog();

            var word = textBox.Text?.Trim();
            if (!string.IsNullOrEmpty(word) && !_wordsList.Contains(word, StringComparer.OrdinalIgnoreCase))
            {
                _wordsList.Add(word);
                _currentSettings.WordsList = _wordsList.ToList();
                UpdateWordsCount();
            }
        }

        private void ImportWords_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import Words List",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var filePath = dialog.FileName;
                    var fileInfo = new FileInfo(filePath);

                    // Validate file size (max 1MB)
                    if (fileInfo.Length > 1024 * 1024)
                    {
                        MessageBox.Show("File is too large. Maximum size is 1MB.", "Import Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var content = File.ReadAllText(filePath);

                    // Check for binary content
                    if (content.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
                    {
                        MessageBox.Show("File appears to be binary. Please select a plain text file.", "Import Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(l => l.Trim())
                                       .Where(l => !string.IsNullOrEmpty(l))
                                       .Distinct(StringComparer.OrdinalIgnoreCase)
                                       .ToList();

                    if (lines.Count == 0)
                    {
                        MessageBox.Show("File is empty or contains no valid words.", "Import Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var newWords = lines.Where(w => !_wordsList.Contains(w, StringComparer.OrdinalIgnoreCase)).ToList();
                    var duplicates = lines.Count - newWords.Count;

                    var message = $"Found {lines.Count} words. {newWords.Count} are new";
                    if (duplicates > 0) message += $", {duplicates} already exist";
                    message += ".\n\nImport these words?";

                    if (MessageBox.Show(message, "Import Words",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        foreach (var word in newWords)
                        {
                            _wordsList.Add(word);
                        }
                        _currentSettings.WordsList = _wordsList.ToList();
                        UpdateWordsCount();

                        MessageBox.Show($"Imported {newWords.Count} new words.", "Import Complete",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"[Words] Import error: {ex.Message}");
                    MessageBox.Show($"Failed to import words: {ex.Message}", "Import Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportWords_Click(object sender, RoutedEventArgs e)
        {
            if (_wordsList.Count == 0)
            {
                MessageBox.Show("No words to export.", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Words List",
                Filter = "Text files (*.txt)|*.txt",
                DefaultExt = ".txt",
                FileName = "talkkeys-words.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, string.Join(Environment.NewLine, _wordsList));
                    MessageBox.Show($"Exported {_wordsList.Count} words.", "Export Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _logger.Log($"[Words] Export error: {ex.Message}");
                    MessageBox.Show($"Failed to export words: {ex.Message}", "Export Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_historyService == null) return;

            var count = _historyService.GetCount();
            if (count == 0)
            {
                MessageBox.Show("No transcription history to clear.", "Clear History",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Clear all {count} transcriptions from history?\n\nThis cannot be undone.",
                "Clear History", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _historyService.ClearHistory();
                UpdateAnalyzeButtonText();
                MessageBox.Show("Transcription history cleared.", "History Cleared",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion
    }
}
