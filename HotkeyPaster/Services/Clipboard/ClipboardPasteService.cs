using System;
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
        public void PasteText(string text)
        {
            // Store current clipboard content
            string previousClipboard = string.Empty;
            if (System.Windows.Clipboard.ContainsText())
            {
                previousClipboard = System.Windows.Clipboard.GetText();
            }

            // Set our text to clipboard
            System.Windows.Clipboard.SetText(text);

            // Simulate Ctrl+V
            SendKeys.SendWait("^v");

            // Restore previous clipboard content after a short delay
            Thread.Sleep(100);
            if (!string.IsNullOrEmpty(previousClipboard))
            {
                System.Windows.Clipboard.SetText(previousClipboard);
            }
        }
    }
}
