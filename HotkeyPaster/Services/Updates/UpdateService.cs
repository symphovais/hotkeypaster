using System;
using System.Threading.Tasks;
using TalkKeys.Logging;
using Velopack;
using Velopack.Sources;

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

    public class UpdateService : IUpdateService
    {
        private const string GITHUB_RELEASES_URL = "https://github.com/symphovais/hotkeypaster/releases";

        private readonly ILogger? _logger;
        private readonly UpdateManager _updateManager;
        private UpdateInfo? _pendingUpdate;
        private bool _updateDownloaded;

        public event EventHandler<UpdateInfo>? UpdateAvailable;
        public event EventHandler<string>? UpdateDownloaded;
        public event EventHandler<string>? UpdateError;

        public bool IsUpdateReadyToInstall => _updateDownloaded;

        public UpdateService(ILogger? logger = null)
        {
            _logger = logger;

            // Create update manager with GitHub releases as the source
            _updateManager = new UpdateManager(new GithubSource(GITHUB_RELEASES_URL, null, false));

            _logger?.Log($"[UpdateService] Initialized with source: {GITHUB_RELEASES_URL}");
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                _logger?.Log("[UpdateService] Checking for updates...");

                // Check if we're running in a Velopack-managed installation
                if (!_updateManager.IsInstalled)
                {
                    _logger?.Log("[UpdateService] App is not installed via Velopack - skipping update check");
                    return null;
                }

                var currentVersion = _updateManager.CurrentVersion?.ToString() ?? "unknown";
                _logger?.Log($"[UpdateService] Current version: {currentVersion}");

                // Check for updates
                var updateInfo = await _updateManager.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    _logger?.Log("[UpdateService] No updates available");
                    return new UpdateInfo
                    {
                        IsUpdateAvailable = false,
                        CurrentVersion = currentVersion
                    };
                }

                var newVersion = updateInfo.TargetFullRelease.Version.ToString();
                _logger?.Log($"[UpdateService] Update available: {newVersion}");

                _pendingUpdate = new UpdateInfo
                {
                    IsUpdateAvailable = true,
                    CurrentVersion = currentVersion,
                    NewVersion = newVersion,
                    DownloadSize = updateInfo.TargetFullRelease.Size
                };

                UpdateAvailable?.Invoke(this, _pendingUpdate);
                return _pendingUpdate;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[UpdateService] Error checking for updates: {ex.Message}");
                UpdateError?.Invoke(this, ex.Message);
                return null;
            }
        }

        public async Task DownloadAndApplyUpdateAsync()
        {
            try
            {
                if (!_updateManager.IsInstalled)
                {
                    _logger?.Log("[UpdateService] Cannot update - app not installed via Velopack");
                    return;
                }

                _logger?.Log("[UpdateService] Downloading update...");

                var updateInfo = await _updateManager.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    _logger?.Log("[UpdateService] No update to download");
                    return;
                }

                // Download the update
                await _updateManager.DownloadUpdatesAsync(updateInfo);
                _updateDownloaded = true;

                var version = updateInfo.TargetFullRelease.Version.ToString();
                _logger?.Log($"[UpdateService] Update downloaded: {version}");

                UpdateDownloaded?.Invoke(this, version);
            }
            catch (Exception ex)
            {
                _logger?.Log($"[UpdateService] Error downloading update: {ex.Message}");
                UpdateError?.Invoke(this, ex.Message);
            }
        }

        public void RestartAndApplyUpdate()
        {
            try
            {
                if (!_updateDownloaded)
                {
                    _logger?.Log("[UpdateService] No update downloaded to apply");
                    return;
                }

                _logger?.Log("[UpdateService] Applying update and restarting...");

                // This will apply the update and restart the app
                _updateManager.ApplyUpdatesAndRestart(null);
            }
            catch (Exception ex)
            {
                _logger?.Log($"[UpdateService] Error applying update: {ex.Message}");
                UpdateError?.Invoke(this, ex.Message);
            }
        }
    }
}
