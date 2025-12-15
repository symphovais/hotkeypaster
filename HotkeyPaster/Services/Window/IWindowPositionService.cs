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

        /// <summary>
        /// Positions the window at the top-right corner of the screen.
        /// </summary>
        /// <param name="window">The window to position</param>
        /// <param name="marginRight">Distance from right edge</param>
        /// <param name="marginTop">Distance from top edge</param>
        void PositionTopRight(Window window, double marginRight = 20, double marginTop = 20);

        /// <summary>
        /// Positions the window at specific coordinates, ensuring it stays on a valid screen.
        /// </summary>
        /// <param name="window">The window to position</param>
        /// <param name="x">X coordinate (null for default positioning)</param>
        /// <param name="y">Y coordinate (null for default positioning)</param>
        /// <param name="defaultPosition">Default position if coordinates are invalid</param>
        void PositionAt(Window window, double? x, double? y, DefaultWindowPosition defaultPosition = DefaultWindowPosition.TopRight);

        /// <summary>
        /// Ensures the window is visible on a valid screen, repositioning if necessary.
        /// </summary>
        /// <param name="window">The window to validate</param>
        void EnsureVisible(Window window);

        /// <summary>
        /// Positions the window near the cursor position, ensuring it stays on the screen.
        /// </summary>
        /// <param name="window">The window to position</param>
        /// <param name="cursorX">Cursor X position in screen coordinates (physical pixels)</param>
        /// <param name="cursorY">Cursor Y position in screen coordinates (physical pixels)</param>
        /// <param name="offsetX">Horizontal offset from cursor (default 15)</param>
        /// <param name="offsetY">Vertical offset from cursor (default 15)</param>
        void PositionNearCursor(Window window, double cursorX, double cursorY, double offsetX = 15, double offsetY = 15);
    }

    /// <summary>
    /// Default positions for windows when no saved position is available.
    /// </summary>
    public enum DefaultWindowPosition
    {
        BottomCenter,
        TopRight,
        TopLeft,
        Center
    }
}
