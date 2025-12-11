using System;
using System.Collections.Generic;

namespace TalkKeys.PluginSdk
{
    /// <summary>
    /// Interface for plugins that add items to the system tray context menu.
    /// </summary>
    public interface ITrayMenuPlugin : IPlugin
    {
        /// <summary>
        /// Get menu items to add to the tray context menu.
        /// </summary>
        IReadOnlyList<PluginMenuItem> GetTrayMenuItems();

        /// <summary>
        /// Event raised when menu items need to be refreshed.
        /// For example, when a timer state changes and menu text needs updating.
        /// </summary>
        event EventHandler? TrayMenuItemsChanged;
    }

    /// <summary>
    /// Represents a menu item provided by a plugin.
    /// </summary>
    public class PluginMenuItem
    {
        /// <summary>
        /// Text displayed for this menu item.
        /// </summary>
        public string Text { get; set; } = "";

        /// <summary>
        /// Optional icon (emoji or character).
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Action to execute when clicked.
        /// </summary>
        public Action? OnClick { get; set; }

        /// <summary>
        /// Whether this menu item is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// If true, this is a separator (Text and other properties ignored).
        /// </summary>
        public bool IsSeparator { get; set; }

        /// <summary>
        /// Optional sub-items for nested menus.
        /// </summary>
        public IReadOnlyList<PluginMenuItem>? SubItems { get; set; }
    }
}
