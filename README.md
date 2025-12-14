# TalkKeys

A Windows desktop application for voice-to-text transcription. Press a hotkey, speak, and your words are transcribed and pasted into any application.

[![Get it from Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Download-blue?logo=microsoft)](https://apps.microsoft.com/detail/9P2D7DZQS61J)
[![GitHub release](https://img.shields.io/github/v/release/symphovais/hotkeypaster)](https://github.com/symphovais/hotkeypaster/releases)

## Features

- **Free Tier**: 10 minutes of transcription per day with a free TalkKeys account
- **Global Hotkey**: Press `Ctrl+Shift+Space` to start recording from anywhere
- **Push-to-Talk or Toggle**: Hold hotkey to record, or press once to start/stop
- **Floating Widget**: Draggable, always-on-top widget shows recording status
- **Automatic Pasting**: Transcribed text is automatically pasted into your active window
- **AI Text Cleaning**: Automatic punctuation, capitalization, and filler word removal
- **System Tray Integration**: Runs quietly in the background
- **Multi-Monitor Support**: Widget stays on the correct screen

## Installation

### Microsoft Store (Recommended)

[![Get it from Microsoft Store](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/detail/9P2D7DZQS61J)

The easiest way to install TalkKeys. Automatic updates included.

### GitHub Releases

Download the latest `.msix` package from the [Releases](https://github.com/symphovais/hotkeypaster/releases) page.

> **Note**: Installing from GitHub requires [Developer Mode](ms-settings:developers) enabled in Windows Settings.

## How It Works

1. Press `Ctrl+Shift+Space` or click the floating widget to start recording
2. Speak into your microphone
3. Release the hotkey (push-to-talk) or press again (toggle mode) to stop
4. Audio is transcribed using AI and automatically pasted into your active window

## Getting Started

1. Install TalkKeys from the Microsoft Store or GitHub
2. Launch the app - you'll see the Welcome screen
3. Sign in with Google to create your free TalkKeys account
4. Start transcribing! You get 10 minutes free every day

### Using Your Own API Key

If you prefer to use your own Groq API key (unlimited usage):

1. Open Settings from the system tray
2. Switch to "Use your own API key"
3. Enter your [Groq API key](https://console.groq.com/keys)
4. Save settings

## Usage

| Action | Method |
|--------|--------|
| Start recording | Press `Ctrl+Shift+Space` or click the widget |
| Stop recording | Release hotkey (push-to-talk) or press again (toggle) |
| Cancel recording | Press `Escape` |
| Move widget | Drag the widget to any position |
| Open settings | Right-click tray icon → Settings |
| Sign out | Right-click tray icon → Sign Out |
| Exit application | Right-click tray icon → Exit |

## Settings

Access settings by right-clicking the system tray icon.

| Setting | Description |
|---------|-------------|
| Recording Mode | Push-to-talk (hold) or Toggle (press to start/stop) |
| Audio Device | Select your microphone |
| Start with Windows | Launch TalkKeys automatically on login |
| Auth Mode | TalkKeys account (free tier) or own API key |

## Requirements

- Windows 10/11
- Microphone
- Internet connection

## Privacy

- Audio is processed and immediately discarded
- We never store your recordings or transcribed text
- See our [Privacy Policy](https://talkkeys-api.ahmed-ovais.workers.dev/privacy)

## Technologies

- **.NET 8.0** with WPF
- **NAudio** for audio recording
- **H.Hooks** for global keyboard hooks
- **H.InputSimulator** for keyboard input simulation
- **Polly** for HTTP resilience and retry policies
- **Groq API** for fast AI transcription (Whisper)
- **Cloudflare Workers + D1** for backend services

## Troubleshooting

### Widget not appearing
- Check the system tray for the TalkKeys icon
- Press `Ctrl+Shift+Space` to show the widget

### Transcription not working
- Check your internet connection
- Verify you're signed in (check system tray menu)
- Check your daily usage limit in Settings

### Audio issues
- Open Settings and select the correct audio device
- Check Windows microphone permissions

### "Session Expired" message
- Sign out and sign in again from the system tray menu

## License

MIT License

## Links

- [Website](https://talkkeys-api.ahmed-ovais.workers.dev)
- [Microsoft Store](https://apps.microsoft.com/detail/9P2D7DZQS61J)
- [Privacy Policy](https://talkkeys-api.ahmed-ovais.workers.dev/privacy)
- [Terms of Service](https://talkkeys-api.ahmed-ovais.workers.dev/tos)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

### Development Setup

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Clone and build:
   ```bash
   git clone https://github.com/symphovais/hotkeypaster.git
   cd hotkeypaster
   dotnet build
   ```
3. Run tests:
   ```bash
   dotnet test TalkKeys.Tests
   ```

See [CLAUDE.md](CLAUDE.md) for development guidelines.

## Acknowledgments

- [NAudio](https://github.com/naudio/NAudio) - Audio recording and processing
- [H.Hooks](https://github.com/HavenDV/H.Hooks) - Global keyboard hooks
- [H.InputSimulator](https://github.com/HavenDV/H.InputSimulator) - Keyboard simulation
- [Polly](https://github.com/App-vNext/Polly) - Resilience and transient fault handling
- [Groq](https://groq.com) - Fast AI inference
