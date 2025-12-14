using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TalkKeys.Plugins.FocusTimer
{
    /// <summary>
    /// Widget states for the Focus Timer.
    /// </summary>
    public enum WidgetState
    {
        Idle,
        Focus,
        FocusComplete,
        Break,
        BreakComplete
    }

    /// <summary>
    /// Interaction logic for FocusTimerWidget.xaml
    /// </summary>
    public partial class FocusTimerWidget : Window
    {
        private readonly FocusTimerPlugin _plugin;
        private WidgetState _currentState = WidgetState.Idle;
        private Storyboard? _pulseAnimation;

        // Colors
        private static readonly SolidColorBrush FocusColor = new(Color.FromRgb(16, 185, 129));    // #10B981 green
        private static readonly SolidColorBrush BreakColor = new(Color.FromRgb(59, 130, 246));    // #3B82F6 blue
        private static readonly SolidColorBrush IdleColor = new(Color.FromRgb(75, 85, 99));       // #4B5563 gray

        /// <summary>
        /// Event raised when widget position changes (for persistence).
        /// </summary>
        public event EventHandler<PositionChangedEventArgs>? PositionChanged;

        public FocusTimerWidget(FocusTimerPlugin plugin)
        {
            InitializeComponent();
            _plugin = plugin;
            _pulseAnimation = (Storyboard?)FindResource("PulseAnimation");

            // Position in top-right by default
            Loaded += OnLoaded;
            LocationChanged += OnLocationChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateTodayTime();
            SetState(WidgetState.Idle);
        }

        private void OnLocationChanged(object? sender, EventArgs e)
        {
            // Notify of position change for persistence
            if (IsLoaded && Left >= 0 && Top >= 0)
            {
                PositionChanged?.Invoke(this, new PositionChangedEventArgs(Left, Top));
            }
        }

        /// <summary>
        /// Position widget at specified coordinates, or default to top-right.
        /// </summary>
        public void PositionAt(double? x, double? y)
        {
            if (x.HasValue && y.HasValue && x >= 0 && y >= 0)
            {
                Left = x.Value;
                Top = y.Value;
            }
            else
            {
                // Default: top-right corner with margin
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - Width - 20;
                Top = workArea.Top + 60; // Below where FloatingWidget typically is
            }
        }

        /// <summary>
        /// Set the current widget state and update UI accordingly.
        /// </summary>
        public void SetState(WidgetState state)
        {
            _currentState = state;

            // Hide all panels first
            IdlePanel.Visibility = Visibility.Collapsed;
            ActivePanel.Visibility = Visibility.Collapsed;
            SuggestionPanel.Visibility = Visibility.Collapsed;

            // Stop any existing animation
            if (_pulseAnimation != null)
            {
                ModeIndicator.BeginAnimation(OpacityProperty, null);
            }

            switch (state)
            {
                case WidgetState.Idle:
                    IdlePanel.Visibility = Visibility.Visible;
                    break;

                case WidgetState.Focus:
                    ActivePanel.Visibility = Visibility.Visible;
                    ModeIndicator.Fill = FocusColor;
                    if (ProgressRing != null) ProgressRing.Stroke = FocusColor;
                    StartPulseAnimation();
                    break;

                case WidgetState.FocusComplete:
                    // Note: This state is no longer used in session-based flow
                    // Timer auto-transitions to break
                    break;

                case WidgetState.Break:
                    ActivePanel.Visibility = Visibility.Visible;
                    ModeIndicator.Fill = BreakColor;
                    if (ProgressRing != null) ProgressRing.Stroke = BreakColor;
                    StartPulseAnimation();
                    break;

                case WidgetState.BreakComplete:
                    // Break over - ask if user wants to continue the session
                    SuggestionPanel.Visibility = Visibility.Visible;
                    SuggestionText.Text = "Continue?";
                    SuggestionPrimaryButton.Content = "Yes";
                    SuggestionPrimaryButton.Background = FocusColor;
                    SuggestionSecondaryButton.Content = "Done";
                    break;
            }
        }

        /// <summary>
        /// Update the timer display and progress ring.
        /// </summary>
        public void UpdateTimerDisplay(TimeSpan remaining)
        {
            TimerDisplay.Text = $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}";
            UpdateProgressRing(remaining);
        }

        /// <summary>
        /// Update the progress ring based on remaining time.
        /// </summary>
        private void UpdateProgressRing(TimeSpan remaining)
        {
            if (ProgressRing == null) return;

            // Get total session duration based on current mode
            var totalDuration = _currentState == WidgetState.Focus 
                ? TimeSpan.FromMinutes(_plugin.GetFocusDuration())
                : TimeSpan.FromMinutes(_plugin.GetBreakDuration());

            // Calculate progress percentage (0-1)
            var elapsed = totalDuration - remaining;
            var progressPercentage = Math.Max(0, Math.Min(1, elapsed.TotalSeconds / totalDuration.TotalSeconds));

            // Update stroke dash offset for progress ring
            // Circumference = 2 * π * r = 2 * π * 55 ≈ 346
            var circumference = 346.0;
            var offset = circumference * (1 - progressPercentage);
            ProgressRing.StrokeDashOffset = offset;
        }

        /// <summary>
        /// Update today's focus time display.
        /// </summary>
        public void UpdateTodayTime()
        {
            var minutes = _plugin.GetTodayMinutes();
            TodayTimeDisplay.Text = $"Today: {FocusStatsService.FormatMinutes(minutes)}";
        }

        private void StartPulseAnimation()
        {
            if (_pulseAnimation != null)
            {
                _pulseAnimation.Begin(ModeIndicator, true);
            }
        }

        public void PlayNotificationSound()
        {
            try
            {
                // Use system notification sound
                System.Media.SystemSounds.Exclamation.Play();
            }
            catch
            {
                // Ignore if sound fails
            }
        }

        #region Event Handlers

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging the widget
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Update context menu based on current state
            UpdateContextMenu();
            e.Handled = true;
        }

        private void StartFocusButton_Click(object sender, RoutedEventArgs e)
        {
            _plugin.StartFocus();
        }

        private void StopButton_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _plugin.Stop();
        }

        private void CloseButton_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _plugin.HideWidget();
        }

        private void SuggestionPrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            // Only BreakComplete uses this now (session-based flow)
            // "Yes" → Continue the session with another focus period
            _plugin.StartFocus();
        }

        private void SuggestionSecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            // Only BreakComplete uses this now (session-based flow)
            // "Done" → End the session
            _plugin.Stop();
        }

        // Context Menu Handlers
        
        private void UpdateContextMenu()
        {
            // Update menu visibility based on current state
            bool isActive = _currentState == WidgetState.Focus || _currentState == WidgetState.Break;
            
            MenuPauseResume.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            MenuStop.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            ControlSeparator.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            
            // Update quick start availability
            QuickStart15.IsEnabled = !isActive;
            QuickStart25.IsEnabled = !isActive;
            QuickStart45.IsEnabled = !isActive;
            QuickStart90.IsEnabled = !isActive;
        }

        private void QuickStart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string durationStr)
            {
                if (int.TryParse(durationStr, out int minutes))
                {
                    // Temporarily override focus duration
                    var originalDuration = _plugin.GetFocusDuration();
                    _plugin.GetConfiguration().SetSetting(FocusTimerPlugin.SettingFocusDuration, minutes);
                    _plugin.StartFocus();
                    
                    // Restore original duration after starting (for next time)
                    _plugin.GetConfiguration().SetSetting(FocusTimerPlugin.SettingFocusDuration, originalDuration);
                }
            }
        }

        private void PauseResume_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement pause/resume functionality in Phase 2
            // For now, just show a notification
            System.Windows.MessageBox.Show("Pause/Resume functionality coming soon!", "Focus Timer", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _plugin.Stop();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open timer settings directly
            System.Windows.MessageBox.Show("Open Settings → Plugins → Focus Timer to configure timer settings.", 
                "Timer Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Stats_Click(object sender, RoutedEventArgs e)
        {
            var todayMinutes = _plugin.GetTodayMinutes();
            var formattedTime = FocusStatsService.FormatMinutes(todayMinutes);
            System.Windows.MessageBox.Show($"Today's Focus Time: {formattedTime}\n\nDetailed stats coming soon!", 
                "Focus Stats", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            _plugin.HideWidget();
        }

        #endregion
    }

    /// <summary>
    /// Event args for position changes.
    /// </summary>
    public class PositionChangedEventArgs : EventArgs
    {
        public double X { get; }
        public double Y { get; }

        public PositionChangedEventArgs(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
