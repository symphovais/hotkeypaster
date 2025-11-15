using System;
using System.Windows;

namespace TalkKeys.Services.Windowing
{
    public interface IWindowPositionService
    {
        /// <summary>
        /// Positions the window at the bottom center of the screen.
        /// </summary>
        /// <param name="window">The window to position</param>
        /// <param name="bottomMargin">Distance from bottom of screen in pixels</param>
        /// <param name="targetWindowHandle">Optional handle of the window whose screen should be used for positioning</param>
        void PositionBottomCenter(Window window, double bottomMargin = 20, IntPtr targetWindowHandle = default);
    }
}
