using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace TalkKeys
{
    public partial class WhatsNewWindow : Window
    {
        private const string ReleaseNotesUrl = "https://talkkeys.symphonytek.dk/releases";

        public WhatsNewWindow(bool isFirstInstall = false)
        {
            InitializeComponent();

            // Set version from assembly
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
            }

            // Show welcome section for first-time installs
            if (isFirstInstall)
            {
                WelcomeSection.Visibility = Visibility.Visible;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void ReleaseNotesLink_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(ReleaseNotesUrl);
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}
