using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TalkKeys.Logging;
using TalkKeys.Services.Notifications;
using TalkKeys.Services.Windowing;
using TalkKeys.Services.Clipboard;
using TalkKeys.Services.Audio;
using TalkKeys.Services.Hotkey;
using TalkKeys.Services.Tray;
using TalkKeys.Services.Pipeline;
using TalkKeys.Services.Pipeline.Configuration;
using TalkKeys.Services.Pipeline.Stages;
using TalkKeys.Services.Settings;
using TalkKeys.Services.RecordingMode;

namespace TalkKeys
{
    public partial class App : Application
    {
        private const string MutexName = "Global\\TalkKeys_SingleInstance_Mutex";
        private Mutex? _instanceMutex;

        private IPipelineService? _pipelineService;
        private SettingsService? _settingsService;
        private ILogger? _logger;
        private INotificationService? _notifications;
        private IAudioRecordingService? _audioService;
        private IHotkeyService? _hotkeyService;
        private ITrayService? _trayService;
        private FloatingWidget? _floatingWidget; // Changed from MainWindow
        private WindowPositionService? _positioner;
        private ClipboardPasteService? _clipboardService;
        private ActiveWindowContextService? _contextService;
        private bool _mutexOwned = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check for existing instance
            bool createdNew;
            _instanceMutex = new Mutex(true, MutexName, out createdNew);
            _mutexOwned = createdNew;

            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show(
                    "TalkKeys is already running.\n\nCheck the system tray for the TalkKeys icon.",
                    "TalkKeys Already Running",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Dispose mutex (we don't own it, so don't release)
                _instanceMutex.Dispose();
                _instanceMutex = null;
                Current.Shutdown();
                return;
            }

            // Create services
            _logger = new FileLogger();
            _notifications = new ToastNotificationService();

            // Register global exception handlers
            RegisterGlobalExceptionHandlers();
            _positioner = new WindowPositionService(_logger);
            _clipboardService = new ClipboardPasteService();
            _audioService = new AudioRecordingService(_logger);
            _hotkeyService = new Win32HotkeyService();
            _trayService = new TrayService();
            _contextService = new ActiveWindowContextService();
            _settingsService = new SettingsService();

            // Load settings and configure pipeline service
            var settings = _settingsService.LoadSettings();

            // Check if API key is configured
            if (string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
            {
                _notifications.ShowError("API Key Required",
                    "No OpenAI API key configured. Right-click the tray icon and select Settings to add your API key.");
                _logger.Log("App started without API key - user needs to configure settings");
            }
            else
            {
                _pipelineService = CreatePipelineService(settings);
                if (_pipelineService == null)
                {
                    _notifications.ShowError("Configuration Error",
                        "Failed to initialize transcription pipeline. Check logs for details.");
                    _logger.Log("App started with invalid configuration");
                }
            }

            // Initialize tray
            _trayService.InitializeTray();

            // Always create FloatingWidget (it will show "API Key Required" message if not configured)
            CreateFloatingWidget();

            // Register hotkeys AFTER widget is created
            try
            {
                _hotkeyService.RegisterHotkey("clipboard", System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Alt, System.Windows.Forms.Keys.Q);
                _logger.Log("Registered hotkey: Ctrl+Alt+Q");
            }
            catch (Exception ex)
            {
                _logger.Log($"Hotkey registration failed: {ex}");
                _notifications.ShowError("Hotkey Registration Failed", $"Could not register hotkeys: {ex.Message}");
            }

            // Wire hotkey event
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;

            // Wire tray events
            _trayService.SettingsRequested += (s, args) =>
            {
                var settingsWindow = new SettingsWindow(_settingsService, _logger, _audioService);
                settingsWindow.SettingsChanged += OnSettingsChanged;
                settingsWindow.Show();
            };

            _trayService.ExitRequested += (s, args) =>
            {
                _hotkeyService.UnregisterAllHotkeys();
                _trayService.DisposeTray();
                Current.Shutdown();
            };

            // Log startup
            if (_pipelineService != null)
            {
                var pipelineName = _pipelineService.GetDefaultPipelineName() ?? "unknown";
                _logger.Log($"TalkKeys started with pipeline: {pipelineName}");
            }
            else
            {
                _logger.Log("TalkKeys started without pipeline - waiting for API key configuration");
            }
        }

        private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs args)
        {
            // Check if pipeline is available
            if (_pipelineService == null || _floatingWidget == null)
            {
                _notifications?.ShowError("API Key Required",
                    "No OpenAI API key configured. Right-click the tray icon and select Settings to add your API key.");
                return;
            }

            if (args.HotkeyId == "clipboard")
            {
                // Show widget if hidden
                if (!_floatingWidget.IsVisible)
                {
                    _floatingWidget.Show();
                }

                // Start recording with clipboard mode
                var clipboardHandler = new ClipboardModeHandler(_clipboardService!, _logger!);
                _floatingWidget.StartRecording(clipboardHandler);
            }
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            ReloadPipelineService();
        }

        private void CreateFloatingWidget()
        {
            if (_floatingWidget != null) return;

            _floatingWidget = new FloatingWidget(
                _logger!,
                _audioService!,
                _pipelineService, // Can be null - widget will show "API Key Required"
                _notifications!,
                _contextService!,
                _settingsService!,
                _clipboardService!
            );

            // Subscribe to widget closed event
            _floatingWidget.WidgetClosed += (s, e) =>
            {
                _logger?.Log("FloatingWidget closed to tray");
            };

            // Initialize window handle for hotkeys
            _floatingWidget.InitializeForHotkeys(_hotkeyService!);

            // Load position from settings
            var settings = _settingsService!.LoadSettings();
            double? x = settings.FloatingWidgetX >= 0 ? settings.FloatingWidgetX : null;
            double? y = settings.FloatingWidgetY >= 0 ? settings.FloatingWidgetY : null;

            _floatingWidget.PositionWidget(x, y);

            // Always show widget on startup
            _floatingWidget.Show();
            _logger?.Log("FloatingWidget shown on startup");
        }

        private IPipelineService? CreatePipelineService(AppSettings settings)
        {
            // Don't try to create pipeline without API key
            if (string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
            {
                _logger?.Log("Cannot create pipeline service: No API key configured");
                return null;
            }

            try
            {
                // Get configuration directory
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TalkKeys");
                var pipelineConfigDir = Path.Combine(appDataDir, "Pipelines");

                // Create pipeline factory
                var factory = new PipelineFactory(_logger);

                // Register stage factories (only the ones we need)
                factory.RegisterStageFactory(new AudioValidationStageFactory());
                factory.RegisterStageFactory(new SileroVADStageFactory());
                factory.RegisterStageFactory(new OpenAIWhisperTranscriptionStageFactory());
                factory.RegisterStageFactory(new GPTTextCleaningStageFactory());

                // Create configuration loader
                var configLoader = new PipelineConfigurationLoader(pipelineConfigDir, _logger);

                // Ensure default configuration exists
                configLoader.EnsureDefaultConfigurations(settings.OpenAIApiKey);

                // Create build context
                var buildContext = new PipelineBuildContext
                {
                    OpenAIApiKey = settings.OpenAIApiKey,
                    Logger = _logger
                };

                // Create registry
                var registry = new PipelineRegistry(factory, configLoader, buildContext, _logger);
                registry.LoadConfigurations();

                // Set default pipeline
                var availablePipelines = registry.GetAvailablePipelineNames().ToList();
                if (availablePipelines.Any())
                {
                    registry.SetDefaultPipeline(availablePipelines.First());
                }

                // Create and return pipeline service
                return new PipelineService(registry, _logger);
            }
            catch (Exception ex)
            {
                _logger?.Log($"Failed to create pipeline service: {ex}");
                return null;
            }
        }

        private void ReloadPipelineService()
        {
            if (_settingsService == null) return;

            var settings = _settingsService.LoadSettings();

            // Check if API key is now configured
            if (string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
            {
                _logger?.Log("Settings saved but no API key configured");
                return;
            }

            var newService = CreatePipelineService(settings);

            if (newService != null)
            {
                _pipelineService = newService;
                _logger?.Log($"Pipeline service reloaded with pipeline: {newService.GetDefaultPipelineName()}");

                // Update the floating widget with the new pipeline service
                if (_floatingWidget != null)
                {
                    _floatingWidget.UpdatePipelineService(newService);
                }
            }
            else
            {
                var errorMsg = "Failed to reload pipeline configurations. Check logs for details.";
                _notifications?.ShowError("Settings Error", errorMsg);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Dispose services
                (_hotkeyService as IDisposable)?.Dispose();
                (_trayService as IDisposable)?.Dispose();
                (_audioService as IDisposable)?.Dispose();
                (_pipelineService as IDisposable)?.Dispose();

                // Release mutex only if we own it
                if (_mutexOwned && _instanceMutex != null)
                {
                    _instanceMutex.ReleaseMutex();
                }
                _instanceMutex?.Dispose();
                _instanceMutex = null;
            }
            catch (Exception ex)
            {
                _logger?.Log($"Error during application exit: {ex.Message}");
            }

            base.OnExit(e);
        }

        private void RegisterGlobalExceptionHandlers()
        {
            // WPF UI thread exceptions
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Task exceptions (async/await)
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // Non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.Log($"[CRITICAL] Unhandled UI exception: {e.Exception}");
            _logger?.Log($"Stack trace: {e.Exception.StackTrace}");

            _notifications?.ShowError("Application Error",
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will continue running, but some features may not work correctly.");

            // Mark as handled to prevent crash
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger?.Log($"[CRITICAL] Unobserved task exception: {e.Exception}");

            foreach (var ex in e.Exception.InnerExceptions)
            {
                _logger?.Log($"  Inner exception: {ex.Message}");
                _logger?.Log($"  Stack trace: {ex.StackTrace}");
            }

            // Mark as observed to prevent crash
            e.SetObserved();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            _logger?.Log($"[CRITICAL] Unhandled domain exception: {exception?.Message ?? "Unknown"}");
            _logger?.Log($"Stack trace: {exception?.StackTrace ?? "N/A"}");
            _logger?.Log($"Is terminating: {e.IsTerminating}");

            if (e.IsTerminating)
            {
                MessageBox.Show(
                    $"A fatal error occurred and the application must close:\n\n{exception?.Message ?? "Unknown error"}",
                    "Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
