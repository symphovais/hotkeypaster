# Window Context Detection Feature

## Overview
This feature captures information about the application where the user activates transcription and sends it to the LLM, allowing the AI to intelligently adjust tone and style based on context.

## Philosophy
**Let the LLM do what it does best.** Instead of writing complex parsing logic to interpret window titles and process names, we simply capture the raw information and let GPT figure out the context. The LLM is far better at understanding context than hardcoded rules.

## How It Works

### 1. Context Capture
When the user presses `Ctrl+Shift+Q`, the application:
- Captures the window handle of the currently active application
- Stores it in `_previousWindow` for later analysis

### 2. Raw Information Extraction
Before transcription, the `IActiveWindowContextService` extracts:
- **Process Name**: The executable name (e.g., "OUTLOOK", "WINWORD", "chrome")
- **Window Title**: The full window title (e.g., "RE: Budget Meeting - Message (HTML) - Outlook")

**That's it.** No parsing, no interpretation, no assumptions.

### 3. Context-Aware Cleaning
The raw context is sent to GPT with clear instructions:
```
CONTEXT: The user activated voice transcription from another application. 
Process name: 'OUTLOOK'. 
Window title: 'RE: Budget Meeting - Message (HTML) - Outlook'. 
Based on this context, adjust the tone, formality, and style appropriately.
```

GPT intelligently determines:
- It's Outlook (email client)
- It's a reply to an email about a budget meeting
- Should use formal, professional tone
- Should be concise and business-appropriate

## What the LLM Can Detect

The LLM receives the raw process name and window title, and can intelligently understand:

### Any Application
Since we're not limiting detection to hardcoded apps, the LLM can understand context from **any application**:
- Email clients (Outlook, Thunderbird, Apple Mail, etc.)
- Document editors (Word, Google Docs, LibreOffice, Notion, etc.)
- Chat apps (Slack, Teams, Discord, WhatsApp, Telegram, etc.)
- Browsers with web apps (Gmail, LinkedIn, Twitter, Reddit, etc.)
- Code editors (VS Code, Visual Studio, Sublime, IntelliJ, etc.)
- Note-taking apps (OneNote, Evernote, Obsidian, etc.)
- And literally any other application

### Context Examples
The LLM can infer nuanced context from window titles:
- "RE: Q4 Budget" → Email reply about finances (formal)
- "Slack - #engineering" → Team chat in engineering channel (casual, technical)
- "Document1.docx - Word" → Document editing (structured)
- "Pull Request #123 - GitHub" → Code review (technical, precise)
- "Twitter" → Social media post (casual, concise)
- "Cover Letter.docx" → Job application (very formal)

## Architecture

### New Services
1. **`IActiveWindowContextService`**: Interface for context detection
2. **`ActiveWindowContextService`**: Simple implementation using Windows API (GetWindowText, GetWindowThreadProcessId)
3. **`WindowContext`**: Minimal data class with ProcessName and WindowTitle

### Modified Services
1. **`ITextCleaner`**: Now accepts optional `WindowContext` parameter
2. **`OpenAIGPTTextCleaner`**: Appends raw context to system prompt
3. **`IAudioTranscriptionService`**: Passes context through the pipeline
4. **`MainWindow`**: Captures and provides context during transcription

### Code Simplicity
The entire context detection is **~80 lines of code**:
- Capture window handle (already existed)
- Get process name via Windows API
- Get window title via Windows API
- Pass to LLM

No parsing logic, no enums, no switch statements, no hardcoded app lists.

## Example: What the LLM Receives

### Outlook Email Reply
```
System Prompt:
"You are a text cleaning assistant for HotkeyPaster...
[rules about cleaning text]

CONTEXT: The user activated voice transcription from another application. 
Process name: 'OUTLOOK'. 
Window title: 'RE: Q4 Budget Meeting - Message (HTML) - Outlook'. 
Based on this context, adjust the tone, formality, and style appropriately."

User Message:
"um so basically I think we should like increase the budget by 10 percent you know"

LLM Response:
"I believe we should increase the budget by 10%."
```

### Slack Chat
```
System Prompt:
"[same rules]

CONTEXT: The user activated voice transcription from another application. 
Process name: 'slack'. 
Window title: '#engineering - Slack'. 
Based on this context, adjust the tone, formality, and style appropriately."

User Message:
"hey uh can someone help me with this bug I'm seeing"

LLM Response:
"Hey, can someone help me with this bug I'm seeing?"
```

## Benefits

### 1. Intelligent Adaptation
The LLM automatically adjusts based on ANY application context:
- **Formal contexts** (email, documents): Professional tone, proper grammar
- **Casual contexts** (chat, social media): Natural, conversational flow
- **Technical contexts** (code editors, terminals): Preserves terminology
- **Creative contexts** (note-taking, writing): Maintains voice and style

### 2. Zero Maintenance
No need to update code when:
- New applications are released
- Applications change their window title format
- Users install different software
- Web apps change their branding

The LLM adapts automatically.

### 3. Nuanced Understanding
The LLM can detect subtle context that hardcoded rules would miss:
- "Cover Letter.docx" vs "Grocery List.docx" → Different formality levels
- "RE: Job Application" vs "RE: Lunch Plans" → Different tones
- "#engineering" vs "#random" → Different technical levels

## Privacy Considerations
- Window titles may contain sensitive information (email subjects, document names)
- Context information is only used during the current transcription session
- Not stored permanently, only logged locally for debugging
- Can be disabled by using PassThroughTextCleaner (no GPT cleaning)

## Logging
Context detection is logged for debugging:
```
Window context captured - Process: 'OUTLOOK', Title: 'RE: Meeting Notes - Message (HTML) - Outlook'
```

## Why This Approach is Better

### Before (Hardcoded Parsing)
- 200+ lines of parsing logic
- Hardcoded application list
- Fragile string matching
- Breaks when apps update
- Limited to known applications
- Requires code changes for new apps

### After (LLM-First)
- ~80 lines of code
- Works with ANY application
- Robust to format changes
- Self-maintaining
- Understands nuance and context
- Zero maintenance needed
