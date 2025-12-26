using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using TalkKeys.Logging;
using TalkKeys.PluginSdk;
using TalkKeys.Services.Audio;
using TalkKeys.Services.Clipboard;
using TalkKeys.Services.Auth;
using TalkKeys.Services.Hotkey;
using TalkKeys.Services.Notifications;
using TalkKeys.Services.Pipeline;
using TalkKeys.Services.RecordingMode;
using TalkKeys.Services.Settings;
using TalkKeys.Services.Win32;
using TalkKeys.Services.Windowing;

namespace TalkKeys
{
    public partial class FloatingWidget : Window
    {
        private readonly ILogger _logger;
        private readonly IAudioRecordingService _audio;
        private IPipelineService? _pipelineService;
        private readonly INotificationService _notifications;
        private readonly IActiveWindowContextService _contextService;
        private readonly SettingsService _settingsService;
        private readonly IClipboardPasteService _clipboard;
        private readonly TalkKeysApiService _talkKeysApiService;

        private ClassificationResult? _lastClassification;
        private WindowContext? _lastWindowContext;
        private string? _lastTranscribedText;
        
        private bool _isExpanded = false;
        private string? _currentRecordingPath;
        private IRecordingModeHandler? _currentModeHandler;
        private bool _lastRecordingHadNoAudio;
        private DateTime _recordingStartTime;
        private IntPtr _previousWindow;
        private System.Windows.Threading.DispatcherTimer? _autoCollapseTimer;
        private int _collapseSecondsRemaining = 5;
        private double _compactPositionX;
        private double _compactPositionY;
        private bool _hasValidCompactPosition = false;
        private System.Windows.Threading.DispatcherTimer? _visualizerTimer;
        private Random _random = new Random();

        public event EventHandler? WidgetClosed;

        public FloatingWidget(
            ILogger logger,
            IAudioRecordingService audio,
            IPipelineService? pipelineService,
            INotificationService notifications,
            IActiveWindowContextService contextService,
            SettingsService settingsService,
            IClipboardPasteService clipboard)
        {
            InitializeComponent();

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _pipelineService = pipelineService; // Can be null - will show message when clicked
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _contextService = contextService ?? throw new ArgumentNullException(nameof(contextService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
            _talkKeysApiService = new TalkKeysApiService(_settingsService, _logger);

            // Subscribe to audio events
            _audio.RecordingStarted += OnRecordingStarted;
            _audio.RecordingStopped += OnRecordingStopped;
            _audio.NoAudioDetected += OnNoAudioDetected;
            _audio.AudioLevelChanged += OnAudioLevelChanged;
            _audio.RecordingFailed += OnRecordingFailed;

            // Note: Recording timer is now managed by WidgetRecordingIndicator control

            // Initialize auto-collapse timer
            _autoCollapseTimer = new System.Windows.Threading.DispatcherTimer();
            _autoCollapseTimer.Interval = TimeSpan.FromSeconds(1);
            _autoCollapseTimer.Tick += OnAutoCollapseTimerTick;

            // Initialize visualizer timer for ambient animation (slow, subtle movement)
            _visualizerTimer = new System.Windows.Threading.DispatcherTimer();
            _visualizerTimer.Interval = TimeSpan.FromMilliseconds(700);
            _visualizerTimer.Tick += OnVisualizerTimerTick;

            // Subscribe to display settings changes
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            // Update compact status based on pipeline availability
            UpdateCompactStatus();

            // Note: Visualizer bars are now static dots (Ellipse), no caching needed for animation
            // _visualizerBars is kept for compatibility but won't be used for animation

            // Update hotkey hints from settings
            UpdateHotkeyHints();

            // Start subtle breathing animation on mic glow ring
            this.Loaded += (s, e) =>
            {
                try
                {
                    var breathingAnimation = (Storyboard)this.Resources["BreathingAnimation"];
                    breathingAnimation.Begin(MicGlowRing, true);
                    _logger.Log("Started breathing animation on mic glow ring");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Could not start breathing animation: {ex.Message}");
                }
            };

            _logger.Log("FloatingWidget initialized");
        }

        private bool IsPostPasteSuggestionsEnabled()
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                if (!settings.ExperimentalFeaturesEnabled || !settings.PostPasteSuggestionsEnabled)
                {
                    return false;
                }

                // Classification/rewrite uses the TalkKeys backend which requires a valid TalkKeys access token.

                return !string.IsNullOrWhiteSpace(settings.TalkKeysAccessToken);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Update the hotkey hints in the UI based on current settings.
        /// Call this when settings change.
        /// </summary>
        public void UpdateHotkeyHints()
        {
            try
            {
                var (hotkey, isPushToTalk) = GetCurrentHotkeySettings();

                // Update compact panel tooltip based on mode
                if (isPushToTalk)
                {
                    CompactPanel.ToolTip = $"Hold {hotkey} to record, release to stop";
                }
                else
                {
                    CompactPanel.ToolTip = $"Click or press {hotkey} to record";
                }

                // Update expanded panel stop hint based on mode
                if (isPushToTalk)
                {
                    StopHintText.Text = "Release to stop";
                    StopHintText.ToolTip = "Release the hotkey to stop recording";
                }
                else
                {
                    StopHintText.Text = hotkey;
                    StopHintText.ToolTip = "Press again to stop recording";
                }

                _logger.Log($"Updated hotkey hints: {hotkey}, PushToTalk: {isPushToTalk}");
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to update hotkey hints: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the current hotkey string and mode from settings.
        /// </summary>
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
                        // Check mode
                        isPushToTalk = trigger.Action == RecordingTriggerAction.PushToTalk;

                        // Get hotkey
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
                _logger.Log($"Error reading hotkey settings: {ex.Message}");
            }

            return (defaultHotkey, isPushToTalk);
        }

        public void InitializeForHotkeys(IHotkeyService hotkeyService)
        {
            // Set window handle for hotkey service
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                helper.EnsureHandle();
                _logger.Log($"FloatingWidget handle created: {helper.Handle}");
                hotkeyService.SetWindowHandle(helper.Handle);
                _logger.Log("Window handle set for hotkey service");
            }
            catch (Exception ex)
            {
                _logger.Log($"Window handle setup failed: {ex}");
            }
        }

        public void PositionWidget(double? savedX, double? savedY)
        {
            if (savedX.HasValue && savedY.HasValue && IsPositionValid(savedX.Value, savedY.Value))
            {
                this.Left = savedX.Value;
                this.Top = savedY.Value;
                _logger.Log($"FloatingWidget positioned at saved location: {savedX}, {savedY}");
            }
            else
            {
                // Use WPF SystemParameters which is already DPI-aware
                var workingArea = SystemParameters.WorkArea;
                this.Left = workingArea.Right - this.Width - 20;
                this.Top = workingArea.Top + 20;
                _logger.Log($"FloatingWidget positioned in top-right: {this.Left}, {this.Top}");
            }

            // Store this as the compact position
            _compactPositionX = this.Left;
            _compactPositionY = this.Top;
            _hasValidCompactPosition = true;
            _logger.Log($"Stored initial compact position: {_compactPositionX}, {_compactPositionY}");
        }

        private bool IsPositionValid(double x, double y)
        {
            // WPF coordinates - check if reasonably on screen
            // Use SystemParameters which is DPI-aware
            var virtualScreen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            // Check if position is within the virtual screen (all monitors combined)
            bool isValid = x >= virtualScreen.Left - 50 &&
                           x <= virtualScreen.Right &&
                           y >= virtualScreen.Top - 50 &&
                           y <= virtualScreen.Bottom;

            _logger.Log($"IsPositionValid({x}, {y}): VirtualScreen={virtualScreen}, Valid={isValid}");
            return isValid;
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            _logger.Log("Display settings changed - checking widget position");
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsPositionValid(this.Left, this.Top))
                {
                    PositionWidget(null, null); // Reset to default position
                }
            }));
        }

        public void StartRecording(IRecordingModeHandler modeHandler)
        {
            if (modeHandler == null)
                throw new ArgumentNullException(nameof(modeHandler));

            _currentModeHandler = modeHandler;

            // Capture the currently focused window
            _previousWindow = Win32Helper.GetForegroundWindow();

            // IMPORTANT: Start recording FIRST to minimize audio loss at the beginning
            // The audio device needs time to initialize, so we start it before UI work
            if (!_audio.IsRecording)
            {
                _currentRecordingPath = Path.Combine(Path.GetTempPath(), $"TalkKeys_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                _audio.StartRecording(_currentRecordingPath);
            }

            // Then expand widget (UI work happens while audio is already recording)
            ExpandWidget();
        }

        private void ExpandWidget()
        {
            if (_isExpanded) return;

            _logger.Log("Expanding FloatingWidget");
            _isExpanded = true;

            // Save current compact position BEFORE expansion (if not already saved)
            if (!_hasValidCompactPosition)
            {
                _compactPositionX = this.Left;
                _compactPositionY = this.Top;
                _hasValidCompactPosition = true;
            }
            _logger.Log($"Compact position for restoration: {_compactPositionX}, {_compactPositionY}");

            // Calculate expanded dimensions (minimal pill style)
            // Height needs room for panel (40) + border (1.5) + shadow blur
            const double expandedWidth = 230;
            const double expandedHeight = 48;

            // Hide compact panel, show expanded panel
            CompactPanel.Visibility = Visibility.Collapsed;
            ExpandedPanel.Visibility = Visibility.Visible;

            // Reset recording indicator to initial state
            WidgetRecordingIndicator.Reset();

            // Calculate position for expanded widget with screen bounds awareness
            var expandedPosition = CalculateExpandedPosition(expandedWidth, expandedHeight);

            // Position expanded widget
            this.Left = expandedPosition.X;
            this.Top = expandedPosition.Y;

            // Resize window
            this.Width = expandedWidth;
            this.Height = expandedHeight;

            // Play expand animation
            var expandAnimation = (Storyboard)this.Resources["ExpandAnimation"];
            expandAnimation.Begin(ExpandedPanel);

            // Ensure window is on top and focused
            this.Topmost = true;
            this.Activate();
            this.Focus();

            _logger.Log($"Expanded widget at position: {this.Left}, {this.Top}");
        }

        private Point CalculateExpandedPosition(double expandedWidth, double expandedHeight)
        {
            // Get DPI scale factor for this window
            var dpiScale = GetDpiScale();
            _logger.Log($"DPI Scale: {dpiScale}");

            // Convert WPF coordinates to screen pixels for proper screen detection
            var compactCenterXPixels = (_compactPositionX + 90) * dpiScale;
            var compactCenterYPixels = (_compactPositionY + 50) * dpiScale;

            var screen = GetScreenForPoint(compactCenterXPixels, compactCenterYPixels);

            // Convert screen bounds from pixels back to WPF units
            var workingArea = new Rect(
                screen.WorkingArea.Left / dpiScale,
                screen.WorkingArea.Top / dpiScale,
                screen.WorkingArea.Width / dpiScale,
                screen.WorkingArea.Height / dpiScale);

            _logger.Log($"Screen: {screen.DeviceName}");
            _logger.Log($"Screen WorkingArea (pixels): L={screen.WorkingArea.Left}, T={screen.WorkingArea.Top}, R={screen.WorkingArea.Right}, B={screen.WorkingArea.Bottom}");
            _logger.Log($"WorkingArea (WPF units): L={workingArea.Left}, T={workingArea.Top}, R={workingArea.Right}, B={workingArea.Bottom}");
            _logger.Log($"Compact position: {_compactPositionX}, {_compactPositionY}");

            double newX = _compactPositionX;
            double newY = _compactPositionY;

            // Check if expanded widget would go off the right edge
            if (newX + expandedWidth > workingArea.Right)
            {
                newX = workingArea.Right - expandedWidth - 10;
                _logger.Log($"Adjusted X to prevent right overflow: {newX}");
            }

            // Check if expanded widget would go off the left edge
            if (newX < workingArea.Left)
            {
                newX = workingArea.Left + 10;
                _logger.Log($"Adjusted X to prevent left overflow: {newX}");
            }

            // Check if expanded widget would go off the bottom edge
            if (newY + expandedHeight > workingArea.Bottom)
            {
                newY = workingArea.Bottom - expandedHeight - 10;
                _logger.Log($"Adjusted Y to prevent bottom overflow: {newY}");
            }

            // Check if expanded widget would go off the top edge
            if (newY < workingArea.Top)
            {
                newY = workingArea.Top + 10;
                _logger.Log($"Adjusted Y to prevent top overflow: {newY}");
            }

            _logger.Log($"Final expanded position: {newX}, {newY}");
            return new Point(newX, newY);
        }

        private double GetDpiScale()
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformToDevice.M11;
            }
            return 1.0;
        }

        private System.Windows.Forms.Screen GetScreenForPoint(double xPixels, double yPixels)
        {
            // Use pixel coordinates for Screen API
            var point = new System.Drawing.Point((int)xPixels, (int)yPixels);
            var screen = System.Windows.Forms.Screen.FromPoint(point);
            _logger.Log($"GetScreenForPoint({xPixels}, {yPixels}) -> {screen.DeviceName}");
            return screen;
        }

        private void CollapseWidget()
        {
            if (!_isExpanded) return;

            _logger.Log("Collapsing FloatingWidget");
            _isExpanded = false;

            // Stop timers
            _autoCollapseTimer?.Stop();

            // Show compact panel, hide expanded panel
            CompactPanel.Visibility = Visibility.Visible;
            ExpandedPanel.Visibility = Visibility.Collapsed;

            // Resize window back to compact (minimal pill dimensions)
            this.Width = 110;
            this.Height = 44;

            // Restore original compact position
            this.Left = _compactPositionX;
            this.Top = _compactPositionY;
            _logger.Log($"Restored compact position: {_compactPositionX}, {_compactPositionY}");

            // Save the compact position
            SavePosition();

            // Return focus to previous window
            if (_previousWindow != IntPtr.Zero)
            {
                Win32Helper.SetForegroundWindow(_previousWindow);
            }
        }

        private bool _isToastVisible = false;

        private void ShowTextView(string text)
        {
            _logger.Log($"Showing text view. IsExpanded={_isExpanded}");
            _isToastVisible = true;

            _lastTranscribedText = text;
            _lastClassification = null;
            _lastWindowContext = null;
            SuggestionsButton.Visibility = Visibility.Collapsed;
            SuggestionHintText.Text = string.Empty;
            SuggestionHintText.Visibility = Visibility.Collapsed;

            // Set transcribed text
            TranscribedText.Text = text;

            // Stop any recording-related timers
            _autoCollapseTimer?.Stop();

            // Reset recording indicator
            WidgetRecordingIndicator.Reset();

            // Hide other panels
            ExpandedPanel.Visibility = Visibility.Collapsed;
            CompactPanel.Visibility = Visibility.Collapsed;
            _isExpanded = false;

            // Show text view panel
            TextViewPanel.Visibility = Visibility.Visible;

            // Calculate dynamic height based on text length
            const double textViewWidth = 280;
            const double headerHeight = 28;     // Header row
            const double headerMargin = 10;     // Margin below header
            const double textAreaPadding = 20;  // Padding inside text area (10 each side)
            const double textMargin = 8;        // Margin below text area
            const double buttonHeight = 24;     // Copy button height
            const double gridMargin = 24;       // Grid margin (12 each side)

            // Calculate text area height based on content
            // Text area inner width: 280 - 24 (grid margin) - 20 (border padding) = 236px
            // At 13px font, ~35-40 chars per line
            const double charsPerLine = 38;
            const double lineHeight = 18;
            const double minTextAreaHeight = 36;  // ~2 lines
            const double maxTextAreaHeight = 100; // ~5-6 lines

            int lineCount = (int)Math.Ceiling(text.Length / charsPerLine);
            lineCount = Math.Max(1, lineCount); // At least 1 line
            double calculatedTextHeight = lineCount * lineHeight;
            double textAreaHeight = Math.Max(minTextAreaHeight, Math.Min(maxTextAreaHeight, calculatedTextHeight));

            // Set text area border height
            TextAreaBorder.Height = textAreaHeight + textAreaPadding;

            // Calculate total window height
            double textViewHeight = gridMargin + headerHeight + headerMargin + textAreaHeight + textAreaPadding + textMargin + buttonHeight;

            // Resize window to fit text view
            this.Width = textViewWidth;
            this.Height = textViewHeight;

            // Restore to saved compact position first
            this.Left = _compactPositionX;
            this.Top = _compactPositionY;

            // Now check screen bounds using the correct screen for this position
            var dpiScale = GetDpiScale();
            var centerXPixels = (_compactPositionX + textViewWidth / 2) * dpiScale;
            var centerYPixels = (_compactPositionY + textViewHeight / 2) * dpiScale;
            var screen = GetScreenForPoint(centerXPixels, centerYPixels);

            // Convert screen bounds to WPF units
            var workingArea = new Rect(
                screen.WorkingArea.Left / dpiScale,
                screen.WorkingArea.Top / dpiScale,
                screen.WorkingArea.Width / dpiScale,
                screen.WorkingArea.Height / dpiScale);

            // Adjust if text view would go off screen
            if (this.Left + this.Width > workingArea.Right)
            {
                this.Left = workingArea.Right - this.Width - 10;
            }
            if (this.Top + this.Height > workingArea.Bottom)
            {
                this.Top = workingArea.Bottom - this.Height - 10;
            }

            _logger.Log($"Text view position: {this.Left}, {this.Top}, size: {this.Width}x{this.Height}, text length: {text.Length}, lines: {lineCount}");

            // Start auto-collapse timer (10 seconds)
            _collapseSecondsRemaining = 10;
            AutoCollapseText.Text = "10s";
            _autoCollapseTimer?.Start();
        }

        // Keep old method name for compatibility
        private void ShowSuccessToast(string text) => ShowTextView(text);

        private void HideTextView()
        {
            if (!_isToastVisible) return;

            _logger.Log("Hiding text view");
            _isToastVisible = false;

            // Stop auto-collapse timer
            _autoCollapseTimer?.Stop();

            // Hide text view panel
            TextViewPanel.Visibility = Visibility.Collapsed;

            // Reset text area height and copy button for next use
            TextAreaBorder.Height = double.NaN; // Auto
            CopyButton.Content = "📋 Copy";
            CopyButton.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(156, 163, 175)); // Reset to gray #9CA3AF

            SuggestionsButton.Visibility = Visibility.Collapsed;
            SuggestionHintText.Text = string.Empty;
            SuggestionHintText.Visibility = Visibility.Collapsed;
            _lastClassification = null;
            _lastWindowContext = null;
            _lastTranscribedText = null;

            // Show compact panel
            CompactPanel.Visibility = Visibility.Visible;

            // Reset window to compact size (minimal pill dimensions)
            this.Width = 110;
            this.Height = 44;

            // Restore compact position
            this.Left = _compactPositionX;
            this.Top = _compactPositionY;

            _logger.Log($"Text view hidden, restored to: {_compactPositionX}, {_compactPositionY}");
        }

        // Keep old method name for compatibility
        private void HideSuccessToast() => HideTextView();

        private void CollapseToast_Click(object sender, RoutedEventArgs e)
        {
            HideSuccessToast();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging the widget
            if (!_isExpanded)
            {
                this.DragMove();
                SavePosition();
            }
        }

        private void CompactButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if pipeline is configured
            if (_pipelineService == null)
            {
                _notifications.ShowError("API Key Required",
                    "No Groq API key configured. Right-click the tray icon and select Settings to add your API key.");
                return;
            }

            // Start recording with clipboard mode
            var clipboardHandler = new ClipboardModeHandler(_clipboard, _logger);
            StartRecording(clipboardHandler);
        }

        /// <summary>
        /// Updates the pipeline service (called when API key is configured)
        /// </summary>
        public void UpdatePipelineService(IPipelineService pipelineService)
        {
            _pipelineService = pipelineService;
            UpdateCompactStatus();
            _logger.Log("FloatingWidget pipeline service updated");
        }

        private void UpdateCompactStatus()
        {
            if (_pipelineService == null)
            {
                CompactStatus.Text = "!";
                CompactStatus.Visibility = Visibility.Visible;
                CompactStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red
                // Change indicator dots to red to indicate not configured
                var redBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(239, 68, 68));
                Bar1.Fill = redBrush;
                Bar2.Fill = redBrush;
                Bar3.Fill = redBrush;
            }
            else
            {
                CompactStatus.Visibility = Visibility.Collapsed;
                // Restore indicator dots to muted gray (ready state)
                var readyBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(61, 66, 80)); // #3d4250
                Bar1.Fill = readyBrush;
                Bar2.Fill = readyBrush;
                Bar3.Fill = readyBrush;
            }
        }

        private static string ShortenDeviceName(string deviceName)
        {
            // Remove common prefixes/suffixes to keep it short
            var shortened = deviceName
                .Replace("Microphone (", "")
                .Replace("Microphone Array (", "")
                .Replace(")", "")
                .Replace(" - ", " ")
                .Trim();

            // Truncate if still too long
            return shortened.Length > 18 ? shortened[..15] + "..." : shortened;
        }

        private void CompactPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            // Show close button on hover
            var anim = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150));
            CompactCloseButton.BeginAnimation(OpacityProperty, anim);
        }

        private void CompactPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            // Hide close button
            var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
            CompactCloseButton.BeginAnimation(OpacityProperty, anim);
        }

        private void CollapseWidget_Click(object sender, RoutedEventArgs e)
        {
            // Stop recording if in progress
            if (_audio.IsRecording)
            {
                _audio.StopRecording();
            }
            
            CollapseWidget();
        }

        private void CloseWidget_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // Prevent triggering the compact button click
            
            // Hide widget (will be shown via tray or hotkey)
            this.Hide();
            WidgetClosed?.Invoke(this, EventArgs.Empty);
            
            // Save state
            var settings = _settingsService.LoadSettings();
            settings.FloatingWidgetVisible = false;
            _settingsService.SaveSettings(settings);
            
            _logger.Log("FloatingWidget closed to system tray");
        }

        private void SavePosition()
        {
            // Update stored compact position
            _compactPositionX = this.Left;
            _compactPositionY = this.Top;
            _hasValidCompactPosition = true;

            // Persist to settings
            var settings = _settingsService.LoadSettings();
            settings.FloatingWidgetX = this.Left;
            settings.FloatingWidgetY = this.Top;
            _settingsService.SaveSettings(settings);
            _logger.Log($"Saved widget position: {this.Left}, {this.Top}");
        }

        private void OnRecordingStarted(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!this.IsVisible || _currentModeHandler == null)
                {
                    _logger.Log("OnRecordingStarted: Ignoring event (widget not visible or no mode handler)");
                    return;
                }

                // Start recording indicator (handles timer, pulse animation, and level bars)
                _recordingStartTime = DateTime.Now;
                WidgetRecordingIndicator.StartRecording();
            });
        }

        private void OnRecordingFailed(object? sender, Services.Audio.RecordingFailedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _logger.Log($"OnRecordingFailed: {e.TechnicalError}");

                // Collapse widget since recording couldn't start
                if (_isExpanded)
                {
                    CollapseWidget();
                }

                // Clear mode handler since we're not recording
                _currentModeHandler = null;
                _currentRecordingPath = null;
            });
        }

        private async void OnRecordingStopped(object? sender, EventArgs e)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                if (_lastRecordingHadNoAudio)
                {
                    _lastRecordingHadNoAudio = false;
                    _logger.Log("OnRecordingStopped: Skipping transcription due to no audio detected");
                    CollapseWidget();
                    return;
                }

                if (!this.IsVisible || string.IsNullOrEmpty(_currentRecordingPath))
                {
                    _logger.Log("OnRecordingStopped: Ignoring event (widget not visible or no recording path)");
                    return;
                }

                // Show processing state (purple dot)
                WidgetRecordingIndicator.ShowProcessing();

                // Automatically start transcription
                await TranscribeAndPaste();
            });
        }

        private void OnNoAudioDetected(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!this.IsVisible || string.IsNullOrEmpty(_currentRecordingPath))
                {
                    return;
                }

                _lastRecordingHadNoAudio = true;
                _logger.Log("No audio detected - silently collapsing widget");

                // Reset recording indicator
                WidgetRecordingIndicator.Reset();

                // Clean up temp file
                if (!string.IsNullOrEmpty(_currentRecordingPath) && File.Exists(_currentRecordingPath))
                {
                    try
                    {
                        File.Delete(_currentRecordingPath);
                        _logger.Log($"Cleaned up temp recording file after no-audio detection: {_currentRecordingPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Failed to delete temp file after no-audio detection {_currentRecordingPath}: {ex.Message}");
                    }
                }

                _currentRecordingPath = null;
                // No notification - just silently collapse. User will notice nothing happened.
            });
        }

        private void OnAudioLevelChanged(object? sender, AudioLevelEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Update recording indicator level bars
                if (_isExpanded)
                {
                    WidgetRecordingIndicator.UpdateAudioLevel(e.Level);
                }
            });
        }

        private void OnAutoCollapseTimerTick(object? sender, EventArgs e)
        {
            _collapseSecondsRemaining--;
            AutoCollapseText.Text = $"{_collapseSecondsRemaining}s";

            if (_collapseSecondsRemaining <= 0)
            {
                _autoCollapseTimer?.Stop();
                if (_isToastVisible)
                {
                    HideTextView();
                }
                else
                {
                    CollapseWidget();
                }
            }
        }

        private void OnVisualizerTimerTick(object? sender, EventArgs e)
        {
            // Disabled: No animation in idle state - just show static bars
            // The widget is ready when visible, no need for ambient movement
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(TranscribedText.Text);
                // Change button text to indicate success
                // Subtle feedback - green text, no filled background
                CopyButton.Content = "✓ Copied";
                CopyButton.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(34, 197, 94)); // Green text

                // Auto-dismiss after 2 seconds
                _collapseSecondsRemaining = 2;
                AutoCollapseText.Text = "2s";

                _logger.Log("Copied transcribed text to clipboard, will dismiss in 2s");
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to copy to clipboard: {ex.Message}");
            }
        }

        private void SuggestionsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastTranscribedText == null)
                {
                    return;
                }

                var suggestedType = _lastClassification?.Type;
                var suggestedTargets = _lastClassification?.SuggestedTargets;

                var window = new RewriteWindow(
                    _settingsService,
                    _logger,
                    _lastTranscribedText,
                    _lastWindowContext,
                    suggestedType,
                    suggestedTargets);
                window.Show();
            }
            catch (Exception ex)
            {
                _logger.Log($"Failed to open rewrite window: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task TryClassifyAsync(string text, WindowContext? windowContext)
        {
            try
            {
                if (!IsPostPasteSuggestionsEnabled())
                {
                    return;
                }

                _lastWindowContext = windowContext;

                var result = await _talkKeysApiService.ClassifyTextAsync(text, windowContext);
                if (!result.Success)
                {
                    _logger.Log($"[Suggestions] Classification failed: {result.Error}");
                    return;
                }

                _lastClassification = result;

                var hasTargets = result.SuggestedTargets != null && result.SuggestedTargets.Count > 0;
                if (result.Confidence >= 0.65 && hasTargets)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (_isToastVisible)
                        {
                            var typeText = string.IsNullOrWhiteSpace(result.Type)
                                ? "other"
                                : result.Type.Trim();

                            if (!string.IsNullOrWhiteSpace(typeText))
                            {
                                typeText = char.ToUpperInvariant(typeText[0]) + typeText.Substring(1);
                            }

                            SuggestionHintText.Text = $"Looks like: {typeText}";
                            SuggestionHintText.Visibility = Visibility.Visible;
                            SuggestionsButton.Visibility = Visibility.Visible;
                            _collapseSecondsRemaining = Math.Max(_collapseSecondsRemaining, 10);
                            AutoCollapseText.Text = $"{_collapseSecondsRemaining}s";
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[Suggestions] Classification error: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task TranscribeAndPaste()
        {
            if (string.IsNullOrEmpty(_currentRecordingPath) || !File.Exists(_currentRecordingPath))
            {
                _notifications.ShowError("No Recording", "No audio file found to transcribe.");
                WidgetRecordingIndicator.ShowError();
                return;
            }

            string? tempFileToCleanup = _currentRecordingPath;
            try
            {
                // Read audio file
                byte[] audioData = await File.ReadAllBytesAsync(_currentRecordingPath);
                _logger.Log($"Read audio file: {audioData.Length} bytes");

                // Get window context from the previously focused window
                var windowContext = _contextService.GetWindowContext(_previousWindow);
                if (windowContext.IsValid)
                {
                    _logger.Log($"Window context captured - Process: '{windowContext.ProcessName}'");
                }

                // Create progress reporter (minimal - just log)
                IProgress<Services.ProgressEventArgs> progress = new Progress<Services.ProgressEventArgs>(e =>
                {
                    _logger.Log($"Pipeline progress: {e.Message} ({e.PercentComplete}%)");
                });

                // Execute pipeline (should never be null here since we check in StartRecording)
                var result = await _pipelineService!.ExecuteAsync(audioData, windowContext, progress);
                _logger.Log($"Pipeline execution complete: Success={result.IsSuccess}, WordCount={result.WordCount}");

                if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Text))
                {
                    var errorMsg = result.ErrorMessage ?? "Could not transcribe audio.";
                    _notifications.ShowError("Pipeline Failed", errorMsg);
                    WidgetRecordingIndicator.ShowError();
                    return;
                }

                // Show success state - green dot
                WidgetRecordingIndicator.ShowSuccess();

                // CRITICAL: Return focus to the previous window BEFORE pasting
                // The paste command (Ctrl+V) needs to go to the target window, not this widget
                bool focusRestored = await RestoreFocusWithRetryAsync(_previousWindow);
                if (!focusRestored)
                {
                    _logger.Log("WARNING: Could not restore focus to previous window - paste may fail");
                }

                // Handle transcription using mode handler (paste to clipboard)
                if (_currentModeHandler != null)
                {
                    await _currentModeHandler.HandleTranscriptionAsync(result);
                    _logger.Log($"Mode handler processed result: {result.WordCount} words ({result.Language ?? "unknown"})");
                }
                else
                {
                    _logger.Log("Warning: No mode handler available to process transcription result");
                }

                // Show text view with transcribed text (user can copy if paste didn't work)
                ShowTextView(result.Text);

                // Fire-and-forget classification for post-paste suggestions
                _ = TryClassifyAsync(result.Text, windowContext);
            }
            catch (Exception ex)
            {
                _logger.Log($"Transcription error: {ex}");
                _notifications.ShowError("Transcription Error", ex.Message);
                WidgetRecordingIndicator.ShowError();
            }
            finally
            {
                // Clean up temporary recording file
                if (!string.IsNullOrEmpty(tempFileToCleanup) && File.Exists(tempFileToCleanup))
                {
                    try
                    {
                        File.Delete(tempFileToCleanup);
                        _logger.Log($"Cleaned up temp recording file: {tempFileToCleanup}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Failed to delete temp file {tempFileToCleanup}: {ex.Message}");
                    }
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Escape key closes the widget or toast
            if (e.Key == Key.Escape)
            {
                if (_isToastVisible)
                {
                    _logger.Log("Escape key pressed - hiding toast");
                    HideSuccessToast();
                    e.Handled = true;
                }
                else if (_isExpanded)
                {
                    _logger.Log("Escape key pressed - collapsing widget");

                    if (_audio.IsRecording)
                    {
                        _audio.StopRecording();
                    }

                    CollapseWidget();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Attempts to restore focus to the specified window with retry logic.
        /// Returns true if focus was successfully restored.
        /// </summary>
        private async System.Threading.Tasks.Task<bool> RestoreFocusWithRetryAsync(IntPtr targetWindow)
        {
            if (targetWindow == IntPtr.Zero)
            {
                _logger.Log("RestoreFocus: No target window specified");
                return false;
            }

            const int maxRetries = 3;
            const int retryDelayMs = 50;
            const int postFocusDelayMs = 100;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                // Check if window handle is still valid
                if (!Win32Helper.IsWindow(targetWindow))
                {
                    _logger.Log($"RestoreFocus: Target window {targetWindow} is no longer valid");
                    return false;
                }

                // Attempt to set focus
                bool success = Win32Helper.SetForegroundWindow(targetWindow);

                if (success)
                {
                    // Verify the focus was actually set
                    await System.Threading.Tasks.Task.Delay(postFocusDelayMs);
                    var currentFocus = Win32Helper.GetForegroundWindow();

                    if (currentFocus == targetWindow)
                    {
                        _logger.Log($"RestoreFocus: Successfully restored focus to {targetWindow} on attempt {attempt}");
                        return true;
                    }
                    else
                    {
                        _logger.Log($"RestoreFocus: SetForegroundWindow returned true but focus is {currentFocus}, not {targetWindow}");
                    }
                }
                else
                {
                    _logger.Log($"RestoreFocus: SetForegroundWindow returned false on attempt {attempt}");
                }

                if (attempt < maxRetries)
                {
                    await System.Threading.Tasks.Task.Delay(retryDelayMs);
                }
            }

            _logger.Log($"RestoreFocus: Failed after {maxRetries} attempts");
            return false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Unsubscribe from events
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _talkKeysApiService.Dispose();
            base.OnClosing(e);
        }
    }
}
