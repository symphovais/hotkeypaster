using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TalkKeys.Logging;
using TalkKeys.Services.Hotkey;

namespace TalkKeys.Services.Triggers
{
    /// <summary>
    /// Trigger plugin for keyboard hotkey-based recording activation.
    /// Supports both toggle mode and push-to-talk mode.
    /// </summary>
    public class KeyboardTriggerPlugin : ITriggerPlugin
    {
        private const string TRIGGER_ID = "keyboard:hotkey";
        private readonly ILogger? _logger;
        private LowLevelKeyboardHook? _keyboardHook;
        private TriggerPluginConfiguration _configuration;
        private bool _isRunning;
        private bool _disposed;

        // UI elements for settings panel
        private ComboBox? _modeComboBox;
        private TextBox? _hotkeyTextBox;
        private System.Windows.Forms.Keys _currentKey = System.Windows.Forms.Keys.Space;
        private System.Windows.Forms.Keys _currentModifiers = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift;

        public string PluginId => "keyboard";
        public string DisplayName => "Keyboard Hotkey";
        public string Description => "Trigger recording using a keyboard shortcut (Ctrl+Shift+Space by default)";
        public string Icon => "⌨️";
        public bool IsAvailable => true; // Keyboard is always available
        public string StatusMessage => "Ready";

        public event EventHandler<TriggerEventArgs>? TriggerActivated;
        public event EventHandler<TriggerEventArgs>? TriggerDeactivated;
        public event EventHandler<EventArgs>? AvailabilityChanged;

        public KeyboardTriggerPlugin(ILogger? logger = null)
        {
            _logger = logger;
            _configuration = GetDefaultConfiguration();
        }

        public IReadOnlyList<TriggerInfo> GetAvailableTriggers()
        {
            return new List<TriggerInfo>
            {
                new TriggerInfo
                {
                    TriggerId = TRIGGER_ID,
                    DisplayName = "Global Hotkey",
                    Description = "Press a keyboard shortcut to start/stop recording",
                    SupportsPushToTalk = true,
                    SupportsKeyboardShortcut = false // This IS the keyboard shortcut trigger
                }
            };
        }

        public void Initialize(TriggerPluginConfiguration configuration)
        {
            _configuration = configuration;
            ApplyConfiguration();
        }

        public void Start()
        {
            if (_isRunning || !_configuration.Enabled) return;

            try
            {
                _keyboardHook = new LowLevelKeyboardHook();
                _keyboardHook.SetTargetKey(_currentKey, _currentModifiers);
                _keyboardHook.KeyDown += OnKeyDown;
                _keyboardHook.KeyUp += OnKeyUp;
                _keyboardHook.Start();
                _isRunning = true;
                _logger?.Log($"[KeyboardTrigger] Started with hotkey: {FormatHotkey(_currentModifiers, _currentKey)}");
            }
            catch (Exception ex)
            {
                _logger?.Log($"[KeyboardTrigger] Failed to start: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            if (_keyboardHook != null)
            {
                _keyboardHook.KeyDown -= OnKeyDown;
                _keyboardHook.KeyUp -= OnKeyUp;
                _keyboardHook.Stop();
                _keyboardHook.Dispose();
                _keyboardHook = null;
            }
            _isRunning = false;
            _logger?.Log("[KeyboardTrigger] Stopped");
        }

        public void UpdateConfiguration(TriggerPluginConfiguration configuration)
        {
            var wasRunning = _isRunning;
            if (wasRunning) Stop();

            _configuration = configuration;
            ApplyConfiguration();

            if (wasRunning && _configuration.Enabled) Start();
        }

        public TriggerPluginConfiguration GetConfiguration() => _configuration;

        public TriggerPluginConfiguration GetDefaultConfiguration()
        {
            return new TriggerPluginConfiguration
            {
                PluginId = PluginId,
                Enabled = true,
                Triggers = new List<TriggerConfiguration>
                {
                    new TriggerConfiguration
                    {
                        TriggerId = TRIGGER_ID,
                        DisplayName = "Global Hotkey",
                        Enabled = true,
                        Action = RecordingTriggerAction.ToggleRecording,
                        Settings = new Dictionary<string, object>
                        {
                            ["Hotkey"] = "Ctrl+Shift+Space"
                        }
                    }
                }
            };
        }

        public FrameworkElement CreateSettingsPanel()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            // Header
            var header = new TextBlock
            {
                Text = "Keyboard Hotkey Settings",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 15),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937"))
            };
            panel.Children.Add(header);

            // Enable checkbox
            var enablePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            var enableCheckBox = new CheckBox
            {
                IsChecked = _configuration.Enabled,
                VerticalAlignment = VerticalAlignment.Center
            };
            enableCheckBox.Checked += (s, e) => { _configuration.Enabled = true; OnConfigurationChanged(); };
            enableCheckBox.Unchecked += (s, e) => { _configuration.Enabled = false; OnConfigurationChanged(); };
            enablePanel.Children.Add(enableCheckBox);
            enablePanel.Children.Add(new TextBlock
            {
                Text = "Enable keyboard hotkey trigger",
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(enablePanel);

            // Hotkey setting
            var hotkeyLabel = new TextBlock
            {
                Text = "Hotkey",
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(hotkeyLabel);

            _hotkeyTextBox = new TextBox
            {
                Text = FormatHotkey(_currentModifiers, _currentKey),
                IsReadOnly = true,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 5),
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            _hotkeyTextBox.PreviewKeyDown += OnHotkeyCapture;
            _hotkeyTextBox.GotFocus += (s, e) => _hotkeyTextBox.Text = "Press keys...";
            _hotkeyTextBox.LostFocus += (s, e) =>
            {
                if (_hotkeyTextBox.Text == "Press keys...")
                    _hotkeyTextBox.Text = FormatHotkey(_currentModifiers, _currentKey);
            };
            panel.Children.Add(_hotkeyTextBox);

            var hotkeyHint = new TextBlock
            {
                Text = "Click and press a key combination",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 0, 0, 15)
            };
            panel.Children.Add(hotkeyHint);

            // Mode setting
            var modeLabel = new TextBlock
            {
                Text = "Recording Mode",
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(modeLabel);

            _modeComboBox = new ComboBox
            {
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 5)
            };
            _modeComboBox.Items.Add(new ComboBoxItem { Content = "Toggle (press to start, press again to stop)", Tag = RecordingTriggerAction.ToggleRecording });
            _modeComboBox.Items.Add(new ComboBoxItem { Content = "Push-to-Talk (hold to record, release to stop)", Tag = RecordingTriggerAction.PushToTalk });

            var currentAction = GetTriggerConfig()?.Action ?? RecordingTriggerAction.ToggleRecording;
            _modeComboBox.SelectedIndex = currentAction == RecordingTriggerAction.PushToTalk ? 1 : 0;
            _modeComboBox.SelectionChanged += OnModeChanged;
            panel.Children.Add(_modeComboBox);

            var modeHint = new TextBlock
            {
                Text = "Toggle: Press once to start, press again to stop\nPush-to-Talk: Hold the hotkey while speaking",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(modeHint);

            return panel;
        }

        private void OnHotkeyCapture(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                System.Windows.Input.Keyboard.ClearFocus();
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore modifier-only presses
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            var modifiers = System.Windows.Forms.Keys.None;
            if ((System.Windows.Input.Keyboard.Modifiers & ModifierKeys.Control) != 0)
                modifiers |= System.Windows.Forms.Keys.Control;
            if ((System.Windows.Input.Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                modifiers |= System.Windows.Forms.Keys.Shift;
            if ((System.Windows.Input.Keyboard.Modifiers & ModifierKeys.Alt) != 0)
                modifiers |= System.Windows.Forms.Keys.Alt;
            if ((System.Windows.Input.Keyboard.Modifiers & ModifierKeys.Windows) != 0)
                modifiers |= System.Windows.Forms.Keys.LWin;

            // Convert WPF key to WinForms key
            var formsKey = (System.Windows.Forms.Keys)KeyInterop.VirtualKeyFromKey(key);

            _currentKey = formsKey;
            _currentModifiers = modifiers;

            if (_hotkeyTextBox != null)
            {
                _hotkeyTextBox.Text = FormatHotkey(modifiers, formsKey);
            }

            // Update configuration
            var triggerConfig = GetTriggerConfig();
            if (triggerConfig != null)
            {
                triggerConfig.Settings["Hotkey"] = FormatHotkey(modifiers, formsKey);
            }

            OnConfigurationChanged();
            System.Windows.Input.Keyboard.ClearFocus();
        }

        private void OnModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_modeComboBox?.SelectedItem is ComboBoxItem item && item.Tag is RecordingTriggerAction action)
            {
                var triggerConfig = GetTriggerConfig();
                if (triggerConfig != null)
                {
                    triggerConfig.Action = action;
                    OnConfigurationChanged();
                }
            }
        }

        private TriggerConfiguration? GetTriggerConfig()
        {
            return _configuration.Triggers.Find(t => t.TriggerId == TRIGGER_ID);
        }

        private void ApplyConfiguration()
        {
            var triggerConfig = GetTriggerConfig();
            if (triggerConfig?.Settings.TryGetValue("Hotkey", out var hotkeyObj) == true && hotkeyObj is string hotkey)
            {
                ParseHotkey(hotkey, out _currentKey, out _currentModifiers);
            }
        }

        private void OnKeyDown(object? sender, KeyboardHookEventArgs e)
        {
            var triggerConfig = GetTriggerConfig();
            if (triggerConfig == null || !triggerConfig.Enabled) return;

            _logger?.Log($"[KeyboardTrigger] Key down: {e.Key}");
            TriggerActivated?.Invoke(this, new TriggerEventArgs(TRIGGER_ID, triggerConfig.Action));
        }

        private void OnKeyUp(object? sender, KeyboardHookEventArgs e)
        {
            var triggerConfig = GetTriggerConfig();
            if (triggerConfig == null || !triggerConfig.Enabled) return;

            // Only fire deactivated for push-to-talk mode
            if (triggerConfig.Action == RecordingTriggerAction.PushToTalk)
            {
                _logger?.Log($"[KeyboardTrigger] Key up: {e.Key}");
                TriggerDeactivated?.Invoke(this, new TriggerEventArgs(TRIGGER_ID, triggerConfig.Action));
            }
        }

        private void OnConfigurationChanged()
        {
            // Restart with new configuration if running
            if (_isRunning)
            {
                Stop();
                Start();
            }
        }

        private static string FormatHotkey(System.Windows.Forms.Keys modifiers, System.Windows.Forms.Keys key)
        {
            var parts = new List<string>();
            if ((modifiers & System.Windows.Forms.Keys.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & System.Windows.Forms.Keys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & System.Windows.Forms.Keys.Shift) != 0) parts.Add("Shift");
            if ((modifiers & System.Windows.Forms.Keys.LWin) != 0) parts.Add("Win");
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }

        private static void ParseHotkey(string hotkey, out System.Windows.Forms.Keys key, out System.Windows.Forms.Keys modifiers)
        {
            key = System.Windows.Forms.Keys.Q;
            modifiers = System.Windows.Forms.Keys.None;

            var parts = hotkey.Split('+');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                switch (trimmed.ToLower())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= System.Windows.Forms.Keys.Control;
                        break;
                    case "alt":
                        modifiers |= System.Windows.Forms.Keys.Alt;
                        break;
                    case "shift":
                        modifiers |= System.Windows.Forms.Keys.Shift;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= System.Windows.Forms.Keys.LWin;
                        break;
                    default:
                        if (Enum.TryParse<System.Windows.Forms.Keys>(trimmed, true, out var parsed))
                        {
                            key = parsed;
                        }
                        break;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
