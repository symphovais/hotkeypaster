# TalkKeys - Complete Documentation

> Voice-to-text for Windows. Press a hotkey, speak naturally, and watch your words appear anywhere you type.

---

## Table of Contents

1. [Overview](#overview)
2. [User Features](#user-features)
   - [Core Transcription](#core-transcription)
   - [Recording Modes](#recording-modes)
   - [Floating Widget](#floating-widget)
   - [WTF - What are the Facts](#wtf---what-are-the-facts)
   - [Remote Control API](#remote-control-api)
   - [Jabra Headset Integration](#jabra-headset-integration)
   - [Authentication & Accounts](#authentication--accounts)
   - [Settings & Configuration](#settings--configuration)
3. [Technical Architecture](#technical-architecture)
   - [Project Structure](#project-structure)
   - [Pipeline Architecture](#pipeline-architecture)
   - [Services Layer](#services-layer)
   - [Resilience Patterns](#resilience-patterns)
   - [Data Flow](#data-flow)
4. [API Reference](#api-reference)
5. [Privacy & Security](#privacy--security)

---

## Overview

TalkKeys is a Windows desktop application that transforms voice into text instantly. Unlike traditional dictation software that requires you to work within a specific application, TalkKeys works **system-wide** - press a global hotkey from any application, speak naturally, and your transcribed text is automatically pasted where your cursor was.

### Key Highlights

- **10 minutes free daily** - No credit card required
- **Works everywhere** - Any application that accepts text input
- **AI-powered cleanup** - Automatic punctuation, capitalization, and filler word removal
- **Privacy-first** - Audio is processed and immediately discarded
- **Extensible** - Plugin system, HTTP API, hardware button support

### System Requirements

- Windows 10/11 (x64)
- .NET 8.0 Runtime
- Microphone
- Internet connection

---

## User Features

### Core Transcription

The fundamental feature of TalkKeys is voice-to-text transcription with automatic paste.

**How it works:**
1. Press `Ctrl+Shift+Space` (or your custom hotkey) from any application
2. A small floating widget appears showing you're recording
3. Speak naturally - the AI handles punctuation and formatting
4. Press the hotkey again (or release if using push-to-talk)
5. Your transcribed text is automatically pasted into the active window

**AI Text Cleaning:**
- Automatic punctuation (periods, commas, question marks)
- Proper capitalization (sentences, proper nouns)
- Removal of filler words ("um", "uh", stutters)
- Context-aware formatting based on the target application

**Cancellation:**
- Press `Escape` at any time to cancel recording without transcribing

---

### Recording Modes

TalkKeys supports two recording modes to match your workflow:

#### Toggle Mode (Default)
- Press hotkey once to **start** recording
- Press hotkey again to **stop** and transcribe
- Best for: Longer dictation, hands-free operation

#### Push-to-Talk Mode
- **Hold** the hotkey to record
- **Release** to stop and transcribe
- Best for: Quick phrases, walkie-talkie style input

Configure your preferred mode in Settings > Transcription.

---

### Floating Widget

The floating widget provides visual feedback during recording and displays your transcribed text.

#### Widget States

| State | Appearance | Meaning |
|-------|------------|---------|
| Idle | Small purple dot | Ready to record |
| Recording | Expanded red bar with timer | Currently recording |
| Processing | Purple pulsing | Transcribing audio |
| Success | Green flash | Transcription complete |
| Error | Red with message | Something went wrong |

#### Features

- **Always-on-top** - Stays visible over other windows
- **Draggable** - Position anywhere on screen (persists across sessions)
- **Audio level visualization** - See your voice input in real-time
- **Recording timer** - Know how long you've been speaking
- **Hotkey hints** - Shows your configured shortcut
- **Text preview** - See transcribed text after recording
- **Copy button** - Manually copy if paste didn't work
- **Auto-collapse** - Returns to minimal size after 10 seconds
- **Multi-monitor aware** - Works correctly across displays

#### Visual Feedback

During recording, the widget shows:
- Elapsed time (e.g., "0:05")
- Audio level bars that react to your voice
- Current recording state

After transcription:
- The transcribed text in a expandable panel
- A copy button for manual clipboard access
- Auto-dismisses after 10 seconds

---

### WTF - What are the Facts

Select confusing text anywhere and get a plain-English explanation instantly.

**Use Cases:**
- Legal documents with complex jargon
- Technical documentation
- Code snippets you don't understand
- Academic papers
- Medical reports

**How to use:**
1. Select any text (up to 2000 characters)
2. Press `Ctrl+Win+E`
3. A popup appears with a clear, no-BS explanation
4. Popup auto-dismisses after 20 seconds (configurable)

**Example:**
```
Selected: "The party of the first part hereinafter referred to as
the Lessor agrees to indemnify and hold harmless..."

Explanation: "The landlord agrees to protect the tenant from any
legal claims or costs that might come up."
```

---

### Remote Control API

Control TalkKeys programmatically via HTTP. Perfect for:
- Hardware buttons (Jabra headsets, Stream Deck, etc.)
- AI assistants (trigger transcription via voice command)
- Automation scripts
- Custom integrations

#### Base URL
```
http://localhost:38450
```

#### Quick Start

**PowerShell:**
```powershell
# Check status
Invoke-WebRequest -Uri "http://localhost:38450/status" | ConvertFrom-Json

# Start recording
Invoke-WebRequest -Uri "http://localhost:38450/starttranscription" -Method POST

# Stop and transcribe
Invoke-WebRequest -Uri "http://localhost:38450/stoptranscription" -Method POST
```

**cURL:**
```bash
# Start recording
curl -X POST http://localhost:38450/starttranscription

# Stop recording
curl -X POST http://localhost:38450/stoptranscription
```

**JavaScript:**
```javascript
// Start transcription
await fetch('http://localhost:38450/starttranscription', { method: 'POST' });

// Check status
const status = await fetch('http://localhost:38450/status').then(r => r.json());
console.log(status.recording); // true/false
```

#### Available Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | API capabilities and endpoint list |
| GET | `/status` | Current state (idle/recording/processing) |
| POST | `/starttranscription` | Start voice recording |
| POST | `/stoptranscription` | Stop recording and transcribe |
| POST | `/canceltranscription` | Cancel without transcribing |
| POST | `/explain` | Trigger WTF on selected text |
| GET | `/microphones` | List available microphones |
| POST | `/microphone` | Set active microphone |
| GET | `/shortcuts` | Get configured hotkeys |
| POST | `/shortcuts` | Update hotkey bindings |

Full API documentation: https://talkkeys.symphonytek.dk/api-docs

---

### Jabra Headset Integration

TalkKeys has native support for the **Jabra Engage 50 II** headset, enabling hardware button control.

#### Supported Buttons

**Three Dot Button (•••)**
- Toggle recording on/off
- Push-to-talk while held
- Custom keyboard shortcut

**Hook Button**
- Toggle recording on/off
- Push-to-talk while held
- Custom keyboard shortcut

#### Setup

1. Connect your Jabra Engage 50 II headset
2. TalkKeys automatically detects it
3. Go to Settings > Jabra to configure button actions
4. Optionally enable "Auto-select Jabra audio" to use headset mic

#### Use Case: Hands-Free Dictation

With the Three Dot button configured for push-to-talk:
1. Press and hold the button on your headset
2. Speak your message
3. Release the button
4. Text appears in your active application

No keyboard required - perfect for when your hands are busy.

---

### Authentication & Accounts

TalkKeys offers two ways to authenticate:

#### Option 1: TalkKeys Account (Recommended)

**Free tier includes:**
- 10 minutes of transcription per day
- Resets at midnight UTC
- No credit card required
- No API key management

**Setup:**
1. Launch TalkKeys
2. Click "Sign in with Google"
3. Authorize in your browser
4. You're ready to go!

**Account features:**
- Usage tracking in Settings
- Automatic session management
- Sign out anytime

#### Option 2: Own API Key

For unlimited transcription, use your own Groq API key:

**Benefits:**
- No daily limits
- Direct API access
- Full control over your credentials

**Setup:**
1. Get a free API key from [console.groq.com](https://console.groq.com)
2. In TalkKeys, choose "Use own API key"
3. Paste your key
4. Unlimited transcription!

---

### Settings & Configuration

Access settings via the system tray icon > Settings.

#### General Settings

| Setting | Description | Default |
|---------|-------------|---------|
| Start with Windows | Launch TalkKeys at login | Off |
| Recording Mode | Toggle or Push-to-Talk | Toggle |
| Text Cleaning | AI punctuation/capitalization | On |
| Audio Device | Select microphone | System default |

#### Transcription Hotkey

Default: `Ctrl+Shift+Space`

Click "Change" to set a custom hotkey. Supports:
- Ctrl, Shift, Alt, Win modifiers
- Any letter, number, or function key

#### WTF (Explainer) Settings

| Setting | Description | Default |
|---------|-------------|---------|
| Hotkey | Trigger shortcut | Ctrl+Win+E |
| Auto-dismiss | Popup timeout | 20 seconds |

#### Remote Control Settings

| Setting | Description | Default |
|---------|-------------|---------|
| Enabled | API server on/off | On |
| Port | Localhost port | 38450 |

#### Jabra Settings

| Setting | Description | Default |
|---------|-------------|---------|
| Three Dot Action | Button behavior | Toggle |
| Hook Action | Button behavior | Toggle |
| Auto-select audio | Use Jabra mic | Off |

---

## Technical Architecture

### Project Structure

```
TalkKeys/
├── HotkeyPaster/                 # Main WPF application
│   ├── App.xaml.cs               # Application entry point
│   ├── FloatingWidget.xaml       # Recording UI widget
│   ├── SettingsWindow.xaml       # Settings interface
│   ├── WelcomeWindow.xaml        # First-run setup
│   ├── AboutWindow.xaml          # About/What's New
│   ├── Services/
│   │   ├── Audio/                # Audio recording
│   │   ├── Transcription/        # API integration
│   │   ├── Clipboard/            # Paste operations
│   │   ├── Pipeline/             # Processing pipeline
│   │   ├── Settings/             # Configuration
│   │   ├── Controller/           # Central controller
│   │   ├── RemoteControl/        # HTTP API server
│   │   └── Hotkey/               # Global hotkeys
│   └── Plugins/                  # Built-in plugins
│
├── TalkKeys.PluginSdk/           # Plugin framework
├── TalkKeys.JabraTriggerPlugin/  # Jabra headset support
├── TalkKeys.Tests/               # Integration tests
└── backend/                      # Cloudflare Workers API
```

### Pipeline Architecture

TalkKeys uses a **composition-based pipeline pattern** for audio processing:

```
┌─────────────────────────────────────────────────────────────────┐
│                         Pipeline                                 │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐   ┌─────────────────┐   ┌─────────────────┐   │
│  │   Audio     │ → │  Transcription  │ → │  Text Cleaning  │   │
│  │ Validation  │   │     Stage       │   │     Stage       │   │
│  └─────────────┘   └─────────────────┘   └─────────────────┘   │
│                                                                  │
│  Context: { audioBytes, rawText, cleanedText, metrics }         │
└─────────────────────────────────────────────────────────────────┘
```

**Key Components:**

| Component | Purpose |
|-----------|---------|
| `PipelineContext` | Shared data passed through all stages |
| `IPipelineStage` | Interface for processing stages |
| `Pipeline` | Executes stages sequentially with retry logic |
| `PipelineFactory` | Creates pipelines from JSON configuration |
| `PipelineRegistry` | Manages available pipeline definitions |

**Benefits:**
- Modular - Add/remove stages without code changes
- Configurable - JSON-driven pipeline definitions
- Observable - Rich metrics from each stage
- Testable - Stages can be tested in isolation

### Services Layer

#### AudioRecordingService

Handles microphone input via NAudio:

```
Microphone → WaveInEvent → WAV Buffer → File
                ↓
         Audio Level Events → UI Visualization
```

**Features:**
- 16kHz, 16-bit, mono recording (Whisper-optimized)
- Device retry logic (handles Windows audio locking)
- Real-time audio level calculation
- Silent recording detection

#### TalkKeysApiService

Communicates with the backend API:

```
┌─────────────┐     HTTPS      ┌─────────────────────┐
│  TalkKeys   │ ────────────── │  Cloudflare Workers │
│   Desktop   │                │      Backend        │
└─────────────┘                └─────────────────────┘
                                        │
                                        ↓
                               ┌─────────────────┐
                               │    Groq API     │
                               │  (Whisper/LLM)  │
                               └─────────────────┘
```

**Endpoints used:**
- `/api/whisper` - Audio transcription
- `/api/clean` - Text formatting
- `/api/explain` - WTF explanations
- `/api/usage` - Quota checking

#### ClipboardPasteService

Handles text insertion via clipboard:

```
1. Backup current clipboard
2. Set transcribed text
3. Simulate Ctrl+V
4. Restore original clipboard (async, 300ms delay)
```

**Why clipboard + Ctrl+V?**
- Works in any application
- No need to detect input method
- Handles complex text (Unicode, formatting)
- Most reliable cross-application approach

### Resilience Patterns

TalkKeys implements multi-layer resilience using Polly:

#### Network Layer (HTTP)
```csharp
// 3 retries with exponential backoff + jitter
Retry: 1s → 2s → 4s (with random jitter)

Handles:
- HTTP 5xx (server errors)
- HTTP 429 (rate limiting)
- HTTP 408 (timeout)
- Network exceptions
```

#### Audio Device Layer
```csharp
// 3 retries for microphone access
Retry: 200ms between attempts

Handles:
- Device busy (another app using mic)
- Driver initialization delays
- Windows audio service latency
```

#### Clipboard Layer
```csharp
// 3 retries for clipboard access
Retry: 50ms between attempts

Handles:
- COM exceptions (clipboard locked)
- Cross-thread access issues
```

### Data Flow

Complete flow from hotkey press to pasted text:

```
1. HOTKEY PRESS
   ├─ H.Hooks intercepts Ctrl+Shift+Space
   ├─ TalkKeysController.StartTranscriptionAsync()
   └─ FloatingWidget shows recording state

2. RECORDING
   ├─ AudioRecordingService captures audio
   ├─ NAudio WaveInEvent → WAV buffer
   ├─ Audio levels → UI visualization
   └─ Temporary file on disk

3. HOTKEY RELEASE / SECOND PRESS
   ├─ AudioRecordingService.StopRecording()
   ├─ WAV file finalized
   └─ Pipeline execution begins

4. PIPELINE EXECUTION
   ├─ Stage 1: AudioValidationStage
   │   └─ Validate size (<25MB), extract duration
   ├─ Stage 2: TalkKeysTranscriptionStage
   │   └─ POST /api/whisper → raw text
   └─ Stage 3: TalkKeysTextCleaningStage
       └─ POST /api/clean → formatted text

5. PASTE OPERATION
   ├─ Backup clipboard
   ├─ Set transcribed text
   ├─ Simulate Ctrl+V
   └─ Async restore clipboard

6. COMPLETION
   ├─ FloatingWidget shows success
   ├─ Text preview displayed
   └─ Widget collapses after 10s
```

---

## API Reference

### Status Response

```json
GET /status

{
  "success": true,
  "status": "idle",       // "idle" | "recording" | "processing"
  "recording": false,
  "processing": false,
  "authenticated": true,
  "version": "1.2.0"
}
```

### Start Transcription

```json
POST /starttranscription

// Success
{
  "success": true,
  "status": "recording",
  "message": "Recording started"
}

// Already recording
{
  "success": false,
  "status": "recording",
  "message": "Already recording"
}
```

### Stop Transcription

```json
POST /stoptranscription

{
  "success": true,
  "status": "processing",
  "message": "Processing transcription"
}
```

### List Microphones

```json
GET /microphones

{
  "success": true,
  "microphones": [
    { "index": 0, "name": "Jabra Engage 50", "current": true },
    { "index": 1, "name": "Realtek Audio", "current": false }
  ]
}
```

### Set Microphone

```json
POST /microphone
Content-Type: application/json

{ "index": 1 }

// Response
{
  "success": true,
  "message": "Microphone set to: Realtek Audio"
}
```

### Get Shortcuts

```json
GET /shortcuts

{
  "success": true,
  "shortcuts": {
    "transcription": "Ctrl+Shift+Space",
    "explain": "Ctrl+Win+E"
  }
}
```

---

## Privacy & Security

### What We Collect

| Data | Stored? | Purpose |
|------|---------|---------|
| Audio recordings | **No** | Processed and immediately discarded |
| Transcribed text | **No** | Sent directly to your device |
| Email (Google sign-in) | Yes | Account identification |
| Usage duration | Yes | Daily limit enforcement |

### What We Don't Collect

- Your actual transcribed content
- Clipboard history
- Keystrokes
- Window titles or content
- Any personally identifiable information beyond email

### Data Security

- All API communication uses HTTPS/TLS
- OAuth 2.0 for Google authentication
- JWT tokens for session management
- No third-party analytics or tracking
- API keys stored locally in AppData folder

### Third-Party Services

| Service | Purpose | Data Shared |
|---------|---------|-------------|
| Google OAuth | Authentication | Email, name |
| Groq API | Transcription/AI | Audio bytes (not stored) |
| Cloudflare | Backend hosting | Request metadata |

For full details, see our [Privacy Policy](https://talkkeys.symphonytek.dk/privacy).

---

## Libraries & Acknowledgments

TalkKeys is built with these excellent open-source libraries:

| Library | Purpose | License |
|---------|---------|---------|
| [NAudio](https://github.com/naudio/NAudio) | Audio recording | MIT |
| [H.Hooks](https://github.com/HavenDV/H.Hooks) | Global keyboard hooks | MIT |
| [H.InputSimulator](https://github.com/HavenDV/H.InputSimulator) | Keyboard simulation | MIT |
| [HidSharp](https://github.com/IntergatedCircuits/HidSharp) | USB HID (Jabra) | Apache-2.0 |
| [Polly](https://github.com/App-vNext/Polly) | Resilience patterns | BSD-3-Clause |
| [Groq](https://groq.com) | AI inference | - |

---

## Version History

### v1.2.0 - Remote Control & WTF (December 2024)

**Major Features:**
- Remote Control API at `localhost:38450`
- WTF (What are the Facts) text explanation feature
- Text preview panel after transcription

**Improvements:**
- Combined About/What's New window
- Hero feature cards for releases
- Better error messages

### v1.1.0 - Stability Improvements

- Hotkeys persist after restart
- More reliable pasting
- Network resilience with auto-retry

### v1.0.0 - Initial Release

- Voice-to-text transcription
- Global hotkey support
- AI text cleaning
- Google OAuth authentication
- Jabra headset support

---

## Links

- **Website:** https://talkkeys.symphonytek.dk
- **Microsoft Store:** https://apps.microsoft.com/detail/9P2D7DZQS61J
- **GitHub:** https://github.com/symphovais/hotkeypaster
- **API Documentation:** https://talkkeys.symphonytek.dk/api-docs
- **Privacy Policy:** https://talkkeys.symphonytek.dk/privacy
- **Terms of Service:** https://talkkeys.symphonytek.dk/tos

---

*TalkKeys is made with love by symphonytek ApS.*
