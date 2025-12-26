using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TalkKeys.Services.Auth;
using TalkKeys.Services.Windowing;

namespace TalkKeys.Plugins.Explainer
{
    /// <summary>
    /// Event args for when an action button is clicked
    /// </summary>
    public class ActionClickedEventArgs : EventArgs
    {
        public SuggestedAction Action { get; }
        public string OriginalText { get; }
        public string ContextType { get; }
        public WindowContext? WindowContext { get; }

        public ActionClickedEventArgs(SuggestedAction action, string originalText, string contextType, WindowContext? windowContext)
        {
            Action = action;
            OriginalText = originalText;
            ContextType = contextType;
            WindowContext = windowContext;
        }
    }

    /// <summary>
    /// Popup window that displays insights (WTF, Plain) in a unified vertical layout.
    /// </summary>
    public partial class ExplainerPopup : Window
    {
        private DispatcherTimer? _dismissTimer;
        private int _remainingSeconds;
        private Action<string>? _logAction;
        private int _autoDismissSeconds = 20;

        // Smart Actions fields
        private List<SuggestedAction>? _suggestedActions;
        private string? _originalText;
        private string? _contextType;
        private WindowContext? _windowContext;

        // Reply recording state
        private bool _isRecording;
        private string? _generatedReply;

        // Width management
        private const double NormalWidth = 320;
        private const double ExpandedWidth = 420;

        /// <summary>
        /// Event fired when an action button is clicked
        /// </summary>
        public event EventHandler<ActionClickedEventArgs>? ActionClicked;

        /// <summary>
        /// Event fired when user requests to start recording for Reply
        /// </summary>
        public event EventHandler? RecordingStartRequested;

        /// <summary>
        /// Event fired when user requests to stop recording
        /// </summary>
        public event EventHandler? RecordingStopRequested;

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
        /// Sets the suggested actions to display in the Quick Actions section.
        /// </summary>
        public void SetActions(List<SuggestedAction> actions, string contextType, string originalText, WindowContext? windowContext)
        {
            _suggestedActions = actions;
            _contextType = contextType;
            _originalText = originalText;
            _windowContext = windowContext;

            Log($"SetActions called: {actions.Count} actions, context={contextType}");

            // Clear existing buttons
            ActionButtonsPanel.Children.Clear();

            if (actions.Count == 0)
            {
                ActionsSection.Visibility = Visibility.Collapsed;
                return;
            }

            // Show section and create buttons (max 3)
            ActionsSection.Visibility = Visibility.Visible;
            foreach (var action in actions.Take(3))
            {
                var button = CreateActionButton(action);
                ActionButtonsPanel.Children.Add(button);
            }
        }

        /// <summary>
        /// Clears the actions section (hide it).
        /// </summary>
        public void ClearActions()
        {
            _suggestedActions = null;
            _contextType = null;
            _originalText = null;
            _windowContext = null;
            ActionButtonsPanel.Children.Clear();
            ActionsSection.Visibility = Visibility.Collapsed;
        }

        private System.Windows.Controls.Button CreateActionButton(SuggestedAction action)
        {
            var icon = GetIconEmoji(action.Icon);
            var button = new System.Windows.Controls.Button
            {
                Content = $"{icon} {action.Label}",
                Tag = action,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 8, 8),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D3748")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderBrush = action.Primary
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4B5563")),
                BorderThickness = new Thickness(1),
                FontSize = 12,
                FontWeight = action.Primary ? FontWeights.SemiBold : FontWeights.Normal
            };

            // Custom template for rounded corners
            button.Style = CreateActionButtonStyle(action.Primary);

            button.Click += OnActionButtonClick;

            return button;
        }

        private Style CreateActionButtonStyle(bool isPrimary)
        {
            var style = new Style(typeof(System.Windows.Controls.Button));

            var template = new ControlTemplate(typeof(System.Windows.Controls.Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));
            borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);

            template.VisualTree = borderFactory;
            style.Setters.Add(new Setter(TemplateProperty, template));

            // Hover trigger
            var hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6"))));
            hoverTrigger.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"))));
            style.Triggers.Add(hoverTrigger);

            return style;
        }

        private void OnActionButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is SuggestedAction action)
            {
                Log($"Action button clicked: {action.Id} ({action.Label})");

                // Stop the dismiss timer while handling the action
                _dismissTimer?.Stop();

                // Raise the event
                ActionClicked?.Invoke(this, new ActionClickedEventArgs(
                    action,
                    _originalText ?? "",
                    _contextType ?? "other",
                    _windowContext));
            }
        }

        private static string GetIconEmoji(string iconName)
        {
            return iconName.ToLowerInvariant() switch
            {
                "message" => "💬",
                "forward" => "↗️",
                "compress" or "summarize" => "📝",
                "thread" => "🧵",
                "edit" or "simplify" => "✏️",
                "code" or "explain" => "💻",
                "comment" => "💭",
                _ => "⚡"
            };
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

        #region Reply Section Methods

        /// <summary>
        /// Shows the Reply section (called when Reply action is clicked)
        /// </summary>
        public void ShowReplySection()
        {
            Log("ShowReplySection called");

            // Collapse WTF, Plain, and Actions sections to reduce height
            WtfSection.Visibility = Visibility.Collapsed;
            PlainSection.Visibility = Visibility.Collapsed;
            ActionsSection.Visibility = Visibility.Collapsed;

            // Expand width for reply mode
            SetWindowWidth(ExpandedWidth);

            ReplySection.Visibility = Visibility.Visible;
            RecordingPanel.Visibility = Visibility.Visible;
            InstructionPanel.Visibility = Visibility.Collapsed;
            GeneratedReplyPanel.Visibility = Visibility.Collapsed;
            ReplyLoadingPanel.Visibility = Visibility.Collapsed;

            // Reset recording state
            _isRecording = false;
            RecordButtonIcon.Text = "🎤";
            RecordingStatusText.Text = "Tap to record your reply instructions";
            RecordingHintText.Visibility = Visibility.Visible;

            // Show status text, hide recording indicator
            RecordingStatusPanel.Visibility = Visibility.Visible;
            ReplyRecordingIndicator.Visibility = Visibility.Collapsed;
            ReplyRecordingIndicator.Reset();

            // Stop auto-dismiss timer when showing reply section
            _dismissTimer?.Stop();
        }

        /// <summary>
        /// Hides the Reply section and restores WTF/Plain sections
        /// </summary>
        public void HideReplySection()
        {
            Log("HideReplySection called");
            ReplySection.Visibility = Visibility.Collapsed;
            _isRecording = false;

            // Restore WTF and Plain sections
            WtfSection.Visibility = Visibility.Visible;
            PlainSection.Visibility = Visibility.Visible;

            // Restore quick actions if we have any
            if (_suggestedActions?.Count > 0)
                ActionsSection.Visibility = Visibility.Visible;

            // Reset to normal width
            SetWindowWidth(NormalWidth);
        }

        /// <summary>
        /// Updates UI to show recording in progress
        /// </summary>
        public void ShowRecordingInProgress()
        {
            Log("ShowRecordingInProgress called");
            _isRecording = true;
            RecordButtonIcon.Text = "⏹️";

            // Hide status text, show recording indicator
            RecordingStatusPanel.Visibility = Visibility.Collapsed;
            ReplyRecordingIndicator.Visibility = Visibility.Visible;
            ReplyRecordingIndicator.StartRecording();
        }

        /// <summary>
        /// Updates the audio level visualization during recording
        /// </summary>
        public void UpdateAudioLevel(double level)
        {
            if (_isRecording)
            {
                ReplyRecordingIndicator.UpdateAudioLevel(level);
            }
        }

        /// <summary>
        /// Shows the transcribed instruction after recording
        /// </summary>
        public void ShowRecordingInstruction(string instruction)
        {
            Log($"ShowRecordingInstruction: {instruction.Length} chars");
            _isRecording = false;
            RecordButtonIcon.Text = "🎤";

            // Show status text with re-record message, hide recording indicator
            RecordingStatusPanel.Visibility = Visibility.Visible;
            RecordingStatusText.Text = "Tap to re-record";
            RecordingStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
            RecordingHintText.Visibility = Visibility.Collapsed;

            ReplyRecordingIndicator.Visibility = Visibility.Collapsed;
            ReplyRecordingIndicator.ShowProcessing();

            InstructionText.Text = instruction;
            InstructionPanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Shows loading state while generating reply
        /// </summary>
        public void ShowReplyLoading(string message = "Generating reply...")
        {
            Log($"ShowReplyLoading: {message}");
            ReplyLoadingText.Text = message;
            ReplyLoadingPanel.Visibility = Visibility.Visible;
            GeneratedReplyPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Shows the generated reply with copy button
        /// </summary>
        public void ShowGeneratedReply(string reply)
        {
            Log($"ShowGeneratedReply: {reply.Length} chars");
            _generatedReply = reply;
            ReplyLoadingPanel.Visibility = Visibility.Collapsed;
            GeneratedReplyText.Text = reply;
            GeneratedReplyText.Foreground = new SolidColorBrush(Colors.White);
            GeneratedReplyPanel.Visibility = Visibility.Visible;
            CopyReplyButton.Content = "📋 Copy";

            // Expand width for better readability of generated reply
            SetWindowWidth(ExpandedWidth);

            // Restart dismiss timer with longer duration
            StartDismissTimer(30);
        }

        /// <summary>
        /// Shows error in the reply section
        /// </summary>
        public void ShowReplyError(string message)
        {
            Log($"ShowReplyError: {message}");
            ReplyLoadingPanel.Visibility = Visibility.Collapsed;
            GeneratedReplyText.Text = $"Error: {message}";
            GeneratedReplyText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCA5A5"));
            GeneratedReplyPanel.Visibility = Visibility.Visible;

            // Show recording indicator with error state
            ReplyRecordingIndicator.ShowError();
        }

        private void SetWindowWidth(double width)
        {
            if (Math.Abs(Width - width) < 1) return;

            Log($"SetWindowWidth: {Width} -> {width}");
            Width = width;

            // Adjust position to keep right edge stable if expanding past screen edge
            UpdateLayout();
            var dpiScale = GetDpiScale();
            var screen = Screen.FromPoint(new System.Drawing.Point((int)(Left * dpiScale), (int)(Top * dpiScale)));
            var workArea = new Rect(
                screen.WorkingArea.Left / dpiScale,
                screen.WorkingArea.Top / dpiScale,
                screen.WorkingArea.Width / dpiScale,
                screen.WorkingArea.Height / dpiScale);

            if (Left + Width > workArea.Right)
            {
                Left = workArea.Right - Width - 10;
            }
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            Log($"RecordButton_Click, isRecording={_isRecording}");

            if (_isRecording)
            {
                // Stop recording
                RecordingStopRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // Start recording
                RecordingStartRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CopyReplyButton_Click(object sender, RoutedEventArgs e)
        {
            Log("CopyReplyButton_Click");

            if (string.IsNullOrEmpty(_generatedReply))
                return;

            try
            {
                System.Windows.Clipboard.SetText(_generatedReply);
                CopyReplyButton.Content = "✓ Copied";

                // Reset button text after 2 seconds
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    CopyReplyButton.Content = "📋 Copy";
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                Log($"Copy failed: {ex.Message}");
            }
        }

        #endregion
    }
}
