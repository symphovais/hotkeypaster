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
using TalkKeys.Services.Diary;
using Whisper.net.Ggml;

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
        private PipelineFactory? _pipelineFactory;
        private PipelineBuildContext? _pipelineBuildContext;
        private PipelineBuildContextFactory? _buildContextFactory;
        private IAudioRecordingService? _audioService;
        private IHotkeyService? _hotkeyService;
        private ITrayService? _trayService;
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
            _buildContextFactory = new PipelineBuildContextFactory(_logger);
            _notifications = new ToastNotificationService();

            // Register global exception handlers
            RegisterGlobalExceptionHandlers();
            var positioner = new WindowPositionService(_logger);
            var clipboardService = new ClipboardPasteService();
            _audioService = new AudioRecordingService();
            _hotkeyService = new Win32HotkeyService();
            _trayService = new TrayService();
            var contextService = new ActiveWindowContextService();
            _settingsService = new SettingsService();
            var diaryService = new DiaryService(_logger);

            // Load settings and configure pipeline service
            var settings = _settingsService.LoadSettings();
            _pipelineService = CreatePipelineService(settings);

            if (_pipelineService == null)
            {
                _notifications.ShowError("Configuration Required",
                    "Please configure transcription settings. Right-click the tray icon and select Settings.");
                _logger.Log("App started with invalid configuration - user needs to configure settings");
            }

            // Initialize tray
            _trayService.InitializeTray();

            // Create MainWindow and inject services (only if pipeline service is available)
            MainWindow? mainWindow = null;

            if (_pipelineService != null)
            {
                mainWindow = new MainWindow(
                    _notifications,
                    positioner,
                    clipboardService,
                    _logger,
                    _audioService,
                    _hotkeyService,
                    _pipelineService,
                    contextService
                );

                // Register hotkeys
                try
                {
                    _hotkeyService.RegisterHotkey("clipboard", System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift, System.Windows.Forms.Keys.Q);
                    _logger.Log("Registered Clipboard hotkey: Ctrl+Shift+Q");

                    _hotkeyService.RegisterHotkey("diary", System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift, System.Windows.Forms.Keys.D);
                    _logger.Log("Registered Diary hotkey: Ctrl+Shift+D");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Hotkey registration failed: {ex}");
                    _notifications.ShowError("Hotkey Registration Failed", $"Could not register hotkeys: {ex.Message}");
                }

                // Wire hotkey event - route to appropriate mode handler based on hotkey ID
                _hotkeyService.HotkeyPressed += (s, args) =>
                {
                    if (args.HotkeyId == "clipboard")
                    {
                        var clipboardHandler = new ClipboardModeHandler(clipboardService, _logger);
                        mainWindow.ShowWindow(clipboardHandler);
                    }
                    else if (args.HotkeyId == "diary")
                    {
                        var diaryHandler = new DiaryModeHandler(diaryService, _logger);
                        mainWindow.ShowWindow(diaryHandler);
                    }
                };
            }
            else
            {
                // If no pipeline service, hotkey opens settings
                _hotkeyService.HotkeyPressed += (s, args) =>
                {
                    // Create factory and build context for settings window (even if pipeline service failed)
                    if (_pipelineFactory == null || _pipelineBuildContext == null)
                    {
                        _pipelineFactory = new PipelineFactory(_logger);
                        _pipelineBuildContext = _buildContextFactory!.Create(settings);
                    }

                    var settingsWindow = new SettingsWindow(_settingsService, _audioService, _logger, _pipelineFactory, _pipelineBuildContext, _buildContextFactory!);
                    settingsWindow.SettingsChanged += (sender, eventArgs) =>
                    {
                        ReloadPipelineService();
                        // TODO: Create MainWindow after successful config
                    };
                    settingsWindow.Show();
                };
            }

            // Wire tray events
            _trayService.SettingsRequested += (s, args) =>
            {
                // Ensure factory and build context exist
                if (_pipelineFactory == null || _pipelineBuildContext == null)
                {
                    var currentSettings = _settingsService.LoadSettings();
                    _pipelineFactory = new PipelineFactory(_logger);
                    _pipelineBuildContext = _buildContextFactory!.Create(currentSettings);
                }

                var settingsWindow = new SettingsWindow(_settingsService, _audioService, _logger, _pipelineFactory, _pipelineBuildContext, _buildContextFactory!);
                settingsWindow.SettingsChanged += (sender, eventArgs) => ReloadPipelineService();
                settingsWindow.Show();
            };

            _trayService.ViewDiaryRequested += (s, args) =>
            {
                var diaryViewerWindow = new DiaryViewerWindow(diaryService);
                diaryViewerWindow.Show();
            };

            _trayService.NewDiaryEntryRequested += (s, args) =>
            {
                if (mainWindow != null)
                {
                    var diaryHandler = new DiaryModeHandler(diaryService, _logger);
                    mainWindow.ShowWindow(diaryHandler);
                }
                else
                {
                    _notifications.ShowError("Configuration Required",
                        "Please configure transcription settings first. Right-click the tray icon and select Settings.");
                }
            };

            _trayService.ExitRequested += (s, args) =>
            {
                _hotkeyService.UnregisterAllHotkeys();
                _trayService.DisposeTray();
                Current.Shutdown();
            };

            // Log startup (no notification needed)
            if (_pipelineService != null)
            {
                var pipelineName = _pipelineService.GetDefaultPipelineName() ?? "unknown";
                _logger.Log($"TalkKeys started with pipeline: {pipelineName}");
            }
        }

        private IPipelineService? CreatePipelineService(AppSettings settings)
        {
            try
            {
                // Get configuration directory
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TalkKeys");
                var pipelineConfigDir = Path.Combine(appDataDir, "Pipelines");

                // Create pipeline factory
                var factory = new PipelineFactory(_logger);
                _pipelineFactory = factory; // Store for benchmark window

                // Register all stage factories
                factory.RegisterStageFactory(new AudioValidationStageFactory());
                factory.RegisterStageFactory(new RNNoiseStageFactory());
                factory.RegisterStageFactory(new SileroVADStageFactory());
                factory.RegisterStageFactory(new OpenAIWhisperTranscriptionStageFactory());
                factory.RegisterStageFactory(new LocalWhisperTranscriptionStageFactory());
                factory.RegisterStageFactory(new GPTTextCleaningStageFactory());
                factory.RegisterStageFactory(new PassThroughCleaningStageFactory());

                // Create configuration loader
                var configLoader = new PipelineConfigurationLoader(pipelineConfigDir, _logger);

                // Ensure default configurations exist
                configLoader.EnsureDefaultConfigurations(
                    settings.OpenAIApiKey ?? string.Empty,
                    settings.LocalModelPath);

                // Create build context using factory
                var buildContext = _buildContextFactory!.Create(settings);
                _pipelineBuildContext = buildContext; // Store for benchmark window

                // Create registry
                var registry = new PipelineRegistry(factory, configLoader, buildContext, _logger);
                registry.LoadConfigurations();

                // Set default pipeline based on user's selected preset
                var availablePipelines = registry.GetAvailablePipelineNames().ToList();
                if (availablePipelines.Any())
                {
                    // Map preset names to actual pipeline names
                    string? pipelineName = settings.SelectedPipeline switch
                    {
                        PipelinePresets.MaximumQuality => "CloudQuality",        // RNNoise + VAD + Cloud
                        PipelinePresets.BalancedQuality => "CloudNoiseReduction", // RNNoise + Cloud
                        PipelinePresets.FastCloud => "FastCloud",                 // Cloud only
                        PipelinePresets.MaximumPrivacy => "LocalPrivacy",        // RNNoise + VAD + Local
                        PipelinePresets.FastLocal => "FastLocal",                 // Local only
                        _ => "CloudQuality" // Default to maximum quality
                    };

                    // Set the selected pipeline if it exists, otherwise use first available
                    if (availablePipelines.Contains(pipelineName))
                    {
                        registry.SetDefaultPipeline(pipelineName);
                    }
                    else if (availablePipelines.Any())
                    {
                        // Fallback to first available if preferred doesn't exist
                        registry.SetDefaultPipeline(availablePipelines.First());
                    }
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
            var newService = CreatePipelineService(settings);

            if (newService != null)
            {
                _pipelineService = newService;
                _logger?.Log($"Pipeline service reloaded with pipeline: {newService.GetDefaultPipelineName()}");
                // No success notification needed - settings window already provides feedback
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