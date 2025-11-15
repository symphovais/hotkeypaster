using System;

namespace TalkKeys.Services.Windowing
{
    /// <summary>
    /// Service for detecting the context of the active window.
    /// </summary>
    public interface IActiveWindowContextService
    {
        /// <summary>
        /// Gets the context information for a given window handle.
        /// </summary>
        /// <param name="windowHandle">The handle of the window to analyze</param>
        /// <returns>WindowContext containing application and context information</returns>
        WindowContext GetWindowContext(IntPtr windowHandle);
    }
}
