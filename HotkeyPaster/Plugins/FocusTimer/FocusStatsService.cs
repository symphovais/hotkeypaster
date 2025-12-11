using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TalkKeys.Logging;

namespace TalkKeys.Plugins.FocusTimer
{
    /// <summary>
    /// Service for persisting and loading focus timer statistics.
    /// </summary>
    public class FocusStatsService
    {
        private static readonly string StatsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TalkKeys"
        );
        private static readonly string StatsPath = Path.Combine(StatsDir, "focus-stats.json");
        private const int MaxDaysToKeep = 30;

        private readonly ILogger? _logger;
        private FocusStats? _cachedStats;
        private readonly object _lock = new();

        public FocusStatsService(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Load stats from disk.
        /// </summary>
        public FocusStats LoadStats()
        {
            lock (_lock)
            {
                if (_cachedStats != null)
                    return _cachedStats;

                try
                {
                    if (File.Exists(StatsPath))
                    {
                        var json = File.ReadAllText(StatsPath);
                        var stats = JsonSerializer.Deserialize<FocusStats>(json);
                        if (stats != null)
                        {
                            _cachedStats = stats;
                            return stats;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[FocusStatsService] Error loading stats: {ex.Message}");
                }

                _cachedStats = new FocusStats();
                return _cachedStats;
            }
        }

        /// <summary>
        /// Save stats to disk.
        /// </summary>
        public bool SaveStats(FocusStats stats)
        {
            lock (_lock)
            {
                try
                {
                    // Cleanup old entries
                    CleanupOldEntries(stats);

                    Directory.CreateDirectory(StatsDir);
                    var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    File.WriteAllText(StatsPath, json);
                    _cachedStats = stats;
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[FocusStatsService] Error saving stats: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Add focus minutes for today.
        /// </summary>
        public void AddFocusMinutes(int minutes)
        {
            var stats = LoadStats();
            var today = DateTime.Now.ToString("yyyy-MM-dd");

            if (stats.DailyMinutes.ContainsKey(today))
                stats.DailyMinutes[today] += minutes;
            else
                stats.DailyMinutes[today] = minutes;

            SaveStats(stats);
            _logger?.Log($"[FocusStatsService] Added {minutes} minutes for {today}. Total: {stats.DailyMinutes[today]}");
        }

        /// <summary>
        /// Get today's total focus minutes.
        /// </summary>
        public int GetTodayMinutes()
        {
            var stats = LoadStats();
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            return stats.DailyMinutes.TryGetValue(today, out var minutes) ? minutes : 0;
        }

        /// <summary>
        /// Format minutes as human-readable string (e.g., "1h 25m" or "45m").
        /// </summary>
        public static string FormatMinutes(int totalMinutes)
        {
            if (totalMinutes <= 0)
                return "0m";

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours > 0 && minutes > 0)
                return $"{hours}h {minutes}m";
            else if (hours > 0)
                return $"{hours}h";
            else
                return $"{minutes}m";
        }

        /// <summary>
        /// Remove entries older than MaxDaysToKeep days.
        /// </summary>
        private void CleanupOldEntries(FocusStats stats)
        {
            var cutoffDate = DateTime.Now.AddDays(-MaxDaysToKeep).ToString("yyyy-MM-dd");
            var keysToRemove = stats.DailyMinutes.Keys
                .Where(k => string.Compare(k, cutoffDate, StringComparison.Ordinal) < 0)
                .ToList();

            foreach (var key in keysToRemove)
            {
                stats.DailyMinutes.Remove(key);
            }

            if (keysToRemove.Count > 0)
            {
                _logger?.Log($"[FocusStatsService] Cleaned up {keysToRemove.Count} old entries");
            }
        }

        /// <summary>
        /// Invalidate cache to force reload from disk.
        /// </summary>
        public void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedStats = null;
            }
        }
    }
}
