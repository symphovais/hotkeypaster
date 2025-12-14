using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.Hooks;
using TalkKeys.Logging;
using TalkKeys.PluginSdk;
using Key = System.Windows.Input.Key;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using Keyboard = System.Windows.Input.Keyboard;
using Cursors = System.Windows.Input.Cursors;

namespace TalkKeys.Services.Triggers
{
    /// <summary>
    /// Trigger plugin for keyboard hotkey-based recording activation.
    /// Supports both toggle mode and push-to-talk mode.
    /// Uses H.Hooks library for global keyboard hook.
    /// </summary>
    public class KeyboardTriggerPlugin : ITriggerPlugin
    {
        private const string TRIGGER_ID = "keyboard:hotkey";
        private readonly ILogger? _logger;
        private LowLevelKeyboardHook? _keyboardHook;
        private TriggerPluginConfiguration _configuration;
        private bool _isRunning;
        private bool _disposed;
        private bool _isKeyCurrentlyDown;

        // UI elements for settings panel
        private ComboBox? _modeComboBox;
        private TextBox? _hotkeyTextBox;

        // Current hotkey configuration (using WPF Key/ModifierKeys)
        private Key _currentKey = Key.Space;
        private ModifierKeys _currentModifiers = ModifierKeys.Control | ModifierKeys.Shift;

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
                _keyboardHook = new LowLevelKeyboardHook
                {
                    IsExtendedMode = true,       // Support key combinations like Ctrl+Shift+Space
                    HandleModifierKeys = true,    // Process modifier keys in events
                    IsLeftRightGranularity = false // Treat Left/Right modifiers the same
                };

                _keyboardHook.Down += OnKeyDown;
                _keyboardHook.Up += OnKeyUp;
                _keyboardHook.Start();

                _isRunning = true;
                _logger?.Log($"[KeyboardTrigger] Started with hotkey: {FormatHotkey(_currentModifiers, _currentKey)} (using H.Hooks)");
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
                _keyboardHook.Down -= OnKeyDown;
                _keyboardHook.Up -= OnKeyUp;
                _keyboardHook.Dispose();
                _keyboardHook = null;
            }
            _isRunning = false;
            _isKeyCurrentlyDown = false;
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
                Text = "Click and press a key combination (supports Win key)",
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

        private void OnHotkeyCapture(object sender, System.Windows.Input.KeyEventArgs e)
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

            _currentModifiers = System.Windows.Input.Keyboard.Modifiers;
            _currentKey = key;

            if (_hotkeyTextBox != null)
            {
                _hotkeyTextBox.Text = FormatHotkey(_currentModifiers, _currentKey);
            }

            // Update configuration
            var triggerConfig = GetTriggerConfig();
            if (triggerConfig != null)
            {
                triggerConfig.Settings["Hotkey"] = FormatHotkey(_currentModifiers, _currentKey);
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
            if (triggerConfig?.Settings.TryGetValue("Hotkey", out var hotkeyObj) == true)
            {
                string? hotkey = null;

                // Handle both string and JsonElement (from deserialized settings)
                if (hotkeyObj is string hotkeyStr)
                {
                    hotkey = hotkeyStr;
                }
                else if (hotkeyObj is System.Text.Json.JsonElement jsonElement &&
                         jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    hotkey = jsonElement.GetString();
                }

                if (!string.IsNullOrEmpty(hotkey))
                {
                    ParseHotkey(hotkey, out _currentKey, out _currentModifiers);
                    _logger?.Log($"[KeyboardTrigger] Applied hotkey from config: {hotkey}");
                }
            }
        }

        private void OnKeyDown(object? sender, H.Hooks.KeyboardEventArgs args)
        {
            if (!IsTargetHotkey(args)) return;
            if (_isKeyCurrentlyDown) return; // Prevent repeated key-down events

            var triggerConfig = GetTriggerConfig();
            if (triggerConfig == null || !triggerConfig.Enabled) return;

            _isKeyCurrentlyDown = true;
            _logger?.Log($"[KeyboardTrigger] Hotkey pressed: {FormatHotkey(_currentModifiers, _currentKey)}");
            TriggerActivated?.Invoke(this, new TriggerEventArgs(TRIGGER_ID, triggerConfig.Action));
        }

        private void OnKeyUp(object? sender, H.Hooks.KeyboardEventArgs args)
        {
            if (!_isKeyCurrentlyDown) return;

            // Check if the main key was released OR if any required modifier was released
            // Convert WPF Key to H.Hooks Key by name (enum values don't match!)
            bool mainKeyReleased = false;
            if (Enum.TryParse<H.Hooks.Key>(_currentKey.ToString(), out var targetKey))
            {
                mainKeyReleased = args.CurrentKey == targetKey;
            }
            bool modifierReleased = IsModifierReleased(args);

            if (!mainKeyReleased && !modifierReleased) return;

            var triggerConfig = GetTriggerConfig();
            if (triggerConfig == null || !triggerConfig.Enabled) return;

            _isKeyCurrentlyDown = false;

            // Only fire deactivated for push-to-talk mode
            if (triggerConfig.Action == RecordingTriggerAction.PushToTalk)
            {
                _logger?.Log($"[KeyboardTrigger] Hotkey released");
                TriggerDeactivated?.Invoke(this, new TriggerEventArgs(TRIGGER_ID, triggerConfig.Action));
            }
        }

        private bool IsTargetHotkey(H.Hooks.KeyboardEventArgs args)
        {
            // Convert WPF Key to H.Hooks Key by name (enum values don't match!)
            if (!Enum.TryParse<H.Hooks.Key>(_currentKey.ToString(), out var targetKey))
            {
                return false;
            }

            // Get pressed keys as a HashSet for efficient lookup
            var pressedKeys = args.Keys.Values.ToHashSet();

            // Check if the main key is currently pressed
            if (!pressedKeys.Contains(targetKey))
            {
                return false;
            }

            // Check modifiers - accept both left and right variants
            bool ctrlRequired = (_currentModifiers & ModifierKeys.Control) != 0;
            bool shiftRequired = (_currentModifiers & ModifierKeys.Shift) != 0;
            bool altRequired = (_currentModifiers & ModifierKeys.Alt) != 0;
            bool winRequired = (_currentModifiers & ModifierKeys.Windows) != 0;

            // H.Hooks may report generic (Control/Shift) or specific (LeftCtrl/RightCtrl) keys - check all variants
            bool ctrlPressed = pressedKeys.Contains(H.Hooks.Key.LeftCtrl) || pressedKeys.Contains(H.Hooks.Key.RightCtrl) || pressedKeys.Contains(H.Hooks.Key.Control);
            bool shiftPressed = pressedKeys.Contains(H.Hooks.Key.LeftShift) || pressedKeys.Contains(H.Hooks.Key.RightShift) || pressedKeys.Contains(H.Hooks.Key.Shift);
            bool altPressed = pressedKeys.Contains(H.Hooks.Key.LeftAlt) || pressedKeys.Contains(H.Hooks.Key.RightAlt) || pressedKeys.Contains(H.Hooks.Key.Alt);
            bool winPressed = pressedKeys.Contains(H.Hooks.Key.LWin) || pressedKeys.Contains(H.Hooks.Key.RWin);

            // All required modifiers must be pressed, and no extra modifiers
            if (ctrlRequired != ctrlPressed) return false;
            if (shiftRequired != shiftPressed) return false;
            if (altRequired != altPressed) return false;
            if (winRequired != winPressed) return false;

            return true;
        }

        private bool IsModifierReleased(H.Hooks.KeyboardEventArgs args)
        {
            var releasedKey = args.CurrentKey;

            // Check if any required modifier was released (check both specific and generic variants)
            if ((_currentModifiers & ModifierKeys.Control) != 0 &&
                (releasedKey == H.Hooks.Key.LeftCtrl || releasedKey == H.Hooks.Key.RightCtrl || releasedKey == H.Hooks.Key.Control))
                return true;
            if ((_currentModifiers & ModifierKeys.Shift) != 0 &&
                (releasedKey == H.Hooks.Key.LeftShift || releasedKey == H.Hooks.Key.RightShift || releasedKey == H.Hooks.Key.Shift))
                return true;
            if ((_currentModifiers & ModifierKeys.Alt) != 0 &&
                (releasedKey == H.Hooks.Key.LeftAlt || releasedKey == H.Hooks.Key.RightAlt || releasedKey == H.Hooks.Key.Alt))
                return true;
            if ((_currentModifiers & ModifierKeys.Windows) != 0 &&
                (releasedKey == H.Hooks.Key.LWin || releasedKey == H.Hooks.Key.RWin))
                return true;

            return false;
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

        private static string FormatHotkey(ModifierKeys modifiers, Key key)
        {
            var parts = new List<string>();
            if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }

        private static void ParseHotkey(string hotkey, out Key key, out ModifierKeys modifiers)
        {
            key = Key.Q;
            modifiers = ModifierKeys.None;

            var parts = hotkey.Split('+');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                switch (trimmed.ToLower())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= ModifierKeys.Control;
                        break;
                    case "alt":
                        modifiers |= ModifierKeys.Alt;
                        break;
                    case "shift":
                        modifiers |= ModifierKeys.Shift;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= ModifierKeys.Windows;
                        break;
                    default:
                        if (Enum.TryParse<Key>(trimmed, true, out var parsed))
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
