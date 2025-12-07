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
using TalkKeys.Services.Triggers;
using TalkKeys.Services.Updates;

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
        private FloatingWidget? _floatingWidget;
        private WindowPositionService? _positioner;
        private ClipboardPasteService? _clipboardService;
        private ActiveWindowContextService? _contextService;
        private TriggerPluginManager? _triggerPluginManager;
        private IUpdateService? _updateService;
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
            if (string.IsNullOrWhiteSpace(settings.OpenAIApiKey) &&
                string.IsNullOrWhiteSpace(settings.GroqApiKey))
            {
                _notifications.ShowError("API Key Required",
                    "No API key configured. Right-click the tray icon and select Settings to add your API key.");
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

            // Initialize trigger plugin system
            InitializeTriggerPlugins(settings);

            // Wire tray events
            _trayService.SettingsRequested += (s, args) =>
            {
                var settingsWindow = new SettingsWindow(_settingsService, _logger, _audioService, _triggerPluginManager);
                settingsWindow.SettingsChanged += OnSettingsChanged;
                settingsWindow.Show();
            };

            _trayService.ExitRequested += (s, args) =>
            {
                _triggerPluginManager?.StopAll();
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

            // Auto-select Jabra Engage 50 II as audio device if enabled
            AutoSelectJabraDeviceIfEnabled(settings);

            // Check for updates in the background
            CheckForUpdatesAsync();
        }

        private async void CheckForUpdatesAsync()
        {
            try
            {
                _updateService = new UpdateService(_logger);

                // Subscribe to update events
                _updateService.UpdateAvailable += OnUpdateAvailable;
                _updateService.UpdateDownloaded += OnUpdateDownloaded;
                _updateService.UpdateError += (s, msg) => _logger?.Log($"[Update] Error: {msg}");

                // Check for updates
                var updateInfo = await _updateService.CheckForUpdatesAsync();

                if (updateInfo?.IsUpdateAvailable == true)
                {
                    _logger?.Log($"[Update] New version available: {updateInfo.NewVersion}");

                    // Automatically download the update in the background
                    await _updateService.DownloadAndApplyUpdateAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Update] Failed to check for updates: {ex.Message}");
            }
        }

        private void OnUpdateAvailable(object? sender, UpdateInfo info)
        {
            _logger?.Log($"[Update] Update available: {info.CurrentVersion} -> {info.NewVersion}");

            // Show toast notification about the update
            Current.Dispatcher.Invoke(() =>
            {
                _notifications?.ShowInfo("Update Available",
                    $"TalkKeys {info.NewVersion} is available. It will be installed when you restart.");
            });
        }

        private void OnUpdateDownloaded(object? sender, string version)
        {
            _logger?.Log($"[Update] Update downloaded: {version}");

            // Show toast notification that update is ready
            Current.Dispatcher.Invoke(() =>
            {
                _notifications?.ShowInfo("Update Ready",
                    $"TalkKeys {version} has been downloaded. Restart to apply the update.");

                // Add "Restart to Update" option to tray menu
                _trayService?.AddUpdateMenuItem(() =>
                {
                    _updateService?.RestartAndApplyUpdate();
                });
            });
        }

        private void InitializeTriggerPlugins(AppSettings settings)
        {
            _triggerPluginManager = new TriggerPluginManager(_logger);

            // Register built-in plugins
            _triggerPluginManager.RegisterPlugin(new KeyboardTriggerPlugin(_logger));

            // Discover and load external plugins from the Plugins folder
            // (including the Jabra plugin which is now loaded as an external DLL)
            _triggerPluginManager.DiscoverExternalPlugins();

            // Subscribe to trigger events
            _triggerPluginManager.TriggerActivated += OnTriggerActivated;
            _triggerPluginManager.TriggerDeactivated += OnTriggerDeactivated;

            // Initialize with stored configurations (or defaults)
            _triggerPluginManager.Initialize(settings.TriggerPlugins);

            // Start all enabled plugins
            _triggerPluginManager.StartAll();

            _logger?.Log($"Trigger plugin system initialized. Plugins directory: {_triggerPluginManager.PluginsDirectory}");
        }

        private void OnTriggerActivated(object? sender, TriggerEventArgs e)
        {
            _logger?.Log($"Trigger activated: {e.TriggerId}, Action: {e.Action}");

            Current.Dispatcher.Invoke(() =>
            {
                switch (e.Action)
                {
                    case RecordingTriggerAction.ToggleRecording:
                        HandleToggleRecording();
                        break;

                    case RecordingTriggerAction.PushToTalk:
                        HandlePushToTalkStart();
                        break;

                    case RecordingTriggerAction.KeyboardShortcut:
                        // Keyboard shortcuts are handled by the plugin itself
                        break;

                    case RecordingTriggerAction.Disabled:
                        // Do nothing
                        break;
                }
            });
        }

        private void OnTriggerDeactivated(object? sender, TriggerEventArgs e)
        {
            _logger?.Log($"Trigger deactivated: {e.TriggerId}");

            Current.Dispatcher.Invoke(() =>
            {
                if (e.Action == RecordingTriggerAction.PushToTalk)
                {
                    HandlePushToTalkStop();
                }
            });
        }

        private void HandleToggleRecording()
        {
            if (_floatingWidget == null || _pipelineService == null)
            {
                _notifications?.ShowError("API Key Required",
                    "No API key configured. Right-click the tray icon and select Settings to add your API key.");
                return;
            }

            // Toggle recording
            if (_audioService?.IsRecording == true)
            {
                // Stop recording
                _logger?.Log("Toggle: Stopping recording");
                _audioService.StopRecording();
            }
            else
            {
                // Start recording
                _logger?.Log("Toggle: Starting recording");

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

        private void HandlePushToTalkStart()
        {
            if (_floatingWidget == null || _pipelineService == null)
            {
                _notifications?.ShowError("API Key Required",
                    "No API key configured. Right-click the tray icon and select Settings to add your API key.");
                return;
            }

            // Only start if not already recording
            if (_audioService?.IsRecording != true)
            {
                _logger?.Log("Push-to-talk: Starting recording");

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

        private void HandlePushToTalkStop()
        {
            if (_audioService?.IsRecording == true)
            {
                _logger?.Log("Push-to-talk: Stopping recording");
                _audioService.StopRecording();
            }
        }

        private void AutoSelectJabraDeviceIfEnabled(AppSettings settings)
        {
            // Check if Jabra plugin has auto-select enabled
            if (settings.TriggerPlugins.TryGetValue("jabra", out var jabraConfig))
            {
                if (jabraConfig.Settings.TryGetValue("AutoSelectAudioDevice", out var autoSelectObj)
                    && autoSelectObj is bool autoSelect && autoSelect)
                {
                    AutoSelectJabraDevice();
                    return;
                }
            }

            // Legacy check for older settings format
            if (settings.JabraAutoSelectDevice)
            {
                AutoSelectJabraDevice();
            }
        }

        private void AutoSelectJabraDevice()
        {
            if (_audioService == null) return;

            var devices = _audioService.GetAvailableDevices();
            _logger?.Log($"Available audio devices: {string.Join(", ", devices)}");

            // Look for Jabra Engage 50 II device
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].Contains("Engage 50", StringComparison.OrdinalIgnoreCase))
                {
                    _audioService.SetDevice(i);
                    _logger?.Log($"Auto-selected Jabra Engage 50 II as audio device (index {i})");
                    return;
                }
            }

            _logger?.Log("Jabra Engage 50 II not found in audio devices - using default");
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            ReloadPipelineService();

            // Reload trigger plugin configurations
            if (_triggerPluginManager != null && _settingsService != null)
            {
                var settings = _settingsService.LoadSettings();

                // Update each plugin's configuration
                foreach (var kvp in settings.TriggerPlugins)
                {
                    _triggerPluginManager.UpdatePluginConfiguration(kvp.Key, kvp.Value);
                }

                _logger?.Log("Trigger plugin configurations reloaded");
            }
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
            // Don't try to create pipeline without the required API key for the selected provider
            if (settings.TranscriptionProvider == TranscriptionProvider.OpenAI &&
                string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
            {
                _logger?.Log("Cannot create pipeline service: No OpenAI API key configured");
                return null;
            }

            if (settings.TranscriptionProvider == TranscriptionProvider.Groq &&
                string.IsNullOrWhiteSpace(settings.GroqApiKey))
            {
                _logger?.Log("Cannot create pipeline service: No Groq API key configured");
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
                factory.RegisterStageFactory(new OpenAIWhisperTranscriptionStageFactory());
                factory.RegisterStageFactory(new GroqWhisperTranscriptionStageFactory());
                factory.RegisterStageFactory(new GPTTextCleaningStageFactory());
                factory.RegisterStageFactory(new GroqTextCleaningStageFactory());

                // Create configuration loader
                var configLoader = new PipelineConfigurationLoader(pipelineConfigDir, _logger);

                // Ensure default configuration exists based on selected provider
                configLoader.EnsureDefaultConfigurations(
                    settings.OpenAIApiKey,
                    settings.GroqApiKey,
                    settings.TranscriptionProvider);

                // Create build context
                var buildContext = new PipelineBuildContext
                {
                    OpenAIApiKey = settings.OpenAIApiKey,
                    GroqApiKey = settings.GroqApiKey,
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

            // Check if the API key for the selected provider is configured
            bool hasRequiredApiKey = settings.TranscriptionProvider switch
            {
                TranscriptionProvider.OpenAI => !string.IsNullOrWhiteSpace(settings.OpenAIApiKey),
                TranscriptionProvider.Groq => !string.IsNullOrWhiteSpace(settings.GroqApiKey),
                _ => false
            };

            if (!hasRequiredApiKey)
            {
                _logger?.Log($"Settings saved but no API key configured for {settings.TranscriptionProvider}");
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
                _triggerPluginManager?.Dispose();
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
