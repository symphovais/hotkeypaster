using System;
using System.Windows.Forms;

namespace TalkKeys.Services.Tray
{
    public interface ITrayService
    {
        event EventHandler? SettingsRequested;
        event EventHandler? ShowcaseRequested;
        event EventHandler? ExitRequested;
        void InitializeTray();
        void DisposeTray();
    }

    public sealed class TrayService : ITrayService, IDisposable
    {
        public event EventHandler? SettingsRequested;
        public event EventHandler? ShowcaseRequested;
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
                Text = "TalkKeys - Ctrl+Shift+Q"
            };

            _notifyIcon.DoubleClick += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Settings", null, (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty));
            contextMenu.Items.Add("Design System Showcase", null, (s, e) => ShowcaseRequested?.Invoke(this, EventArgs.Empty));
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty));
            _notifyIcon.ContextMenuStrip = contextMenu;
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
