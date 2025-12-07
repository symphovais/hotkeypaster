using System;
using Velopack;

namespace TalkKeys
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Velopack MUST be the first thing to run in the app
            // This handles install, uninstall, and update lifecycle events
            VelopackApp.Build().Run();

            // Now run the WPF application
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
