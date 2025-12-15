using System;
using System.Runtime.InteropServices;
using System.Threading;
using WindowsInput;

namespace TalkKeys.Services.Clipboard
{
    public interface IClipboardPasteService
    {
        PasteResult PasteText(string text);
    }

    public sealed class ClipboardPasteService : IClipboardPasteService
    {
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 50;

        private readonly InputSimulator _inputSimulator = new();

        public PasteResult PasteText(string text)
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
                return PasteResult.Fail($"Failed to set clipboard text after {MaxRetries} attempts: {ex.Message}");
            }

            if (!clipboardSet)
            {
                return PasteResult.Fail("Failed to set clipboard text - clipboard may be locked by another application");
            }

            // Simulate Ctrl+V using InputSimulator library (well-tested, widely used)
            try
            {
                _inputSimulator.Keyboard.ModifiedKeyStroke(
                    VirtualKeyCode.CONTROL,
                    VirtualKeyCode.VK_V);
            }
            catch (Exception ex)
            {
                return PasteResult.Fail($"Failed to send paste command: {ex.Message}");
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

            return PasteResult.Ok();
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
