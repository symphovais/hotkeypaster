// About content for desktop app
export const aboutContent = {
  appName: "TalkKeys",
  tagline: "Voice-to-text for Windows",
  description: "A voice-to-text application that lets you speak naturally and have your words typed automatically in any application.",
  madeWithLove: "This app was built purely for the joy of creating software. It's a passion project exploring voice recognition, WPF, and modern .NET development.",
  libraries: [
    { name: "NAudio", description: "Audio recording and processing" },
    { name: "H.Hooks", description: "Global keyboard hooks" },
    { name: "H.InputSimulator", description: "Keyboard input simulation" },
    { name: "HidSharp", description: "USB HID device support" },
    { name: "Polly", description: "Resilience and retry patterns" },
    { name: "Groq", description: "Fast AI inference (Whisper)" }
  ],
  links: [
    { title: "Website", url: "https://talkkeys.symphonytek.dk", description: "Learn more about TalkKeys" },
    { title: "Release Notes", url: "https://talkkeys.symphonytek.dk/releases", description: "See what's new" },
    { title: "API Documentation", url: "https://talkkeys.symphonytek.dk/api-docs", description: "Remote Control API reference" },
    { title: "GitHub", url: "https://github.com/symphovais/hotkeypaster", description: "View source code" }
  ],
  releases: [
    {
      version: "1.2.2",
      title: "Savage WTF & Flip Cards",
      heroFeatures: [
        {
          icon: "🔥",
          title: "Savage Mode",
          description: "WTF got meaner - now exposes hidden motives in 10 words or less",
          color: "#EF4444",
          badge: "Brutal"
        },
        {
          icon: "🔄",
          title: "Flip to Plain",
          description: "Tap to flip between savage WTF and neutral plain English",
          color: "#8B5CF6",
          badge: "New"
        }
      ],
      slides: [
        {
          icon: "🔥",
          iconBackground: "#FEE2E2",
          title: "Savage WTF Mode",
          description: "The WTF explainer now cuts through corporate theater with zero patience. It exposes the real motive behind what people say.",
          highlights: [
            { text: "10 words or less - no fluff", color: "#DC2626" },
            { text: "Exposes power moves and ego trips", color: "#DC2626" },
            { text: "Uses jagged words that hit hard", color: "#DC2626" }
          ]
        },
        {
          icon: "🔄",
          iconBackground: "#EDE9FE",
          title: "Flip Between Views",
          description: "Tap the result card to flip between WTF (savage) and Plain (neutral) translations. The new button is impossible to miss.",
          highlights: [
            { text: "Purple pill button for flipping", color: "#7C3AED" },
            { text: "Smooth card flip animation", color: "#7C3AED" },
            { text: "Plain mode for professional contexts", color: "#7C3AED" }
          ]
        },
        {
          icon: "⚡",
          iconBackground: "#FEF3C7",
          title: "Faster & More Reliable",
          description: "Switched to a faster AI model for instant responses. Fixed token issues that sometimes caused empty results.",
          highlights: [
            { text: "llama-3.1-8b-instant model", color: "#D97706" },
            { text: "No more 'no explanation' errors", color: "#D97706" },
            { text: "Sub-second response times", color: "#D97706" }
          ]
        },
        {
          icon: "🚀",
          iconBackground: "#F3F4F6",
          title: "Ready to Go!",
          description: "Select any text and press Ctrl+Win+E for the savage truth!",
          isGetStarted: true
        }
      ]
    },
    {
      version: "1.2.1",
      title: "Words List & Smarter AI",
      heroFeatures: [
        {
          icon: "📝",
          title: "Words List",
          description: "Teach TalkKeys your vocabulary - names, tech terms, and jargon spelled correctly",
          color: "#8B5CF6",
          badge: "AI Analysis"
        },
        {
          icon: "😏",
          title: "Wittier WTF",
          description: "The sarcastic translator just got funnier and more savage",
          color: "#F59E0B",
          badge: "Upgraded"
        }
      ],
      slides: [
        {
          icon: "📝",
          iconBackground: "#EDE9FE",
          title: "Words List",
          description: "Add words you commonly use. TalkKeys will recognize and spell them correctly, even when Whisper mishears them.",
          badge: { label: "New:", value: "Settings → Words", backgroundColor: "#8B5CF6" },
          highlights: [
            { text: "AI analyzes your transcription history", color: "#7C3AED" },
            { text: "Suggests words that might be misspelled", color: "#7C3AED" },
            { text: "One-click to add suggestions", color: "#7C3AED" }
          ]
        },
        {
          icon: "😏",
          iconBackground: "#FEF3C7",
          title: "Wittier WTF Translations",
          description: "The WTF feature now delivers punchier, funnier translations of corporate speak and jargon.",
          highlights: [
            { text: "More sarcastic and witty responses", color: "#D97706" },
            { text: "Better examples for context", color: "#D97706" },
            { text: "Dry humor that actually lands", color: "#D97706" }
          ]
        },
        {
          icon: "🚀",
          iconBackground: "#F3F4F6",
          title: "Ready to Go!",
          description: "Check out Settings → Words to start building your vocabulary list!",
          isGetStarted: true
        }
      ]
    },
    {
      version: "1.2.0",
      title: "Remote Control & WTF",
      heroFeatures: [
        {
          icon: "🔗",
          title: "Remote Control API",
          description: "Control TalkKeys from external apps, hardware buttons, or AI assistants via HTTP",
          color: "#3B82F6",
          badge: "localhost:38450"
        },
        {
          icon: "🤔",
          title: "WTF - What are the Facts",
          description: "Select any text and get the facts explained simply",
          color: "#10B981",
          badge: "Ctrl+Win+E"
        }
      ],
      slides: [
        {
          icon: "🔗",
          iconBackground: "#EFF6FF",
          title: "Remote Control API",
          description: "Control TalkKeys from external applications via HTTP API. Perfect for hardware buttons like Jabra headsets and AI assistants.",
          badge: { label: "API:", value: "http://localhost:38450/", backgroundColor: "#3B82F6" },
          highlights: [
            { text: "Start/stop transcription remotely", color: "#3B82F6" },
            { text: "Get status and microphone list", color: "#3B82F6" },
            { text: "Works with Mango Plus & other apps", color: "#3B82F6" }
          ]
        },
        {
          icon: "🤔",
          iconBackground: "#ECFDF5",
          title: "WTF - What are the Facts",
          description: "Select any confusing text - code, legal jargon, technical docs - and get the facts explained simply.",
          badge: { label: "Hotkey:", value: "Ctrl + Win + E", backgroundColor: "#10B981" },
          highlights: [
            { text: "Works with any selected text", color: "#059669" },
            { text: "AI-powered explanations", color: "#059669" },
            { text: "Results appear in a clean popup", color: "#059669" }
          ]
        },
        {
          icon: "📋",
          iconBackground: "#F3F4F6",
          title: "Text Preview",
          description: "See your transcribed text with a convenient copy button. Perfect when paste doesn't work in specific apps.",
          highlights: [
            { text: "Auto-expands after transcription", color: "#374151" },
            { text: "One-click copy to clipboard", color: "#374151" },
            { text: "Auto-collapses after 10 seconds", color: "#374151" }
          ]
        },
        {
          icon: "🚀",
          iconBackground: "#F3F4F6",
          title: "Ready to Go!",
          description: "TalkKeys is ready. Press your hotkey anytime to start dictating, or try the new WTF feature!",
          isGetStarted: true
        }
      ]
    },
    {
      version: "1.1.0",
      title: "Stability Improvements",
      slides: [
        {
          icon: "🛡️",
          iconBackground: "#F3F4F6",
          title: "Rock Solid",
          description: "Major stability improvements for a smooth, reliable experience every time.",
          highlights: [
            { text: "Hotkeys persist after restart", color: "#374151" },
            { text: "More reliable pasting", color: "#374151" },
            { text: "Network resilience with auto-retry", color: "#374151" }
          ]
        },
        {
          icon: "🚀",
          iconBackground: "#F3F4F6",
          title: "Ready to Go!",
          description: "TalkKeys is ready. Press your hotkey anytime to start dictating.",
          isGetStarted: true
        }
      ]
    },
    {
      version: "1.0.8",
      title: "Error Handling",
      slides: [
        {
          icon: "🎙️",
          iconBackground: "#F3E8FF",
          title: "Better Microphone Handling",
          description: "Improved microphone access error handling with user-friendly messages.",
          highlights: [
            { text: "Clear error messages when mic is unavailable", color: "#059669" },
            { text: "Better recovery from permission denials", color: "#059669" }
          ]
        },
        {
          icon: "🚀",
          iconBackground: "#F3E8FF",
          title: "Ready to Go!",
          description: "TalkKeys is ready. Press your hotkey anytime to start dictating.",
          isGetStarted: true
        }
      ]
    }
  ]
};
