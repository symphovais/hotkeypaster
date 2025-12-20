using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TalkKeys.Plugins.Explainer
{
    /// <summary>
    /// Small popup window that displays explanation text near the cursor.
    /// </summary>
    public partial class ExplainerPopup : Window
    {
        private DispatcherTimer? _dismissTimer;
        private int _remainingSeconds;
        private Action<string>? _logAction;

        // Toggle state
        private string? _wtfText;
        private string? _plainText;
        private bool _showingWtf = true;
        private bool _isFetchingPlain;
        private Func<Task<string?>>? _fetchPlainCallback;

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

        private int _autoDismissSeconds = 8;
        private bool _isLoading;

        /// <summary>
        /// Sets the content of the popup without showing it.
        /// </summary>
        public void SetContent(string message, bool isLoading = false, bool isError = false, int autoDismissSeconds = 8)
        {
            Log($"SetContent called: isLoading={isLoading}, isError={isError}, msgLen={message.Length}");

            // Stop any existing timer
            _dismissTimer?.Stop();
            _autoDismissSeconds = autoDismissSeconds;
            _isLoading = isLoading;

            if (isLoading)
            {
                LoadingPanel.Visibility = Visibility.Visible;
                ContentPanel.Visibility = Visibility.Collapsed;
                ErrorBorder.Visibility = Visibility.Collapsed;
                Log("Set to loading state");
            }
            else if (isError)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                ContentPanel.Visibility = Visibility.Collapsed;
                ErrorBorder.Visibility = Visibility.Visible;
                ErrorText.Text = message;
                _autoDismissSeconds = 4; // Shorter dismiss for errors
                Log($"Set to error state: {message}");
            }
            else
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                ContentPanel.Visibility = Visibility.Visible;
                ErrorBorder.Visibility = Visibility.Collapsed;
                MessageText.Text = message;
                Log($"Set to content state, message length: {message.Length}");
            }
        }

        /// <summary>
        /// Sets the content with toggle support. Shows WTF text initially with option to fetch plain.
        /// </summary>
        public void SetContentWithToggle(string wtfText, Func<Task<string?>> fetchPlainCallback, int autoDismissSeconds = 8)
        {
            Log($"SetContentWithToggle called: wtfLen={wtfText.Length}");

            // Stop any existing timer
            _dismissTimer?.Stop();
            _autoDismissSeconds = autoDismissSeconds;
            _isLoading = false;

            // Store toggle state
            _wtfText = wtfText;
            _plainText = null; // Will be fetched on demand
            _showingWtf = true;
            _isFetchingPlain = false;
            _fetchPlainCallback = fetchPlainCallback;

            // Show content
            LoadingPanel.Visibility = Visibility.Collapsed;
            ContentPanel.Visibility = Visibility.Visible;
            ErrorBorder.Visibility = Visibility.Collapsed;
            MessageText.Text = wtfText;

            // Set initial mode indicator
            ModeIcon.Text = "🔥";
            ModeLabel.Text = "WTF";
            FlipHintText.Text = "tap to see plain →";
            FlipHint.Visibility = Visibility.Visible;

            Log($"Content set with toggle support");
        }

        private void Content_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Don't flip if clicking close button or if already flipping
            if (_isFetchingPlain) return;

            e.Handled = true;
            _ = FlipToOtherView();
        }

        private async Task FlipToOtherView()
        {
            if (_isFetchingPlain) return;

            Log($"Flip requested. Currently showing: {(_showingWtf ? "WTF" : "Plain")}");

            // If switching to plain and we don't have it yet, fetch it first
            if (_showingWtf && _plainText == null && _fetchPlainCallback != null)
            {
                _isFetchingPlain = true;
                FlipHintText.Text = "loading...";
                Log("Fetching plain translation...");

                try
                {
                    _plainText = await _fetchPlainCallback();
                    Log($"Got plain translation: {_plainText?.Length ?? 0} chars");
                }
                catch (Exception ex)
                {
                    Log($"Error fetching plain: {ex.Message}");
                    _plainText = "(Could not load plain version)";
                }
                finally
                {
                    _isFetchingPlain = false;
                }
            }

            // Animate flip out
            var flipOut = (Storyboard)Resources["FlipOut"];
            var tcs = new TaskCompletionSource<bool>();
            EventHandler? handler = null;
            handler = (s, e) =>
            {
                flipOut.Completed -= handler;
                tcs.SetResult(true);
            };
            flipOut.Completed += handler;
            flipOut.Begin(this);
            await tcs.Task;

            // Change content at the midpoint (when card is edge-on)
            if (_showingWtf)
            {
                // Switch to plain
                if (_plainText != null)
                {
                    MessageText.Text = _plainText;
                    ModeIcon.Text = "📋";
                    ModeLabel.Text = "Plain";
                    FlipHintText.Text = "← tap for WTF";
                    _showingWtf = false;
                    Log("Switched to plain view");
                }
            }
            else
            {
                // Switch to WTF
                if (_wtfText != null)
                {
                    MessageText.Text = _wtfText;
                    ModeIcon.Text = "🔥";
                    ModeLabel.Text = "WTF";
                    FlipHintText.Text = "tap to see plain →";
                    _showingWtf = true;
                    Log("Switched to WTF view");
                }
            }

            // Animate flip in
            var flipIn = (Storyboard)Resources["FlipIn"];
            flipIn.Begin(this);

            // Reset countdown on flip
            if (_dismissTimer != null && _remainingSeconds > 0)
            {
                _remainingSeconds = _autoDismissSeconds;
                UpdateCountdown();
            }
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

            // Start auto-dismiss countdown
            if (!_isLoading)
            {
                StartDismissTimer(_autoDismissSeconds);
                Log($"Dismiss timer started: {_autoDismissSeconds}s");
            }
        }

        /// <summary>
        /// Legacy method: Show the popup with a message at the specified cursor position.
        /// </summary>
        public void ShowMessage(string message, Point cursorPos, bool isLoading = false, bool isError = false, int autoDismissSeconds = 8)
        {
            SetContent(message, isLoading, isError, autoDismissSeconds);
            PositionNearCursor(cursorPos);
            ShowWithAnimation();
        }

        /// <summary>
        /// Positions the popup near the cursor (fallback if no WindowPositionService).
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
            // Need to update layout first to get actual size
            UpdateLayout();
            Log($"Window size after UpdateLayout: {ActualWidth}x{ActualHeight}");

            if (x + ActualWidth > workArea.Right)
            {
                var oldX = x;
                x = workArea.Right - ActualWidth - 10;
                Log($"Adjusted X from {oldX} to {x} (was beyond right edge)");
            }
            if (x < workArea.Left)
            {
                var oldX = x;
                x = workArea.Left + 10;
                Log($"Adjusted X from {oldX} to {x} (was beyond left edge)");
            }

            if (y + ActualHeight > workArea.Bottom)
            {
                var oldY = y;
                y = cursorPos.Y / dpiScale - ActualHeight - 15; // Show above cursor
                Log($"Adjusted Y from {oldY} to {y} (was beyond bottom edge)");
            }
            if (y < workArea.Top)
            {
                var oldY = y;
                y = workArea.Top + 10;
                Log($"Adjusted Y from {oldY} to {y} (was beyond top edge)");
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

            // Fallback: try to get from window handle
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
            // Only dismiss on right-click, left-click is for flipping
            if (e.ChangedButton == MouseButton.Right)
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
