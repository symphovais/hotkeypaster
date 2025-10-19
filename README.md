# 🎙️ HotkeyPaster

A powerful Windows desktop application for voice-to-text transcription with automatic clipboard pasting. Press a hotkey, speak, and instantly paste transcribed text anywhere.

## ✨ Features

### 🎯 Core Functionality
- **Global Hotkey**: Press `Ctrl+Shift+Q` to start/stop recording from anywhere
- **Automatic Pasting**: Transcribed text is automatically pasted into your active window
- **System Tray Integration**: Runs quietly in the background with easy access

### 🎙️ Dual Transcription Modes

#### ☁️ Cloud Mode (OpenAI Whisper)
- Uses OpenAI's Whisper API for transcription
- Most accurate results
- Requires API key and internet connection
- Fast processing with cloud infrastructure

#### 📍 Local Mode (Whisper.net)
- Runs entirely offline on your machine
- **100% Private** - audio never leaves your computer
- **Free** - no API costs
- Works without internet connection
- Supports GPU acceleration (CUDA) if available

### ✨ Text Processing
- **Optional GPT-4 Cleaning**: Removes filler words, fixes grammar, improves formatting
- **Works with both modes**: Available for local and cloud transcription
- **Toggle on/off**: Enable or disable in settings

### ⚡ Performance Testing
- **Speed Test Tool**: Compare local vs cloud transcription speed
- **Detailed Metrics**: Time, word count, words per second
- **Side-by-side Results**: See transcribed text from both methods
- **Winner Declaration**: Know which method is faster on your hardware

### ⚙️ Modern Settings UI
- Beautiful card-based interface
- Real-time validation
- Auto-save configuration
- Model selection (shows only downloaded models)
- API key management

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- .NET 8.0 Runtime

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/hotkeypaster.git
   cd hotkeypaster
   ```

2. **Build the project**
   ```bash
   cd HotkeyPaster
   dotnet build
   ```

3. **Run the application**
   ```bash
   dotnet run
   ```

### First-Time Setup

1. **Right-click the tray icon** and select **Settings**

2. **Choose your transcription mode:**
   - **Cloud Mode**: Enter your OpenAI API key
   - **Local Mode**: Download a model first (see below)

3. **Configure text cleaning** (optional)

4. **Click Save & Close**

## 📥 Downloading Local Models

For local transcription, you need to download a Whisper model:

### Using PowerShell Script
```powershell
.\download-model.cmd
```

### Manual Download
1. Download from [Hugging Face](https://huggingface.co/ggerganov/whisper.cpp/tree/main)
2. Save to `%APPDATA%\HotkeyPaster\Models\`
3. Restart the app and select the model in settings

### Available Models

| Model | Size | Speed | Accuracy | Best For |
|-------|------|-------|----------|----------|
| Tiny | ~75 MB | ⚡⚡⚡⚡⚡ | ⭐⭐ | Quick notes, testing |
| Base | ~142 MB | ⚡⚡⚡⚡ | ⭐⭐⭐ | **Recommended default** |
| Small | ~466 MB | ⚡⚡⚡ | ⭐⭐⭐⭐ | Better accuracy |
| Medium | ~1.5 GB | ⚡⚡ | ⭐⭐⭐⭐⭐ | High accuracy |
| Large V3 | ~2.9 GB | ⚡ | ⭐⭐⭐⭐⭐ | Maximum accuracy |

## 🎮 Usage

### Basic Workflow
1. **Start Recording**: Press `Ctrl+Shift+Q`
2. **Speak**: Talk into your microphone
3. **Stop Recording**: Press `Ctrl+Shift+Q` again
4. **Transcribe**: Click "Transcribe & Paste" button
5. **Auto-Paste**: Text is automatically pasted to your active window

### Settings
- **Right-click tray icon** → **Settings**
- **Double-click tray icon** → Opens settings

### Speed Test
- Open **Settings** → Click **Run Speed Test**
- Records 10 seconds of audio
- Compares local vs cloud performance
- Shows detailed results

## 🏗️ Architecture

### Clean Architecture
```
HotkeyPaster/
├── Services/
│   ├── Audio/              # Audio recording (NAudio)
│   ├── Clipboard/          # Clipboard operations
│   ├── Hotkey/             # Global hotkey registration
│   ├── Notifications/      # Toast notifications
│   ├── Settings/           # Configuration persistence
│   ├── Transcription/      # Transcription services
│   ├── Tray/               # System tray integration
│   └── Window/             # Window positioning
├── MainWindow.xaml         # Main UI
├── SettingsWindow.xaml     # Settings UI
└── SpeedTestWindow.xaml    # Speed test UI
```

### Dependency Injection
- **ITranscriber**: Interface for transcription (OpenAI or Local)
- **ITextCleaner**: Interface for text cleaning (GPT or PassThrough)
- **IAudioTranscriptionService**: Orchestrates transcription pipeline

### Key Technologies
- **.NET 8.0** with WPF
- **Whisper.net** for local transcription
- **NAudio** for audio recording
- **OpenAI API** for cloud transcription and text cleaning
- **Windows Forms** for system tray

## 📊 Comparison: Local vs Cloud

| Feature | Local (Whisper.net) | Cloud (OpenAI) |
|---------|-------------------|----------------|
| **Privacy** | ✅ Fully offline | ❌ Data sent to OpenAI |
| **Cost** | ✅ Free | ❌ Pay per use |
| **Speed** | ⚡ Depends on hardware | ⚡⚡ Fast (cloud) |
| **Accuracy** | ⭐⭐⭐⭐ Good | ⭐⭐⭐⭐⭐ Excellent |
| **Setup** | 📥 One-time download | 🔑 API key needed |
| **Internet** | ✅ Works offline | ❌ Requires connection |
| **Disk Space** | 142 MB - 2.9 GB | None |

## 🔧 Configuration

Settings are stored in: `%APPDATA%\HotkeyPaster\settings.json`

```json
{
  "TranscriptionMode": "Local",
  "LocalModelPath": "C:\\Users\\...\\ggml-base.bin",
  "EnableTextCleaning": true,
  "OpenAIApiKey": "sk-..."
}
```

## 🐛 Troubleshooting

### Audio Issues
- **Check microphone permissions** in Windows Settings
- **Ensure 16kHz recording** is supported by your device
- **Test with different models** if local transcription fails

### Model Issues
- **Download failed**: Check internet connection
- **Model not showing**: Ensure it's in `%APPDATA%\HotkeyPaster\Models\`
- **Slow transcription**: Use smaller model (Tiny or Base)

### API Issues
- **Invalid API key**: Verify key at platform.openai.com
- **Rate limits**: Wait a moment and try again
- **Network errors**: Check internet connection

## 📝 License

MIT License - See LICENSE file for details

## 🙏 Acknowledgments

- [Whisper.net](https://github.com/sandrohanea/whisper.net) by Sandro Hanea
- [OpenAI Whisper](https://openai.com/research/whisper) by OpenAI
- [NAudio](https://github.com/naudio/NAudio) by Mark Heath

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📧 Support

For issues and questions, please open an issue on GitHub.

---

**Made with ❤️ for productivity enthusiasts**
