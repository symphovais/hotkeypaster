using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace TalkKeys
{
    public partial class AboutWindow : Window
    {
        private const string WebsiteUrl = "https://talkkeys.symphonytek.dk";
        private const string ReleaseNotesUrl = "https://talkkeys.symphonytek.dk/releases";

        public AboutWindow()
        {
            InitializeComponent();

            // Set version from assembly
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ReleaseNotesLink_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(ReleaseNotesUrl);
        }

        private void WebsiteLink_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(WebsiteUrl);
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silently ignore if browser can't be opened
            }
        }
    }
}
