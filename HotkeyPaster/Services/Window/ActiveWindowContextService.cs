using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TalkKeys.Services.Windowing
{
    /// <summary>
    /// Service for detecting the context of the active window using Windows API.
    /// </summary>
    public class ActiveWindowContextService : IActiveWindowContextService
    {
        // Windows API imports
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public WindowContext GetWindowContext(IntPtr windowHandle)
        {
            var context = new WindowContext();

            if (windowHandle == IntPtr.Zero)
                return context;

            try
            {
                // Get window title
                context.WindowTitle = GetWindowTitle(windowHandle);

                // Get process information
                if (GetWindowThreadProcessId(windowHandle, out uint processId) != 0)
                {
                    try
                    {
                        var process = Process.GetProcessById((int)processId);
                        context.ProcessName = process.ProcessName;

                        // Detect application type and context
                        DetectApplicationTypeAndContext(context);
                    }
                    catch (ArgumentException)
                    {
                        // Process may have exited
                    }
                }
            }
            catch (Exception)
            {
                // Return partial context if available
            }

            return context;
        }

        private string GetWindowTitle(IntPtr windowHandle)
        {
            try
            {
                int length = GetWindowTextLength(windowHandle);
                if (length == 0)
                    return string.Empty;

                var builder = new StringBuilder(length + 1);
                GetWindowText(windowHandle, builder, builder.Capacity);
                return builder.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private void DetectApplicationTypeAndContext(WindowContext context)
        {
            // No parsing needed - just pass raw info to LLM
            // The LLM is much better at understanding context than our hardcoded rules
        }
    }
}
