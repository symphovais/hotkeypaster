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
                    _transcriptionService
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

            // Show success notification
            if (_transcriptionService != null)
            {
                var mode = settings.TranscriptionMode == TranscriptionMode.Local ? "Local" : "Cloud";
                _notifications.ShowInfo("Hotkey Paster Started",
                    $"Mode: {mode}. Press Ctrl+Shift+Q to record. Right-click tray icon for settings.");
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
                    // Cloud transcription
                    if (string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
                    {
                        _logger?.Log("Cloud mode selected but no API key configured");
                        return null;
                    }

                    transcriber = new OpenAIWhisperTranscriber(settings.OpenAIApiKey);
                }

                // Create text cleaner based on settings
                if (settings.EnableTextCleaning && !string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
                {
                    textCleaner = new OpenAIGPTTextCleaner(settings.OpenAIApiKey);
                }
                else
                {
                    textCleaner = new PassThroughTextCleaner();
                }

                return new OpenAITranscriptionService(transcriber, textCleaner);
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
                _notifications?.ShowInfo("Settings Applied", "Transcription settings updated successfully.");
            }
            else
            {
                _notifications?.ShowError("Settings Error", "Failed to apply new settings. Check configuration.");
            }
        }
    }
}