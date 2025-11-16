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
        private IAudioRecordingService? _audioService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check for existing instance
            bool createdNew;
            _instanceMutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show(
                    "TalkKeys is already running.\n\nCheck the system tray for the TalkKeys icon.",
                    "TalkKeys Already Running",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Release mutex and shutdown
                _instanceMutex.Dispose();
                _instanceMutex = null;
                Current.Shutdown();
                return;
            }

            // Create services
            _logger = new FileLogger();
            _notifications = new ToastNotificationService();
            var positioner = new WindowPositionService(_logger);
            var clipboardService = new ClipboardPasteService();
            _audioService = new AudioRecordingService();
            var hotkeyService = new Win32HotkeyService();
            var trayService = new TrayService();
            var contextService = new ActiveWindowContextService();
            _settingsService = new SettingsService();

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
            trayService.InitializeTray();

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
                    hotkeyService,
                    _pipelineService,
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
                // If no pipeline service, hotkey opens settings
                hotkeyService.HotkeyPressed += (s, args) =>
                {
                    // Create factory and build context for settings window (even if pipeline service failed)
                    if (_pipelineFactory == null || _pipelineBuildContext == null)
                    {
                        _pipelineFactory = new PipelineFactory(_logger);
                        _pipelineBuildContext = new PipelineBuildContext
                        {
                            OpenAIApiKey = settings.OpenAIApiKey,
                            LocalModelPath = settings.LocalModelPath
                        };
                    }

                    var settingsWindow = new SettingsWindow(_settingsService, _audioService, _logger, _pipelineFactory, _pipelineBuildContext);
                    settingsWindow.SettingsChanged += (sender, eventArgs) =>
                    {
                        ReloadPipelineService();
                        // TODO: Create MainWindow after successful config
                    };
                    settingsWindow.Show();
                };
            }

            // Wire tray events
            trayService.SettingsRequested += (s, args) =>
            {
                // Ensure factory and build context exist
                if (_pipelineFactory == null || _pipelineBuildContext == null)
                {
                    var currentSettings = _settingsService.LoadSettings();
                    _pipelineFactory = new PipelineFactory(_logger);
                    _pipelineBuildContext = new PipelineBuildContext
                    {
                        OpenAIApiKey = currentSettings.OpenAIApiKey,
                        LocalModelPath = currentSettings.LocalModelPath
                    };
                }

                var settingsWindow = new SettingsWindow(_settingsService, _audioService, _logger, _pipelineFactory, _pipelineBuildContext);
                settingsWindow.SettingsChanged += (sender, eventArgs) => ReloadPipelineService();
                settingsWindow.Show();
            };

            trayService.ShowcaseRequested += (s, args) =>
            {
                var showcaseWindow = new ComponentShowcaseWindow();
                showcaseWindow.Show();
            };

            trayService.ExitRequested += (s, args) =>
            {
                hotkeyService.UnregisterHotkey();
                trayService.DisposeTray();
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

                // Create build context
                var buildContext = new PipelineBuildContext
                {
                    Logger = _logger,
                    AppSettings = settings,
                    OpenAIApiKey = settings.OpenAIApiKey,
                    LocalModelPath = settings.LocalModelPath
                };
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
            // Release the mutex when the application exits
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
            _instanceMutex = null;

            base.OnExit(e);
        }
    }
}