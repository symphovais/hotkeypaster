# Local Transcription with Whisper.net

HotkeyPaster now supports **offline, local transcription** using Whisper.net! This means you can transcribe audio without sending data to OpenAI's servers.

## 🚀 Quick Start

### Enable Local Transcription

Set the environment variable:
```powershell
$env:USE_LOCAL_TRANSCRIPTION = "true"
```

Or permanently:
```powershell
[System.Environment]::SetEnvironmentVariable('USE_LOCAL_TRANSCRIPTION', 'true', 'User')
```

### First Run

On first run with local transcription enabled:
1. The app will automatically download the Whisper Base model (~142 MB)
2. Models are stored in `%APPDATA%\HotkeyPaster\Models\`
3. Download happens only once - subsequent runs use the cached model

## 🎯 Configuration Options

### Transcription Modes

| Mode | Environment Variable | Requirements |
|------|---------------------|--------------|
| **OpenAI** (default) | `USE_LOCAL_TRANSCRIPTION=false` or not set | `OPENAI_API_KEY` required |
| **Local** | `USE_LOCAL_TRANSCRIPTION=true` | No API key needed |

### Text Cleaning

Even with local transcription, you can optionally use OpenAI for text cleanup:

- **With API key**: Local transcription + OpenAI text cleanup
- **Without API key**: Local transcription + no cleanup (raw output)

## 📊 Available Models

The default model is **Base** (good balance of speed and accuracy). You can modify the model in `App.xaml.cs`:

| Model | Size | Speed | Accuracy | Best For |
|-------|------|-------|----------|----------|
| Tiny | ~75 MB | ⚡⚡⚡⚡⚡ | ⭐⭐ | Quick notes, testing |
| Base | ~142 MB | ⚡⚡⚡⚡ | ⭐⭐⭐ | **Recommended default** |
| Small | ~466 MB | ⚡⚡⚡ | ⭐⭐⭐⭐ | Better accuracy needed |
| Medium | ~1.5 GB | ⚡⚡ | ⭐⭐⭐⭐⭐ | High accuracy, slower |
| Large V3 | ~2.9 GB | ⚡ | ⭐⭐⭐⭐⭐ | Maximum accuracy |

## 🔧 Advanced Configuration

### Change Model

Edit `App.xaml.cs` line 107:
```csharp
var modelType = GgmlType.Base;  // Change to Small, Medium, etc.
```

### Model Storage Location

Models are stored in:
```
%APPDATA%\HotkeyPaster\Models\
```

To free up space, delete unused models from this directory.

### Performance Tips

1. **GPU Acceleration**: Whisper.net.AllRuntimes includes CUDA support
   - If you have an NVIDIA GPU with CUDA installed, it will automatically be used
   - Significant speed improvement for larger models

2. **Model Selection**:
   - Use **Tiny** or **Base** for real-time feel
   - Use **Small** or **Medium** for better accuracy
   - Use **Large** only if you need maximum accuracy and don't mind waiting

## 🆚 Local vs OpenAI Comparison

| Feature | Local (Whisper.net) | OpenAI API |
|---------|-------------------|------------|
| **Privacy** | ✅ Fully offline | ❌ Data sent to OpenAI |
| **Cost** | ✅ Free | ❌ Pay per use |
| **Speed** | ⚡ Depends on hardware | ⚡⚡ Fast (cloud) |
| **Accuracy** | ⭐⭐⭐⭐ Good | ⭐⭐⭐⭐⭐ Excellent |
| **Setup** | 📥 One-time download | 🔑 API key needed |
| **Internet** | ✅ Works offline | ❌ Requires connection |
| **Disk Space** | 142 MB - 2.9 GB | None |

## 🐛 Troubleshooting

### Model Download Fails
- Check internet connection
- Ensure `%APPDATA%\HotkeyPaster\Models\` is writable
- Try downloading manually from [Hugging Face](https://huggingface.co/ggerganov/whisper.cpp)

### Slow Transcription
- Use a smaller model (Tiny or Base)
- Check if GPU acceleration is working
- Close other resource-intensive applications

### Out of Memory
- Use a smaller model
- Close other applications
- Ensure you have enough RAM (4GB+ recommended for Medium/Large models)

## 💡 Recommended Setup

**For most users:**
```powershell
$env:USE_LOCAL_TRANSCRIPTION = "true"
# Uses Base model, fully offline, good accuracy
```

**For best accuracy (with API key):**
```powershell
$env:USE_LOCAL_TRANSCRIPTION = "true"
$env:OPENAI_API_KEY = "your-key-here"
# Local transcription + OpenAI text cleanup
```

**For cloud-based (original):**
```powershell
$env:USE_LOCAL_TRANSCRIPTION = "false"
$env:OPENAI_API_KEY = "your-key-here"
# Full OpenAI pipeline
```
