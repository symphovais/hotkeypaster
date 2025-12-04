# TalkKeys

A simple Windows desktop application for voice-to-text transcription. Press a hotkey, speak, and your words are transcribed and pasted into any application.

## Features

- **Global Hotkey**: Press `Ctrl+Alt+Q` to start recording from anywhere
- **Floating Widget**: Draggable, always-on-top widget shows recording status
- **Automatic Pasting**: Transcribed text is automatically pasted into your active window
- **System Tray Integration**: Runs quietly in the background
- **Multi-Monitor Support**: Widget stays on the correct screen
- **Voice Activity Detection**: Uses Silero VAD to detect speech
- **Text Cleaning**: Optional GPT-4 powered text cleaning (removes filler words, fixes grammar)

## How It Works

1. Press `Ctrl+Alt+Q` or click the floating widget to start recording
2. Speak into your microphone
3. Press `Space` to stop recording (or `Escape` to cancel)
4. Audio is processed through the transcription pipeline:
   - Voice Activity Detection (Silero VAD)
   - Speech-to-Text (OpenAI Whisper API)
   - Text Cleaning (optional, GPT-4)
5. Text is automatically pasted into your previously active window

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- OpenAI API key
- Microphone

## Quick Start

### Option 1: Installer (Recommended)

1. Download the latest `TalkKeys-Setup-x.x.x.exe` from the [Releases](https://github.com/yourusername/talkkeys/releases) page
2. Run the installer
3. TalkKeys will start automatically and appear in your system tray

The installer will:
- Install TalkKeys to your Program Files
- Add TalkKeys to Windows startup (can be disabled in Settings)
- Create a desktop shortcut (optional)

### Option 2: Build from Source

1. Clone the repository
   ```bash
   git clone https://github.com/yourusername/talkkeys.git
   cd talkkeys
   ```

2. Build the project
   ```bash
   cd HotkeyPaster
   dotnet build
   ```

3. Run the application
   ```bash
   dotnet run
   ```

### Building the Installer

To build the installer yourself:

1. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php)
2. Run the build script:
   ```cmd
   build-installer.cmd
   ```
3. The installer will be created in `installer\output\`

### First-Time Setup

1. The floating widget will appear showing "API key required"
2. Right-click the system tray icon and select **Settings**
3. Enter your OpenAI API key
4. Click **Save**
5. The widget will now show "Ready to record"

## Usage

| Action | Method |
|--------|--------|
| Start recording | Press `Ctrl+Alt+Q` or click the widget |
| Stop recording | Press `Space` |
| Cancel recording | Press `Escape` |
| Move widget | Drag the widget to any position |
| Hide widget | Click the X button on the widget |
| Show widget | Press `Ctrl+Alt+Q` |
| Open settings | Right-click tray icon → Settings |
| Exit application | Right-click tray icon → Exit |

## Settings

Settings are stored in: `%APPDATA%\TalkKeys\settings.json`

| Setting | Description |
|---------|-------------|
| OpenAI API Key | Required for transcription |
| Audio Device | Select your microphone |
| Start with Windows | Launch TalkKeys automatically on login |

## Architecture

```
HotkeyPaster/
├── Services/
│   ├── Audio/           # Audio recording (NAudio)
│   ├── Clipboard/       # Clipboard paste operations
│   ├── Hotkey/          # Global hotkey registration
│   ├── Notifications/   # Toast notifications
│   ├── Pipeline/        # Transcription pipeline
│   │   ├── Stages/      # VAD, Whisper, Text Cleaning
│   │   └── Configuration/
│   ├── Settings/        # Configuration persistence
│   ├── Tray/            # System tray integration
│   └── Windowing/       # Window context service
├── FloatingWidget.xaml  # Main floating UI
└── SettingsWindow.xaml  # Settings UI
```

## Technologies

- **.NET 8.0** with WPF
- **NAudio** for audio recording
- **Silero VAD** for voice activity detection
- **OpenAI Whisper API** for transcription
- **OpenAI GPT-4** for text cleaning (optional)

## Troubleshooting

### Widget not appearing
- Check the system tray for the TalkKeys icon
- Press `Ctrl+Alt+Q` to show the widget

### Transcription not working
- Verify your OpenAI API key is correct
- Check your internet connection
- Ensure your microphone is working

### Audio issues
- Open Settings and select the correct audio device
- Check Windows microphone permissions

### Hotkey not working
- Another application may be using `Ctrl+Alt+Q`
- Restart TalkKeys

## License

MIT License

## Acknowledgments

- [NAudio](https://github.com/naudio/NAudio) by Mark Heath
- [Silero VAD](https://github.com/snakers4/silero-vad)
- [OpenAI](https://openai.com) for Whisper and GPT APIs
