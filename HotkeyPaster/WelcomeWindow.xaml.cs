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
                    _logger?.Log("[Welcome] Sign-in successful");
                    _userName = result.Name ?? result.Email?.Split('@')[0];
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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _logger?.Log("[Welcome] User clicked close button");
            DialogResult = false;
            Close();
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
