using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace HotkeyPaster.Services.Windowing
{
    public sealed class WindowPositionService : IWindowPositionService
    {
        public void PositionBottomCenter(Window window, double bottomMargin = 20, IntPtr targetWindowHandle = default)
        {
            if (window == null) return;

            // Try to get the screen from the target window handle (where user is working)
            Screen? screen = null;
            try
            {
                // First priority: use the target window handle if provided
                if (targetWindowHandle != IntPtr.Zero)
                {
                    screen = Screen.FromHandle(targetWindowHandle);
                }
                // Second priority: use the window's own handle
                else
                {
                    var handle = new WindowInteropHelper(window).Handle;
                    if (handle != IntPtr.Zero)
                    {
                        screen = Screen.FromHandle(handle);
                    }
                }
            }
            catch
            {
                // ignore
            }

            // Fallback to primary screen
            if (screen == null)
            {
                screen = Screen.PrimaryScreen ?? (Screen.AllScreens.Length > 0 ? Screen.AllScreens[0] : null);
            }

            if (screen != null)
            {
                var wa = screen.WorkingArea;
                window.Left = wa.Left + (wa.Width - window.Width) / 2;
                window.Top = wa.Bottom - window.Height - bottomMargin;
            }
            else
            {
                // Final fallback: WPF SystemParameters
                var wa = SystemParameters.WorkArea;
                window.Left = wa.Left + (wa.Width - window.Width) / 2;
                window.Top = wa.Bottom - window.Height - bottomMargin;
            }
        }
    }
}
