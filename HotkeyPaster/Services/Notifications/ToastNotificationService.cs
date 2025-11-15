using Microsoft.Toolkit.Uwp.Notifications; // v7.1.2
using System;
using System.Runtime.InteropServices;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace TalkKeys.Services.Notifications
{
    public sealed class ToastNotificationService : INotificationService
    {
        private static bool _initialized;

        [DllImport("shell32.dll")]
        private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appID);

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            try
            {
                // Set a stable AppUserModelID so toasts are attributable on Windows 10/11
                SetCurrentProcessExplicitAppUserModelID("TalkKeys.App");
                _initialized = true;
            }
            catch
            {
                _initialized = true;
            }
        }

        private static void ShowToast(string title, string message)
        {
            var content = new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .GetToastContent();
            var xml = new XmlDocument();
            xml.LoadXml(content.GetContent());
            var toast = new ToastNotification(xml);
            ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
        }

        public void ShowInfo(string title, string message)
        {
            EnsureInitialized();
            ShowToast(title, message);
        }

        public void ShowError(string title, string message)
        {
            EnsureInitialized();
            ShowToast(title, message);
        }
    }
}
