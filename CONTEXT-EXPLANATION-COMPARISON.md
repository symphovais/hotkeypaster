# Context Explanation: Before vs After

## The Problem You Identified

You asked: **"Where are we telling what Context is and what the user is actually doing?"**

You were right - the original explanation was too brief and vague.

---

## BEFORE (Original - Too Vague)

```
CONTEXT: The user activated voice transcription from another application. 
Process name: 'OUTLOOK'. 
Window title: 'RE: Q4 Budget Meeting - Message (HTML) - Outlook'. 
Based on this context, adjust the tone, formality, and style of the cleaned text appropriately. 
For example: formal for emails/documents, casual for chat apps, technical for code editors, professional for business apps.
```

### Problems:
- ❌ Doesn't explain what the plugin does
- ❌ Doesn't explain the workflow (hotkey → speak → paste back)
- ❌ Doesn't clearly state the LLM's task
- ❌ Examples are brief and unclear
- ❌ Doesn't explain WHY context matters

---

## AFTER (Enhanced - Clear and Explicit)

```
=== CONTEXT INFORMATION ===
The user was working in another application when they pressed a hotkey to activate voice transcription. 
They spoke into their microphone, and now you're cleaning up that transcribed text. 
The transcribed text will be automatically pasted back into the application they were using.

Application details:
- Process name: 'OUTLOOK'
- Window title: 'RE: Q4 Budget Meeting - Message (HTML) - Outlook'

Your task: Analyze the process name and window title to understand what application the user is in and what they're doing. 
Then adjust the tone, formality, and style of the cleaned text to match that context.

Examples of how to adapt:
- Email clients (Outlook, Gmail): Use formal, professional language
- Chat apps (Slack, Teams, Discord): Keep it casual and conversational
- Documents (Word, Google Docs): Use structured, clear writing
- Code editors (VS Code, Visual Studio): Preserve technical terms precisely
- Social media (Twitter, Facebook): Keep it brief and conversational
- Professional contexts (LinkedIn, cover letters): Very formal and polished
```

### Improvements:
- ✅ **Explains the plugin**: "pressed a hotkey to activate voice transcription"
- ✅ **Explains the workflow**: hotkey → speak → clean → paste back
- ✅ **Explains WHY context matters**: "will be automatically pasted back into the application they were using"
- ✅ **Clear task**: "Analyze the process name and window title to understand..."
- ✅ **Concrete examples**: Specific apps with specific tone guidance
- ✅ **Structured format**: Easy to read with clear sections

---

## What This Tells the LLM

### The Complete Picture

1. **What happened**: User pressed a hotkey while in another app
2. **What they did**: Spoke into microphone
3. **What you're doing**: Cleaning the transcribed text
4. **What happens next**: Text gets pasted back into their app
5. **Why context matters**: The text needs to fit the destination app
6. **Your task**: Figure out the app from process name + window title
7. **How to adapt**: Specific examples for different app types

### The LLM Now Understands:

```
User in Outlook → Writing email reply about budget → 
Spoke casually with filler words → 
I need to clean it up → 
It's going back into an email → 
Should be formal and professional
```

vs

```
User in Slack → Chatting in #engineering channel → 
Spoke casually with filler words → 
I need to clean it up → 
It's going into a chat message → 
Should stay casual but clean
```

---

## Real Example: Full System Prompt

Here's what the LLM actually receives now:

```
You are a text cleaning assistant for a voice-to-text transcription application called HotkeyPaster. 
The user speaks into their microphone, and the audio is transcribed to text. 
Your job is to clean up the raw transcription. 

RULES:
1. ONLY fix issues - do NOT add new content or information that wasn't spoken
2. Remove filler words (um, uh, like, you know, I mean, sort of, kind of)
3. Fix grammar errors and add proper punctuation
4. Ensure proper capitalization
5. Remove or replace profanity and inappropriate language
6. Keep the original meaning and approximate length
7. If context about the target application is provided, adjust tone and formality accordingly
8. Return ONLY the cleaned text, no explanations or meta-commentary


=== CONTEXT INFORMATION ===
The user was working in another application when they pressed a hotkey to activate voice transcription. 
They spoke into their microphone, and now you're cleaning up that transcribed text. 
The transcribed text will be automatically pasted back into the application they were using.

Application details:
- Process name: 'OUTLOOK'
- Window title: 'RE: Q4 Budget Meeting - Message (HTML) - Outlook'

Your task: Analyze the process name and window title to understand what application the user is in and what they're doing. 
Then adjust the tone, formality, and style of the cleaned text to match that context.

Examples of how to adapt:
- Email clients (Outlook, Gmail): Use formal, professional language
- Chat apps (Slack, Teams, Discord): Keep it casual and conversational
- Documents (Word, Google Docs): Use structured, clear writing
- Code editors (VS Code, Visual Studio): Preserve technical terms precisely
- Social media (Twitter, Facebook): Keep it brief and conversational
- Professional contexts (LinkedIn, cover letters): Very formal and polished
```

---

## Why This Matters

### Before: Ambiguous
The LLM had to guess:
- What is this "context"?
- Why does it matter?
- What should I do with it?
- How much should I adapt?

### After: Crystal Clear
The LLM knows exactly:
- ✅ The user pressed a hotkey in another app
- ✅ They spoke, and I'm cleaning the transcription
- ✅ It's going back into that app
- ✅ I need to match the tone to the destination
- ✅ Here are concrete examples of how to adapt

---

## Code Location

This explanation is generated in:
```
HotkeyPaster/Services/Window/WindowContext.cs
Method: GetContextPrompt()
Lines: 28-62
```

Every time transcription happens, this method builds the context explanation and appends it to the system prompt before sending to GPT.

---

## Key Insight

**The better you explain the task to the LLM, the better results you get.**

We went from a vague one-liner to a structured, detailed explanation that tells the LLM:
1. What the plugin does
2. What the user is doing
3. What the LLM's job is
4. Why context matters
5. How to adapt to different contexts

This is **prompt engineering** at work - being explicit and clear with the LLM produces much better results.
