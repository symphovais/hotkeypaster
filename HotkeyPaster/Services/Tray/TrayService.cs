using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TalkKeys.PluginSdk;

namespace TalkKeys.Services.Tray
{
    public interface ITrayService
    {
        event EventHandler? SettingsRequested;
        event EventHandler? AboutRequested;
        event EventHandler? ExitRequested;
        void InitializeTray();
        void DisposeTray();
        void AddUpdateMenuItem(Action onUpdate);
        void SetPluginMenuItems(IReadOnlyList<PluginMenuItem> items);
        void RefreshPluginMenuItems(IReadOnlyList<PluginMenuItem> items);
    }

    public sealed class TrayService : ITrayService, IDisposable
    {
        public event EventHandler? SettingsRequested;
        public event EventHandler? AboutRequested;
        public event EventHandler? ExitRequested;

        private NotifyIcon? _notifyIcon;
        private readonly List<ToolStripItem> _pluginMenuItems = new();
        private int _pluginMenuInsertIndex = -1;

        public void InitializeTray()
        {
            // Load custom icon from the application directory
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            var icon = System.IO.File.Exists(iconPath)
                ? new System.Drawing.Icon(iconPath)
                : System.Drawing.SystemIcons.Application;

            _notifyIcon = new NotifyIcon
            {
                Icon = icon,
                Visible = true,
                Text = "TalkKeys - Press Ctrl+Shift+Space to record"
            };

            _notifyIcon.DoubleClick += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

            var contextMenu = new ContextMenuStrip();

            contextMenu.Items.Add("Settings", null, (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty));
            contextMenu.Items.Add("About", null, (s, e) => AboutRequested?.Invoke(this, EventArgs.Empty));
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty));

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        public void AddUpdateMenuItem(Action onUpdate)
        {
            if (_notifyIcon?.ContextMenuStrip == null) return;

            var menu = _notifyIcon.ContextMenuStrip;

            // Check if update item already exists
            foreach (ToolStripItem item in menu.Items)
            {
                if (item.Text == "Restart to Update")
                    return; // Already added
            }

            // Insert update item at the top with emphasis
            var updateItem = new ToolStripMenuItem("Restart to Update")
            {
                Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(99, 102, 241) // Purple accent
            };
            updateItem.Click += (s, e) => onUpdate?.Invoke();

            menu.Items.Insert(0, updateItem);
            menu.Items.Insert(1, new ToolStripSeparator());
        }

        public void SetPluginMenuItems(IReadOnlyList<PluginMenuItem> items)
        {
            if (_notifyIcon?.ContextMenuStrip == null) return;

            var menu = _notifyIcon.ContextMenuStrip;

            // Remove existing plugin items
            foreach (var item in _pluginMenuItems)
            {
                menu.Items.Remove(item);
            }
            _pluginMenuItems.Clear();

            if (items == null || items.Count == 0) return;

            // Find insert position (before Settings, after any update items)
            _pluginMenuInsertIndex = 0;
            for (int i = 0; i < menu.Items.Count; i++)
            {
                if (menu.Items[i].Text == "Settings")
                {
                    _pluginMenuInsertIndex = i;
                    break;
                }
            }

            // Add plugin items
            int insertAt = _pluginMenuInsertIndex;

            // Add separator before plugin items if we have any
            var separator = new ToolStripSeparator();
            menu.Items.Insert(insertAt++, separator);
            _pluginMenuItems.Add(separator);

            foreach (var pluginItem in items)
            {
                var menuItem = CreateToolStripItem(pluginItem);
                menu.Items.Insert(insertAt++, menuItem);
                _pluginMenuItems.Add(menuItem);
            }
        }

        public void RefreshPluginMenuItems(IReadOnlyList<PluginMenuItem> items)
        {
            // Simply re-set the items - this handles all updates
            SetPluginMenuItems(items);
        }

        private ToolStripItem CreateToolStripItem(PluginMenuItem item)
        {
            if (item.IsSeparator)
                return new ToolStripSeparator();

            var text = string.IsNullOrEmpty(item.Icon) ? item.Text : $"{item.Icon} {item.Text}";
            var menuItem = new ToolStripMenuItem(text)
            {
                Enabled = item.IsEnabled
            };

            if (item.OnClick != null)
            {
                menuItem.Click += (s, e) => item.OnClick();
            }

            // Handle sub-items
            if (item.SubItems != null && item.SubItems.Count > 0)
            {
                foreach (var subItem in item.SubItems)
                {
                    menuItem.DropDownItems.Add(CreateToolStripItem(subItem));
                }
            }

            return menuItem;
        }

        public void DisposeTray()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }

        public void Dispose()
        {
            DisposeTray();
        }
    }
}
