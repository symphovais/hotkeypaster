using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TalkKeys.Logging;
using TalkKeys.Services.Auth;
using TalkKeys.Services.Settings;
using TalkKeys.Services.Windowing;

namespace TalkKeys
{
    public partial class RewriteWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly ILogger _logger;
        private readonly TalkKeysApiService _apiService;
        private readonly WindowContext? _windowContext;
        private readonly string _originalText;

        private string _selectedTarget = "email";
        private string _selectedTone = "neutral";

        private readonly Dictionary<string, RadioButton> _targetButtons;

        public RewriteWindow(
            SettingsService settingsService,
            ILogger logger,
            string originalText,
            WindowContext? windowContext,
            string? suggestedType,
            IReadOnlyList<string>? suggestedTargets)
        {
            InitializeComponent();

            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiService = new TalkKeysApiService(_settingsService, _logger);
            _windowContext = windowContext;
            _originalText = originalText ?? string.Empty;

            // Map target buttons for programmatic selection
            _targetButtons = new Dictionary<string, RadioButton>(StringComparer.OrdinalIgnoreCase)
            {
                { "email", TargetEmail },
                { "chat", TargetChat },
                { "document", TargetDocument },
                { "code", TargetCode },
                { "other", TargetOther }
            };

            // Set initial target selection based on suggestions
            SelectInitialTarget(suggestedType, suggestedTargets);

            // Set text and update UI
            OutputTextBox.Text = _originalText;
            OutputTextBox.TextChanged += OutputTextBox_TextChanged;
            UpdateCharCount();
            UpdatePlaceholderVisibility();
        }

        private void SelectInitialTarget(string? suggestedType, IReadOnlyList<string>? suggestedTargets)
        {
            string? selected = null;

            if (!string.IsNullOrWhiteSpace(suggestedType) && _targetButtons.ContainsKey(suggestedType))
            {
                selected = suggestedType;
            }
            else if (suggestedTargets != null)
            {
                selected = suggestedTargets.FirstOrDefault(t => _targetButtons.ContainsKey(t));
            }

            _selectedTarget = selected ?? "email";

            // Check the appropriate button
            foreach (var kvp in _targetButtons)
            {
                kvp.Value.IsChecked = kvp.Key.Equals(_selectedTarget, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void Target_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Content is string content)
            {
                _selectedTarget = content.ToLowerInvariant();
            }
        }

        private void Tone_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Content is string content)
            {
                _selectedTone = content.ToLowerInvariant();
            }
        }

        private void CustomInstruction_GotFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        private void CustomInstruction_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        private void UpdatePlaceholderVisibility()
        {
            CustomInstructionPlaceholder.Visibility =
                string.IsNullOrEmpty(CustomInstructionTextBox.Text) && !CustomInstructionTextBox.IsFocused
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void OutputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCharCount();
        }

        private void UpdateCharCount()
        {
            var length = OutputTextBox.Text?.Length ?? 0;
            CharCountText.Text = length > 0 ? $"{length:N0} chars" : "";
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GenerateButton.IsEnabled = false;
                GenerateButton.Content = "Rewriting...";
                StatusIcon.Text = "⏳";
                StatusText.Text = "Generating...";

                var custom = string.IsNullOrWhiteSpace(CustomInstructionTextBox.Text)
                    ? null
                    : CustomInstructionTextBox.Text.Trim();

                var inputText = OutputTextBox.Text;

                var result = await _apiService.RewriteTextAsync(
                    inputText,
                    _selectedTarget,
                    _selectedTone,
                    custom,
                    _windowContext);

                if (!result.Success)
                {
                    StatusIcon.Text = "⚠️";
                    StatusText.Text = result.Error ?? "Rewrite failed";
                    return;
                }

                OutputTextBox.Text = result.RewrittenText ?? string.Empty;
                StatusIcon.Text = "✨";
                StatusText.Text = "Rewritten";
            }
            catch (Exception ex)
            {
                StatusIcon.Text = "❌";
                StatusText.Text = ex.Message;
                _logger.Log($"[RewriteWindow] Generate failed: {ex.Message}");
            }
            finally
            {
                GenerateButton.IsEnabled = true;
                GenerateButton.Content = "✨ Rewrite";
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(OutputTextBox.Text);
                CopyButton.Content = "✓ Copied";

                // Reset after delay
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    CopyButton.Content = "📋 Copy";
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                StatusIcon.Text = "❌";
                StatusText.Text = ex.Message;
                _logger.Log($"[RewriteWindow] Copy failed: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            try
            {
                DragMove();
            }
            catch
            {
                // Ignore drag errors
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _apiService.Dispose();
            base.OnClosed(e);
        }
    }
}
