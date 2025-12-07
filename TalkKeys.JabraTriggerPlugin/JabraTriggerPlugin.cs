using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TalkKeys.PluginSdk;

namespace TalkKeys.JabraTriggerPlugin
{
    /// <summary>
    /// Trigger plugin for Jabra Engage 50 II headset buttons.
    /// </summary>
    public class JabraTriggerPlugin : ITriggerPlugin
    {
        private const string TRIGGER_ID_THREE_DOT = "jabra:three-dot";
        private const string TRIGGER_ID_HOOK = "jabra:hook";

        private readonly KeyboardShortcutService _keyboardShortcutService;
        private JabraButtonListener? _jabraListener;
        private TriggerPluginConfiguration _configuration;
        private bool _isRunning;
        private bool _disposed;
        private bool _isAvailable;
        private string _statusMessage = "Checking...";

        // UI elements
        private ComboBox? _threeDotActionComboBox;
        private StackPanel? _threeDotShortcutPanel;
        private TextBox? _threeDotShortcutTextBox;
        private ComboBox? _hookActionComboBox;
        private StackPanel? _hookShortcutPanel;
        private TextBox? _hookShortcutTextBox;

        public string PluginId => "jabra";
        public string DisplayName => "Jabra Engage 50 II";
        public string Description => "Use the programmable buttons on your Jabra Engage 50 II headset to control recording";
        public string Icon => "🎧";
        public bool IsAvailable => _isAvailable;
        public string StatusMessage => _statusMessage;

        public event EventHandler<TriggerEventArgs>? TriggerActivated;
        public event EventHandler<TriggerEventArgs>? TriggerDeactivated;
        public event EventHandler<EventArgs>? AvailabilityChanged;

        public JabraTriggerPlugin()
        {
            _keyboardShortcutService = new KeyboardShortcutService();
            _configuration = GetDefaultConfiguration();
            CheckAvailability();
        }

        private void CheckAvailability()
        {
            _isAvailable = JabraButtonListener.IsDeviceConnected();
            _statusMessage = _isAvailable ? "Connected" : "Not detected";
        }

        public IReadOnlyList<TriggerInfo> GetAvailableTriggers()
        {
            return new List<TriggerInfo>
            {
                new TriggerInfo
                {
                    TriggerId = TRIGGER_ID_THREE_DOT,
                    DisplayName = "Three Dot Button",
                    Description = "The three-dot menu button on the headset",
                    SupportsPushToTalk = true,
                    SupportsKeyboardShortcut = true
                },
                new TriggerInfo
                {
                    TriggerId = TRIGGER_ID_HOOK,
                    DisplayName = "Hook Button",
                    Description = "The hook/answer button on the headset",
                    SupportsPushToTalk = true,
                    SupportsKeyboardShortcut = true
                }
            };
        }

        public void Initialize(TriggerPluginConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Start()
        {
            if (_isRunning || !_configuration.Enabled) return;

            CheckAvailability();
            if (!_isAvailable)
            {
                return;
            }

            try
            {
                _jabraListener = new JabraButtonListener();
                _jabraListener.ButtonPressed += OnButtonPressed;
                _jabraListener.ButtonReleased += OnButtonReleased;
                _jabraListener.Error += OnError;
                _jabraListener.Start();
                _isRunning = true;
            }
            catch (Exception)
            {
                // Failed to start - device may have been disconnected
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            if (_jabraListener != null)
            {
                _jabraListener.ButtonPressed -= OnButtonPressed;
                _jabraListener.ButtonReleased -= OnButtonReleased;
                _jabraListener.Error -= OnError;
                _jabraListener.Stop();
                _jabraListener.Dispose();
                _jabraListener = null;
            }
            _isRunning = false;
        }

        public void UpdateConfiguration(TriggerPluginConfiguration configuration)
        {
            _configuration = configuration;
            // No restart needed - configuration is read on each button press
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
                        TriggerId = TRIGGER_ID_THREE_DOT,
                        DisplayName = "Three Dot Button",
                        Enabled = true,
                        Action = RecordingTriggerAction.ToggleRecording
                    },
                    new TriggerConfiguration
                    {
                        TriggerId = TRIGGER_ID_HOOK,
                        DisplayName = "Hook Button",
                        Enabled = true,
                        Action = RecordingTriggerAction.Disabled
                    }
                },
                Settings = new Dictionary<string, object>
                {
                    ["AutoSelectAudioDevice"] = true
                }
            };
        }

        public FrameworkElement CreateSettingsPanel()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            // Header with status
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            headerPanel.Children.Add(new TextBlock
            {
                Text = "Jabra Engage 50 II Settings",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937")),
                VerticalAlignment = VerticalAlignment.Center
            });
            var statusText = new TextBlock
            {
                Text = _statusMessage,
                FontSize = 12,
                Margin = new Thickness(15, 0, 0, 0),
                Foreground = new SolidColorBrush(_isAvailable
                    ? (Color)ColorConverter.ConvertFromString("#10B981")
                    : (Color)ColorConverter.ConvertFromString("#9CA3AF")),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(statusText);
            panel.Children.Add(headerPanel);

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
                Text = "Enable Jabra button triggers",
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(enablePanel);

            // Auto-select audio device
            var autoSelectPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            var autoSelectEnabled = _configuration.Settings.TryGetValue("AutoSelectAudioDevice", out var autoSelectObj)
                && autoSelectObj is bool autoSelect && autoSelect;
            var autoSelectCheckBox = new CheckBox
            {
                IsChecked = autoSelectEnabled,
                VerticalAlignment = VerticalAlignment.Center
            };
            autoSelectCheckBox.Checked += (s, e) => { _configuration.Settings["AutoSelectAudioDevice"] = true; OnConfigurationChanged(); };
            autoSelectCheckBox.Unchecked += (s, e) => { _configuration.Settings["AutoSelectAudioDevice"] = false; OnConfigurationChanged(); };
            autoSelectPanel.Children.Add(autoSelectCheckBox);
            autoSelectPanel.Children.Add(new TextBlock
            {
                Text = "Auto-select Jabra as audio input device",
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(autoSelectPanel);

            // Three Dot Button section
            panel.Children.Add(CreateButtonSection(
                "Three Dot Button",
                TRIGGER_ID_THREE_DOT,
                ref _threeDotActionComboBox,
                ref _threeDotShortcutPanel,
                ref _threeDotShortcutTextBox));

            // Hook Button section
            panel.Children.Add(CreateButtonSection(
                "Hook Button",
                TRIGGER_ID_HOOK,
                ref _hookActionComboBox,
                ref _hookShortcutPanel,
                ref _hookShortcutTextBox));

            return panel;
        }

        private FrameworkElement CreateButtonSection(
            string title,
            string triggerId,
            ref ComboBox? actionComboBox,
            ref StackPanel? shortcutPanel,
            ref TextBox? shortcutTextBox)
        {
            var section = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var content = new StackPanel();

            // Title
            content.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Action combo box
            content.Children.Add(new TextBlock
            {
                Text = "Action",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var triggerConfig = GetTriggerConfig(triggerId);
            actionComboBox = new ComboBox
            {
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 10),
                Tag = triggerId
            };
            actionComboBox.Items.Add(new ComboBoxItem { Content = "Disabled", Tag = RecordingTriggerAction.Disabled });
            actionComboBox.Items.Add(new ComboBoxItem { Content = "TalkKeys - Toggle Recording", Tag = RecordingTriggerAction.ToggleRecording });
            actionComboBox.Items.Add(new ComboBoxItem { Content = "TalkKeys - Push to Talk", Tag = RecordingTriggerAction.PushToTalk });
            actionComboBox.Items.Add(new ComboBoxItem { Content = "Keyboard Shortcut", Tag = RecordingTriggerAction.KeyboardShortcut });

            var currentAction = triggerConfig?.Action ?? RecordingTriggerAction.Disabled;
            actionComboBox.SelectedIndex = currentAction switch
            {
                RecordingTriggerAction.Disabled => 0,
                RecordingTriggerAction.ToggleRecording => 1,
                RecordingTriggerAction.PushToTalk => 2,
                RecordingTriggerAction.KeyboardShortcut => 3,
                _ => 0
            };

            // Store refs for closure
            var localShortcutPanel = shortcutPanel = new StackPanel
            {
                Visibility = currentAction == RecordingTriggerAction.KeyboardShortcut ? Visibility.Visible : Visibility.Collapsed
            };
            var localActionComboBox = actionComboBox;
            var localTriggerId = triggerId;

            actionComboBox.SelectionChanged += (s, e) =>
            {
                if (localActionComboBox.SelectedItem is ComboBoxItem item && item.Tag is RecordingTriggerAction action)
                {
                    var config = GetTriggerConfig(localTriggerId);
                    if (config != null)
                    {
                        config.Action = action;
                        localShortcutPanel.Visibility = action == RecordingTriggerAction.KeyboardShortcut
                            ? Visibility.Visible : Visibility.Collapsed;
                        OnConfigurationChanged();
                    }
                }
            };

            content.Children.Add(actionComboBox);

            // Shortcut panel (conditionally visible)
            shortcutPanel.Children.Add(new TextBlock
            {
                Text = "Keyboard Shortcut",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var localShortcutTextBox = shortcutTextBox = new TextBox
            {
                Text = triggerConfig?.KeyboardShortcut ?? "Click to set shortcut...",
                IsReadOnly = true,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = triggerId
            };

            shortcutTextBox.GotFocus += (s, e) =>
            {
                localShortcutTextBox.Text = "Press keys...";
                localShortcutTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
            };

            shortcutTextBox.LostFocus += (s, e) =>
            {
                localShortcutTextBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB"));
                if (localShortcutTextBox.Text == "Press keys...")
                {
                    var config = GetTriggerConfig(localTriggerId);
                    localShortcutTextBox.Text = string.IsNullOrEmpty(config?.KeyboardShortcut)
                        ? "Click to set shortcut..."
                        : config.KeyboardShortcut;
                }
            };

            shortcutTextBox.PreviewKeyDown += (s, e) => HandleShortcutKeyDown(localShortcutTextBox, localTriggerId, e);

            shortcutPanel.Children.Add(shortcutTextBox);
            content.Children.Add(shortcutPanel);

            section.Child = content;
            return section;
        }

        private void HandleShortcutKeyDown(TextBox textBox, string triggerId, KeyEventArgs e)
        {
            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                System.Windows.Input.Keyboard.ClearFocus();
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore modifier-only presses
            if (KeyboardShortcutService.IsValidMainKey(key) == false)
                return;

            var shortcut = KeyboardShortcutService.KeyToShortcutString(key, System.Windows.Input.Keyboard.Modifiers);

            if (!string.IsNullOrEmpty(shortcut))
            {
                textBox.Text = shortcut;
                var config = GetTriggerConfig(triggerId);
                if (config != null)
                {
                    config.KeyboardShortcut = shortcut;
                    OnConfigurationChanged();
                }
                System.Windows.Input.Keyboard.ClearFocus();
            }
        }

        private TriggerConfiguration? GetTriggerConfig(string triggerId)
        {
            return _configuration.Triggers.Find(t => t.TriggerId == triggerId);
        }

        private void OnButtonPressed(object? sender, JabraButtonEventArgs e)
        {
            var triggerId = e.ButtonId switch
            {
                JabraButtonListener.ButtonIds.ThreeDot => TRIGGER_ID_THREE_DOT,
                JabraButtonListener.ButtonIds.HookIcon => TRIGGER_ID_HOOK,
                _ => null
            };

            if (triggerId == null)
            {
                return;
            }

            var config = GetTriggerConfig(triggerId);
            if (config == null || !config.Enabled || config.Action == RecordingTriggerAction.Disabled)
            {
                return;
            }

            // Handle keyboard shortcut action locally
            if (config.Action == RecordingTriggerAction.KeyboardShortcut)
            {
                if (!string.IsNullOrEmpty(config.KeyboardShortcut))
                {
                    _keyboardShortcutService.SendShortcut(config.KeyboardShortcut);
                }
                return;
            }

            // Raise event for other actions
            TriggerActivated?.Invoke(this, new TriggerEventArgs(triggerId, config.Action, config.KeyboardShortcut));
        }

        private void OnButtonReleased(object? sender, JabraButtonEventArgs e)
        {
            var triggerId = e.ButtonId switch
            {
                JabraButtonListener.ButtonIds.ThreeDot => TRIGGER_ID_THREE_DOT,
                JabraButtonListener.ButtonIds.HookIcon => TRIGGER_ID_HOOK,
                _ => null
            };

            if (triggerId == null) return;

            var config = GetTriggerConfig(triggerId);
            if (config == null || !config.Enabled) return;

            // Only fire deactivated for push-to-talk mode
            if (config.Action == RecordingTriggerAction.PushToTalk)
            {
                TriggerDeactivated?.Invoke(this, new TriggerEventArgs(triggerId, config.Action));
            }
        }

        private void OnError(object? sender, Exception e)
        {
            _isAvailable = false;
            _statusMessage = "Disconnected";
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnConfigurationChanged()
        {
            // Restart if needed
            if (_isRunning && !_configuration.Enabled)
            {
                Stop();
            }
            else if (!_isRunning && _configuration.Enabled)
            {
                Start();
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
