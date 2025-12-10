using System;

namespace TalkKeys
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Run the WPF application
            // Note: Updates are managed by Microsoft Store for packaged versions
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
