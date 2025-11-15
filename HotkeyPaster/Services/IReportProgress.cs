using System;

namespace TalkKeys.Services
{
    /// <summary>
    /// Interface for services that can report progress updates.
    /// </summary>
    public interface IReportProgress
    {
        /// <summary>
        /// Event raised when progress status changes.
        /// </summary>
        event EventHandler<ProgressEventArgs>? ProgressChanged;
    }

    /// <summary>
    /// Event arguments for progress updates.
    /// </summary>
    public class ProgressEventArgs : EventArgs
    {
        /// <summary>
        /// The status message describing the current operation.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Optional percentage completion (0-100), or null if not applicable.
        /// </summary>
        public int? PercentComplete { get; }

        public ProgressEventArgs(string message, int? percentComplete = null)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            PercentComplete = percentComplete;
        }
    }
}
