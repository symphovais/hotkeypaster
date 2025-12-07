using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TalkKeys.Services.Hotkey
{
    /// <summary>
    /// Event args for key press/release events
    /// </summary>
    public class KeyboardHookEventArgs : EventArgs
    {
        public Keys Key { get; }
        public Keys Modifiers { get; }
        public bool IsKeyDown { get; }

        public KeyboardHookEventArgs(Keys key, Keys modifiers, bool isKeyDown)
        {
            Key = key;
            Modifiers = modifiers;
            IsKeyDown = isKeyDown;
        }
    }

    /// <summary>
    /// Low-level keyboard hook that can detect both key press and key release
    /// for push-to-talk functionality
    /// </summary>
    public sealed class LowLevelKeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc;
        private bool _disposed;

        // Target key combination to monitor
        private Keys _targetKey = Keys.Q;
        private Keys _targetModifiers = Keys.Control | Keys.Alt;
        private bool _isEnabled;
        private bool _isKeyCurrentlyDown;

        /// <summary>
        /// Fired when the target key combination is pressed
        /// </summary>
        public event EventHandler<KeyboardHookEventArgs>? KeyDown;

        /// <summary>
        /// Fired when the target key combination is released
        /// </summary>
        public event EventHandler<KeyboardHookEventArgs>? KeyUp;

        /// <summary>
        /// Whether the hook is currently active
        /// </summary>
        public bool IsEnabled => _isEnabled;

        /// <summary>
        /// Configure the target key combination to monitor
        /// </summary>
        public void SetTargetKey(Keys key, Keys modifiers)
        {
            _targetKey = key & Keys.KeyCode;
            _targetModifiers = modifiers;
        }

        /// <summary>
        /// Start the keyboard hook
        /// </summary>
        public void Start()
        {
            if (_isEnabled) return;

            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            if (curModule != null)
            {
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
                _isEnabled = _hookId != IntPtr.Zero;
            }
        }

        /// <summary>
        /// Stop the keyboard hook
        /// </summary>
        public void Stop()
        {
            if (!_isEnabled) return;

            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            _isEnabled = false;
            _isKeyCurrentlyDown = false;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                bool isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool isKeyUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                if (isKeyDown || isKeyUp)
                {
                    var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    var pressedKey = (Keys)hookStruct.vkCode;

                    // Check if the pressed key matches our target
                    if (pressedKey == _targetKey)
                    {
                        // Check modifiers
                        var currentModifiers = GetCurrentModifiers();

                        if (AreModifiersMatch(currentModifiers, _targetModifiers))
                        {
                            if (isKeyDown && !_isKeyCurrentlyDown)
                            {
                                _isKeyCurrentlyDown = true;
                                KeyDown?.Invoke(this, new KeyboardHookEventArgs(pressedKey, currentModifiers, true));
                            }
                            else if (isKeyUp && _isKeyCurrentlyDown)
                            {
                                _isKeyCurrentlyDown = false;
                                KeyUp?.Invoke(this, new KeyboardHookEventArgs(pressedKey, currentModifiers, false));
                            }
                        }
                    }
                    // Also check if any modifier is released while key was down
                    else if (_isKeyCurrentlyDown && IsModifierKey(pressedKey) && isKeyUp)
                    {
                        // Modifier released while main key was held - treat as release
                        _isKeyCurrentlyDown = false;
                        KeyUp?.Invoke(this, new KeyboardHookEventArgs(_targetKey, _targetModifiers, false));
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static Keys GetCurrentModifiers()
        {
            Keys modifiers = Keys.None;

            if ((GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0)
                modifiers |= Keys.Control;
            if ((GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0)
                modifiers |= Keys.Shift;
            if ((GetAsyncKeyState((int)Keys.Menu) & 0x8000) != 0) // Alt key
                modifiers |= Keys.Alt;
            if ((GetAsyncKeyState((int)Keys.LWin) & 0x8000) != 0 || (GetAsyncKeyState((int)Keys.RWin) & 0x8000) != 0)
                modifiers |= Keys.LWin;

            return modifiers;
        }

        private static bool AreModifiersMatch(Keys current, Keys target)
        {
            // Check if the required modifiers are pressed
            bool ctrlRequired = (target & Keys.Control) == Keys.Control;
            bool ctrlPressed = (current & Keys.Control) == Keys.Control;

            bool shiftRequired = (target & Keys.Shift) == Keys.Shift;
            bool shiftPressed = (current & Keys.Shift) == Keys.Shift;

            bool altRequired = (target & Keys.Alt) == Keys.Alt;
            bool altPressed = (current & Keys.Alt) == Keys.Alt;

            bool winRequired = (target & Keys.LWin) == Keys.LWin;
            bool winPressed = (current & Keys.LWin) == Keys.LWin;

            return ctrlRequired == ctrlPressed &&
                   shiftRequired == shiftPressed &&
                   altRequired == altPressed &&
                   winRequired == winPressed;
        }

        private static bool IsModifierKey(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu ||
                   key == Keys.LWin || key == Keys.RWin;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
