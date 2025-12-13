using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TalkKeys.Services.Clipboard
{
    public interface IClipboardPasteService
    {
        void PasteText(string text);
    }

    public sealed class ClipboardPasteService : IClipboardPasteService
    {
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 50;

        #region Win32 API for SendInput (more reliable than SendKeys)

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_V = 0x56;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public INPUTUNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        #endregion

        public void PasteText(string text)
        {
            // Store current clipboard content
            string previousClipboard = string.Empty;
            try
            {
                if (TryClipboardOperation(() => System.Windows.Clipboard.ContainsText()))
                {
                    previousClipboard = TryClipboardOperation(() => System.Windows.Clipboard.GetText()) ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                // Log but continue - we can still paste even if we can't preserve clipboard
                System.Diagnostics.Debug.WriteLine($"Failed to get clipboard content: {ex.Message}");
            }

            // Set our text to clipboard
            bool clipboardSet = false;
            try
            {
                clipboardSet = TryClipboardOperation(() =>
                {
                    System.Windows.Clipboard.SetText(text);
                    return true;
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set clipboard text after {MaxRetries} attempts: {ex.Message}", ex);
            }

            if (!clipboardSet)
            {
                throw new InvalidOperationException("Failed to set clipboard text - clipboard may be locked by another application");
            }

            // Simulate Ctrl+V using SendInput (more reliable than SendKeys)
            try
            {
                SendCtrlV();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to send paste command: {ex.Message}", ex);
            }

            // Restore previous clipboard content asynchronously
            // Using a background thread so we don't block the UI
            if (!string.IsNullOrEmpty(previousClipboard))
            {
                var clipboardToRestore = previousClipboard;
                System.Threading.Tasks.Task.Run(async () =>
                {
                    // Wait for paste to complete in target app
                    await System.Threading.Tasks.Task.Delay(300);

                    // Clipboard must be accessed from STA thread
                    var thread = new Thread(() =>
                    {
                        try
                        {
                            System.Windows.Clipboard.SetText(clipboardToRestore);
                        }
                        catch
                        {
                            // Ignore errors when restoring - not critical
                        }
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join(1000); // Timeout after 1 second
                });
            }
        }

        /// <summary>
        /// Sends Ctrl+V using the Windows SendInput API.
        /// More reliable than SendKeys.SendWait for various applications.
        /// </summary>
        private static void SendCtrlV()
        {
            var inputs = new INPUT[4];
            int inputSize = Marshal.SizeOf(typeof(INPUT));

            // Ctrl key down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = VK_CONTROL;
            inputs[0].u.ki.dwFlags = KEYEVENTF_KEYDOWN;

            // V key down
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = VK_V;
            inputs[1].u.ki.dwFlags = KEYEVENTF_KEYDOWN;

            // V key up
            inputs[2].type = INPUT_KEYBOARD;
            inputs[2].u.ki.wVk = VK_V;
            inputs[2].u.ki.dwFlags = KEYEVENTF_KEYUP;

            // Ctrl key up
            inputs[3].type = INPUT_KEYBOARD;
            inputs[3].u.ki.wVk = VK_CONTROL;
            inputs[3].u.ki.dwFlags = KEYEVENTF_KEYUP;

            uint sent = SendInput(4, inputs, inputSize);
            if (sent != 4)
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"SendInput failed: only {sent}/4 inputs sent (error: {error})");
            }
        }

        private T TryClipboardOperation<T>(Func<T> operation)
        {
            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    return operation();
                }
                catch (COMException) when (i < MaxRetries - 1)
                {
                    // Clipboard is busy, wait and retry
                    Thread.Sleep(RetryDelayMs);
                }
            }

            // Final attempt without catching
            return operation();
        }
    }
}
