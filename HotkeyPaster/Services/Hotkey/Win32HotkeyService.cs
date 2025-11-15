using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TalkKeys.Services.Hotkey
{
    public sealed class Win32HotkeyService : IHotkeyService, IDisposable
    {
        public event EventHandler? HotkeyPressed;

        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_Q = 0x51; // Q key

        private IntPtr _windowHandle;
        private HwndSource? _source;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public void RegisterHotkey()
        {
            if (_windowHandle == IntPtr.Zero || _source != null) return;

            _source = HwndSource.FromHwnd(_windowHandle);
            _source.AddHook(HwndHook);

            bool registered = RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_Q);
            if (!registered)
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to register hotkey Ctrl+Shift+Q. Error: {error}");
            }
        }

        public void UnregisterHotkey()
        {
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                _source = null;
            }
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
            }
        }

        public void SetWindowHandle(IntPtr handle)
        {
            _windowHandle = handle;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterHotkey();
        }
    }
}
