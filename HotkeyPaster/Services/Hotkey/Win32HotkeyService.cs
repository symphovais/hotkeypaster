using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;

namespace TalkKeys.Services.Hotkey
{
    public sealed class Win32HotkeyService : IHotkeyService, IDisposable
    {
        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

        private const int WM_HOTKEY = 0x0312;
        private int _nextHotkeyId = 9000;

        private IntPtr _windowHandle;
        private HwndSource? _source;
        private readonly Dictionary<int, string> _hotkeyIdMap = new Dictionary<int, string>();
        private readonly Dictionary<string, int> _stringToIntMap = new Dictionary<string, int>();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public void SetWindowHandle(IntPtr handle)
        {
            _windowHandle = handle;

            if (_source == null && _windowHandle != IntPtr.Zero)
            {
                _source = HwndSource.FromHwnd(_windowHandle);
                _source?.AddHook(HwndHook);
            }
        }

        public void RegisterHotkey(string id, Keys modifiers, Keys key)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id));

            if (_windowHandle == IntPtr.Zero)
                throw new InvalidOperationException("Window handle must be set before registering hotkeys");

            // Convert Windows.Forms.Keys to Win32 modifiers
            uint win32Modifiers = 0;
            if ((modifiers & Keys.Control) == Keys.Control)
                win32Modifiers |= 0x0002; // MOD_CONTROL
            if ((modifiers & Keys.Shift) == Keys.Shift)
                win32Modifiers |= 0x0004; // MOD_SHIFT
            if ((modifiers & Keys.Alt) == Keys.Alt)
                win32Modifiers |= 0x0001; // MOD_ALT
            if ((modifiers & Keys.LWin) == Keys.LWin || (modifiers & Keys.RWin) == Keys.RWin)
                win32Modifiers |= 0x0008; // MOD_WIN

            // Get virtual key code (strip modifiers)
            uint vk = (uint)(key & Keys.KeyCode);

            // Generate unique internal ID
            int internalId = _nextHotkeyId++;

            bool registered = RegisterHotKey(_windowHandle, internalId, win32Modifiers, vk);
            if (!registered)
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to register hotkey '{id}' ({modifiers}+{key}). Error: {error}");
            }

            _hotkeyIdMap[internalId] = id;
            _stringToIntMap[id] = internalId;
        }

        public void UnregisterHotkey(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            if (_stringToIntMap.TryGetValue(id, out int internalId))
            {
                if (_windowHandle != IntPtr.Zero)
                {
                    UnregisterHotKey(_windowHandle, internalId);
                }

                _hotkeyIdMap.Remove(internalId);
                _stringToIntMap.Remove(id);
            }
        }

        public void UnregisterAllHotkeys()
        {
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                _source = null;
            }

            if (_windowHandle != IntPtr.Zero)
            {
                foreach (var internalId in _hotkeyIdMap.Keys)
                {
                    UnregisterHotKey(_windowHandle, internalId);
                }
            }

            _hotkeyIdMap.Clear();
            _stringToIntMap.Clear();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int internalId = wParam.ToInt32();
                if (_hotkeyIdMap.TryGetValue(internalId, out string? hotkeyId))
                {
                    HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(hotkeyId));
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
        }
    }
}
