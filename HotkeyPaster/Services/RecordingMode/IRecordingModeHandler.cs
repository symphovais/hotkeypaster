using System.Threading.Tasks;
using TalkKeys.Services.Pipeline;

namespace TalkKeys.Services.RecordingMode
{
    /// <summary>
    /// Defines the behavior and UI for different recording modes (clipboard, diary, notes, etc.)
    /// </summary>
    public interface IRecordingModeHandler
    {
        /// <summary>
        /// Gets the title shown during recording (e.g., "Recording..." or "Recording Diary Entry...")
        /// </summary>
        string GetModeTitle();

        /// <summary>
        /// Gets the instruction text shown to the user (e.g., "Press Space to finish")
        /// </summary>
        string GetInstructionText();

        /// <summary>
        /// Gets the icon/emoji shown during recording
        /// </summary>
        string GetRecordingIcon();

        /// <summary>
        /// Handles the transcription result after recording completes
        /// </summary>
        /// <param name="result">The transcription result from the pipeline</param>
        /// <returns>Task representing the async operation</returns>
        Task HandleTranscriptionAsync(PipelineResult result);

        /// <summary>
        /// Gets the success message to display after handling completes
        /// </summary>
        /// <param name="result">The transcription result</param>
        /// <returns>Success message (e.g., "3 words pasted" or "Diary entry saved")</returns>
        string GetSuccessMessage(PipelineResult result);
    }
}
