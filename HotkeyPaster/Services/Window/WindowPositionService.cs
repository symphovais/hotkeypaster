using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using TalkKeys.Logging;

namespace TalkKeys.Services.Windowing
{
    public sealed class WindowPositionService : IWindowPositionService
    {
        private readonly ILogger? _logger;

        public WindowPositionService(ILogger? logger = null)
        {
            _logger = logger;
        }

        private double GetDpiScale(Window window)
        {
            try
            {
                var source = PresentationSource.FromVisual(window);
                if (source?.CompositionTarget != null)
                {
                    return source.CompositionTarget.TransformToDevice.M11;
                }
            }
            catch
            {
                // Ignore
            }
            return 1.0;
        }

        public void PositionBottomCenter(Window window, double bottomMargin = 20, IntPtr targetWindowHandle = default)
        {
            if (window == null) return;

            // Ensure window has valid dimensions
            if (window.Width <= 0 || window.Height <= 0)
            {
                window.Width = 320;
                window.Height = 120;
            }

            Screen? screen = GetValidScreen(targetWindowHandle, window);
            
            if (screen != null)
            {
                var wa = screen.WorkingArea;
                PositionWindowOnScreen(window, wa, bottomMargin);
            }
            else
            {
                // Final fallback: WPF SystemParameters
                var wa = SystemParameters.WorkArea;
                window.Left = wa.Left + (wa.Width - window.Width) / 2;
                window.Top = wa.Bottom - window.Height - bottomMargin;
            }

            // Final validation: ensure window is actually visible on some screen
            EnsureWindowIsVisible(window, bottomMargin);
        }

        private Screen? GetValidScreen(IntPtr targetWindowHandle, Window window)
        {
            Screen? screen = null;

            // Log all available screens for debugging
            _logger?.Log($"Available screens: {Screen.AllScreens.Length}");
            foreach (var s in Screen.AllScreens)
            {
                _logger?.Log($"  Screen: Bounds={s.Bounds}, Primary={s.Primary}, DeviceName={s.DeviceName}");
            }

            try
            {
                // First priority: use the target window handle if provided
                if (targetWindowHandle != IntPtr.Zero)
                {
                    screen = Screen.FromHandle(targetWindowHandle);
                    _logger?.Log($"Screen from target handle {targetWindowHandle}: Bounds={screen?.Bounds}, DeviceName={screen?.DeviceName}");
                    if (IsScreenValid(screen))
                    {
                        _logger?.Log($"Using screen from target handle - valid");
                        return screen;
                    }
                    _logger?.Log($"Screen from target handle - INVALID, trying fallback");
                }

                // Second priority: use the window's own handle
                var handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    screen = Screen.FromHandle(handle);
                    _logger?.Log($"Screen from window handle {handle}: Bounds={screen?.Bounds}");
                    if (IsScreenValid(screen))
                    {
                        _logger?.Log($"Using screen from window handle - valid");
                        return screen;
                    }
                    _logger?.Log($"Screen from window handle - INVALID, trying fallback");
                }

                // Third priority: check if window's current position is on a valid screen
                if (window.Left != 0 || window.Top != 0)
                {
                    var point = new System.Drawing.Point((int)window.Left, (int)window.Top);
                    screen = Screen.FromPoint(point);
                    _logger?.Log($"Screen from window position ({window.Left},{window.Top}): Bounds={screen?.Bounds}");
                    if (IsScreenValid(screen))
                    {
                        _logger?.Log($"Using screen from window position - valid");
                        return screen;
                    }
                    _logger?.Log($"Screen from window position - INVALID, trying fallback");
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"Error getting screen: {ex.Message}");
            }

            // Fourth priority: find the primary screen
            screen = Screen.PrimaryScreen;
            _logger?.Log($"Primary screen: Bounds={screen?.Bounds}, DeviceName={screen?.DeviceName}");
            if (IsScreenValid(screen))
            {
                _logger?.Log($"Using primary screen - valid");
                return screen;
            }
            _logger?.Log($"Primary screen - INVALID, trying any valid screen");

            // Fifth priority: find any valid screen
            foreach (var s in Screen.AllScreens)
            {
                _logger?.Log($"Checking screen: Bounds={s.Bounds}, DeviceName={s.DeviceName}");
                if (IsScreenValid(s))
                {
                    _logger?.Log($"Using first valid screen: {s.DeviceName}");
                    return s;
                }
            }

            _logger?.Log($"NO VALID SCREEN FOUND!");
            return null;
        }

        private bool IsScreenValid(Screen? screen)
        {
            if (screen == null) return false;

            var wa = screen.WorkingArea;
            var bounds = screen.Bounds;

            // Check if screen has reasonable dimensions
            if (wa.Width <= 0 || wa.Height <= 0) return false;
            if (bounds.Width <= 0 || bounds.Height <= 0) return false;

            // Check if screen coordinates are reasonable (not a disconnected/phantom monitor)
            // In typical multi-monitor setups, screens are arranged side-by-side or stacked
            // A screen at -9600 is clearly a disconnected monitor that Windows still remembers
            // Allow some negative values (up to -5000) for reasonable multi-monitor arrangements
            if (bounds.Left < -5000 || bounds.Top < -5000) return false;
            if (bounds.Right > 20000 || bounds.Bottom > 10000) return false;

            // Additional check: if the screen is too far from origin, it's likely disconnected
            // Most real multi-monitor setups don't exceed 3-4 monitors in any direction
            var distanceFromOrigin = Math.Abs(bounds.Left) + Math.Abs(bounds.Top);
            if (distanceFromOrigin > 10000) return false;

            // Check if this screen is actually accessible (not a disconnected virtual display)
            // A disconnected display might still be in Screen.AllScreens but with invalid coordinates
            try
            {
                // Verify the screen is in the current screen collection
                if (!Screen.AllScreens.Contains(screen)) return false;
            }
            catch
            {
                return false;
            }

            return true;
        }

        private void PositionWindowOnScreen(Window window, System.Drawing.Rectangle workingArea, double bottomMargin)
        {
            // Get DPI scale factor (e.g., 1.5 for 150% scaling)
            double dpiScale = GetDpiScale(window);
            
            _logger?.Log($"PositionWindowOnScreen: WorkingArea={workingArea}, WindowSize={window.Width}x{window.Height}, DpiScale={dpiScale}");

            // Convert physical screen coordinates to WPF logical coordinates
            // Screen.WorkingArea returns physical pixels, but WPF uses logical pixels
            double logicalLeft = workingArea.Left / dpiScale;
            double logicalTop = workingArea.Top / dpiScale;
            double logicalWidth = workingArea.Width / dpiScale;
            double logicalHeight = workingArea.Height / dpiScale;

            _logger?.Log($"Logical screen: Left={logicalLeft}, Top={logicalTop}, Width={logicalWidth}, Height={logicalHeight}");

            // Calculate centered position at bottom of screen (in logical pixels)
            double left = logicalLeft + (logicalWidth - window.Width) / 2;
            double top = logicalTop + logicalHeight - window.Height - bottomMargin;

            _logger?.Log($"Initial calculated position: Left={left}, Top={top}");

            // Ensure window stays within screen bounds with some padding
            const double padding = 10;
            
            // Horizontal bounds check
            if (left < logicalLeft + padding)
            {
                left = logicalLeft + padding;
            }
            else if (left + window.Width > logicalLeft + logicalWidth - padding)
            {
                left = logicalLeft + logicalWidth - window.Width - padding;
            }

            // Vertical bounds check
            if (top < logicalTop + padding)
            {
                top = logicalTop + padding;
            }
            else if (top + window.Height > logicalTop + logicalHeight - padding)
            {
                top = logicalTop + logicalHeight - window.Height - padding;
            }

            _logger?.Log($"After bounds check: Left={left}, Top={top}");

            window.Left = left;
            window.Top = top;
        }

        private void EnsureWindowIsVisible(Window window, double bottomMargin)
        {
            // Check if the window's position is on any valid screen
            bool isVisible = false;

            foreach (var screen in Screen.AllScreens)
            {
                if (!IsScreenValid(screen)) continue;

                var bounds = screen.Bounds;
                var windowRect = new System.Drawing.Rectangle(
                    (int)window.Left,
                    (int)window.Top,
                    (int)window.Width,
                    (int)window.Height
                );

                // Check if at least 50% of the window is visible on this screen
                var intersection = System.Drawing.Rectangle.Intersect(bounds, windowRect);
                double visibleArea = intersection.Width * intersection.Height;
                double totalArea = window.Width * window.Height;

                if (visibleArea >= totalArea * 0.5)
                {
                    isVisible = true;
                    break;
                }
            }

            // If window is not visible on any screen, force it to primary screen
            if (!isVisible)
            {
                var primaryScreen = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault(IsScreenValid);
                if (primaryScreen != null)
                {
                    var wa = primaryScreen.WorkingArea;
                    PositionWindowOnScreen(window, wa, bottomMargin);
                }
                else
                {
                    // Ultimate fallback: center on virtual screen
                    window.Left = (SystemParameters.VirtualScreenWidth - window.Width) / 2;
                    window.Top = (SystemParameters.VirtualScreenHeight - window.Height) / 2;
                }
            }
        }

        public void PositionTopRight(Window window, double marginRight = 20, double marginTop = 20)
        {
            if (window == null) return;

            var screen = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault(IsScreenValid);
            if (screen == null)
            {
                // Fallback to WPF SystemParameters
                var wa = SystemParameters.WorkArea;
                window.Left = wa.Right - window.Width - marginRight;
                window.Top = wa.Top + marginTop;
                return;
            }

            var workingArea = screen.WorkingArea;
            double dpiScale = GetDpiScale(window);

            // Convert to logical coordinates
            double logicalRight = (workingArea.Left + workingArea.Width) / dpiScale;
            double logicalTop = workingArea.Top / dpiScale;

            window.Left = logicalRight - window.Width - marginRight;
            window.Top = logicalTop + marginTop;

            EnsureVisible(window);
        }

        public void PositionAt(Window window, double? x, double? y, DefaultWindowPosition defaultPosition = DefaultWindowPosition.TopRight)
        {
            if (window == null) return;

            // If both coordinates provided and valid, use them
            if (x.HasValue && y.HasValue && x >= 0 && y >= 0)
            {
                window.Left = x.Value;
                window.Top = y.Value;

                // Validate position is on a visible screen
                if (IsWindowOnValidScreen(window))
                {
                    return;
                }
                // Position is off-screen, fall through to default positioning
            }

            // Use default position
            switch (defaultPosition)
            {
                case DefaultWindowPosition.BottomCenter:
                    PositionBottomCenter(window);
                    break;
                case DefaultWindowPosition.TopRight:
                    PositionTopRight(window);
                    break;
                case DefaultWindowPosition.TopLeft:
                    PositionTopLeft(window);
                    break;
                case DefaultWindowPosition.Center:
                    PositionCenter(window);
                    break;
            }
        }

        public void EnsureVisible(Window window)
        {
            if (window == null) return;

            if (!IsWindowOnValidScreen(window))
            {
                // Reposition to primary screen top-right
                PositionTopRight(window);
            }
        }

        private void PositionTopLeft(Window window, double marginLeft = 20, double marginTop = 20)
        {
            var screen = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault(IsScreenValid);
            if (screen == null)
            {
                window.Left = marginLeft;
                window.Top = marginTop;
                return;
            }

            var workingArea = screen.WorkingArea;
            double dpiScale = GetDpiScale(window);

            double logicalLeft = workingArea.Left / dpiScale;
            double logicalTop = workingArea.Top / dpiScale;

            window.Left = logicalLeft + marginLeft;
            window.Top = logicalTop + marginTop;
        }

        private void PositionCenter(Window window)
        {
            var screen = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault(IsScreenValid);
            if (screen == null)
            {
                var wa = SystemParameters.WorkArea;
                window.Left = wa.Left + (wa.Width - window.Width) / 2;
                window.Top = wa.Top + (wa.Height - window.Height) / 2;
                return;
            }

            var workingArea = screen.WorkingArea;
            double dpiScale = GetDpiScale(window);

            double logicalLeft = workingArea.Left / dpiScale;
            double logicalTop = workingArea.Top / dpiScale;
            double logicalWidth = workingArea.Width / dpiScale;
            double logicalHeight = workingArea.Height / dpiScale;

            window.Left = logicalLeft + (logicalWidth - window.Width) / 2;
            window.Top = logicalTop + (logicalHeight - window.Height) / 2;
        }

        private bool IsWindowOnValidScreen(Window window)
        {
            foreach (var screen in Screen.AllScreens)
            {
                if (!IsScreenValid(screen)) continue;

                var bounds = screen.Bounds;
                var windowRect = new System.Drawing.Rectangle(
                    (int)window.Left,
                    (int)window.Top,
                    (int)window.Width,
                    (int)window.Height
                );

                // Check if at least 50% of the window is visible on this screen
                var intersection = System.Drawing.Rectangle.Intersect(bounds, windowRect);
                double visibleArea = intersection.Width * intersection.Height;
                double totalArea = window.Width * window.Height;

                if (visibleArea >= totalArea * 0.5)
                {
                    return true;
                }
            }
            return false;
        }

        public void PositionNearCursor(Window window, double cursorX, double cursorY, double offsetX = 15, double offsetY = 15)
        {
            if (window == null) return;

            _logger?.Log($"[WindowPosition] PositionNearCursor: cursor=({cursorX},{cursorY}), offset=({offsetX},{offsetY})");

            // Find the screen containing the cursor (using physical pixel coordinates)
            var cursorPoint = new System.Drawing.Point((int)cursorX, (int)cursorY);
            var screen = Screen.FromPoint(cursorPoint);

            // Validate screen
            if (!IsScreenValid(screen))
            {
                _logger?.Log($"[WindowPosition] Screen at cursor is invalid, using primary");
                screen = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault(IsScreenValid);
            }

            if (screen == null)
            {
                _logger?.Log($"[WindowPosition] No valid screen found, using fallback");
                window.Left = cursorX + offsetX;
                window.Top = cursorY + offsetY;
                return;
            }

            _logger?.Log($"[WindowPosition] Screen: {screen.DeviceName}, Bounds={screen.Bounds}, WorkingArea={screen.WorkingArea}");

            // Get the DPI scale for the target screen
            // Note: We need DPI for the specific screen, not the window's current screen
            double dpiScale = GetDpiForScreen(screen);
            _logger?.Log($"[WindowPosition] DPI scale for screen: {dpiScale}");

            // Convert cursor position from physical pixels to WPF logical units
            double logicalCursorX = cursorX / dpiScale;
            double logicalCursorY = cursorY / dpiScale;

            // Calculate initial position with offset
            double x = logicalCursorX + offsetX;
            double y = logicalCursorY + offsetY;

            _logger?.Log($"[WindowPosition] Initial position (logical): ({x},{y})");

            // Convert screen working area to logical units
            var wa = screen.WorkingArea;
            double logicalLeft = wa.Left / dpiScale;
            double logicalTop = wa.Top / dpiScale;
            double logicalRight = (wa.Left + wa.Width) / dpiScale;
            double logicalBottom = (wa.Top + wa.Height) / dpiScale;

            _logger?.Log($"[WindowPosition] WorkArea (logical): L={logicalLeft}, T={logicalTop}, R={logicalRight}, B={logicalBottom}");

            // Ensure window has been measured
            window.UpdateLayout();
            double windowWidth = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
            double windowHeight = window.ActualHeight > 0 ? window.ActualHeight : window.Height;

            // Use default size if still not valid (including NaN)
            if (double.IsNaN(windowWidth) || windowWidth <= 0) windowWidth = 320;
            if (double.IsNaN(windowHeight) || windowHeight <= 0) windowHeight = 150;

            _logger?.Log($"[WindowPosition] Window size: {windowWidth}x{windowHeight}");

            const double padding = 10;

            // Adjust X if window goes beyond right edge
            if (x + windowWidth > logicalRight - padding)
            {
                x = logicalRight - windowWidth - padding;
                _logger?.Log($"[WindowPosition] Adjusted X (right edge): {x}");
            }

            // Adjust X if window goes beyond left edge
            if (x < logicalLeft + padding)
            {
                x = logicalLeft + padding;
                _logger?.Log($"[WindowPosition] Adjusted X (left edge): {x}");
            }

            // Adjust Y if window goes beyond bottom edge - show above cursor
            if (y + windowHeight > logicalBottom - padding)
            {
                y = logicalCursorY - windowHeight - offsetY;
                _logger?.Log($"[WindowPosition] Adjusted Y (bottom edge, showing above cursor): {y}");
            }

            // Adjust Y if window goes beyond top edge
            if (y < logicalTop + padding)
            {
                y = logicalTop + padding;
                _logger?.Log($"[WindowPosition] Adjusted Y (top edge): {y}");
            }

            _logger?.Log($"[WindowPosition] Final position: ({x},{y})");

            window.Left = x;
            window.Top = y;
        }

        /// <summary>
        /// Gets the DPI scale factor for a specific screen.
        /// </summary>
        private double GetDpiForScreen(Screen screen)
        {
            try
            {
                // Try to get DPI using Windows API (works for per-monitor DPI)
                var hMonitor = MonitorFromPoint(
                    new System.Drawing.Point(screen.Bounds.Left + screen.Bounds.Width / 2, screen.Bounds.Top + screen.Bounds.Height / 2),
                    MONITOR_DEFAULTTONEAREST);

                if (hMonitor != IntPtr.Zero)
                {
                    uint dpiX, dpiY;
                    int result = GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                    if (result == 0) // S_OK
                    {
                        return dpiX / 96.0; // 96 is the default DPI
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[WindowPosition] GetDpiForMonitor failed: {ex.Message}");
            }

            // Fallback to 1.0 (100% scaling)
            return 1.0;
        }

        // P/Invoke declarations for per-monitor DPI
        private const int MDT_EFFECTIVE_DPI = 0;
        private const int MONITOR_DEFAULTTONEAREST = 2;

        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, int dwFlags);
    }
}
