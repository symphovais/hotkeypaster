# TalkKeys Development Guidelines

## Library-First Development

**MANDATORY**: Before implementing any functionality, search for well-maintained, widely-adopted libraries.

### When Implementing New Features:
1. **Search first** - Look for NuGet packages that solve the problem
2. **Evaluate candidates** based on:
   - Download count (prefer 50K+ downloads)
   - Last update date (prefer updated within 12 months)
   - .NET version support (must support .NET 8)
   - License compatibility (MIT, Apache-2.0, etc.)
   - API simplicity and documentation
3. **Contain dependencies** - Wrap library usage in a service/adapter class
4. **Only build custom** when no suitable library exists or libraries have critical limitations

### Current Libraries Used:
- **NAudio** - Audio recording and processing
- **H.Hooks** - Global keyboard hooks (hotkeys, push-to-talk)
- **H.InputSimulator** - Keyboard input simulation
- **HidSharp** - USB HID device communication (Jabra headsets)
- **Microsoft.Toolkit.Uwp.Notifications** - Toast notifications
- **Polly** - HTTP resilience (retry with exponential backoff, jitter)

### Why Library-First:
- Reduces bugs (battle-tested code)
- Saves development time
- Gets security updates automatically
- Better edge case handling

---

## Integration Testing Requirements

**MANDATORY**: Every code change must be evaluated against integration tests.

### For Bug Fixes:
1. Before fixing a bug, check if there's an existing test that covers this scenario
2. If no test exists, **create one first** that reproduces the bug (it should fail)
3. Fix the bug and verify the new test passes
4. Run the full test suite: `dotnet test TalkKeys.Tests`

### For New Features:
1. Write integration tests alongside the feature implementation
2. Tests should cover:
   - Happy path (normal usage)
   - Edge cases (empty inputs, nulls, boundaries)
   - Error handling (what happens when things go wrong)

### For Refactoring:
1. Run existing tests before refactoring
2. After refactoring, all existing tests must still pass
3. Add new tests if the refactoring exposes new testable behavior

### Test Location
All integration tests are in `TalkKeys.Tests/`:
- `AudioRecordingServiceTests.cs` - Audio recording functionality
- `ClipboardPasteServiceTests.cs` - Clipboard and paste operations
- `SettingsServiceTests.cs` - Settings persistence and serialization
- `PipelineTests.cs` - Pipeline execution and stages
- `HotkeyTests.cs` - Trigger configuration and hotkey handling

### Running Tests
```bash
# Run all tests
dotnet test TalkKeys.Tests

# Run with detailed output
dotnet test TalkKeys.Tests --logger "console;verbosity=detailed"

# Run specific test class
dotnet test TalkKeys.Tests --filter "FullyQualifiedName~PipelineTests"
```

### Tests That Require Hardware
Some tests are skipped by default because they require hardware (microphone):
- `StartRecording_WithMicrophone_CreatesFile`
- `StartRecording_WhenDeviceBusy_RetriesAndFails`

Run these manually when testing audio recording changes.

---

## Code Quality
- No warnings allowed in the codebase. All compiler warnings must be resolved or explicitly suppressed with justification.

---

## AI Models (DO NOT CHANGE)

**IMPORTANT**: The following AI model configuration has been tested and approved. Do not change these without explicit user approval.

### Backend API (Cloudflare Worker → Groq)
| Feature | Model | Reason |
|---------|-------|--------|
| Text Cleaning | `openai/gpt-oss-20b` | Best quality for transcription cleanup |
| WTF Explainer | `openai/gpt-oss-20b` | Best quality for witty translations |
| Words Analysis | `openai/gpt-oss-20b` | Consistent with other text features |

### Desktop App (Direct Groq calls for OwnApiKey mode)
| Feature | Model |
|---------|-------|
| Text Cleaning | `openai/gpt-oss-20b` |
| Words Analysis | `llama-3.1-8b-instant` |

**Note**: Whisper (speech-to-text) uses `whisper-large-v3-turbo` via Groq - this is separate from the text LLMs.

---

## Release History

### v1.2.0 - Remote Control & WTF (Published: 2025-12-15)
**Major Features:**
- **Remote Control API** - HTTP API at `localhost:38450` for external integration (Jabra headsets, Mango Plus, AI assistants)
- **WTF (What are the Facts)** - Select text + Ctrl+Win+E to get facts explained simply
- **Text Preview** - Shows transcribed text after recording with copy button

**UI Changes:**
- Combined About/What's New into single elegant window
- Added hero feature cards for major releases
- Renamed "Plain English Explainer" to "WTF - What are the Facts"

**Bug Fixes:**
- Fixed TalkKeysApiService disposal bug that broke WTF after settings save
