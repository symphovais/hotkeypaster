# Unreleased Changes

This file tracks user-facing enhancements for inclusion in release notes.

---

## Smart Actions (Context-Aware Replies)

### New Feature: Reply Generation
- **Smart Actions buttons** appear in the WTF popup based on app context (email, chat, code)
- Detects context from the active application (Outlook, Slack, Teams, VS Code, etc.)
- **Voice-to-Reply**: Click "Reply", speak your instructions, get a professional reply generated
- AI generates contextual replies that respond TO the original message, not just clean your voice input

### Backend
- New `/api/suggest-actions` endpoint - detects context type and suggests relevant actions
- New `/api/generate-reply` endpoint - generates professional replies from voice instructions

---

## Redesigned Floating Widget

### Idle State
- **Minimal pill design** with soft depth and rounded edges
- **Raised circular mic button** - visually distinct with gradient surface and inner highlight ring
- **Subtle breathing animation** on mic button (slow 2.5s heartbeat-style glow)
- **Static dot indicators** replace animated waveform - present but not implying active recording
- Cool-toned, restrained dark UI (`#1a1d24` background)
- Clear visual hierarchy: mic button (highest contrast) → indicators → container (lowest)

### Recording State
- Purple gradient border with soft glow effect
- Keyboard shortcut hint in styled badge
- Same minimal aesthetic as idle state

### Transcription Panel
- Matches dark minimal theme
- Simple green dot for success status
- Cleaner, more subtle action buttons

### Design Philosophy
- Feels present and ready, but never intrusive
- No busy animations in idle state
- Widget says "I'm here when you need me" without demanding attention

---

## Session Management Improvements

### Fixed False Logouts
- Previously, ANY API error would log the user out (including network issues)
- Now only actual auth failures (401/403) trigger logout
- Network errors, server errors (5xx), timeouts no longer cause false session expiration
- Users stay logged in even with intermittent connectivity

---

## Welcome Screen Updates

### Added Close Button
- Users can now close the welcome screen without logging in
- X button in top-right corner
- Useful for users who want to explore before committing

### Removed Deprecated Options
- Removed "Use my own Groq API key" option (TalkKeys account is now the only auth method)
- Cleaner, simpler onboarding flow

---

## Technical Improvements

### Reusable RecordingIndicator Control
- New `Controls/RecordingIndicator.xaml` - shared recording UI component
- Used in both FloatingWidget and ExplainerPopup
- States: Recording (red pulse), Processing (purple), Success (green), Error (red)
- Includes timer display and audio level bars

### Code Quality
- Added `TokenValidationResult` class for proper auth error discrimination
- Improved logging for debugging session issues

---

## Version Info

These changes are pending release. Last updated: 2024-12-26
