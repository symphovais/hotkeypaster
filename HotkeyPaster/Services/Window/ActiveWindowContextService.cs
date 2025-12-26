using System;
using TalkKeys.Services.Win32;

namespace TalkKeys.Services.Windowing
{
    /// <summary>
    /// Service for detecting the context of the active window using Windows API.
    /// </summary>
    public class ActiveWindowContextService : IActiveWindowContextService
    {
        public WindowContext GetWindowContext(IntPtr windowHandle)
        {
            var context = new WindowContext();

            if (windowHandle == IntPtr.Zero)
                return context;

            try
            {
                // Get window title using centralized helper
                context.WindowTitle = Win32Helper.GetWindowTitle(windowHandle);

                // Get process name using centralized helper
                context.ProcessName = Win32Helper.GetProcessNameForWindow(windowHandle) ?? string.Empty;
            }
            catch (Exception)
            {
                // Return partial context if available
            }

            return context;
        }
    }
}
