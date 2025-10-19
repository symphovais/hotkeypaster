using System;
using System.IO;

namespace HotkeyPaster.Logging
{
    public interface ILogger
    {
        void Log(string message);
    }

    public sealed class FileLogger : ILogger
    {
        private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HotkeyPaster");
        private static readonly string LogPath = Path.Combine(LogDir, "logs.txt");

        public void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}");
            }
            catch
            {
                // Avoid throwing in logger
            }
        }
    }
}
