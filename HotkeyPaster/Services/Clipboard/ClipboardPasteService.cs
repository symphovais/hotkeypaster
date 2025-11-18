using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

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

            // Simulate Ctrl+V
            try
            {
                SendKeys.SendWait("^v");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to send paste command: {ex.Message}", ex);
            }

            // Restore previous clipboard content after a short delay
            Thread.Sleep(100);
            if (!string.IsNullOrEmpty(previousClipboard))
            {
                try
                {
                    TryClipboardOperation(() =>
                    {
                        System.Windows.Clipboard.SetText(previousClipboard);
                        return true;
                    });
                }
                catch
                {
                    // Ignore errors when restoring - not critical
                }
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
