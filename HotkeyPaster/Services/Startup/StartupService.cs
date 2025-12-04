using System;
using Microsoft.Win32;

namespace TalkKeys.Services.Startup
{
    /// <summary>
    /// Service for managing Windows startup registration.
    /// </summary>
    public interface IStartupService
    {
        /// <summary>
        /// Gets whether the application is set to start with Windows.
        /// </summary>
        bool IsStartupEnabled { get; }

        /// <summary>
        /// Enables or disables starting the application with Windows.
        /// </summary>
        void SetStartupEnabled(bool enabled);
    }

    public sealed class StartupService : IStartupService
    {
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "TalkKeys";

        public bool IsStartupEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                    var value = key?.GetValue(AppName) as string;
                    return !string.IsNullOrEmpty(value);
                }
                catch
                {
                    return false;
                }
            }
        }

        public void SetStartupEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key == null) return;

                if (enabled)
                {
                    // Get the current executable path
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch
            {
                // Silently fail - user may not have permission
            }
        }
    }
}
