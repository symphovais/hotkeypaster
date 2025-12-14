using System;
using System.Windows.Forms;

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
    }

    public sealed class TrayService : ITrayService, IDisposable
    {
        public event EventHandler? SettingsRequested;
        public event EventHandler? AboutRequested;
        public event EventHandler? ExitRequested;

        private NotifyIcon? _notifyIcon;

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
