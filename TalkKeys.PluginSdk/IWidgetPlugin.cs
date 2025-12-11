using System;

namespace TalkKeys.PluginSdk
{
    /// <summary>
    /// Interface for plugins that provide a floating widget window.
    /// </summary>
    public interface IWidgetPlugin : IPlugin
    {
        /// <summary>
        /// Whether the widget is currently visible.
        /// </summary>
        bool IsWidgetVisible { get; }

        /// <summary>
        /// Show the plugin's widget.
        /// </summary>
        void ShowWidget();

        /// <summary>
        /// Hide the plugin's widget.
        /// </summary>
        void HideWidget();

        /// <summary>
        /// Toggle widget visibility.
        /// </summary>
        void ToggleWidget();

        /// <summary>
        /// Position the widget at specified coordinates.
        /// </summary>
        /// <param name="x">X coordinate, or null to use default position.</param>
        /// <param name="y">Y coordinate, or null to use default position.</param>
        void PositionWidget(double? x, double? y);

        /// <summary>
        /// Event raised when widget position changes (for persistence).
        /// </summary>
        event EventHandler<WidgetPositionChangedEventArgs>? WidgetPositionChanged;

        /// <summary>
        /// Event raised when widget visibility changes.
        /// </summary>
        event EventHandler<WidgetVisibilityChangedEventArgs>? WidgetVisibilityChanged;
    }

    /// <summary>
    /// Event arguments for widget position changes.
    /// </summary>
    public class WidgetPositionChangedEventArgs : EventArgs
    {
        public double X { get; }
        public double Y { get; }

        public WidgetPositionChangedEventArgs(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Event arguments for widget visibility changes.
    /// </summary>
    public class WidgetVisibilityChangedEventArgs : EventArgs
    {
        public bool IsVisible { get; }

        public WidgetVisibilityChangedEventArgs(bool isVisible)
        {
            IsVisible = isVisible;
        }
    }
}
