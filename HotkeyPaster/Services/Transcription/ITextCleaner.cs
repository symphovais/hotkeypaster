using System;
using System.Threading.Tasks;

namespace HotkeyPaster.Services.Transcription
{
    /// <summary>
    /// Interface for cleaning and formatting transcribed text.
    /// </summary>
    public interface ITextCleaner
    {
        /// <summary>
        /// Cleans and formats raw transcribed text.
        /// </summary>
        /// <param name="rawText">Raw transcribed text to clean</param>
        /// <param name="onProgressUpdate">Optional callback for streaming progress updates</param>
        /// <returns>Cleaned and formatted text</returns>
        Task<string> CleanAsync(string rawText, Action<string>? onProgressUpdate = null);
    }
}
