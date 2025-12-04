using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using TalkKeys.Logging;
using TalkKeys.Services.Audio;
using TalkKeys.Services.Clipboard;
using TalkKeys.Services.Hotkey;
using TalkKeys.Services.Notifications;
using TalkKeys.Services.Pipeline;
using TalkKeys.Services.RecordingMode;
using TalkKeys.Services.Settings;
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
        
        private bool _isExpanded = false;
        private string? _currentRecordingPath;
        private IRecordingModeHandler? _currentModeHandler;
        private bool _lastRecordingHadNoAudio;
        private System.Windows.Threading.DispatcherTimer? _recordingTimer;
        private DateTime _recordingStartTime;
        private IntPtr _previousWindow;
        private System.Windows.Threading.DispatcherTimer? _autoCollapseTimer;
        private int _collapseSecondsRemaining = 5;
        private double _compactPositionX;
        private double _compactPositionY;
        private bool _hasValidCompactPosition = false;

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

            // Subscribe to audio events
            _audio.RecordingStarted += OnRecordingStarted;
            _audio.RecordingStopped += OnRecordingStopped;
            _audio.NoAudioDetected += OnNoAudioDetected;
            _audio.AudioLevelChanged += OnAudioLevelChanged;

            // Initialize recording timer
            _recordingTimer = new System.Windows.Threading.DispatcherTimer();
            _recordingTimer.Interval = TimeSpan.FromMilliseconds(100);
            _recordingTimer.Tick += OnRecordingTimerTick;

            // Initialize auto-collapse timer
            _autoCollapseTimer = new System.Windows.Threading.DispatcherTimer();
            _autoCollapseTimer.Interval = TimeSpan.FromSeconds(1);
            _autoCollapseTimer.Tick += OnAutoCollapseTimerTick;

            // Subscribe to display settings changes
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            // Update compact status based on pipeline availability
            UpdateCompactStatus();

            _logger.Log("FloatingWidget initialized");
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

            // Expand widget
            ExpandWidget();

            // Start recording if not already
            if (!_audio.IsRecording)
            {
                _currentRecordingPath = Path.Combine(Path.GetTempPath(), $"TalkKeys_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                _audio.StartRecording(_currentRecordingPath);
            }
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

            // Calculate expanded dimensions
            const double expandedWidth = 320;
            const double expandedHeight = 160;

            // Hide compact panel, show expanded panel
            CompactPanel.Visibility = Visibility.Collapsed;
            ExpandedPanel.Visibility = Visibility.Visible;

            // Calculate position for expanded widget with screen bounds awareness
            var expandedPosition = CalculateExpandedPosition(expandedWidth, expandedHeight);

            // Position expanded widget
            this.Left = expandedPosition.X;
            this.Top = expandedPosition.Y;

            // Resize window
            this.Width = expandedWidth;
            this.Height = expandedHeight;

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

            // Reset panels to recording state
            RecordingPanel.Visibility = Visibility.Visible;
            SuccessPanel.Visibility = Visibility.Collapsed;

            // Show compact panel, hide expanded panel
            CompactPanel.Visibility = Visibility.Visible;
            ExpandedPanel.Visibility = Visibility.Collapsed;

            // Resize window back to compact (new card-style dimensions)
            this.Width = 180;
            this.Height = 100;

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
                    "No OpenAI API key configured. Right-click the tray icon and select Settings to add your API key.");
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
                CompactStatus.Text = "API key required";
                CompactStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red
                CompactInstruction.Text = "Click to configure";
            }
            else
            {
                CompactStatus.Text = "Ready to record";
                CompactStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(99, 102, 241)); // Indigo
                CompactInstruction.Text = "Click or Ctrl+Alt+Q";
            }
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

                // Play system beep
                System.Media.SystemSounds.Beep.Play();

                RecordingStatus.Text = _currentModeHandler.GetModeTitle();
                SubStatus.Text = $"🎤 {_audio.DeviceName} • {_currentModeHandler.GetInstructionText()}";
                RecordingIcon.Text = _currentModeHandler.GetRecordingIcon();

                // Start recording timer
                _recordingStartTime = DateTime.Now;
                RecordingTimer.Text = "0:00";
                _recordingTimer?.Start();

                // Reset audio level
                AudioLevelBar.Value = 0;

                // Show keyboard hints
                KeyboardHints.Visibility = Visibility.Visible;

                // Start pulse animation
                var pulseAnimation = (Storyboard)this.Resources["PulseAnimation"];
                pulseAnimation.Begin(RecordingPulse);
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

                // Stop pulse animation
                var pulseAnimation = (Storyboard)this.Resources["PulseAnimation"];
                pulseAnimation.Stop(RecordingPulse);
                RecordingPulse.Opacity = 1;

                // Stop recording timer
                _recordingTimer?.Stop();

                // Hide keyboard hints during transcription
                KeyboardHints.Visibility = Visibility.Collapsed;

                // Update UI for transcription
                RecordingStatus.Text = "Transcribing...";
                SubStatus.Text = "Processing with AI";
                RecordingIcon.Text = "⚡";

                // Change pulse color to purple for transcription
                RecordingPulse.Fill = new System.Windows.Media.RadialGradientBrush(
                    System.Windows.Media.Color.FromRgb(139, 92, 246),
                    System.Windows.Media.Color.FromRgb(124, 58, 237)
                );

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

                var pulseAnimation = (Storyboard)this.Resources["PulseAnimation"];
                pulseAnimation.Stop(RecordingPulse);
                RecordingPulse.Opacity = 1;

                RecordingStatus.Text = "No audio detected";
                SubStatus.Text = "Check your microphone";
                RecordingIcon.Text = "❌";

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
                _notifications.ShowError("Microphone Issue", "TalkKeys could not hear anything from your microphone. Please check your input device and try again.");
            });
        }

        private void OnRecordingTimerTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _recordingStartTime;
            RecordingTimer.Text = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
        }

        private void OnAudioLevelChanged(object? sender, AudioLevelEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AudioLevelBar.Value = e.Level;
            });
        }

        private void OnAutoCollapseTimerTick(object? sender, EventArgs e)
        {
            _collapseSecondsRemaining--;
            AutoCollapseText.Text = $"• Collapsing in {_collapseSecondsRemaining}s";
            
            if (_collapseSecondsRemaining <= 0)
            {
                _autoCollapseTimer?.Stop();
                CollapseWidget();
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(TranscribedText.Text);
                // Change button text briefly to indicate success
                CopyButton.Content = "✓ Copied!";
                
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, args) =>
                {
                    CopyButton.Content = "📋 Copy";
                    timer.Stop();
                };
                timer.Start();
            }
            catch
            {
                // Silently fail if clipboard access fails
            }
        }

        private async System.Threading.Tasks.Task TranscribeAndPaste()
        {
            if (string.IsNullOrEmpty(_currentRecordingPath) || !File.Exists(_currentRecordingPath))
            {
                _notifications.ShowError("No Recording", "No audio file found to transcribe.");
                RecordingStatus.Text = "Error";
                SubStatus.Text = "No audio found";
                RecordingIcon.Text = "❌";
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
                    _logger.Log($"Window context captured - Process: '{windowContext.ProcessName}', Title: '{windowContext.WindowTitle}'");
                }

                // Create progress reporter
                IProgress<Services.ProgressEventArgs> progress = new Progress<Services.ProgressEventArgs>(e =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        SubStatus.Text = e.Message;
                        _logger.Log($"Pipeline progress: {e.Message} ({e.PercentComplete}%)");
                    });
                });

                // Execute pipeline (should never be null here since we check in StartRecording)
                var result = await _pipelineService!.ExecuteAsync(audioData, windowContext, progress);
                _logger.Log($"Pipeline execution complete: Success={result.IsSuccess}, WordCount={result.WordCount}");

                if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Text))
                {
                    var errorMsg = result.ErrorMessage ?? "Could not transcribe audio.";
                    _notifications.ShowError("Pipeline Failed", errorMsg);
                    RecordingStatus.Text = "Failed";
                    SubStatus.Text = errorMsg;
                    RecordingIcon.Text = "❌";
                    RecordingPulse.Fill = new System.Windows.Media.RadialGradientBrush(
                        System.Windows.Media.Color.FromRgb(239, 68, 68),
                        System.Windows.Media.Color.FromRgb(220, 38, 38)
                    );
                    return;
                }

                // Show success state briefly
                RecordingStatus.Text = "Complete!";
                SubStatus.Text = _currentModeHandler?.GetSuccessMessage(result) ?? $"{result.WordCount} words";
                RecordingIcon.Text = "✓";
                RecordingPulse.Fill = new System.Windows.Media.RadialGradientBrush(
                    System.Windows.Media.Color.FromRgb(34, 197, 94),
                    System.Windows.Media.Color.FromRgb(22, 163, 74)
                );

                // Wait a moment before handling transcription
                await System.Threading.Tasks.Task.Delay(300);

                // CRITICAL: Return focus to the previous window BEFORE pasting
                // The paste command (Ctrl+V) needs to go to the target window, not this widget
                if (_previousWindow != IntPtr.Zero)
                {
                    Win32Helper.SetForegroundWindow(_previousWindow);
                    _logger.Log($"Returned focus to previous window: {_previousWindow}");
                    // Give Windows time to complete the focus switch
                    await System.Threading.Tasks.Task.Delay(150);
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

                // Small delay to let paste complete
                await System.Threading.Tasks.Task.Delay(300);

                // NOW show transcribed text in integrated success panel
                TranscribedText.Text = result.Text;
                
                // Switch to success panel
                RecordingPanel.Visibility = Visibility.Collapsed;
                SuccessPanel.Visibility = Visibility.Visible;
                
                // Start auto-collapse timer
                _collapseSecondsRemaining = 5;
                AutoCollapseText.Text = "• Collapsing in 5s";
                _autoCollapseTimer?.Start();
            }
            catch (Exception ex)
            {
                _logger.Log($"Transcription error: {ex}");
                _notifications.ShowError("Transcription Error", ex.Message);
                RecordingStatus.Text = "Error";
                SubStatus.Text = "Failed";
                RecordingIcon.Text = "❌";
                RecordingPulse.Fill = new System.Windows.Media.RadialGradientBrush(
                    System.Windows.Media.Color.FromRgb(239, 68, 68),
                    System.Windows.Media.Color.FromRgb(220, 38, 38)
                );
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
            // Space key stops recording and starts transcription
            if (e.Key == Key.Space && _audio.IsRecording && _isExpanded)
            {
                _logger.Log("Space key pressed - stopping recording");
                _audio.StopRecording();
                e.Handled = true;
            }
            // Escape key closes the widget
            else if (e.Key == Key.Escape && _isExpanded)
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

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Unsubscribe from events
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            base.OnClosing(e);
        }
    }

    // Helper class for Win32 API calls
    internal static class Win32Helper
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
