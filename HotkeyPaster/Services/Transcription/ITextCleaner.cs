using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TalkKeys.Services.Windowing;

namespace TalkKeys.Services.Transcription
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
        /// <param name="windowContext">Optional context about where the transcription will be pasted</param>
        /// <param name="wordsList">Optional list of preferred word spellings to use</param>
        /// <returns>Cleaned and formatted text</returns>
        Task<string> CleanAsync(
            string rawText,
            Action<string>? onProgressUpdate = null,
            WindowContext? windowContext = null,
            IReadOnlyList<string>? wordsList = null);
    }
}
