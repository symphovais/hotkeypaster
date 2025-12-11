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
    }
}
