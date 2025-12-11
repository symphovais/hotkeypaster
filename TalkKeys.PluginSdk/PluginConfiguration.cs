using System.Collections.Generic;

namespace TalkKeys.PluginSdk
{
    /// <summary>
    /// Configuration for a general-purpose plugin.
    /// Persisted in AppSettings.Plugins dictionary.
    /// </summary>
    public class PluginConfiguration
    {
        /// <summary>
        /// Plugin identifier.
        /// </summary>
        public string PluginId { get; set; } = "";

        /// <summary>
        /// Whether the plugin is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Widget visibility state (for IWidgetPlugin).
        /// </summary>
        public bool WidgetVisible { get; set; } = true;

        /// <summary>
        /// Widget X position (for IWidgetPlugin). -1 means not set.
        /// </summary>
        public double WidgetX { get; set; } = -1;

        /// <summary>
        /// Widget Y position (for IWidgetPlugin). -1 means not set.
        /// </summary>
        public double WidgetY { get; set; } = -1;

        /// <summary>
        /// Plugin-specific settings.
        /// Use this dictionary for custom plugin configuration values.
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        /// <summary>
        /// Get a setting value with type conversion.
        /// </summary>
        public T GetSetting<T>(string key, T defaultValue)
        {
            if (Settings.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T typedValue)
                        return typedValue;

                    // Handle System.Text.Json deserialization - values come back as JsonElement
                    if (value is System.Text.Json.JsonElement jsonElement)
                    {
                        if (typeof(T) == typeof(int))
                            return (T)(object)jsonElement.GetInt32();
                        if (typeof(T) == typeof(bool))
                            return (T)(object)jsonElement.GetBoolean();
                        if (typeof(T) == typeof(string))
                            return (T)(object)(jsonElement.GetString() ?? string.Empty);
                        if (typeof(T) == typeof(double))
                            return (T)(object)jsonElement.GetDouble();
                    }

                    // Handle JSON deserialization quirks (int64 for ints, etc.)
                    if (typeof(T) == typeof(int) && value is long longVal)
                        return (T)(object)(int)longVal;
                    if (typeof(T) == typeof(int) && value is double doubleVal)
                        return (T)(object)(int)doubleVal;
                    if (typeof(T) == typeof(bool) && value is bool boolVal)
                        return (T)(object)boolVal;

                    return (T)System.Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Set a setting value.
        /// </summary>
        public void SetSetting<T>(string key, T value)
        {
            if (value == null)
                Settings.Remove(key);
            else
                Settings[key] = value;
        }
    }
}
