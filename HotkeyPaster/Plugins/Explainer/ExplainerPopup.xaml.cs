using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TalkKeys.Plugins.Explainer
{
    /// <summary>
    /// Popup window that displays insights (WTF, Plain) in a unified vertical layout.
    /// </summary>
    public partial class ExplainerPopup : Window
    {
        private DispatcherTimer? _dismissTimer;
        private int _remainingSeconds;
        private Action<string>? _logAction;
        private int _autoDismissSeconds = 20;

        public ExplainerPopup(Action<string>? logAction = null)
        {
            _logAction = logAction;
            Log("ExplainerPopup constructor called");
            InitializeComponent();
            Opacity = 0;
            Log("ExplainerPopup initialized, Opacity=0");
        }

        private void Log(string message)
        {
            _logAction?.Invoke($"[ExplainerPopup] {message}");
            Debug.WriteLine($"[ExplainerPopup] {message}");
        }

        /// <summary>
        /// Shows loading state while fetching all content.
        /// </summary>
        public void ShowLoading()
        {
            Log("ShowLoading called");
            _dismissTimer?.Stop();

            LoadingPanel.Visibility = Visibility.Visible;
            ContentPanel.Visibility = Visibility.Collapsed;
            ErrorBorder.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Sets all content at once - WTF and Plain.
        /// </summary>
        public void SetAllContent(
            string wtfText,
            string plainText,
            int autoDismissSeconds = 20)
        {
            Log($"SetAllContent called: wtf={wtfText.Length}chars, plain={plainText.Length}chars");

            _dismissTimer?.Stop();
            _autoDismissSeconds = autoDismissSeconds;

            // Hide loading, show content
            LoadingPanel.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Visible;
            ErrorBorder.Visibility = Visibility.Collapsed;

            // Set WTF text
            WtfText.Text = wtfText;

            // Set Plain text
            PlainText.Text = plainText;

            // Start dismiss timer if popup is visible
            if (IsVisible)
            {
                StartDismissTimer(_autoDismissSeconds);
            }
        }

        /// <summary>
        /// Shows an error message.
        /// </summary>
        public void ShowError(string message)
        {
            Log($"ShowError: {message}");
            _dismissTimer?.Stop();
            _autoDismissSeconds = 5;

            LoadingPanel.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Collapsed;
            ErrorBorder.Visibility = Visibility.Visible;
            ErrorText.Text = message;
        }

        /// <summary>
        /// Shows the popup with fade-in animation.
        /// </summary>
        public void ShowWithAnimation()
        {
            Log($"ShowWithAnimation called. Left={Left}, Top={Top}");

            Show();
            Log($"Show() done. IsVisible={IsVisible}, Opacity={Opacity}");

            Activate();
            Log("Activate() done");

            // Fade in
            var fadeIn = (Storyboard)Resources["FadeIn"];
            fadeIn.Begin(this);
            Log("FadeIn animation started");

            // Start auto-dismiss countdown (unless still loading)
            if (LoadingPanel.Visibility != Visibility.Visible)
            {
                StartDismissTimer(_autoDismissSeconds);
                Log($"Dismiss timer started: {_autoDismissSeconds}s");
            }
        }

        /// <summary>
        /// Positions the popup near the cursor.
        /// </summary>
        public void PositionNearCursor(Point cursorPos)
        {
            Log($"PositionNearCursor: raw cursor = ({cursorPos.X},{cursorPos.Y})");

            // Get DPI scale
            var dpiScale = GetDpiScale();
            Log($"DPI scale = {dpiScale}");

            // Convert screen coordinates to WPF units
            double x = cursorPos.X / dpiScale;
            double y = cursorPos.Y / dpiScale;
            Log($"After DPI conversion: ({x},{y})");

            // Offset from cursor
            x += 15;
            y += 15;

            // Get the screen containing the cursor
            var screen = Screen.FromPoint(new System.Drawing.Point((int)cursorPos.X, (int)cursorPos.Y));
            Log($"Screen: {screen.DeviceName}, Bounds={screen.Bounds}, WorkingArea={screen.WorkingArea}");

            var workArea = new Rect(
                screen.WorkingArea.Left / dpiScale,
                screen.WorkingArea.Top / dpiScale,
                screen.WorkingArea.Width / dpiScale,
                screen.WorkingArea.Height / dpiScale);
            Log($"WorkArea (WPF units): {workArea}");

            // Ensure window fits on screen
            UpdateLayout();
            Log($"Window size after UpdateLayout: {ActualWidth}x{ActualHeight}");

            if (x + ActualWidth > workArea.Right)
            {
                x = workArea.Right - ActualWidth - 10;
                Log($"Adjusted X to {x} (was beyond right edge)");
            }
            if (x < workArea.Left)
            {
                x = workArea.Left + 10;
                Log($"Adjusted X to {x} (was beyond left edge)");
            }

            if (y + ActualHeight > workArea.Bottom)
            {
                y = cursorPos.Y / dpiScale - ActualHeight - 15; // Show above cursor
                Log($"Adjusted Y to {y} (was beyond bottom edge)");
            }
            if (y < workArea.Top)
            {
                y = workArea.Top + 10;
                Log($"Adjusted Y to {y} (was beyond top edge)");
            }

            Left = x;
            Top = y;
            Log($"Final position: Left={Left}, Top={Top}");
        }

        private double GetDpiScale()
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformToDevice.M11;
            }

            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    var src = PresentationSource.FromVisual(this);
                    return src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                }
            }
            catch { }

            return 1.0;
        }

        private void StartDismissTimer(int seconds)
        {
            _dismissTimer?.Stop();
            _remainingSeconds = seconds;
            UpdateCountdown();

            _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _dismissTimer.Tick += OnDismissTimerTick;
            _dismissTimer.Start();
        }

        private void OnDismissTimerTick(object? sender, EventArgs e)
        {
            _remainingSeconds--;
            UpdateCountdown();

            if (_remainingSeconds <= 0)
            {
                _dismissTimer?.Stop();
                CloseWithFade();
            }
        }

        private void UpdateCountdown()
        {
            CountdownText.Text = $"{_remainingSeconds}s";
        }

        private void CloseWithFade()
        {
            var fadeOut = (Storyboard)Resources["FadeOut"];
            fadeOut.Completed += (s, e) => Close();
            fadeOut.Begin(this);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Left-click to drag the window
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
            // Right-click or middle-click to dismiss
            else if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle)
            {
                _dismissTimer?.Stop();
                CloseWithFade();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _dismissTimer?.Stop();
            CloseWithFade();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Escape to dismiss
            if (e.Key == Key.Escape)
            {
                _dismissTimer?.Stop();
                CloseWithFade();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _dismissTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
