using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TalkKeys.Logging;
using TalkKeys.PluginSdk;

namespace TalkKeys.Plugins.FocusTimer
{
    /// <summary>
    /// Timer mode states.
    /// </summary>
    public enum TimerMode
    {
        Idle,
        Focus,
        Break
    }

    /// <summary>
    /// Focus Timer plugin - Pomodoro-style productivity timer.
    /// </summary>
    public class FocusTimerPlugin : IWidgetPlugin, ITrayMenuPlugin
    {
        private readonly ILogger? _logger;
        private readonly FocusStatsService _statsService;
        private FocusTimerWidget? _widget;
        private PluginConfiguration _configuration;
        private DispatcherTimer? _timer;

        // Timer state
        private TimerMode _currentMode = TimerMode.Idle;
        private TimeSpan _remaining;
        private DateTime _sessionStartTime;
        private int _sessionMinutesCompleted;

        // Settings keys
        public const string SettingFocusDuration = "FocusDuration";
        public const string SettingBreakDuration = "BreakDuration";
        public const string SettingPlaySound = "PlaySound";

        // Default values
        private const int DefaultFocusDuration = 25;
        private const int DefaultBreakDuration = 5;

        #region IPlugin Implementation

        public string PluginId => "focus-timer";
        public string DisplayName => "Focus Timer";
        public string Description => "Pomodoro-style focus timer with 25-minute sessions and break reminders";
        public string Icon => "⏱️";
        public Version Version => new(1, 0, 0);

        public bool IsWidgetVisible => _widget?.IsVisible ?? false;

        public event EventHandler<WidgetPositionChangedEventArgs>? WidgetPositionChanged;
        public event EventHandler<WidgetVisibilityChangedEventArgs>? WidgetVisibilityChanged;
        public event EventHandler? TrayMenuItemsChanged;

        public FocusTimerPlugin(ILogger? logger = null)
        {
            _logger = logger;
            _statsService = new FocusStatsService(logger);
            _configuration = GetDefaultConfiguration();
        }

        public void Initialize(PluginConfiguration configuration)
        {
            _configuration = configuration;

            // Initialize timer
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTimerTick;

            _logger?.Log($"[FocusTimer] Initialized. Focus: {GetFocusDuration()}min, Break: {GetBreakDuration()}min");
        }

        public void Activate()
        {
            _logger?.Log("[FocusTimer] Activated");
            // Widget is shown by PluginManager.ActivateAll() if WidgetVisible is true
        }

        public void Deactivate()
        {
            _timer?.Stop();
            _currentMode = TimerMode.Idle;
            _logger?.Log("[FocusTimer] Deactivated");
        }

        public PluginConfiguration GetConfiguration()
        {
            return _configuration;
        }

        public PluginConfiguration GetDefaultConfiguration()
        {
            return new PluginConfiguration
            {
                PluginId = PluginId,
                Enabled = false, // Disabled by default
                WidgetVisible = true,
                WidgetX = -1,
                WidgetY = -1,
                Settings = new Dictionary<string, object>
                {
                    [SettingFocusDuration] = DefaultFocusDuration,
                    [SettingBreakDuration] = DefaultBreakDuration,
                    [SettingPlaySound] = true
                }
            };
        }

        public FrameworkElement? CreateSettingsPanel()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            // Header
            panel.Children.Add(new TextBlock
            {
                Text = $"{Icon} Focus Timer Settings",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 15)
            });

            // Preset label
            panel.Children.Add(new TextBlock
            {
                Text = "Choose a Pomodoro preset:",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Presets: (name, focus minutes, break minutes, description)
            var presets = new (string Name, int Focus, int Break, string Desc)[]
            {
                ("Classic", 25, 5, "Standard Pomodoro"),
                ("Short", 15, 3, "Quick tasks"),
                ("Extended", 50, 10, "Deep work"),
                ("Deep Work", 90, 20, "Long sessions"),
            };

            // Current selection indicator
            var currentFocus = GetFocusDuration();
            var currentBreak = GetBreakDuration();
            var currentLabel = new TextBlock
            {
                Text = $"Current: {currentFocus} min focus / {currentBreak} min break",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)), // Green
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 0, 0, 12)
            };
            panel.Children.Add(currentLabel);

            // Preset buttons in a wrap panel
            var buttonPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 15) };

            foreach (var preset in presets)
            {
                var isSelected = currentFocus == preset.Focus && currentBreak == preset.Break;
                var button = new Button
                {
                    Margin = new Thickness(0, 0, 8, 8),
                    Padding = new Thickness(12, 8, 12, 8),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = isSelected
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)) // Green when selected
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55, 65, 81)),   // Gray otherwise
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    Content = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock
                            {
                                Text = preset.Name,
                                FontWeight = FontWeights.SemiBold,
                                FontSize = 12,
                                HorizontalAlignment = HorizontalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = $"{preset.Focus}/{preset.Break} min",
                                FontSize = 11,
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                                HorizontalAlignment = HorizontalAlignment.Center
                            }
                        }
                    }
                };

                // Round corners via template
                button.Style = CreatePresetButtonStyle(isSelected);

                var capturedPreset = preset;
                button.Click += (s, e) =>
                {
                    _configuration.SetSetting(SettingFocusDuration, capturedPreset.Focus);
                    _configuration.SetSetting(SettingBreakDuration, capturedPreset.Break);
                    currentLabel.Text = $"Current: {capturedPreset.Focus} min focus / {capturedPreset.Break} min break";

                    // Update all button styles
                    foreach (Button btn in buttonPanel.Children)
                    {
                        var btnPreset = presets[buttonPanel.Children.IndexOf(btn)];
                        var nowSelected = btnPreset.Focus == capturedPreset.Focus && btnPreset.Break == capturedPreset.Break;
                        btn.Style = CreatePresetButtonStyle(nowSelected);
                        btn.Background = nowSelected
                            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129))
                            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55, 65, 81));
                    }
                };

                buttonPanel.Children.Add(button);
            }
            panel.Children.Add(buttonPanel);

            // Play sound checkbox
            var soundCheckBox = new CheckBox
            {
                Content = "Play notification sound",
                IsChecked = _configuration.GetSetting(SettingPlaySound, true),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)),
                Margin = new Thickness(0, 5, 0, 0)
            };
            soundCheckBox.Checked += (s, e) => _configuration.SetSetting(SettingPlaySound, true);
            soundCheckBox.Unchecked += (s, e) => _configuration.SetSetting(SettingPlaySound, false);
            panel.Children.Add(soundCheckBox);

            return panel;
        }

        private static Style CreatePresetButtonStyle(bool isSelected)
        {
            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

            var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentPresenterFactory);
            template.VisualTree = borderFactory;

            // Hover trigger
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty,
                new System.Windows.Media.SolidColorBrush(isSelected
                    ? System.Windows.Media.Color.FromRgb(5, 150, 105)   // Darker green on hover when selected
                    : System.Windows.Media.Color.FromRgb(75, 85, 99)))); // Lighter gray on hover
            template.Triggers.Add(hoverTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        public void Dispose()
        {
            _timer?.Stop();
            _widget?.Close();
        }

        #endregion

        #region IWidgetPlugin Implementation

        public void ShowWidget()
        {
            _logger?.Log($"[FocusTimer] ShowWidget called");

            if (_widget == null)
            {
                _logger?.Log($"[FocusTimer] Creating new FocusTimerWidget");
                _widget = new FocusTimerWidget(this);
                _widget.PositionChanged += OnWidgetPositionChanged;
                _widget.Closed += OnWidgetClosed;
            }

            // Position widget before showing (using saved coordinates or default)
            double? x = _configuration.WidgetX >= 0 ? _configuration.WidgetX : null;
            double? y = _configuration.WidgetY >= 0 ? _configuration.WidgetY : null;
            _logger?.Log($"[FocusTimer] Positioning at x={x}, y={y} (config: {_configuration.WidgetX}, {_configuration.WidgetY})");
            _widget.PositionAt(x, y);

            _logger?.Log($"[FocusTimer] Calling Show()");
            _widget.Show();
            _widget.UpdateTodayTime();
            _logger?.Log($"[FocusTimer] Widget shown, IsVisible={_widget.IsVisible}");
            WidgetVisibilityChanged?.Invoke(this, new WidgetVisibilityChangedEventArgs(true));
        }

        public void HideWidget()
        {
            _widget?.Hide();
            WidgetVisibilityChanged?.Invoke(this, new WidgetVisibilityChangedEventArgs(false));
        }

        public void ToggleWidget()
        {
            if (IsWidgetVisible)
                HideWidget();
            else
                ShowWidget();
        }

        public void PositionWidget(double? x, double? y)
        {
            if (_widget != null)
            {
                _widget.PositionAt(x, y);
            }
        }

        private void OnWidgetPositionChanged(object? sender, PositionChangedEventArgs e)
        {
            _configuration.WidgetX = e.X;
            _configuration.WidgetY = e.Y;
            WidgetPositionChanged?.Invoke(this, new WidgetPositionChangedEventArgs(e.X, e.Y));
        }

        private void OnWidgetClosed(object? sender, EventArgs e)
        {
            _widget = null;
            WidgetVisibilityChanged?.Invoke(this, new WidgetVisibilityChangedEventArgs(false));
        }

        #endregion

        #region ITrayMenuPlugin Implementation

        public IReadOnlyList<PluginMenuItem> GetTrayMenuItems()
        {
            var items = new List<PluginMenuItem>();

            // Show/Hide Widget
            items.Add(new PluginMenuItem
            {
                Text = IsWidgetVisible ? "Hide Focus Timer" : "Show Focus Timer",
                Icon = Icon,
                OnClick = ToggleWidget
            });

            // Timer-specific items based on state (session-based flow)
            switch (_currentMode)
            {
                case TimerMode.Idle:
                    items.Add(new PluginMenuItem
                    {
                        Text = $"Start Session ({GetFocusDuration()} min focus)",
                        OnClick = StartFocus
                    });
                    break;

                case TimerMode.Focus:
                    items.Add(new PluginMenuItem
                    {
                        Text = $"Focusing: {FormatTimeRemaining()}",
                        IsEnabled = false
                    });
                    items.Add(new PluginMenuItem
                    {
                        Text = "End Session",
                        OnClick = Stop
                    });
                    break;

                case TimerMode.Break:
                    items.Add(new PluginMenuItem
                    {
                        Text = $"Break: {FormatTimeRemaining()}",
                        IsEnabled = false
                    });
                    items.Add(new PluginMenuItem
                    {
                        Text = "End Session",
                        OnClick = Stop
                    });
                    break;
            }

            // Today's stats
            var todayMinutes = GetTodayMinutes();
            if (todayMinutes > 0)
            {
                items.Add(new PluginMenuItem { IsSeparator = true });
                items.Add(new PluginMenuItem
                {
                    Text = $"Today: {FocusStatsService.FormatMinutes(todayMinutes)}",
                    IsEnabled = false
                });
            }

            return items;
        }

        #endregion

        #region Timer Control Methods

        public void StartFocus()
        {
            _currentMode = TimerMode.Focus;
            _remaining = TimeSpan.FromMinutes(GetFocusDuration());
            _sessionStartTime = DateTime.Now;
            _sessionMinutesCompleted = 0;

            _timer?.Start();
            UpdateWidgetState();
            TrayMenuItemsChanged?.Invoke(this, EventArgs.Empty);

            _logger?.Log($"[FocusTimer] Session started - focus period ({GetFocusDuration()} min)");
        }

        public void StartBreak()
        {
            // Save any completed focus time before break
            SaveCompletedFocusTime();

            _currentMode = TimerMode.Break;
            _remaining = TimeSpan.FromMinutes(GetBreakDuration());

            _timer?.Start();
            UpdateWidgetState();
            TrayMenuItemsChanged?.Invoke(this, EventArgs.Empty);

            _logger?.Log($"[FocusTimer] Break started ({GetBreakDuration()} min)");
        }

        public void Stop()
        {
            // Don't save incomplete sessions
            _timer?.Stop();
            _currentMode = TimerMode.Idle;
            _remaining = TimeSpan.Zero;

            UpdateWidgetState();
            TrayMenuItemsChanged?.Invoke(this, EventArgs.Empty);

            _logger?.Log("[FocusTimer] Session ended");
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            _remaining = _remaining.Subtract(TimeSpan.FromSeconds(1));

            // Track completed minutes during focus
            if (_currentMode == TimerMode.Focus)
            {
                var elapsedMinutes = (int)(DateTime.Now - _sessionStartTime).TotalMinutes;
                if (elapsedMinutes > _sessionMinutesCompleted)
                {
                    _sessionMinutesCompleted = elapsedMinutes;
                }
            }

            if (_remaining <= TimeSpan.Zero)
            {
                _timer?.Stop();
                OnTimerComplete();
            }
            else
            {
                _widget?.UpdateTimerDisplay(_remaining);
            }
        }

        private void OnTimerComplete()
        {
            if (_currentMode == TimerMode.Focus)
            {
                // Save completed focus time
                SaveCompletedFocusTime();
                _widget?.UpdateTodayTime();

                _logger?.Log("[FocusTimer] Focus session complete - auto-starting break");

                // Opinionated: Automatically start break (no prompt)
                // Play notification sound before break
                _widget?.PlayNotificationSound();

                // Auto-transition to break
                _currentMode = TimerMode.Break;
                _remaining = TimeSpan.FromMinutes(GetBreakDuration());
                _timer?.Start();
                UpdateWidgetState();
                TrayMenuItemsChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (_currentMode == TimerMode.Break)
            {
                // Break is over - ask if user wants to continue
                _widget?.SetState(WidgetState.BreakComplete);
                _widget?.PlayNotificationSound();

                _logger?.Log("[FocusTimer] Break complete");
                _currentMode = TimerMode.Idle;
                TrayMenuItemsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SaveCompletedFocusTime()
        {
            if (_sessionMinutesCompleted > 0 || _currentMode == TimerMode.Focus)
            {
                // Calculate actual minutes completed
                var focusDuration = GetFocusDuration();
                var minutesToSave = _remaining <= TimeSpan.Zero
                    ? focusDuration // Full session completed
                    : _sessionMinutesCompleted; // Partial session

                if (minutesToSave > 0)
                {
                    _statsService.AddFocusMinutes(minutesToSave);
                    _sessionMinutesCompleted = 0;
                }
            }
        }

        private void UpdateWidgetState()
        {
            if (_widget == null) return;

            switch (_currentMode)
            {
                case TimerMode.Idle:
                    _widget.SetState(WidgetState.Idle);
                    break;
                case TimerMode.Focus:
                    _widget.SetState(WidgetState.Focus);
                    _widget.UpdateTimerDisplay(_remaining);
                    break;
                case TimerMode.Break:
                    _widget.SetState(WidgetState.Break);
                    _widget.UpdateTimerDisplay(_remaining);
                    break;
            }

            _widget.UpdateTodayTime();
        }

        #endregion

        #region Helper Methods

        public int GetFocusDuration() => _configuration.GetSetting(SettingFocusDuration, DefaultFocusDuration);
        public int GetBreakDuration() => _configuration.GetSetting(SettingBreakDuration, DefaultBreakDuration);
        public int GetTodayMinutes() => _statsService.GetTodayMinutes();

        private string FormatTimeRemaining()
        {
            return $"{(int)_remaining.TotalMinutes}:{_remaining.Seconds:D2}";
        }

        #endregion
    }
}
