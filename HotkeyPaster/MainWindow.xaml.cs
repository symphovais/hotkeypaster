using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Animation;
using HotkeyPaster.Services.Notifications;
using HotkeyPaster.Services.Windowing;
using HotkeyPaster.Services.Clipboard;
using HotkeyPaster.Logging;
using HotkeyPaster.Services.Audio;
using HotkeyPaster.Services.Hotkey;
using HotkeyPaster.Services.Transcription;

namespace HotkeyPaster
{
    internal static class Logger
    {
        private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HotkeyPaster");
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
        // Windows API imports for focus management
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private IntPtr _previousWindow;
        private readonly INotificationService _notifications;
        private readonly IWindowPositionService _positioner;
        private readonly IClipboardPasteService _clipboard;
        private readonly ILogger _logger;
        private readonly IAudioRecordingService _audio;
        private readonly IHotkeyService _hotkeyService;
        private readonly IAudioTranscriptionService _transcription;
        private string? _currentRecordingPath;

        // The text to paste
        private const string TEXT_TO_PASTE = "Hello from the hotkey paster!";

        public MainWindow(INotificationService notifications, IWindowPositionService positioner, IClipboardPasteService clipboard, ILogger logger, IAudioRecordingService audio, IHotkeyService hotkeyService, IAudioTranscriptionService transcription)
        {
            InitializeComponent();
            Logger.Log("MainWindow ctor: InitializeComponent done.");
            _notifications = notifications;
            _positioner = positioner;
            _clipboard = clipboard;
            _logger = logger;
            _audio = audio;
            _hotkeyService = hotkeyService;
            _transcription = transcription;

            _audio.RecordingStarted += OnRecordingStarted;
            _audio.RecordingStopped += OnRecordingStopped;

            // Register hotkey
            try
            {
                var helper = new WindowInteropHelper(this);
                helper.EnsureHandle();
                Logger.Log($"Window handle created: {helper.Handle}");
                _hotkeyService.SetWindowHandle(helper.Handle);
                _hotkeyService.RegisterHotkey();
                Logger.Log("Hotkey registered successfully.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Hotkey registration failed: {ex}");
                _notifications.ShowError("Hotkey Registration Failed", $"Could not register Ctrl+Shift+Q: {ex.Message}");
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
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Logger.Log("Window_Closing: cleanup.");
        }

        private void OnRecordingStarted(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                RecordingStatus.Text = "Recording...";
                SubStatus.Text = "Press Space to finish";
                RecordingIcon.Text = "🎙️";
                ActionButton.Content = "⏹";
                ActionButton.IsEnabled = true;
                
                // Start pulse animation
                var pulseAnimation = (Storyboard)this.Resources["PulseAnimation"];
                pulseAnimation.Begin(RecordingPulse);
            });
        }

        private async void OnRecordingStopped(object? sender, EventArgs e)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                // Stop pulse animation
                var pulseAnimation = (Storyboard)this.Resources["PulseAnimation"];
                pulseAnimation.Stop(RecordingPulse);
                RecordingPulse.Opacity = 1;
                
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

        private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            _audio.StopRecording();
        }

        public void ShowWindow()
        {
            // Capture the currently focused window so we can return focus later
            _previousWindow = GetForegroundWindow();
            
            this.Show();
            this.Visibility = Visibility.Visible;
            this.Opacity = 0;
            PositionWindowAtBottom();
            
            // Force to front reliably
            this.Topmost = false;
            this.Activate();
            this.Topmost = true;
            
            // Set focus so keyboard events work
            this.Focus();
            
            // Fade in animation
            var fadeIn = (Storyboard)this.Resources["FadeIn"];
            fadeIn.Begin(this);
            
            Logger.Log("ShowWindow: show+visible, positioned, brought to front.");

            // Start recording if not already
            if (!_audio.IsRecording)
            {
                _currentRecordingPath = Path.Combine(Path.GetTempPath(), $"HotkeyPaster_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                _audio.StartRecording(_currentRecordingPath);
            }
        }

        private void HideWindow()
        {
            this.Visibility = Visibility.Hidden;
            Logger.Log("HideWindow: set Visibility.Hidden.");
            
            // Return focus to previous window if available
            if (_previousWindow != IntPtr.Zero)
            {
                SetForegroundWindow(_previousWindow);
                Logger.Log($"HideWindow: SetForegroundWindow({_previousWindow}).");
            }
        }

        private void PositionWindowAtBottom()
        {
            _positioner.PositionBottomCenter(this, 20);
            Logger.Log($"PositionWindowAtBottom via service: Left={this.Left}, Top={this.Top}.");
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

            try
            {
                // Read audio file
                byte[] audioData = await File.ReadAllBytesAsync(_currentRecordingPath);
                Logger.Log($"Read audio file: {audioData.Length} bytes");

                // Transcribe with real-time progress updates
                var result = await _transcription.TranscribeStreamingAsync(audioData, partialText =>
                {
                    // Update UI with partial text as it arrives
                    Dispatcher.Invoke(() =>
                    {
                        int wordCount = partialText.Split(new[] { ' ', '\n', '\r', '\t' }, 
                            StringSplitOptions.RemoveEmptyEntries).Length;
                        SubStatus.Text = $"Processing... {wordCount} words";
                    });
                });
                
                Logger.Log($"Transcription complete: {result.WordCount} words, {result.DurationSeconds:F2}s");

                if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Text))
                {
                    _notifications.ShowError("Transcription Failed", "Could not transcribe audio.");
                    RecordingStatus.Text = "Failed";
                    SubStatus.Text = "Transcription error";
                    RecordingIcon.Text = "❌";
                    RecordingPulse.Fill = new System.Windows.Media.RadialGradientBrush(
                        System.Windows.Media.Color.FromRgb(239, 68, 68),
                        System.Windows.Media.Color.FromRgb(220, 38, 38)
                    );
                    return;
                }

                // Show success state briefly
                RecordingStatus.Text = "Complete!";
                SubStatus.Text = $"{result.WordCount} words";
                RecordingIcon.Text = "✓";
                RecordingPulse.Fill = new System.Windows.Media.RadialGradientBrush(
                    System.Windows.Media.Color.FromRgb(34, 197, 94),
                    System.Windows.Media.Color.FromRgb(22, 163, 74)
                );

                // Wait a moment to show success
                await System.Threading.Tasks.Task.Delay(600);

                // Hide window
                HideWindow();

                // Small delay to let window hide
                await System.Threading.Tasks.Task.Delay(100);

                // Paste transcribed text
                _clipboard.PasteText(result.Text);
                Logger.Log("Transcribed text pasted successfully.");

                // Show success notification
                _notifications.ShowInfo("Transcription Complete", $"Pasted {result.WordCount} words ({result.Language ?? "unknown"})");
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
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            HideWindow();
            Logger.Log("CloseButton_Click: HideWindow called.");
        }
    }
}