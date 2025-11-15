using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using HotkeyPaster.Logging;
using HotkeyPaster.Services.Notifications;
using HotkeyPaster.Services.Windowing;
using HotkeyPaster.Services.Clipboard;
using HotkeyPaster.Services.Audio;
using HotkeyPaster.Services.Hotkey;
using HotkeyPaster.Services.Tray;
using HotkeyPaster.Services.Pipeline;
using HotkeyPaster.Services.Pipeline.Configuration;
using HotkeyPaster.Services.Pipeline.Stages;
using HotkeyPaster.Services.Settings;
using Whisper.net.Ggml;

namespace HotkeyPaster
{
    public partial class App : Application
    {
        private IPipelineService? _pipelineService;
        private SettingsService? _settingsService;
        private ILogger? _logger;
        private INotificationService? _notifications;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create services
            _logger = new FileLogger();
            _notifications = new ToastNotificationService();
            var positioner = new WindowPositionService(_logger);
            var clipboardService = new ClipboardPasteService();
            var audioService = new AudioRecordingService();
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
                    audioService,
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
                    var settingsWindow = new SettingsWindow(_settingsService, audioService, _logger);
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
                var settingsWindow = new SettingsWindow(_settingsService, audioService, _logger);
                settingsWindow.SettingsChanged += (sender, eventArgs) => ReloadPipelineService();
                settingsWindow.Show();
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
                _logger.Log($"Hotkey Paster started with pipeline: {pipelineName}");
            }
        }

        private IPipelineService? CreatePipelineService(AppSettings settings)
        {
            try
            {
                // Get configuration directory
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "HotkeyPaster");
                var pipelineConfigDir = Path.Combine(appDataDir, "Pipelines");

                // Create pipeline factory
                var factory = new PipelineFactory(_logger);

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

                // Create registry
                var registry = new PipelineRegistry(factory, configLoader, buildContext, _logger);
                registry.LoadConfigurations();

                // Set default pipeline based on current settings
                var availablePipelines = registry.GetAvailablePipelineNames().ToList();
                if (availablePipelines.Any())
                {
                    // Try to set default based on transcription mode
                    if (settings.TranscriptionMode == TranscriptionMode.Local && availablePipelines.Contains("LocalPrivacy"))
                    {
                        registry.SetDefaultPipeline("LocalPrivacy");
                    }
                    else if (availablePipelines.Contains("FastCloud"))
                    {
                        registry.SetDefaultPipeline("FastCloud");
                    }
                    // Otherwise use the first available
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
    }
}