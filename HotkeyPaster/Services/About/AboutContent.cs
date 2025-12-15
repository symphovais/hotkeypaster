using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TalkKeys.Services.About
{
    /// <summary>
    /// Content for the About/What's New window, loaded from backend
    /// </summary>
    public class AboutContent
    {
        [JsonPropertyName("appName")]
        public string AppName { get; set; } = "TalkKeys";

        [JsonPropertyName("tagline")]
        public string Tagline { get; set; } = "Voice-to-text for Windows";

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("madeWithLove")]
        public string MadeWithLove { get; set; } = string.Empty;

        [JsonPropertyName("libraries")]
        public List<LibraryInfo> Libraries { get; set; } = new();

        [JsonPropertyName("links")]
        public List<LinkInfo> Links { get; set; } = new();

        [JsonPropertyName("releases")]
        public List<ReleaseInfo> Releases { get; set; } = new();
    }

    public class LibraryInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class LinkInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class ReleaseInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("heroFeatures")]
        public List<HeroFeatureInfo> HeroFeatures { get; set; } = new();

        [JsonPropertyName("slides")]
        public List<SlideInfo> Slides { get; set; } = new();
    }

    public class HeroFeatureInfo
    {
        [JsonPropertyName("icon")]
        public string Icon { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#7C3AED";

        [JsonPropertyName("badge")]
        public string? Badge { get; set; }
    }

    public class SlideInfo
    {
        [JsonPropertyName("icon")]
        public string Icon { get; set; } = string.Empty;

        [JsonPropertyName("iconBackground")]
        public string IconBackground { get; set; } = "#F3E8FF";

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("highlights")]
        public List<HighlightInfo> Highlights { get; set; } = new();

        [JsonPropertyName("badge")]
        public BadgeInfo? Badge { get; set; }

        [JsonPropertyName("isGetStarted")]
        public bool IsGetStarted { get; set; }
    }

    public class HighlightInfo
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("color")]
        public string Color { get; set; } = "#059669";
    }

    public class BadgeInfo
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("backgroundColor")]
        public string BackgroundColor { get; set; } = "#7C3AED";
    }
}
