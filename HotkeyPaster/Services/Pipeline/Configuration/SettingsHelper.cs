using System.Collections.Generic;
using System.Text.Json;

namespace TalkKeys.Services.Pipeline.Configuration
{
    /// <summary>
    /// Helper for extracting typed values from settings dictionaries.
    /// Handles JSON deserialization quirks where values may be JsonElement instead of native types.
    /// </summary>
    public static class SettingsHelper
    {
        /// <summary>
        /// Gets a string value from settings, handling JsonElement conversion.
        /// </summary>
        public static string? GetString(Dictionary<string, object> settings, string key)
        {
            if (!settings.TryGetValue(key, out var value))
                return null;

            return value switch
            {
                string s => s,
                JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
                JsonElement je when je.ValueKind == JsonValueKind.Null => null,
                _ => value?.ToString()
            };
        }

        /// <summary>
        /// Gets a boolean value from settings, handling JsonElement conversion.
        /// </summary>
        public static bool? GetBool(Dictionary<string, object> settings, string key)
        {
            if (!settings.TryGetValue(key, out var value))
                return null;

            return value switch
            {
                bool b => b,
                JsonElement je when je.ValueKind == JsonValueKind.True => true,
                JsonElement je when je.ValueKind == JsonValueKind.False => false,
                string s when bool.TryParse(s, out var b) => b,
                _ => null
            };
        }

        /// <summary>
        /// Gets an integer value from settings, handling JsonElement conversion.
        /// </summary>
        public static int? GetInt(Dictionary<string, object> settings, string key)
        {
            if (!settings.TryGetValue(key, out var value))
                return null;

            return value switch
            {
                int i => i,
                long l => (int)l,
                JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var i) => i,
                string s when int.TryParse(s, out var i) => i,
                _ => null
            };
        }

        /// <summary>
        /// Gets a double value from settings, handling JsonElement conversion.
        /// </summary>
        public static double? GetDouble(Dictionary<string, object> settings, string key)
        {
            if (!settings.TryGetValue(key, out var value))
                return null;

            return value switch
            {
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetDouble(out var d) => d,
                string s when double.TryParse(s, out var d) => d,
                _ => null
            };
        }
    }
}
