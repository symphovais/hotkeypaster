using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TalkKeys.Logging;

namespace TalkKeys.Services.About
{
    /// <summary>
    /// Service to fetch About/What's New content from backend
    /// </summary>
    public class AboutContentService
    {
        private const string ContentUrl = "https://talkkeys-api.ahmed-ovais.workers.dev/about-content";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private AboutContent? _cachedContent;

        public AboutContentService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient { Timeout = Timeout };
        }

        /// <summary>
        /// Gets the about content, fetching from backend or using fallback
        /// </summary>
        public async Task<AboutContent> GetContentAsync()
        {
            // Return cached content if available
            if (_cachedContent != null)
            {
                return _cachedContent;
            }

            try
            {
                _logger.Log("[AboutContent] Fetching content from backend...");
                var response = await _httpClient.GetStringAsync(ContentUrl);
                var content = JsonSerializer.Deserialize<AboutContent>(response);

                if (content != null)
                {
                    _cachedContent = content;
                    _logger.Log("[AboutContent] Successfully loaded content from backend");
                    return content;
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[AboutContent] Failed to fetch from backend: {ex.Message}");
            }

            // Return fallback content
            _logger.Log("[AboutContent] Using fallback content");
            _cachedContent = GetFallbackContent();
            return _cachedContent;
        }

        /// <summary>
        /// Gets fallback content when backend is unavailable
        /// </summary>
        private static AboutContent GetFallbackContent()
        {
            return new AboutContent
            {
                AppName = "TalkKeys",
                Tagline = "Voice-to-text for Windows",
                Description = "A voice-to-text application that lets you speak naturally and have your words typed automatically in any application.",
                MadeWithLove = "This app was built purely for the joy of creating software. It's a passion project exploring voice recognition, WPF, and modern .NET development.",
                Libraries = new List<LibraryInfo>
                {
                    new() { Name = "NAudio", Description = "Audio recording and processing" },
                    new() { Name = "H.Hooks", Description = "Global keyboard hooks" },
                    new() { Name = "H.InputSimulator", Description = "Keyboard input simulation" },
                    new() { Name = "HidSharp", Description = "USB HID device support" },
                    new() { Name = "Polly", Description = "Resilience and retry patterns" },
                    new() { Name = "Groq", Description = "Fast AI inference (Whisper)" }
                },
                Links = new List<LinkInfo>
                {
                    new() { Title = "Website", Url = "https://talkkeys.symphonytek.dk", Description = "Learn more about TalkKeys" },
                    new() { Title = "Release Notes", Url = "https://talkkeys.symphonytek.dk/releases", Description = "See what's new" },
                    new() { Title = "GitHub", Url = "https://github.com/symphovais/hotkeypaster", Description = "View source code" }
                },
                Releases = new List<ReleaseInfo>
                {
                    new()
                    {
                        Version = "1.2.0",
                        Title = "Remote Control & WTF",
                        HeroFeatures = new List<HeroFeatureInfo>
                        {
                            new() { Icon = "\U0001F517", Title = "Remote Control API", Description = "Control TalkKeys from external apps, hardware buttons, or AI assistants via HTTP", Color = "#3B82F6", Badge = "localhost:38450" },
                            new() { Icon = "\U0001F914", Title = "WTF - What are the Facts", Description = "Select any text and get the facts explained simply", Color = "#10B981", Badge = "Ctrl+Win+E" }
                        },
                        Slides = new List<SlideInfo>
                        {
                            new()
                            {
                                Icon = "\U0001F517",
                                IconBackground = "#EFF6FF",
                                Title = "Remote Control API",
                                Description = "Control TalkKeys from external applications via HTTP API. Perfect for hardware buttons like Jabra headsets and AI assistants.",
                                Badge = new BadgeInfo { Label = "API:", Value = "http://localhost:38450/", BackgroundColor = "#3B82F6" },
                                Highlights = new List<HighlightInfo>
                                {
                                    new() { Text = "Start/stop transcription remotely", Color = "#3B82F6" },
                                    new() { Text = "Get status and microphone list", Color = "#3B82F6" },
                                    new() { Text = "Works with Mango Plus & other apps", Color = "#3B82F6" }
                                }
                            },
                            new()
                            {
                                Icon = "\U0001F914",
                                IconBackground = "#ECFDF5",
                                Title = "WTF - What are the Facts",
                                Description = "Select any confusing text - code, legal jargon, technical docs - and get the facts explained simply.",
                                Badge = new BadgeInfo { Label = "Hotkey:", Value = "Ctrl + Win + E", BackgroundColor = "#10B981" },
                                Highlights = new List<HighlightInfo>
                                {
                                    new() { Text = "Works with any selected text", Color = "#059669" },
                                    new() { Text = "AI-powered explanations", Color = "#059669" },
                                    new() { Text = "Results appear in a clean popup", Color = "#059669" }
                                }
                            },
                            new()
                            {
                                Icon = "\U0001F4CB",
                                IconBackground = "#F3F4F6",
                                Title = "Text Preview",
                                Description = "See your transcribed text with a convenient copy button. Perfect when paste doesn't work in specific apps.",
                                Highlights = new List<HighlightInfo>
                                {
                                    new() { Text = "Auto-expands after transcription", Color = "#374151" },
                                    new() { Text = "One-click copy to clipboard", Color = "#374151" },
                                    new() { Text = "Auto-collapses after 10 seconds", Color = "#374151" }
                                }
                            },
                            new()
                            {
                                Icon = "\U0001F680",
                                IconBackground = "#F3F4F6",
                                Title = "Ready to Go!",
                                Description = "TalkKeys is ready. Press your hotkey anytime to start dictating, or try the new WTF feature!",
                                IsGetStarted = true
                            }
                        }
                    },
                    new()
                    {
                        Version = "1.1.0",
                        Title = "Stability Improvements",
                        Slides = new List<SlideInfo>
                        {
                            new()
                            {
                                Icon = "\U0001F6E1\uFE0F",
                                IconBackground = "#F3F4F6",
                                Title = "Rock Solid",
                                Description = "Major stability improvements for a smooth, reliable experience every time.",
                                Highlights = new List<HighlightInfo>
                                {
                                    new() { Text = "Hotkeys persist after restart", Color = "#374151" },
                                    new() { Text = "More reliable pasting", Color = "#374151" },
                                    new() { Text = "Network resilience with auto-retry", Color = "#374151" }
                                }
                            },
                            new()
                            {
                                Icon = "\U0001F680",
                                IconBackground = "#F3F4F6",
                                Title = "Ready to Go!",
                                Description = "TalkKeys is ready. Press your hotkey anytime to start dictating.",
                                IsGetStarted = true
                            }
                        }
                    }
                }
            };
        }
    }
}
