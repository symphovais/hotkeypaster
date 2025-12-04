using System;
using System.Windows;
using System.Windows.Threading;

namespace TalkKeys
{
    public partial class TextPreviewPopup : Window
    {
        private DispatcherTimer? _autoCloseTimer;
        private int _secondsRemaining = 5;

        public TextPreviewPopup(string transcribedText)
        {
            InitializeComponent();
            TranscribedText.Text = transcribedText;
            
            // Start auto-close timer
            _autoCloseTimer = new DispatcherTimer();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(1);
            _autoCloseTimer.Tick += AutoCloseTimer_Tick;
            _autoCloseTimer.Start();
        }

        private void AutoCloseTimer_Tick(object? sender, EventArgs e)
        {
            _secondsRemaining--;
            AutoCloseText.Text = $"• Closing in {_secondsRemaining}s";
            
            if (_secondsRemaining <= 0)
            {
                _autoCloseTimer?.Stop();
                this.Close();
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TranscribedText.Text);
                // Change button text briefly to indicate success
                var button = (System.Windows.Controls.Button)sender;
                var originalContent = button.Content;
                button.Content = "✓ Copied!";
                
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, args) =>
                {
                    button.Content = originalContent;
                    timer.Stop();
                };
                timer.Start();
            }
            catch
            {
                // Silently fail if clipboard access fails
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer?.Stop();
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _autoCloseTimer?.Stop();
            base.OnClosing(e);
        }
    }
}
