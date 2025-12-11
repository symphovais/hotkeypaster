using System.Collections.Generic;

namespace TalkKeys.Plugins.FocusTimer
{
    /// <summary>
    /// Model for tracking daily focus time statistics.
    /// </summary>
    public class FocusStats
    {
        /// <summary>
        /// Daily focus minutes. Key is date in "yyyy-MM-dd" format, value is total minutes focused.
        /// </summary>
        public Dictionary<string, int> DailyMinutes { get; set; } = new();
    }
}
