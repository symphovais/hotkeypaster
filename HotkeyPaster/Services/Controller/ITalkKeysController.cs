using System.Collections.Generic;
using System.Threading.Tasks;
using TalkKeys.Services.Auth;
using TalkKeys.Services.Windowing;

namespace TalkKeys.Services.Controller
{
    /// <summary>
    /// Interface for TalkKeysController to enable testing and mocking
    /// </summary>
    public interface ITalkKeysController
    {
        /// <summary>
        /// Gets the current status of TalkKeys
        /// </summary>
        TalkKeysStatus GetStatus();

        /// <summary>
        /// Gets TalkKeys capabilities for API discovery
        /// </summary>
        TalkKeysCapabilities GetCapabilities();

        /// <summary>
        /// Starts voice transcription recording
        /// </summary>
        Task<ControllerActionResult> StartTranscriptionAsync();

        /// <summary>
        /// Stops voice recording and triggers transcription
        /// </summary>
        Task<ControllerActionResult> StopTranscriptionAsync();

        /// <summary>
        /// Cancels the current recording without transcribing
        /// </summary>
        Task<ControllerActionResult> CancelTranscriptionAsync();

        /// <summary>
        /// Triggers the explain feature for selected text
        /// </summary>
        Task<ControllerActionResult> ExplainSelectedTextAsync();

        /// <summary>
        /// Gets list of available microphones
        /// </summary>
        List<MicrophoneInfo> GetMicrophones();

        /// <summary>
        /// Sets the active microphone
        /// </summary>
        ControllerActionResult SetMicrophone(int index);

        /// <summary>
        /// Gets current shortcut configurations
        /// </summary>
        Dictionary<string, string> GetShortcuts();

        /// <summary>
        /// Updates shortcut configurations
        /// </summary>
        ControllerActionResult SetShortcuts(Dictionary<string, string> shortcuts);

        /// <summary>
        /// Gets suggested actions for the given text and context
        /// </summary>
        Task<ActionSuggestionResult> GetSuggestedActionsAsync(string text, WindowContext? windowContext = null);

        /// <summary>
        /// Generates a reply based on instruction and original text
        /// </summary>
        Task<GeneratedReplyResult> GenerateReplyAsync(string originalText, string instruction, string contextType, WindowContext? windowContext = null);
    }
}
