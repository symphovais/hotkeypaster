using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TalkKeys.Logging;
using TalkKeys.Services.Auth;
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
using TalkKeys.Services.Plugins;
using TalkKeys.Services.Controller;
using TalkKeys.Services.RemoteControl;
using TalkKeys.Plugins.Explainer;

namespace TalkKeys
{
    public partial class App : Application
    {
        private const string MutexName = "Global\\TalkKeys_SingleInstance_Mutex";
        private Mutex? _instanceMutex;

        private IPipelineService? _pipelineService;
        private SettingsService? _settingsService;
        private TalkKeysApiService? _talkKeysApiService;
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
        private PluginManager? _pluginManager;
        private IUpdateService? _updateService;
        private TalkKeysController? _controller;
        private RemoteControlServer? _remoteControlServer;
        private bool _mutexOwned = false;

        // Debouncing for hotkey triggers - prevent rapid re-triggering
        private DateTime _lastTriggerTime = DateTime.MinValue;
        private const int TriggerDebounceMs = 200;  // Minimum ms between triggers (balanced responsiveness vs stability)

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
            _audioService.RecordingFailed += OnRecordingFailed;
            _hotkeyService = new Win32HotkeyService();
            _trayService = new TrayService();
            _contextService = new ActiveWindowContextService();
            _settingsService = new SettingsService();

            // Load settings and check authentication
            var settings = _settingsService.LoadSettings();

            // Check if user is authenticated (either with TalkKeys account or own API key)
            bool needsAuth = !IsAuthenticated(settings);

            // If using TalkKeys account, validate the token is still valid
            if (!needsAuth && settings.AuthMode == AuthMode.TalkKeysAccount)
            {
                if (!ValidateTalkKeysToken(settings))
                {
                    // Token is expired/invalid - clear it and require re-auth
                    ClearTalkKeysAuth(settings);
                    needsAuth = true;
                    _notifications?.ShowInfo("Session Expired", "Your TalkKeys session has expired. Please sign in again.");
                }
            }

            if (needsAuth)
            {
                _logger.Log("Authentication required - showing welcome window");

                // Show welcome window for first-time setup or re-auth
                var welcomeWindow = new WelcomeWindow(_settingsService, _logger);
                var result = welcomeWindow.ShowDialog();

                if (result != true)
                {
                    // User closed without authenticating - exit app
                    _logger.Log("User closed welcome window without authenticating - exiting");
                    Current.Shutdown();
                    return;
                }

                // Reload settings after authentication
                settings = _settingsService.LoadSettings();
            }

            // Check for version change and show What's New screen
            ShowWhatsNewIfVersionChanged(settings);

            // Create pipeline service based on auth mode
            _pipelineService = CreatePipelineService(settings);
            if (_pipelineService == null)
            {
                _notifications?.ShowError("Configuration Error",
                    "Failed to initialize transcription pipeline. Check logs for details.");
                _logger?.Log("App started with invalid configuration");
            }

            // Initialize tray
            _trayService.InitializeTray();

            // Create controller first (before widget so we can wire it up)
            _controller = new TalkKeysController(
                _logger!,
                _audioService!,
                _clipboardService!,
                _notifications!,
                _settingsService!);

            // Set pipeline service if available
            _controller.SetPipelineService(_pipelineService);

            // Always create FloatingWidget (it will show "API Key Required" message if not configured)
            CreateFloatingWidget();

            // Wire up controller to widget
            _controller.SetFloatingWidget(_floatingWidget!);

            // Initialize trigger plugin system
            InitializeTriggerPlugins(settings);

            // Initialize general plugin system (Focus Timer, etc.)
            InitializePlugins(settings);

            // Wire tray events
            _trayService.SettingsRequested += (s, args) =>
            {
                var settingsWindow = new SettingsWindow(_settingsService!, _logger!, _audioService, _triggerPluginManager, _pluginManager);
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

            _trayService.AboutRequested += (s, args) =>
            {
                var aboutWindow = new AboutWindow(_logger!, AboutWindowPage.About);
                aboutWindow.Show();
            };

            // Log startup
            if (_pipelineService != null)
            {
                var pipelineName = _pipelineService.GetDefaultPipelineName() ?? "unknown";
                _logger?.Log($"TalkKeys started with pipeline: {pipelineName}");
            }
            else
            {
                _logger?.Log("TalkKeys started without pipeline - waiting for API key configuration");
            }

            // Auto-select Jabra Engage 50 II as audio device if enabled
            AutoSelectJabraDeviceIfEnabled(settings);

            // Initialize Remote Control API server
            InitializeRemoteControl(settings);

            // Note: Updates are managed by Microsoft Store for packaged versions
            _updateService = new UpdateService(_logger);
        }

        private void InitializeRemoteControl(AppSettings settings)
        {
            if (!settings.RemoteControlEnabled)
            {
                _logger?.Log("[RemoteControl] Remote control is disabled in settings");
                return;
            }

            if (_controller == null)
            {
                _logger?.Log("[RemoteControl] Controller not available, skipping remote control initialization");
                return;
            }

            try
            {
                _remoteControlServer = new RemoteControlServer(_controller, settings.RemoteControlPort, _logger!);
                _remoteControlServer.Start();

                if (_remoteControlServer.IsRunning)
                {
                    _logger?.Log($"[RemoteControl] Server started on port {settings.RemoteControlPort}");
                }
                else
                {
                    _logger?.Log("[RemoteControl] Server failed to start - continuing without remote control");
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[RemoteControl] Failed to initialize: {ex.Message}");
                // Continue without remote control - not critical
            }
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

        private void InitializePlugins(AppSettings settings)
        {
            _logger?.Log($"[App] InitializePlugins - settings.Plugins count: {settings.Plugins?.Count ?? 0}");
            if (settings.Plugins != null)
            {
                foreach (var kvp in settings.Plugins)
                {
                    _logger?.Log($"[App] Saved plugin config: '{kvp.Key}' -> Enabled={kvp.Value?.Enabled}, WidgetVisible={kvp.Value?.WidgetVisible}");
                }
            }

            _pluginManager = new PluginManager(_logger, _positioner);

            // Register Explainer plugin (requires TalkKeys API service)
            // Create API service if not already created (for non-TalkKeys mode, it just won't have auth)
            if (_talkKeysApiService == null)
            {
                _talkKeysApiService = new TalkKeysApiService(_settingsService!, _logger);
            }
            _pluginManager.RegisterPlugin(new ExplainerPlugin(_talkKeysApiService, _settingsService!, _positioner, _logger));

            // Subscribe to plugin events
            _pluginManager.PluginWidgetPositionChanged += OnPluginWidgetPositionChanged;
            _pluginManager.PluginWidgetVisibilityChanged += OnPluginWidgetVisibilityChanged;
            _pluginManager.PluginTrayMenuChanged += OnPluginTrayMenuChanged;

            // Initialize with stored configurations (or defaults)
            _pluginManager.Initialize(settings.Plugins);

            // Activate all enabled plugins
            _pluginManager.ActivateAll();

            // Set initial tray menu items
            UpdatePluginTrayMenuItems();

            _logger?.Log("[App] Plugin system initialized");
        }

        private void OnPluginWidgetPositionChanged(object? sender, PluginWidgetPositionChangedEventArgs e)
        {
            // Save position to settings
            var settings = _settingsService!.LoadSettings();
            if (!settings.Plugins.TryGetValue(e.PluginId, out var config))
            {
                // Create config if it doesn't exist
                config = _pluginManager?.GetPlugin(e.PluginId)?.GetConfiguration() ?? new PluginConfiguration { PluginId = e.PluginId };
                settings.Plugins[e.PluginId] = config;
            }
            config.WidgetX = e.X;
            config.WidgetY = e.Y;
            _settingsService.SaveSettings(settings);
        }

        private void OnPluginWidgetVisibilityChanged(object? sender, PluginWidgetVisibilityChangedEventArgs e)
        {
            // Save visibility to settings
            var settings = _settingsService!.LoadSettings();
            if (!settings.Plugins.TryGetValue(e.PluginId, out var config))
            {
                // Create config if it doesn't exist
                config = _pluginManager?.GetPlugin(e.PluginId)?.GetConfiguration() ?? new PluginConfiguration { PluginId = e.PluginId };
                settings.Plugins[e.PluginId] = config;
            }
            config.WidgetVisible = e.IsVisible;
            _settingsService.SaveSettings(settings);
            _logger?.Log($"[App] Plugin '{e.PluginId}' widget visibility saved: {e.IsVisible}");
        }

        private void OnPluginTrayMenuChanged(object? sender, PluginTrayMenuChangedEventArgs e)
        {
            // Refresh tray menu when plugin state changes
            UpdatePluginTrayMenuItems();
        }

        private void UpdatePluginTrayMenuItems()
        {
            if (_pluginManager == null || _trayService == null) return;

            var menuItems = _pluginManager.GetAllTrayMenuItems();
            _trayService.SetPluginMenuItems(menuItems);
        }

        private void OnTriggerActivated(object? sender, TriggerEventArgs e)
        {
            // Debounce: ignore triggers that come too quickly after the last one
            var now = DateTime.Now;
            var timeSinceLastTrigger = (now - _lastTriggerTime).TotalMilliseconds;
            if (timeSinceLastTrigger < TriggerDebounceMs)
            {
                _logger?.Log($"Trigger debounced: {e.TriggerId} (only {timeSinceLastTrigger:F0}ms since last trigger)");
                return;
            }
            _lastTriggerTime = now;

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

        private void OnRecordingFailed(object? sender, Services.Audio.RecordingFailedEventArgs e)
        {
            _logger?.Log($"Recording failed: {e.TechnicalError}");

            Current.Dispatcher.Invoke(() =>
            {
                _notifications?.ShowError("Microphone Error", e.UserMessage);

                // Collapse widget if it was expanded
                // The widget will handle its own state via the lack of RecordingStarted event
            });
        }

        private void HandleToggleRecording()
        {
            if (_controller == null)
            {
                _logger?.Log("Toggle: Controller not available");
                return;
            }

            // Toggle recording via controller (consistent with API behavior)
            if (_audioService?.IsRecording == true)
            {
                // Stop recording
                _logger?.Log("Toggle: Stopping recording via controller");
                _ = _controller.StopTranscriptionAsync();
            }
            else
            {
                // Start recording
                _logger?.Log("Toggle: Starting recording via controller");
                _ = _controller.StartTranscriptionAsync();
            }
        }

        private void HandlePushToTalkStart()
        {
            if (_controller == null)
            {
                _logger?.Log("Push-to-talk: Controller not available");
                return;
            }

            // Only start if not already recording
            if (_audioService?.IsRecording != true)
            {
                _logger?.Log("Push-to-talk: Starting recording via controller");
                _ = _controller.StartTranscriptionAsync();
            }
        }

        private void HandlePushToTalkStop()
        {
            if (_controller == null)
            {
                _logger?.Log("Push-to-talk: Controller not available");
                return;
            }

            if (_audioService?.IsRecording == true)
            {
                _logger?.Log("Push-to-talk: Stopping recording via controller");
                _ = _controller.StopTranscriptionAsync();
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

            // Reload general plugin configurations
            if (_pluginManager != null && _settingsService != null)
            {
                var settings = _settingsService.LoadSettings();

                // Update each plugin's configuration
                foreach (var kvp in settings.Plugins)
                {
                    _pluginManager.UpdatePluginConfiguration(kvp.Key, kvp.Value);
                }

                // Refresh tray menu
                UpdatePluginTrayMenuItems();

                _logger?.Log("General plugin configurations reloaded");
            }

            // Update hotkey hints in the floating widget
            _floatingWidget?.UpdateHotkeyHints();
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

        /// <summary>
        /// Shows the What's New window if this is a new install or version update.
        /// </summary>
        private void ShowWhatsNewIfVersionChanged(AppSettings settings)
        {
            var currentVersion = GetCurrentVersion();
            var lastSeenVersion = settings.LastSeenVersion;

            // Check if this is first install or version changed
            bool isFirstInstall = string.IsNullOrEmpty(lastSeenVersion);
            bool isVersionChanged = !isFirstInstall && lastSeenVersion != currentVersion;

            if (isFirstInstall || isVersionChanged)
            {
                _logger?.Log($"Showing What's New screen. Current: {currentVersion}, Last seen: {lastSeenVersion ?? "none"}");

                var whatsNewWindow = new AboutWindow(_logger!, AboutWindowPage.WhatsNew);
                whatsNewWindow.ShowDialog();

                // Update last seen version
                settings.LastSeenVersion = currentVersion;
                _settingsService?.SaveSettings(settings);
            }
        }

        /// <summary>
        /// Gets the current app version as a string (e.g., "1.1.0").
        /// </summary>
        private static string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : "1.0.0";
        }

        /// <summary>
        /// Check if user is authenticated (either TalkKeys account or own API key)
        /// </summary>
        private bool IsAuthenticated(AppSettings settings)
        {
            return (settings.AuthMode == AuthMode.TalkKeysAccount && !string.IsNullOrEmpty(settings.TalkKeysAccessToken)) ||
                   (settings.AuthMode == AuthMode.OwnApiKey && !string.IsNullOrEmpty(settings.GroqApiKey));
        }

        /// <summary>
        /// Validates the TalkKeys token by making an API call.
        /// Returns true if token is valid, false if expired/invalid.
        /// </summary>
        private bool ValidateTalkKeysToken(AppSettings settings)
        {
            if (settings.AuthMode != AuthMode.TalkKeysAccount || string.IsNullOrEmpty(settings.TalkKeysAccessToken))
            {
                return true; // Not using TalkKeys, skip validation
            }

            _logger?.Log("[Auth] Validating TalkKeys token...");

            try
            {
                using var apiService = new TalkKeysApiService(_settingsService!, _logger);
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var usage = Task.Run(() => apiService.GetUsageAsync(cts.Token)).GetAwaiter().GetResult();

                if (usage != null)
                {
                    _logger?.Log($"[Auth] Token valid - {usage.RemainingSeconds}s remaining today");
                    return true;
                }
                else
                {
                    _logger?.Log("[Auth] Token validation failed - token may be expired");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Auth] Token validation error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clears TalkKeys authentication tokens from settings.
        /// </summary>
        private void ClearTalkKeysAuth(AppSettings settings)
        {
            settings.TalkKeysAccessToken = null;
            settings.TalkKeysRefreshToken = null;
            settings.TalkKeysUserEmail = null;
            settings.TalkKeysUserName = null;
            _settingsService?.SaveSettings(settings);
            _logger?.Log("[Auth] Cleared expired TalkKeys credentials");
        }

        private IPipelineService? CreatePipelineService(AppSettings settings)
        {
            // Verify authentication
            if (!IsAuthenticated(settings))
            {
                _logger?.Log("Cannot create pipeline service: Not authenticated");
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

                // Register stage factories based on auth mode
                factory.RegisterStageFactory(new AudioValidationStageFactory());

                if (settings.AuthMode == AuthMode.TalkKeysAccount)
                {
                    // Use TalkKeys proxy stages
                    factory.RegisterStageFactory(new TalkKeysTranscriptionStageFactory());
                    factory.RegisterStageFactory(new TalkKeysTextCleaningStageFactory());
                    _logger?.Log("Using TalkKeys account for transcription");
                }
                else
                {
                    // Use direct Groq API stages
                    factory.RegisterStageFactory(new GroqWhisperTranscriptionStageFactory());
                    factory.RegisterStageFactory(new GroqTextCleaningStageFactory());
                    _logger?.Log("Using own Groq API key for transcription");
                }

                // Create configuration loader
                var configLoader = new PipelineConfigurationLoader(pipelineConfigDir, _logger);

                // Ensure default configuration exists based on auth mode
                if (settings.AuthMode == AuthMode.TalkKeysAccount)
                {
                    EnsureTalkKeysPipelineConfig(configLoader);
                }
                else
                {
                    configLoader.EnsureDefaultConfigurations(settings.GroqApiKey!);
                }

                // Ensure API service exists for TalkKeys mode (don't recreate - ExplainerPlugin holds reference)
                if (settings.AuthMode == AuthMode.TalkKeysAccount && _talkKeysApiService == null)
                {
                    _talkKeysApiService = new TalkKeysApiService(_settingsService!, _logger);
                }

                // Create build context
                var buildContext = new PipelineBuildContext
                {
                    GroqApiKey = settings.GroqApiKey,
                    TalkKeysApiService = _talkKeysApiService,
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

        /// <summary>
        /// Create pipeline configuration for TalkKeys account mode
        /// </summary>
        private void EnsureTalkKeysPipelineConfig(PipelineConfigurationLoader configLoader)
        {
            var config = new PipelineConfiguration
            {
                Name = "Default",
                Description = "Transcription via TalkKeys service",
                Enabled = true,
                Stages = new System.Collections.Generic.List<StageConfiguration>
                {
                    new() { Type = "AudioValidation", Enabled = true },
                    new() { Type = "TalkKeysTranscription", Enabled = true },
                    new() { Type = "TalkKeysTextCleaning", Enabled = true }
                }
            };

            configLoader.Save(config);
            _logger?.Log("Created TalkKeys pipeline configuration");
        }

        private void ReloadPipelineService()
        {
            if (_settingsService == null) return;

            var settings = _settingsService.LoadSettings();

            // Check if authenticated
            if (!IsAuthenticated(settings))
            {
                _logger?.Log("Settings saved but not authenticated");
                return;
            }

            var newService = CreatePipelineService(settings);

            if (newService != null)
            {
                _pipelineService = newService;
                _logger?.Log($"Pipeline service reloaded with pipeline: {newService.GetDefaultPipelineName()}");

                // Update the controller (which will also update the widget)
                _controller?.SetPipelineService(newService);
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
                // Stop remote control server
                _remoteControlServer?.Stop();
                _remoteControlServer?.Dispose();

                // Dispose services
                _pluginManager?.Dispose();
                _triggerPluginManager?.Dispose();
                _talkKeysApiService?.Dispose();
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
