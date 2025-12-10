using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using TalkKeys.Logging;
using TalkKeys.PluginSdk;
using TalkKeys.Services.Auth;
using TalkKeys.Services.Settings;

namespace TalkKeys
{
    /// <summary>
    /// Welcome window shown on first launch for authentication
    /// </summary>
    public partial class WelcomeWindow : Window
    {
        private readonly TalkKeysAuthService _authService;
        private readonly SettingsService _settingsService;
        private readonly ILogger? _logger;
        private CancellationTokenSource? _loginCts;
        private string? _userName;

        /// <summary>
        /// Event raised when authentication is completed (either method)
        /// </summary>
        public event EventHandler? AuthenticationCompleted;

        /// <summary>
        /// The authentication mode that was selected/completed
        /// </summary>
        public AuthMode SelectedAuthMode { get; private set; }

        public WelcomeWindow(SettingsService settingsService, ILogger? logger = null)
        {
            InitializeComponent();

            _settingsService = settingsService;
            _logger = logger;
            _authService = new TalkKeysAuthService(settingsService, logger);
        }

        private async void GoogleSignIn_Click(object sender, RoutedEventArgs e)
        {
            _logger?.Log("[Welcome] Starting Google sign-in");

            // Show loading state
            ShowStatus("Opening browser for sign-in...");

            _loginCts = new CancellationTokenSource();

            try
            {
                var result = await _authService.LoginAsync(_loginCts.Token);

                if (result != null)
                {
                    _logger?.Log($"[Welcome] Sign-in successful: {result.Email}");
                    _userName = result.Name ?? result.Email?.Split('@')[0];
                    SelectedAuthMode = AuthMode.TalkKeysAccount;
                    ShowStep2();
                }
                else
                {
                    _logger?.Log("[Welcome] Sign-in was cancelled or failed");
                    ShowButtons();
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Welcome] Sign-in error: {ex.Message}");
                MessageBox.Show(
                    $"Sign-in failed: {ex.Message}",
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ShowButtons();
            }
            finally
            {
                _loginCts?.Dispose();
                _loginCts = null;
            }
        }

        private void OwnApiKey_Click(object sender, RoutedEventArgs e)
        {
            _logger?.Log("[Welcome] User chose to use own API key");

            // Show API key dialog
            var apiKey = ShowApiKeyDialog();

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                // Save the API key and set mode
                var settings = _settingsService.LoadSettings();
                settings.AuthMode = AuthMode.OwnApiKey;
                settings.GroqApiKey = apiKey;
                _settingsService.SaveSettings(settings);

                _logger?.Log("[Welcome] API key saved");
                _userName = null; // No user name for API key mode
                SelectedAuthMode = AuthMode.OwnApiKey;
                ShowStep2();
            }
        }

        private string? ShowApiKeyDialog()
        {
            var dialog = new Window
            {
                Title = "Enter Groq API Key",
                Width = 400,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent
            };

            var border = new System.Windows.Controls.Border
            {
                CornerRadius = new CornerRadius(12),
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24)
            };

            var stack = new System.Windows.Controls.StackPanel();

            var title = new System.Windows.Controls.TextBlock
            {
                Text = "Enter your Groq API Key",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 16)
            };

            var passwordBox = new System.Windows.Controls.PasswordBox
            {
                Height = 40,
                FontSize = 14,
                Padding = new Thickness(10, 10, 10, 10)
            };

            var helpText = new System.Windows.Controls.TextBlock
            {
                Text = "Get your free key from console.groq.com",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 8, 0, 16)
            };

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelBtn = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 0)
            };
            cancelBtn.Click += (s, e) => dialog.DialogResult = false;

            var saveBtn = new System.Windows.Controls.Button
            {
                Content = "Save",
                Width = 80,
                Height = 36,
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7C3AED")),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            saveBtn.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(passwordBox.Password))
                {
                    MessageBox.Show("Please enter an API key.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                dialog.DialogResult = true;
            };

            buttonPanel.Children.Add(cancelBtn);
            buttonPanel.Children.Add(saveBtn);

            stack.Children.Add(title);
            stack.Children.Add(passwordBox);
            stack.Children.Add(helpText);
            stack.Children.Add(buttonPanel);

            border.Child = stack;
            dialog.Content = border;

            if (dialog.ShowDialog() == true)
            {
                return passwordBox.Password;
            }

            return null;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _logger?.Log("[Welcome] User cancelled sign-in");
            _loginCts?.Cancel();
            _authService.CancelLogin();
            ShowButtons();
        }

        private void GetStarted_Click(object sender, RoutedEventArgs e)
        {
            _logger?.Log("[Welcome] User clicked Get Started");
            AuthenticationCompleted?.Invoke(this, EventArgs.Empty);
            DialogResult = true;
            Close();
        }

        private void ShowStatus(string message)
        {
            Step1Content.Visibility = Visibility.Collapsed;
            StatusPanel.Visibility = Visibility.Visible;
            StatusText.Text = message;
        }

        private void ShowButtons()
        {
            StatusPanel.Visibility = Visibility.Collapsed;
            Step1Content.Visibility = Visibility.Visible;
        }

        private void ShowStep2()
        {
            _logger?.Log("[Welcome] Showing setup complete screen");

            // Update step indicators
            Step1Indicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
            Step1Label.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));

            Step2Indicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED"));
            Step2Number.Foreground = Brushes.White;
            Step2Label.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED"));

            // Hide step 1 content
            Step1Content.Visibility = Visibility.Collapsed;
            StatusPanel.Visibility = Visibility.Collapsed;
            HeaderPanel.Visibility = Visibility.Collapsed;

            // Update footer
            FooterText.Text = "You can change settings anytime from the tray icon.";

            // Update welcome text with user name if available
            if (!string.IsNullOrEmpty(_userName))
            {
                WelcomeUserText.Text = $"Welcome, {_userName}!";
            }
            else
            {
                WelcomeUserText.Text = "You're all set!";
            }

            // Load and display the current hotkey
            LoadHotkeyInfo();

            // Show step 2 content
            Step2Content.Visibility = Visibility.Visible;

            // Run fade-in animation
            var fadeIn = (System.Windows.Media.Animation.Storyboard)FindResource("FadeIn");
            fadeIn.Begin(Step2Content);
        }

        private void LoadHotkeyInfo()
        {
            try
            {
                var (hotkey, isPushToTalk) = GetCurrentHotkeySettings();

                // Format hotkey for display (add spaces around +)
                var displayHotkey = hotkey.Replace("+", " + ");
                HotkeyDisplay.Text = displayHotkey;

                // Update mode text
                if (isPushToTalk)
                {
                    HotkeyModeText.Text = "Hold to record, release to stop";
                }
                else
                {
                    HotkeyModeText.Text = "Press to start, press again to stop";
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Welcome] Error loading hotkey info: {ex.Message}");
                // Keep defaults from XAML
            }
        }

        private (string hotkey, bool isPushToTalk) GetCurrentHotkeySettings()
        {
            const string defaultHotkey = "Ctrl+Shift+Space";
            bool isPushToTalk = false;

            try
            {
                var settings = _settingsService.LoadSettings();

                if (settings.TriggerPlugins.TryGetValue("keyboard", out var keyboardConfig))
                {
                    var trigger = keyboardConfig.Triggers.Find(t => t.TriggerId == "keyboard:hotkey");
                    if (trigger != null)
                    {
                        isPushToTalk = trigger.Action == RecordingTriggerAction.PushToTalk;

                        if (trigger.Settings.TryGetValue("Hotkey", out var hotkeyObj))
                        {
                            if (hotkeyObj is string hotkeyStr && !string.IsNullOrEmpty(hotkeyStr))
                            {
                                return (hotkeyStr, isPushToTalk);
                            }
                            // Handle JsonElement for deserialized settings
                            if (hotkeyObj is System.Text.Json.JsonElement jsonElement &&
                                jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var value = jsonElement.GetString();
                                if (!string.IsNullOrEmpty(value))
                                {
                                    return (value, isPushToTalk);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Welcome] Error reading hotkey settings: {ex.Message}");
            }

            return (defaultHotkey, isPushToTalk);
        }

        protected override void OnClosed(EventArgs e)
        {
            _loginCts?.Cancel();
            _loginCts?.Dispose();
            _authService.Dispose();
            base.OnClosed(e);
        }
    }
}
