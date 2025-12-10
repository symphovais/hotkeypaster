using System;
using System.Threading.Tasks;
using TalkKeys.Logging;

namespace TalkKeys.Services.Updates
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string? CurrentVersion { get; set; }
        public string? NewVersion { get; set; }
        public long? DownloadSize { get; set; }
    }

    public interface IUpdateService
    {
        event EventHandler<UpdateInfo>? UpdateAvailable;
        event EventHandler<string>? UpdateDownloaded;
        event EventHandler<string>? UpdateError;

        Task<UpdateInfo?> CheckForUpdatesAsync();
        Task DownloadAndApplyUpdateAsync();
        bool IsUpdateReadyToInstall { get; }
        void RestartAndApplyUpdate();
    }

    /// <summary>
    /// Store apps receive automatic updates through the Microsoft Store.
    /// This stub implementation maintains interface compatibility.
    /// </summary>
    public class UpdateService : IUpdateService
    {
        private readonly ILogger? _logger;

        public event EventHandler<UpdateInfo>? UpdateAvailable;
        public event EventHandler<string>? UpdateDownloaded;
        public event EventHandler<string>? UpdateError;

        public bool IsUpdateReadyToInstall => false;

        public UpdateService(ILogger? logger = null)
        {
            _logger = logger;
            _logger?.Log("[UpdateService] Running as Store app - updates managed by Microsoft Store");
        }

        public Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            // Store handles updates automatically
            _logger?.Log("[UpdateService] Update check skipped - managed by Microsoft Store");
            return Task.FromResult<UpdateInfo?>(null);
        }

        public Task DownloadAndApplyUpdateAsync()
        {
            // Store handles updates automatically
            _logger?.Log("[UpdateService] Download skipped - managed by Microsoft Store");
            return Task.CompletedTask;
        }

        public void RestartAndApplyUpdate()
        {
            // Store handles updates automatically
            _logger?.Log("[UpdateService] Restart skipped - managed by Microsoft Store");
        }

        // Suppress unused event warnings - kept for interface compatibility
        private void SuppressWarnings()
        {
            UpdateAvailable?.Invoke(this, new UpdateInfo());
            UpdateDownloaded?.Invoke(this, string.Empty);
            UpdateError?.Invoke(this, string.Empty);
        }
    }
}
