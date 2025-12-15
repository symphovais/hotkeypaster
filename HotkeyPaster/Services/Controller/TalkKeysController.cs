using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using TalkKeys.Logging;
using TalkKeys.Services.Audio;
using TalkKeys.Services.Clipboard;
using TalkKeys.Services.Notifications;
using TalkKeys.Services.Pipeline;
using TalkKeys.Services.Plugins;
using TalkKeys.Services.RecordingMode;
using TalkKeys.Services.Settings;
using TalkKeys.Services.Triggers;

namespace TalkKeys.Services.Controller
{
    /// <summary>
    /// Unified controller for TalkKeys operations.
    /// Provides a single point of entry for both UI and API triggers.
    /// </summary>
    public class TalkKeysController : ITalkKeysController
    {
        private readonly ILogger _logger;
        private readonly IAudioRecordingService _audioService;
        private readonly IClipboardPasteService _clipboardService;
        private readonly INotificationService _notifications;
        private readonly SettingsService _settingsService;
        private readonly TriggerPluginManager? _triggerPluginManager;
        private readonly PluginManager? _pluginManager;

        private IPipelineService? _pipelineService;
        private FloatingWidget? _floatingWidget;
        private bool _isProcessing;

        // Events for state changes (UI can subscribe)
        public event EventHandler? RecordingStarted;
        public event EventHandler? RecordingStopped;
        public event EventHandler? ProcessingStarted;
        public event EventHandler? ProcessingCompleted;

        public TalkKeysController(
            ILogger logger,
            IAudioRecordingService audioService,
            IClipboardPasteService clipboardService,
            INotificationService notifications,
            SettingsService settingsService,
            TriggerPluginManager? triggerPluginManager = null,
            PluginManager? pluginManager = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _triggerPluginManager = triggerPluginManager;
            _pluginManager = pluginManager;
        }

        /// <summary>
        /// Sets the FloatingWidget instance (called after widget creation)
        /// </summary>
        public void SetFloatingWidget(FloatingWidget widget)
        {
            _floatingWidget = widget;
        }

        /// <summary>
        /// Sets the pipeline service (called after pipeline creation or update)
        /// </summary>
        public void SetPipelineService(IPipelineService? pipelineService)
        {
            _pipelineService = pipelineService;
            _floatingWidget?.UpdatePipelineService(pipelineService!);
        }

        /// <summary>
        /// Gets the current status of TalkKeys
        /// </summary>
        public TalkKeysStatus GetStatus()
        {
            var status = new TalkKeysStatus
            {
                Success = true,
                Recording = _audioService.IsRecording,
                Processing = _isProcessing,
                Version = GetVersion(),
                Authenticated = _pipelineService != null
            };

            if (_isProcessing)
            {
                status.Status = "processing";
            }
            else if (_audioService.IsRecording)
            {
                status.Status = "recording";
            }
            else
            {
                status.Status = "idle";
            }

            return status;
        }

        /// <summary>
        /// Gets TalkKeys capabilities for API discovery
        /// </summary>
        public TalkKeysCapabilities GetCapabilities()
        {
            var settings = _settingsService.LoadSettings();
            var transcriptionShortcut = GetTranscriptionShortcut(settings);
            var explainShortcut = GetExplainShortcut(settings);

            return new TalkKeysCapabilities
            {
                Name = "TalkKeys",
                Version = GetVersion(),
                Capabilities = new List<Capability>
                {
                    new()
                    {
                        Id = "transcription",
                        Name = "Voice Transcription",
                        Description = "Record voice and transcribe to text, paste to active application",
                        Shortcut = transcriptionShortcut,
                        Actions = new List<string> { "starttranscription", "stoptranscription", "canceltranscription" }
                    },
                    new()
                    {
                        Id = "explain",
                        Name = "Plain English Explainer",
                        Description = "Explain selected text in plain English",
                        Shortcut = explainShortcut,
                        Actions = new List<string> { "explain" }
                    }
                },
                Endpoints = new List<EndpointInfo>
                {
                    new() { Method = "GET", Path = "/", Description = "Get capabilities and API info" },
                    new() { Method = "GET", Path = "/status", Description = "Get current status" },
                    new() { Method = "POST", Path = "/starttranscription", Description = "Start voice recording" },
                    new() { Method = "POST", Path = "/stoptranscription", Description = "Stop and transcribe" },
                    new() { Method = "POST", Path = "/canceltranscription", Description = "Cancel recording" },
                    new() { Method = "POST", Path = "/explain", Description = "Explain selected text" },
                    new() { Method = "GET", Path = "/microphones", Description = "List microphones" },
                    new() { Method = "POST", Path = "/microphone", Description = "Set active microphone" },
                    new() { Method = "GET", Path = "/shortcuts", Description = "Get shortcuts" },
                    new() { Method = "POST", Path = "/shortcuts", Description = "Update shortcuts" }
                }
            };
        }

        /// <summary>
        /// Starts voice transcription recording
        /// </summary>
        public Task<ControllerActionResult> StartTranscriptionAsync()
        {
            _logger.Log("[Controller] StartTranscriptionAsync called");

            // Check if authenticated
            if (_pipelineService == null)
            {
                _logger.Log("[Controller] Not authenticated - no pipeline service");
                _notifications.ShowError("API Key Required",
                    "No API key configured. Right-click the tray icon and select Settings to add your API key.");
                return Task.FromResult(ControllerActionResult.Fail("idle", "Not authenticated"));
            }

            // Check if already recording
            if (_audioService.IsRecording)
            {
                _logger.Log("[Controller] Already recording");
                return Task.FromResult(ControllerActionResult.Fail("recording", "Already recording"));
            }

            // Check if processing
            if (_isProcessing)
            {
                _logger.Log("[Controller] Currently processing transcription");
                return Task.FromResult(ControllerActionResult.Fail("processing", "Currently processing transcription"));
            }

            // Ensure widget is available
            if (_floatingWidget == null)
            {
                _logger.Log("[Controller] Widget not available");
                return Task.FromResult(ControllerActionResult.Fail("idle", "Widget not initialized"));
            }

            // Start recording via the widget (which handles all the UI updates)
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Show widget if hidden
                if (!_floatingWidget.IsVisible)
                {
                    _floatingWidget.Show();
                }

                // Start recording with clipboard mode
                var clipboardHandler = new ClipboardModeHandler(_clipboardService, _logger);
                _floatingWidget.StartRecording(clipboardHandler);
            });

            _logger.Log("[Controller] Recording started successfully");
            RecordingStarted?.Invoke(this, EventArgs.Empty);
            return Task.FromResult(ControllerActionResult.Ok("recording", "Recording started"));
        }

        /// <summary>
        /// Stops voice recording and triggers transcription
        /// </summary>
        public Task<ControllerActionResult> StopTranscriptionAsync()
        {
            _logger.Log("[Controller] StopTranscriptionAsync called");

            // Check if recording
            if (!_audioService.IsRecording)
            {
                _logger.Log("[Controller] Not currently recording");
                return Task.FromResult(ControllerActionResult.Fail("idle", "Not recording"));
            }

            // Stop recording (this triggers OnRecordingStopped in the widget which does transcription)
            Application.Current.Dispatcher.Invoke(() =>
            {
                _audioService.StopRecording();
            });

            _logger.Log("[Controller] Recording stopped, transcription will begin");
            RecordingStopped?.Invoke(this, EventArgs.Empty);
            return Task.FromResult(ControllerActionResult.Ok("processing", "Recording stopped, transcribing..."));
        }

        /// <summary>
        /// Cancels the current recording without transcribing
        /// </summary>
        public Task<ControllerActionResult> CancelTranscriptionAsync()
        {
            _logger.Log("[Controller] CancelTranscriptionAsync called");

            // Check if recording
            if (!_audioService.IsRecording)
            {
                _logger.Log("[Controller] Not currently recording");
                return Task.FromResult(ControllerActionResult.Fail("idle", "Not recording"));
            }

            // Cancel by stopping recording and collapsing widget
            // The widget won't transcribe if we signal cancellation
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Stop the recording
                _audioService.StopRecording();
            });

            _logger.Log("[Controller] Recording cancelled");
            return Task.FromResult(ControllerActionResult.Ok("idle", "Recording cancelled"));
        }

        /// <summary>
        /// Triggers the explain feature for selected text
        /// </summary>
        public Task<ControllerActionResult> ExplainSelectedTextAsync()
        {
            _logger.Log("[Controller] ExplainSelectedTextAsync called");

            // Check if authenticated
            if (_pipelineService == null)
            {
                _logger.Log("[Controller] Not authenticated - no pipeline service");
                return Task.FromResult(ControllerActionResult.Fail("idle", "Not authenticated"));
            }

            // Find and trigger the explainer plugin
            if (_pluginManager != null)
            {
                var explainerPlugin = _pluginManager.GetPlugin("explainer");
                if (explainerPlugin != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        explainerPlugin.Activate();
                    });
                    _logger.Log("[Controller] Explainer plugin activated");
                    return Task.FromResult(ControllerActionResult.Ok("processing", "Explaining selected text..."));
                }
            }

            _logger.Log("[Controller] Explainer plugin not available");
            return Task.FromResult(ControllerActionResult.Fail("idle", "Explainer feature not available"));
        }

        /// <summary>
        /// Gets list of available microphones
        /// </summary>
        public List<MicrophoneInfo> GetMicrophones()
        {
            var devices = _audioService.GetAvailableDevices();
            var currentIndex = _audioService.CurrentDeviceIndex;
            var result = new List<MicrophoneInfo>();

            for (int i = 0; i < devices.Length; i++)
            {
                result.Add(new MicrophoneInfo
                {
                    Index = i,
                    Name = devices[i],
                    Current = i == currentIndex
                });
            }

            return result;
        }

        /// <summary>
        /// Sets the active microphone
        /// </summary>
        public ControllerActionResult SetMicrophone(int index)
        {
            _logger.Log($"[Controller] SetMicrophone called with index {index}");

            var devices = _audioService.GetAvailableDevices();
            if (index < 0 || index >= devices.Length)
            {
                _logger.Log($"[Controller] Invalid microphone index: {index}");
                return ControllerActionResult.Fail("idle", $"Invalid microphone index. Valid range: 0-{devices.Length - 1}");
            }

            _audioService.SetDevice(index);

            // Save to settings
            var settings = _settingsService.LoadSettings();
            settings.AudioDeviceIndex = index;
            _settingsService.SaveSettings(settings);

            _logger.Log($"[Controller] Microphone set to index {index}: {devices[index]}");
            return ControllerActionResult.Ok("idle", $"Microphone set to: {devices[index]}");
        }

        /// <summary>
        /// Gets current shortcut configurations
        /// </summary>
        public Dictionary<string, string> GetShortcuts()
        {
            var settings = _settingsService.LoadSettings();
            return new Dictionary<string, string>
            {
                { "transcription", GetTranscriptionShortcut(settings) },
                { "explain", GetExplainShortcut(settings) }
            };
        }

        /// <summary>
        /// Updates shortcut configurations
        /// </summary>
        public ControllerActionResult SetShortcuts(Dictionary<string, string> shortcuts)
        {
            _logger.Log("[Controller] SetShortcuts called");

            if (shortcuts == null || shortcuts.Count == 0)
            {
                return ControllerActionResult.Fail("idle", "No shortcuts provided");
            }

            var settings = _settingsService.LoadSettings();
            bool changed = false;

            foreach (var kvp in shortcuts)
            {
                switch (kvp.Key.ToLowerInvariant())
                {
                    case "transcription":
                        if (UpdateTranscriptionShortcut(settings, kvp.Value))
                        {
                            changed = true;
                        }
                        break;
                    case "explain":
                        if (UpdateExplainShortcut(settings, kvp.Value))
                        {
                            changed = true;
                        }
                        break;
                    default:
                        _logger.Log($"[Controller] Unknown shortcut key: {kvp.Key}");
                        break;
                }
            }

            if (changed)
            {
                _settingsService.SaveSettings(settings);

                // Reload trigger plugin configurations
                if (_triggerPluginManager != null)
                {
                    foreach (var kvp in settings.TriggerPlugins)
                    {
                        _triggerPluginManager.UpdatePluginConfiguration(kvp.Key, kvp.Value);
                    }
                }

                _floatingWidget?.UpdateHotkeyHints();
                _logger.Log("[Controller] Shortcuts updated and saved");
            }

            return ControllerActionResult.Ok("idle", "Shortcuts updated");
        }

        /// <summary>
        /// Notifies the controller that processing has started
        /// </summary>
        public void NotifyProcessingStarted()
        {
            _isProcessing = true;
            ProcessingStarted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Notifies the controller that processing has completed
        /// </summary>
        public void NotifyProcessingCompleted()
        {
            _isProcessing = false;
            ProcessingCompleted?.Invoke(this, EventArgs.Empty);
        }

        private string GetVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : "1.0.0";
        }

        private string GetTranscriptionShortcut(AppSettings settings)
        {
            const string defaultHotkey = "Ctrl+Shift+Space";

            if (settings.TriggerPlugins.TryGetValue("keyboard", out var keyboardConfig))
            {
                var trigger = keyboardConfig.Triggers.Find(t => t.TriggerId == "keyboard:hotkey");
                if (trigger != null && trigger.Settings.TryGetValue("Hotkey", out var hotkeyObj))
                {
                    if (hotkeyObj is string hotkeyStr && !string.IsNullOrEmpty(hotkeyStr))
                    {
                        return hotkeyStr;
                    }
                    if (hotkeyObj is System.Text.Json.JsonElement jsonElement &&
                        jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var value = jsonElement.GetString();
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }
            }

            return defaultHotkey;
        }

        private string GetExplainShortcut(AppSettings settings)
        {
            const string defaultHotkey = "Ctrl+Win+E";

            if (settings.Plugins.TryGetValue("explainer", out var explainerConfig))
            {
                if (explainerConfig.Settings.TryGetValue("Hotkey", out var hotkeyObj))
                {
                    if (hotkeyObj is string hotkeyStr && !string.IsNullOrEmpty(hotkeyStr))
                    {
                        return hotkeyStr;
                    }
                    if (hotkeyObj is System.Text.Json.JsonElement jsonElement &&
                        jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var value = jsonElement.GetString();
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }
            }

            return defaultHotkey;
        }

        private bool UpdateTranscriptionShortcut(AppSettings settings, string newHotkey)
        {
            if (string.IsNullOrWhiteSpace(newHotkey))
            {
                return false;
            }

            if (!settings.TriggerPlugins.TryGetValue("keyboard", out var keyboardConfig))
            {
                _logger.Log("[Controller] Keyboard plugin config not found");
                return false;
            }

            var trigger = keyboardConfig.Triggers.Find(t => t.TriggerId == "keyboard:hotkey");
            if (trigger == null)
            {
                _logger.Log("[Controller] Keyboard hotkey trigger not found");
                return false;
            }

            trigger.Settings["Hotkey"] = newHotkey;
            _logger.Log($"[Controller] Transcription shortcut updated to: {newHotkey}");
            return true;
        }

        private bool UpdateExplainShortcut(AppSettings settings, string newHotkey)
        {
            if (string.IsNullOrWhiteSpace(newHotkey))
            {
                return false;
            }

            if (!settings.Plugins.TryGetValue("explainer", out var explainerConfig))
            {
                // Create config if it doesn't exist
                explainerConfig = new PluginSdk.PluginConfiguration
                {
                    PluginId = "explainer",
                    Enabled = true,
                    Settings = new Dictionary<string, object>()
                };
                settings.Plugins["explainer"] = explainerConfig;
            }

            explainerConfig.Settings["Hotkey"] = newHotkey;
            _logger.Log($"[Controller] Explain shortcut updated to: {newHotkey}");
            return true;
        }
    }
}
