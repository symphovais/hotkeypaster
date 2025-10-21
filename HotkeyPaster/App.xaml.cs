using System;
using System.Threading.Tasks;
using System.Windows;
using HotkeyPaster.Logging;
using HotkeyPaster.Services.Notifications;
using HotkeyPaster.Services.Windowing;
using HotkeyPaster.Services.Clipboard;
using HotkeyPaster.Services.Audio;
using HotkeyPaster.Services.Hotkey;
using HotkeyPaster.Services.Tray;
using HotkeyPaster.Services.Transcription;
using HotkeyPaster.Services.Settings;
using Whisper.net.Ggml;

namespace HotkeyPaster
{
    public partial class App : Application
    {
        private IAudioTranscriptionService? _transcriptionService;
        private SettingsService? _settingsService;
        private ILogger? _logger;
        private INotificationService? _notifications;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create services
            _logger = new FileLogger();
            _notifications = new ToastNotificationService();
            var positioner = new WindowPositionService();
            var clipboardService = new ClipboardPasteService();
            var audioService = new AudioRecordingService();
            var hotkeyService = new Win32HotkeyService();
            var trayService = new TrayService();
            var contextService = new ActiveWindowContextService();
            _settingsService = new SettingsService();

            // Load settings and configure transcription service
            var settings = _settingsService.LoadSettings();
            _transcriptionService = CreateTranscriptionService(settings);

            if (_transcriptionService == null)
            {
                _notifications.ShowError("Configuration Required",
                    "Please configure transcription settings. Right-click the tray icon and select Settings.");
                _logger.Log("App started with invalid configuration - user needs to configure settings");
            }

            // Initialize tray
            trayService.InitializeTray();

            // Create MainWindow and inject services (only if transcription service is available)
            MainWindow? mainWindow = null;
            
            if (_transcriptionService != null)
            {
                mainWindow = new MainWindow(
                    _notifications,
                    positioner,
                    clipboardService,
                    _logger,
                    audioService,
                    hotkeyService,
                    _transcriptionService,
                    contextService
                );

                // Wire hotkey event
                hotkeyService.HotkeyPressed += (s, args) =>
                {
                    mainWindow.ShowWindow();
                };
            }
            else
            {
                // If no transcription service, hotkey opens settings
                hotkeyService.HotkeyPressed += (s, args) =>
                {
                    var settingsWindow = new SettingsWindow(_settingsService, audioService, _logger);
                    settingsWindow.SettingsChanged += (sender, eventArgs) => 
                    {
                        ReloadTranscriptionService();
                        // TODO: Create MainWindow after successful config
                    };
                    settingsWindow.Show();
                };
            }

            // Wire tray events
            trayService.SettingsRequested += (s, args) =>
            {
                var settingsWindow = new SettingsWindow(_settingsService, audioService, _logger);
                settingsWindow.SettingsChanged += (sender, eventArgs) => ReloadTranscriptionService();
                settingsWindow.Show();
            };

            trayService.ExitRequested += (s, args) =>
            {
                hotkeyService.UnregisterHotkey();
                trayService.DisposeTray();
                Current.Shutdown();
            };

            // Log startup (no notification needed)
            if (_transcriptionService != null)
            {
                var mode = settings.TranscriptionMode == TranscriptionMode.Local ? "Local" : "Cloud";
                _logger.Log($"Hotkey Paster started in {mode} mode");
            }
        }

        private IAudioTranscriptionService? CreateTranscriptionService(AppSettings settings)
        {
            try
            {
                ITranscriber transcriber;
                ITextCleaner textCleaner;

                // Create transcriber based on mode
                if (settings.TranscriptionMode == TranscriptionMode.Local)
                {
                    // Local transcription
                    if (string.IsNullOrEmpty(settings.LocalModelPath) || !System.IO.File.Exists(settings.LocalModelPath))
                    {
                        _logger?.Log("Local mode selected but no valid model path configured");
                        return null;
                    }

                    transcriber = new LocalWhisperTranscriber(settings.LocalModelPath);
                }
                else
                {
                    // Cloud transcription (optimized Whisper API)
                    if (string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
                    {
                        _logger?.Log("Cloud mode selected but no API key configured");
                        return null;
                    }

                    transcriber = new OpenAIWhisperTranscriber(settings.OpenAIApiKey);
                }

                // Create text cleaner based on settings (uses optimized parser)
                if (settings.EnableTextCleaning && !string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
                {
                    textCleaner = new OpenAIGPTTextCleaner(settings.OpenAIApiKey);
                }
                else
                {
                    textCleaner = new PassThroughTextCleaner();
                }

                // Use optimized transcription service with improved word counting and duration calculation
                return new OptimizedTranscriptionService(transcriber, textCleaner);
            }
            catch (Exception ex)
            {
                _logger?.Log($"Failed to create transcription service: {ex}");
                return null;
            }
        }

        private void ReloadTranscriptionService()
        {
            if (_settingsService == null) return;

            var settings = _settingsService.LoadSettings();
            var newService = CreateTranscriptionService(settings);

            if (newService != null)
            {
                _transcriptionService = newService;
                _logger?.Log("Transcription service reloaded with new settings");
                // No success notification needed - settings window already provides feedback
            }
            else
            {
                var errorMsg = settings.TranscriptionMode == TranscriptionMode.Local
                    ? "Failed to load local model. The model file may be corrupted or in the wrong format. Check logs for details."
                    : "Failed to apply settings. Check your API key and configuration.";
                _notifications?.ShowError("Settings Error", errorMsg);
            }
        }
    }
}