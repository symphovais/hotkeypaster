using System;
using System.Threading.Tasks;
using TalkKeys.Services.Windowing;

namespace TalkKeys.Services.Transcription
{
    /// <summary>
    /// Simple pass-through text cleaner that returns the text unchanged.
    /// Used when no text cleaning service is available.
    /// </summary>
    public class PassThroughTextCleaner : ITextCleaner
    {
        public Task<string> CleanAsync(string rawText, Action<string>? onProgressUpdate = null, WindowContext? windowContext = null)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                throw new ArgumentException("Raw text cannot be null or empty", nameof(rawText));

            // Invoke progress callback immediately with the final text
            onProgressUpdate?.Invoke(rawText);

            return Task.FromResult(rawText);
        }
    }
}
