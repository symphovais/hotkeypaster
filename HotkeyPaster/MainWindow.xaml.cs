using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using TalkKeys.Services;
using TalkKeys.Services.Notifications;
using TalkKeys.Services.Win32;
using TalkKeys.Services.Windowing;
using TalkKeys.Services.Clipboard;
using TalkKeys.Logging;
using TalkKeys.Services.Audio;
using TalkKeys.Services.Hotkey;
using TalkKeys.Services.Pipeline;
using TalkKeys.Services.RecordingMode;

namespace TalkKeys
{
    internal static class Logger
    {
        private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TalkKeys");
        private static readonly string LogPath = Path.Combine(LogDir, "logs.txt");

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}");
            }
            catch
            {
                // Avoid throwing in logger
            }
        }
    }

    public partial class MainWindow : Window
    {
        private IntPtr _previousWindow;
        private readonly INotificationService _notifications;
        private readonly IWindowPositionService _positioner;
        private readonly IClipboardPasteService _clipboard;
        private readonly ILogger _logger;
        private readonly IAudioRecordingService _audio;
        private readonly IHotkeyService _hotkeyService;
        private IPipelineService _pipelineService;
        private readonly IActiveWindowContextService _contextService;
        private string? _currentRecordingPath;
        private IRecordingModeHandler? _currentModeHandler;
        private bool _lastRecordingHadNoAudio;
        private System.Windows.Threading.DispatcherTimer? _recordingTimer;
        private DateTime _recordingStartTime;

        // The text to paste
        private const string TEXT_TO_PASTE = "Hello from TalkKeys!";

        public MainWindow(INotificationService notifications, IWindowPositionService positioner, IClipboardPasteService clipboard, ILogger logger, IAudioRecordingService audio, IHotkeyService hotkeyService, IPipelineService pipelineService, IActiveWindowContextService contextService)
        {
            InitializeComponent();
            Logger.Log("MainWindow ctor: InitializeComponent done.");
            _notifications = notifications;
            _positioner = positioner;
            _clipboard = clipboard;
            _logger = logger;
            _audio = audio;
            _hotkeyService = hotkeyService;
            _pipelineService = pipelineService;
            _contextService = contextService;

            _audio.RecordingStarted += OnRecordingStarted;
            _audio.RecordingStopped += OnRecordingStopped;
            _audio.NoAudioDetected += OnNoAudioDetected;
            _audio.AudioLevelChanged += OnAudioLevelChanged;

            // Initialize recording timer
            _recordingTimer = new System.Windows.Threading.DispatcherTimer();
            _recordingTimer.Interval = TimeSpan.FromMilliseconds(100);
            _recordingTimer.Tick += OnRecordingTimerTick;

            // Subscribe to display settings changes to handle screen configuration changes
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            Logger.Log("Subscribed to display settings change events.");

            // Set window handle for hotkey service
            try
            {
                var helper = new WindowInteropHelper(this);
                helper.EnsureHandle();
                Logger.Log($"Window handle created: {helper.Handle}");
                _hotkeyService.SetWindowHandle(helper.Handle);
                Logger.Log("Window handle set for hotkey service.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Window handle setup failed: {ex}");
            }
            
            // Start hidden; only show on hotkey or tray action
            this.Visibility = Visibility.Hidden;
            Logger.Log("MainWindow ctor: set Visibility.Hidden and exiting ctor.");

            // Ensure position
            try
            {
                PositionWindowAtBottom();
            }
            catch (Exception ex)
            {
                Logger.Log($"Ctor PositionWindowAtBottom error: {ex}");
            }
        }

        /// <summary>
        /// Updates the pipeline service (called when settings change)
        /// </summary>
        public void UpdatePipelineService(IPipelineService newPipelineService)
        {
            _pipelineService = newPipelineService;
            Logger.Log("Pipeline service updated");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Log("Window_Loaded: positioning window.");
            try
            {
                PositionWindowAtBottom();
                Logger.Log("Window_Loaded: positioned window.");
                
                // Set focus to window so it can receive keyboard events
                this.Focus();
            }
            catch (Exception ex)
            {
                Logger.Log($"Window_Loaded error: {ex}");
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Space key stops recording and starts transcription
            if (e.Key == System.Windows.Input.Key.Space && _audio.IsRecording)
            {
                Logger.Log("Space key pressed - stopping recording");
                _audio.StopRecording();
                e.Handled = true;
            }
            // Escape key closes the window
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                Logger.Log("Escape key pressed - hiding window");
                HideWindow();
                e.Handled = true;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Logger.Log("Window_Closing: cleanup.");
            
            // Unsubscribe from display settings changes
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            // Screen configuration changed (monitor added/removed, resolution changed, etc.)
            Logger.Log("Display settings changed - repositioning window");

            // Reposition window to ensure it's visible on current screen setup
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    PositionWindowAtBottom(_previousWindow);
                    Logger.Log($"Window repositioned after display change: Left={this.Left}, Top={this.Top}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error repositioning window after display change: {ex}");
                }
            }));
        }

        private void OnRecordingStarted(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Only update UI if this window is visible (i.e., it initiated the recording)
                if (!this.IsVisible || _currentModeHandler == null)
                {
                    Logger.Log("OnRecordingStarted: Ignoring event (window not visible or no mode handler)");
                    return;
                }

                // Play system beep to alert user that recording started
                // This prevents accidental triggers from going unnoticed
                System.Media.SystemSounds.Beep.Play();

                RecordingStatus.Text = _currentModeHandler.GetModeTitle();
                SubStatus.Text = $"🎤 {_audio.DeviceName} • {_currentModeHandler.GetInstructionText()}";
                RecordingIcon.Text = _currentModeHandler.GetRecordingIcon();
                ActionButton.Content = "⏹";
                ActionButton.IsEnabled = true;

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
                    Logger.Log("OnRecordingStopped: Skipping transcription due to no audio detected");
                    return;
                }

                // Only process if this window initiated the recording
                // Check if window is visible and has a recording path
                if (!this.IsVisible || string.IsNullOrEmpty(_currentRecordingPath))
                {
                    Logger.Log("OnRecordingStopped: Ignoring event (window not visible or no recording path)");
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
                ActionButton.IsEnabled = false;

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
                    Logger.Log("OnNoAudioDetected: Ignoring event (window not visible or no recording path)");
                    return;
                }

                _lastRecordingHadNoAudio = true;

                var pulseAnimation = (Storyboard)this.Resources["PulseAnimation"];
                pulseAnimation.Stop(RecordingPulse);
                RecordingPulse.Opacity = 1;

                RecordingStatus.Text = "No audio detected";
                SubStatus.Text = "We could not hear anything. Check your microphone and try again.";
                RecordingIcon.Text = "❌";
                ActionButton.Content = "⏺";
                ActionButton.IsEnabled = true;

                if (!string.IsNullOrEmpty(_currentRecordingPath) && File.Exists(_currentRecordingPath))
                {
                    try
                    {
                        File.Delete(_currentRecordingPath);
                        Logger.Log($"Cleaned up temp recording file after no-audio detection: {_currentRecordingPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Failed to delete temp file after no-audio detection {_currentRecordingPath}: {ex.Message}");
                    }
                }

                _currentRecordingPath = null;

                _notifications.ShowError("Microphone Issue", "TalkKeys could not hear anything from your microphone. Please check your input device and try again.");
            });
        }

        private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            _audio.StopRecording();
        }

        public void ShowWindow(IRecordingModeHandler modeHandler)
        {
            if (modeHandler == null)
                throw new ArgumentNullException(nameof(modeHandler));

            try
            {
                // Set the current mode handler
                _currentModeHandler = modeHandler;

                // Capture the currently focused window so we can return focus later
                _previousWindow = Win32Helper.GetForegroundWindow();

                // IMPORTANT: Start recording FIRST to minimize audio loss at the beginning
                // The audio device needs time to initialize, so we start it before UI work
                if (!_audio.IsRecording)
                {
                    _currentRecordingPath = Path.Combine(Path.GetTempPath(), $"TalkKeys_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                    _audio.StartRecording(_currentRecordingPath);
                }

                // Position at bottom center of the screen where user is working BEFORE showing
                // This ensures the window is positioned correctly even if screen config changed
                PositionWindowAtBottom(_previousWindow);

                this.Show();
                this.Visibility = Visibility.Visible;

                // Make window visible immediately (no fade animation)
                this.Opacity = 1;

                // Get window handle
                var hwnd = new WindowInteropHelper(this).Handle;

                // Force window to foreground using Win32 APIs
                Win32Helper.ShowWindow(hwnd, Win32Helper.SW_SHOW);
                Win32Helper.SetWindowPos(hwnd, Win32Helper.HWND_TOPMOST, 0, 0, 0, 0, Win32Helper.SWP_NOMOVE | Win32Helper.SWP_NOSIZE | Win32Helper.SWP_SHOWWINDOW);
                Win32Helper.BringWindowToTop(hwnd);
                Win32Helper.SetForegroundWindow(hwnd);

                // Also try WPF methods
                this.Activate();
                this.Focus();

                Logger.Log($"ShowWindow: mode={modeHandler.GetType().Name}, positioned at Left={this.Left}, Top={this.Top}, Width={this.Width}, Height={this.Height}, Opacity={this.Opacity}, Visibility={this.Visibility}, Handle={hwnd}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in ShowWindow: {ex}");
                _notifications.ShowError("Display Error", "Could not show window. Please try again.");
            }
        }

        private void HideWindow()
        {
            this.Visibility = Visibility.Hidden;
            Logger.Log("HideWindow: set Visibility.Hidden.");

            // Return focus to previous window if available
            if (_previousWindow != IntPtr.Zero)
            {
                Win32Helper.SetForegroundWindow(_previousWindow);
                Logger.Log($"HideWindow: SetForegroundWindow({_previousWindow}).");
            }
        }

        private void PositionWindowAtBottom(IntPtr targetWindowHandle = default)
        {
            _positioner.PositionBottomCenter(this, 20, targetWindowHandle);
            Logger.Log($"PositionWindowAtBottom via service: Left={this.Left}, Top={this.Top}, TargetHandle={targetWindowHandle}.");
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
                Logger.Log($"Read audio file: {audioData.Length} bytes");

                // Get window context from the previously focused window
                var windowContext = _contextService.GetWindowContext(_previousWindow);
                if (windowContext.IsValid)
                {
                    Logger.Log($"Window context captured - Process: '{windowContext.ProcessName}'");
                }
                else
                {
                    Logger.Log("No valid window context detected");
                }

                // Create progress reporter
                var progress = new Progress<ProgressEventArgs>(e =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        SubStatus.Text = e.Message;
                        Logger.Log($"Pipeline progress: {e.Message} ({e.PercentComplete}%)");
                    });
                });

                // Execute pipeline with window context
                var result = await _pipelineService.ExecuteAsync(
                    audioData,
                    windowContext,
                    progress);

                Logger.Log($"Pipeline execution complete: Success={result.IsSuccess}, WordCount={result.WordCount}, Duration={result.Metrics.TotalDurationMs:F2}ms");

                // Log detailed metrics
                if (result.Metrics.StageMetrics.Any())
                {
                    Logger.Log("Pipeline stage metrics:");
                    foreach (var stageMetric in result.Metrics.StageMetrics)
                    {
                        Logger.Log($"  {stageMetric.StageName}: {stageMetric.DurationMs:F2}ms");

                        // Log RNNoise metrics
                        if (stageMetric.CustomMetrics.TryGetValue("NoiseReductionDB", out var noiseDb))
                        {
                            Logger.Log($"    → Noise reduced: {noiseDb:F2} dB");
                        }
                        if (stageMetric.CustomMetrics.TryGetValue("SignalChangePercent", out var signalChange))
                        {
                            Logger.Log($"    → Signal change: {signalChange:F1}%");
                        }

                        // Log VAD metrics
                        if (stageMetric.CustomMetrics.TryGetValue("SilenceRemovedSeconds", out var silenceRemoved))
                        {
                            Logger.Log($"    → Silence removed: {silenceRemoved:F2}s");
                        }
                        if (stageMetric.CustomMetrics.TryGetValue("DurationReductionPercentage", out var durationReduction))
                        {
                            Logger.Log($"    → Duration reduced: {durationReduction:F1}%");
                        }
                        if (stageMetric.CustomMetrics.TryGetValue("SpeechSegmentsDetected", out var segments))
                        {
                            Logger.Log($"    → Speech segments: {segments}");
                        }
                    }
                }

                if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Text))
                {
                    var errorMsg = result.ErrorMessage ?? "Could not transcribe audio.";
                    _notifications.ShowError("Pipeline Failed", errorMsg);
                    RecordingStatus.Text = "Failed";
                    SubStatus.Text = $"{errorMsg} - Press Esc to close";
                    RecordingIcon.Text = "❌";
                    RecordingPulse.Fill = new System.Windows.Media.RadialGradientBrush(
                        System.Windows.Media.Color.FromRgb(239, 68, 68),
                        System.Windows.Media.Color.FromRgb(220, 38, 38)
                    );
                    // Don't hide window on error - let user see the error
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

                // Hide window immediately
                HideWindow();

                // Handle transcription using mode handler
                if (_currentModeHandler != null)
                {
                    await _currentModeHandler.HandleTranscriptionAsync(result);
                    Logger.Log($"Mode handler processed result: {result.WordCount} words ({result.Language ?? "unknown"})");
                }
                else
                {
                    Logger.Log("Warning: No mode handler available to process transcription result");
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Transcription error: {ex}");
                _notifications.ShowError("Transcription Error", ex.Message);
                RecordingStatus.Text = "Error";
                SubStatus.Text = "Failed - Press Esc to close";
                RecordingIcon.Text = "❌";
                RecordingPulse.Fill = new System.Windows.Media.RadialGradientBrush(
                    System.Windows.Media.Color.FromRgb(239, 68, 68),
                    System.Windows.Media.Color.FromRgb(220, 38, 38)
                );
                // Don't hide window on error - let user see the error
            }
            finally
            {
                // Clean up temporary recording file
                if (!string.IsNullOrEmpty(tempFileToCleanup) && File.Exists(tempFileToCleanup))
                {
                    try
                    {
                        File.Delete(tempFileToCleanup);
                        Logger.Log($"Cleaned up temp recording file: {tempFileToCleanup}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Failed to delete temp file {tempFileToCleanup}: {ex.Message}");
                    }
                }
            }
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            HideWindow();
            Logger.Log("CloseButton_Click: HideWindow called.");
        }
    }
}