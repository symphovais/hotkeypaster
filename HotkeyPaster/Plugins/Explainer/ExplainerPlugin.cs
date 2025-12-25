using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using H.Hooks;
using WindowsInput;
using Key = System.Windows.Input.Key;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using HKeyboardEventArgs = H.Hooks.KeyboardEventArgs;
using System.Linq;
using TalkKeys.Logging;
using TalkKeys.PluginSdk;
using TalkKeys.Services.Auth;
using TalkKeys.Services.Settings;
using TalkKeys.Services.Windowing;

namespace TalkKeys.Plugins.Explainer
{
    /// <summary>
    /// Plain English Explainer plugin - Select text, press hotkey, get a blunt explanation.
    /// </summary>
    public class ExplainerPlugin : IPlugin
    {
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 50;
        private const int ClipboardWaitMs = 100;

        // Settings keys
        public const string SettingHotkey = "Hotkey";
        public const string SettingAutoDismissSeconds = "AutoDismissSeconds";
        public const string SettingTone = "Tone";

        // Default values
        private const string DefaultHotkey = "Ctrl+Win+E";
        private const int DefaultAutoDismissSeconds = 20;
        private const string DefaultTone = "wtf"; // wtf or plain

        private readonly ILogger? _logger;
        private readonly TalkKeysApiService _apiService;
        private readonly SettingsService _settingsService;
        private readonly IWindowPositionService? _positionService;
        private readonly InputSimulator _inputSimulator = new();

        private PluginConfiguration _configuration;
        private LowLevelKeyboardHook? _keyboardHook;
        private ExplainerPopup? _currentPopup;
        private bool _isKeyDown;
        private string? _lastSelectedText; // Store for toggle callback

        // Parsed hotkey
        private Key _targetKey;
        private ModifierKeys _targetModifiers;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        #region IPlugin Implementation

        public string PluginId => "explainer";
        public string DisplayName => "WTF - What are the Facts";
        public string Description => "Select text and press hotkey to get the facts explained simply";
        public string Icon => "🤔";
        public Version Version => new(1, 0, 0);

        public ExplainerPlugin(TalkKeysApiService apiService, SettingsService settingsService, IWindowPositionService? positionService = null, ILogger? logger = null)
        {
            _apiService = apiService;
            _settingsService = settingsService;
            _positionService = positionService;
            _logger = logger;
            _configuration = GetDefaultConfiguration();
        }

        public void Initialize(PluginConfiguration configuration)
        {
            _configuration = configuration;

            // Parse hotkey
            var hotkeyString = _configuration.GetSetting(SettingHotkey, DefaultHotkey);
            ParseHotkey(hotkeyString);

            _logger?.Log($"[Explainer] Initialized. Hotkey: {hotkeyString}");
        }

        public void Activate()
        {
            // Set up keyboard hook
            _keyboardHook = new LowLevelKeyboardHook
            {
                IsExtendedMode = true,
                HandleModifierKeys = true,
                IsLeftRightGranularity = false
            };

            _keyboardHook.Down += OnKeyDown;
            _keyboardHook.Start();

            _logger?.Log("[Explainer] Activated - listening for hotkey");
        }

        public void Deactivate()
        {
            if (_keyboardHook != null)
            {
                _keyboardHook.Down -= OnKeyDown;
                _keyboardHook.Stop();
                _keyboardHook.Dispose();
                _keyboardHook = null;
            }

            CloseCurrentPopup();
            _logger?.Log("[Explainer] Deactivated");
        }

        public PluginConfiguration GetConfiguration() => _configuration;

        public PluginConfiguration GetDefaultConfiguration()
        {
            return new PluginConfiguration
            {
                PluginId = PluginId,
                Enabled = true, // Enabled by default
                Settings = new Dictionary<string, object>
                {
                    [SettingHotkey] = DefaultHotkey,
                    [SettingAutoDismissSeconds] = DefaultAutoDismissSeconds,
                    [SettingTone] = DefaultTone
                }
            };
        }

        public FrameworkElement? CreateSettingsPanel()
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            // Header
            panel.Children.Add(new TextBlock
            {
                Text = $"{Icon} WTF - What are the Facts",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 15)
            });

            // Description
            panel.Children.Add(new TextBlock
            {
                Text = "Select any text and press the hotkey to get a blunt, no-BS explanation of what it really means.",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            });

            // Hotkey label
            panel.Children.Add(new TextBlock
            {
                Text = "Hotkey:",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)),
                Margin = new Thickness(0, 0, 0, 5)
            });

            // Hotkey input
            var currentHotkey = _configuration.GetSetting(SettingHotkey, DefaultHotkey);
            var hotkeyTextBox = new TextBox
            {
                Text = currentHotkey,
                IsReadOnly = true,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55, 65, 81)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(75, 85, 99)),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 5),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 14
            };

            hotkeyTextBox.PreviewKeyDown += (s, e) =>
            {
                e.Handled = true;

                var modifiers = System.Windows.Input.Keyboard.Modifiers;
                var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

                // Skip if only modifiers pressed
                if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                    key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                    key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
                    key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin ||
                    key == System.Windows.Input.Key.DeadCharProcessed)
                    return;

                // Add Windows modifier if Win key is pressed
                var finalModifiers = modifiers;
                if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LWin) ||
                    System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RWin))
                {
                    finalModifiers |= ModifierKeys.Windows;
                }

                var hotkeyStr = FormatHotkey(finalModifiers, key);
                hotkeyTextBox.Text = hotkeyStr;
                _configuration.SetSetting(SettingHotkey, hotkeyStr);
                ParseHotkey(hotkeyStr);
            };

            hotkeyTextBox.GotFocus += (s, e) =>
            {
                hotkeyTextBox.Text = "Press keys...";
            };

            hotkeyTextBox.LostFocus += (s, e) =>
            {
                var saved = _configuration.GetSetting(SettingHotkey, DefaultHotkey);
                hotkeyTextBox.Text = saved;
            };

            panel.Children.Add(hotkeyTextBox);

            // Hint
            panel.Children.Add(new TextBlock
            {
                Text = "Click the box and press your desired hotkey combination",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 20)
            });

            // Tone label
            panel.Children.Add(new TextBlock
            {
                Text = "Tone:",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)),
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Tone selector (3 radio buttons in a row)
            var tonePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var currentTone = _configuration.GetSetting(SettingTone, DefaultTone);
            var tones = new[] { ("savage", "🔥 Savage"), ("sarcastic", "😏 Sarcastic"), ("factual", "📋 Factual") };

            foreach (var (value, label) in tones)
            {
                var radio = new RadioButton
                {
                    Content = label,
                    Tag = value,
                    IsChecked = currentTone == value,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new Thickness(0, 0, 15, 0),
                    GroupName = "ToneGroup"
                };

                radio.Checked += (s, e) =>
                {
                    if (s is RadioButton rb && rb.Tag is string tone)
                    {
                        _configuration.SetSetting(SettingTone, tone);
                    }
                };

                tonePanel.Children.Add(radio);
            }

            panel.Children.Add(tonePanel);

            // Tone description
            panel.Children.Add(new TextBlock
            {
                Text = "Savage = brutally honest • Sarcastic = witty • Factual = just the facts",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 15)
            });

            return panel;
        }

        public void Dispose()
        {
            Deactivate();
        }

        #endregion

        #region Hotkey Handling

        private void OnKeyDown(object? sender, HKeyboardEventArgs e)
        {
            if (_isKeyDown) return;

            if (IsTargetHotkey(e))
            {
                _isKeyDown = true;
                e.IsHandled = true;

                // Reset key state after a short delay
                Task.Delay(200).ContinueWith(_ => _isKeyDown = false);

                // Run on UI thread
                Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    await ExplainSelectedTextAsync();
                });
            }
        }

        private bool IsTargetHotkey(HKeyboardEventArgs e)
        {
            // Convert H.Hooks Key to WPF Key
            if (!Enum.TryParse<Key>(e.CurrentKey.ToString(), out var pressedKey))
                return false;

            if (pressedKey != _targetKey)
                return false;

            // Get pressed keys as HashSet for efficient lookup
            var pressedKeys = e.Keys.Values.ToHashSet();

            // Check modifiers (H.Hooks may report generic or specific keys)
            bool ctrlPressed = pressedKeys.Contains(H.Hooks.Key.LeftCtrl) ||
                              pressedKeys.Contains(H.Hooks.Key.RightCtrl) ||
                              pressedKeys.Contains(H.Hooks.Key.Control);
            bool shiftPressed = pressedKeys.Contains(H.Hooks.Key.LeftShift) ||
                               pressedKeys.Contains(H.Hooks.Key.RightShift) ||
                               pressedKeys.Contains(H.Hooks.Key.Shift);
            bool altPressed = pressedKeys.Contains(H.Hooks.Key.LeftAlt) ||
                             pressedKeys.Contains(H.Hooks.Key.RightAlt) ||
                             pressedKeys.Contains(H.Hooks.Key.Alt);
            bool winPressed = pressedKeys.Contains(H.Hooks.Key.LWin) ||
                             pressedKeys.Contains(H.Hooks.Key.RWin);

            bool needCtrl = _targetModifiers.HasFlag(ModifierKeys.Control);
            bool needShift = _targetModifiers.HasFlag(ModifierKeys.Shift);
            bool needAlt = _targetModifiers.HasFlag(ModifierKeys.Alt);
            bool needWin = _targetModifiers.HasFlag(ModifierKeys.Windows);

            return ctrlPressed == needCtrl && shiftPressed == needShift && altPressed == needAlt && winPressed == needWin;
        }

        private void ParseHotkey(string hotkeyString)
        {
            _targetModifiers = ModifierKeys.None;
            _targetKey = Key.None;

            var parts = hotkeyString.Split('+');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                switch (trimmed.ToLower())
                {
                    case "ctrl":
                        _targetModifiers |= ModifierKeys.Control;
                        break;
                    case "shift":
                        _targetModifiers |= ModifierKeys.Shift;
                        break;
                    case "alt":
                        _targetModifiers |= ModifierKeys.Alt;
                        break;
                    case "win":
                    case "windows":
                        _targetModifiers |= ModifierKeys.Windows;
                        break;
                    default:
                        if (Enum.TryParse<Key>(trimmed, true, out var key))
                            _targetKey = key;
                        break;
                }
            }
        }

        private static string FormatHotkey(ModifierKeys modifiers, System.Windows.Input.Key key)
        {
            var parts = new List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }

        #endregion

        #region Explainer Logic

        private async Task ExplainSelectedTextAsync()
        {
            try
            {
                _logger?.Log("[Explainer] Hotkey triggered");

                // Close any existing popup
                CloseCurrentPopup();

                // Get cursor position
                var cursorPos = GetCursorPosition();
                _logger?.Log($"[Explainer] Cursor at: {cursorPos.X}, {cursorPos.Y}");

                // Capture selected text
                var selectedText = await CaptureSelectedTextAsync();

                if (string.IsNullOrWhiteSpace(selectedText))
                {
                    _logger?.Log("[Explainer] No text selected");
                    ShowErrorPopup("No text selected", cursorPos);
                    return;
                }

                if (selectedText.Length > 2000)
                {
                    _logger?.Log("[Explainer] Text too long");
                    ShowErrorPopup("Text too long (max 2000 chars)", cursorPos);
                    return;
                }

                _logger?.Log($"[Explainer] Captured {selectedText.Length} chars");
                _lastSelectedText = selectedText;

                // Show loading popup immediately
                ShowLoadingPopup(cursorPos);

                // Fetch WTF and Plain in parallel
                var wtfTask = _apiService.ExplainTextAsync(selectedText, "wtf");
                var plainTask = _apiService.ExplainTextAsync(selectedText, "plain");

                _logger?.Log("[Explainer] Fetching WTF and Plain in parallel...");
                await Task.WhenAll(wtfTask, plainTask);

                var wtfResult = await wtfTask;
                var plainResult = await plainTask;

                _logger?.Log($"[Explainer] Results - WTF: {wtfResult.Success}, Plain: {plainResult.Success}");

                // Get text or fallback
                var wtfText = wtfResult.Success && !string.IsNullOrWhiteSpace(wtfResult.Explanation)
                    ? wtfResult.Explanation
                    : "(Could not analyze)";

                var plainText = plainResult.Success && !string.IsNullOrWhiteSpace(plainResult.Explanation)
                    ? plainResult.Explanation
                    : "(Could not analyze)";

                var dismissSeconds = _configuration.GetSetting(SettingAutoDismissSeconds, DefaultAutoDismissSeconds);

                // Show unified popup with all content
                ShowUnifiedPopup(wtfText, plainText, cursorPos, dismissSeconds);
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Explainer] Error: {ex.Message}");
                var cursorPos = GetCursorPosition();
                ShowErrorPopup("Something went wrong", cursorPos);
            }
        }

        private async Task<string?> CaptureSelectedTextAsync()
        {
            string? originalClipboard = null;

            try
            {
                _logger?.Log("[Explainer] CaptureSelectedTextAsync starting...");

                // Save current clipboard content
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (TryClipboardOperation(() => System.Windows.Clipboard.ContainsText()))
                        {
                            originalClipboard = TryClipboardOperation(() => System.Windows.Clipboard.GetText());
                            _logger?.Log($"[Explainer] Saved original clipboard: {originalClipboard?.Length ?? 0} chars");
                        }
                        TryClipboardOperation(() =>
                        {
                            System.Windows.Clipboard.Clear();
                            return true;
                        });
                        _logger?.Log("[Explainer] Clipboard cleared");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"[Explainer] Clipboard save error: {ex.Message}");
                    }
                });

                // Wait for user to release modifier keys (Win+Ctrl+E)
                // This is critical - if modifiers are held, Ctrl+C becomes Win+Ctrl+C
                _logger?.Log("[Explainer] Waiting for modifier keys to be released...");
                await Task.Delay(150);

                // Release any held modifier keys explicitly
                _inputSimulator.Keyboard.KeyUp(VirtualKeyCode.LWIN);
                _inputSimulator.Keyboard.KeyUp(VirtualKeyCode.RWIN);
                _inputSimulator.Keyboard.KeyUp(VirtualKeyCode.CONTROL);
                _inputSimulator.Keyboard.KeyUp(VirtualKeyCode.SHIFT);
                _inputSimulator.Keyboard.KeyUp(VirtualKeyCode.MENU); // Alt

                await Task.Delay(50);

                // Simulate Ctrl+C
                _logger?.Log("[Explainer] Sending Ctrl+C...");
                _inputSimulator.Keyboard.ModifiedKeyStroke(
                    VirtualKeyCode.CONTROL,
                    VirtualKeyCode.VK_C);

                await Task.Delay(ClipboardWaitMs);
                _logger?.Log("[Explainer] Ctrl+C sent, reading clipboard...");

                // Read captured text
                string? selectedText = null;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            bool hasText = System.Windows.Clipboard.ContainsText();
                            _logger?.Log($"[Explainer] Clipboard check {i + 1}: ContainsText={hasText}");
                            if (hasText)
                            {
                                selectedText = System.Windows.Clipboard.GetText();
                                _logger?.Log($"[Explainer] Got text: {selectedText?.Length ?? 0} chars");
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.Log($"[Explainer] Clipboard read error {i + 1}: {ex.Message}");
                        }
                        Thread.Sleep(50);
                    }
                });

                // Restore original clipboard asynchronously
                if (originalClipboard != null)
                {
                    var clipboardToRestore = originalClipboard;
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(200);
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                System.Windows.Clipboard.SetText(clipboardToRestore);
                            }
                            catch { }
                        });
                    });
                }

                return selectedText;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Explainer] Capture error: {ex.Message}");
                return null;
            }
        }

        private Point GetCursorPosition()
        {
            GetCursorPos(out POINT pt);
            return new Point(pt.X, pt.Y);
        }

        private void ShowLoadingPopup(Point cursorPos)
        {
            _logger?.Log($"[Explainer] ShowLoadingPopup at ({cursorPos.X},{cursorPos.Y})");

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    CloseCurrentPopup();
                    _currentPopup = new ExplainerPopup(msg => _logger?.Log(msg));
                    _currentPopup.ShowLoading();
                    PositionAndShowPopup(cursorPos);
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[Explainer] ERROR showing loading popup: {ex.Message}");
                }
            });
        }

        private void ShowErrorPopup(string message, Point cursorPos)
        {
            _logger?.Log($"[Explainer] ShowErrorPopup: {message}");

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    CloseCurrentPopup();
                    _currentPopup = new ExplainerPopup(msg => _logger?.Log(msg));
                    _currentPopup.ShowError(message);
                    PositionAndShowPopup(cursorPos);
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[Explainer] ERROR showing error popup: {ex.Message}");
                }
            });
        }

        private void ShowUnifiedPopup(string wtfText, string plainText, Point cursorPos, int autoDismissSeconds)
        {
            _logger?.Log($"[Explainer] ShowUnifiedPopup: wtf={wtfText.Length}, plain={plainText.Length}");

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Update existing popup or create new one
                    if (_currentPopup == null)
                    {
                        _currentPopup = new ExplainerPopup(msg => _logger?.Log(msg));
                        _currentPopup.SetAllContent(wtfText, plainText, autoDismissSeconds);
                        PositionAndShowPopup(cursorPos);
                    }
                    else
                    {
                        // Update content on existing popup (was showing loading)
                        _currentPopup.SetAllContent(wtfText, plainText, autoDismissSeconds);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[Explainer] ERROR showing unified popup: {ex.Message}");
                }
            });
        }

        private void PositionAndShowPopup(Point cursorPos)
        {
            if (_currentPopup == null) return;

            if (_positionService != null)
            {
                _positionService.PositionNearCursor(_currentPopup, cursorPos.X, cursorPos.Y);
            }
            else
            {
                _currentPopup.PositionNearCursor(cursorPos);
            }

            _currentPopup.ShowWithAnimation();
            _logger?.Log($"[Explainer] Popup shown at ({_currentPopup.Left},{_currentPopup.Top})");
        }

        private void CloseCurrentPopup()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_currentPopup != null)
                {
                    try { _currentPopup.Close(); } catch { }
                    _currentPopup = null;
                }
            });
        }

        private T TryClipboardOperation<T>(Func<T> operation)
        {
            for (int i = 0; i < MaxRetries; i++)
            {
                try { return operation(); }
                catch (COMException) when (i < MaxRetries - 1)
                {
                    Thread.Sleep(RetryDelayMs);
                }
            }
            return operation();
        }

        #endregion
    }
}
