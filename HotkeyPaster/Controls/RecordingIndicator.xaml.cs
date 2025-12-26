using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TalkKeys.Controls
{
    /// <summary>
    /// Recording state indicator with pulsing dot, timer, and audio level visualization.
    /// Reusable control for FloatingWidget and ExplainerPopup.
    /// </summary>
    public partial class RecordingIndicator : UserControl
    {
        #region State Colors

        private static readonly Color RecordingInner = Color.FromRgb(239, 68, 68);   // #EF4444
        private static readonly Color RecordingOuter = Color.FromRgb(220, 38, 38);   // #DC2626
        private static readonly Color ProcessingInner = Color.FromRgb(139, 92, 246); // #8B5CF6
        private static readonly Color ProcessingOuter = Color.FromRgb(124, 58, 237); // #7C3AED
        private static readonly Color SuccessInner = Color.FromRgb(34, 197, 94);     // #22C55E
        private static readonly Color SuccessOuter = Color.FromRgb(22, 163, 74);     // #16A34A
        private static readonly Color ErrorInner = Color.FromRgb(239, 68, 68);       // #EF4444
        private static readonly Color ErrorOuter = Color.FromRgb(185, 28, 28);       // #B91C1C
        private static readonly Color IdleInner = Color.FromRgb(107, 114, 128);      // #6B7280
        private static readonly Color IdleOuter = Color.FromRgb(75, 85, 99);         // #4B5563

        private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(75, 85, 99));    // #4B5563
        private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(16, 185, 129)); // #10B981
        private static readonly SolidColorBrush PurpleBrush = new(Color.FromRgb(139, 92, 246)); // #8B5CF6
        private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(239, 68, 68));    // #EF4444

        #endregion

        private Border[]? _levelBars;
        private System.Windows.Threading.DispatcherTimer? _timer;
        private DateTime _startTime;
        private Storyboard? _pulseAnimation;
        private readonly Random _random = new();
        private readonly double[] _baseHeights = { 6, 10, 5, 12, 8 };

        public RecordingIndicator()
        {
            InitializeComponent();
            _levelBars = new Border[] { LevelBar1, LevelBar2, LevelBar3, LevelBar4, LevelBar5 };
            _pulseAnimation = (Storyboard)Resources["PulseAnimation"];

            // Initialize timer
            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _timer.Tick += OnTimerTick;
        }

        #region Dependency Properties

        public static readonly DependencyProperty ShowTimerProperty =
            DependencyProperty.Register(nameof(ShowTimer), typeof(bool), typeof(RecordingIndicator),
                new PropertyMetadata(true, OnShowTimerChanged));

        public bool ShowTimer
        {
            get => (bool)GetValue(ShowTimerProperty);
            set => SetValue(ShowTimerProperty, value);
        }

        private static void OnShowTimerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RecordingIndicator indicator)
            {
                indicator.TimerText.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Start recording state - red pulsing dot, timer starts.
        /// </summary>
        public void StartRecording()
        {
            SetDotColor(RecordingInner, RecordingOuter);
            ResetBarsToGray();

            _startTime = DateTime.Now;
            TimerText.Text = "0:00";
            _timer?.Start();

            StatusDot.Opacity = 1;
            _pulseAnimation?.Begin(this);
        }

        /// <summary>
        /// Stop recording and show processing state - purple dot.
        /// </summary>
        public void ShowProcessing()
        {
            _timer?.Stop();
            _pulseAnimation?.Stop(this);
            StatusDot.Opacity = 1;

            SetDotColor(ProcessingInner, ProcessingOuter);
            SetBarsColor(PurpleBrush);
        }

        /// <summary>
        /// Show success state - green dot.
        /// </summary>
        public void ShowSuccess()
        {
            _timer?.Stop();
            _pulseAnimation?.Stop(this);
            StatusDot.Opacity = 1;

            SetDotColor(SuccessInner, SuccessOuter);
            SetBarsColor(GreenBrush);
        }

        /// <summary>
        /// Show error state - red dot.
        /// </summary>
        public void ShowError()
        {
            _timer?.Stop();
            _pulseAnimation?.Stop(this);
            StatusDot.Opacity = 1;

            SetDotColor(ErrorInner, ErrorOuter);
            SetBarsColor(RedBrush);
        }

        /// <summary>
        /// Reset to idle state.
        /// </summary>
        public void Reset()
        {
            _timer?.Stop();
            _pulseAnimation?.Stop(this);
            StatusDot.Opacity = 1;

            SetDotColor(IdleInner, IdleOuter);
            ResetBarsToGray();
            TimerText.Text = "0:00";
        }

        /// <summary>
        /// Update audio level visualization during recording.
        /// </summary>
        /// <param name="level">Audio level from 0.0 to 1.0</param>
        public void UpdateAudioLevel(double level)
        {
            if (_levelBars == null) return;

            for (int i = 0; i < _levelBars.Length; i++)
            {
                // Scale bar height based on audio level with some randomness
                double variation = _random.NextDouble() * 3 - 1.5;
                double scaledHeight = _baseHeights[i] * (0.3 + level * 0.7) + variation;
                _levelBars[i].Height = Math.Max(3, Math.Min(16, scaledHeight));

                // Color bars based on level threshold
                _levelBars[i].Background = level > 0.1 ? GreenBrush : GrayBrush;
            }
        }

        /// <summary>
        /// Get the current elapsed time.
        /// </summary>
        public TimeSpan GetElapsedTime()
        {
            return DateTime.Now - _startTime;
        }

        #endregion

        #region Private Methods

        private void OnTimerTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _startTime;
            TimerText.Text = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
        }

        private void SetDotColor(Color inner, Color outer)
        {
            DotColorInner.Color = inner;
            DotColorOuter.Color = outer;
        }

        private void SetBarsColor(SolidColorBrush brush)
        {
            if (_levelBars == null) return;
            foreach (var bar in _levelBars)
            {
                bar.Background = brush;
            }
        }

        private void ResetBarsToGray()
        {
            if (_levelBars == null) return;
            for (int i = 0; i < _levelBars.Length; i++)
            {
                _levelBars[i].Background = GrayBrush;
                _levelBars[i].Height = _baseHeights[i];
            }
        }

        #endregion
    }
}
