# Sample Prompts to GPT Text Cleaner

This document shows exactly what the GPT-4.1-nano model receives when cleaning transcribed text with different contexts.

---

## Example 1: Email Reply in Outlook

### System Prompt (sent to GPT)
```
You are a text cleaning assistant for a voice-to-text transcription application called HotkeyPaster. The user speaks into their microphone, and the audio is transcribed to text. Your job is to clean up the raw transcription. 

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
The user was working in another application when they pressed a hotkey to activate voice transcription. They spoke into their microphone, and now you're cleaning up that transcribed text. The transcribed text will be automatically pasted back into the application they were using.

Application details:
- Process name: 'OUTLOOK'
- Window title: 'RE: Q4 Budget Meeting - Message (HTML) - Outlook'

Your task: Analyze the process name and window title to understand what application the user is in and what they're doing. Then adjust the tone, formality, and style of the cleaned text to match that context.

Examples of how to adapt:
- Email clients (Outlook, Gmail): Use formal, professional language
- Chat apps (Slack, Teams, Discord): Keep it casual and conversational
- Documents (Word, Google Docs): Use structured, clear writing
- Code editors (VS Code, Visual Studio): Preserve technical terms precisely
- Social media (Twitter, Facebook): Keep it brief and conversational
- Professional contexts (LinkedIn, cover letters): Very formal and polished
```

### User Message (raw transcription)
```
um so basically I think we should like increase the budget by 10 percent you know because uh the marketing costs have gone up and we need to stay competitive
```

### Expected GPT Response
```
I believe we should increase the budget by 10% because marketing costs have risen and we need to remain competitive.
```

**Why this output?**
- GPT detected it's Outlook (email client)
- Saw "RE: Q4 Budget Meeting" (formal business context)
- Applied professional tone
- Removed all filler words (um, basically, like, you know, uh)
- Made it concise and business-appropriate
- Changed "gone up" → "risen" (more formal)
- Changed "stay" → "remain" (more formal)

---

## Example 2: Slack Chat Message

### System Prompt
```
[Same base rules as above]


=== CONTEXT INFORMATION ===
The user was working in another application when they pressed a hotkey to activate voice transcription. They spoke into their microphone, and now you're cleaning up that transcribed text. The transcribed text will be automatically pasted back into the application they were using.

Application details:
- Process name: 'slack'
- Window title: '#engineering - Slack'

Your task: Analyze the process name and window title to understand what application the user is in and what they're doing. Then adjust the tone, formality, and style of the cleaned text to match that context.

[Examples of how to adapt - same as above]
```

### User Message (raw transcription)
```
hey uh can someone help me with this bug I'm seeing like the API is returning a 500 error you know when I try to post to the endpoint
```

### Expected GPT Response
```
Hey, can someone help me with this bug I'm seeing? The API is returning a 500 error when I try to POST to the endpoint.
```

**Why this output?**
- GPT detected it's Slack (chat app)
- Saw "#engineering" channel (technical but casual)
- Kept casual tone ("Hey")
- Removed filler words but kept conversational style
- Preserved technical terms (API, 500 error, POST, endpoint)
- Proper capitalization of "POST" (HTTP method)

---

## Example 3: Google Docs in Chrome

### System Prompt
```
[Same base rules as above]

CONTEXT: The user activated voice transcription from another application. Process name: 'chrome'. Window title: 'Cover Letter - Google Docs'. Based on this context, adjust the tone, formality, and style of the cleaned text appropriately. For example: formal for emails/documents, casual for chat apps, technical for code editors, professional for business apps.
```

### User Message (raw transcription)
```
um I am writing to express my interest in the software engineer position at your company like I have five years of experience you know in full stack development
```

### Expected GPT Response
```
I am writing to express my interest in the software engineer position at your company. I have five years of experience in full-stack development.
```

**Why this output?**
- GPT detected it's a "Cover Letter" (very formal context)
- Applied highly professional tone
- Removed all filler words
- Proper hyphenation (full-stack)
- Formal structure with proper sentences

---

## Example 4: Discord Chat

### System Prompt
```
[Same base rules as above]

CONTEXT: The user activated voice transcription from another application. Process name: 'Discord'. Window title: '#general - My Gaming Server - Discord'. Based on this context, adjust the tone, formality, and style of the cleaned text appropriately. For example: formal for emails/documents, casual for chat apps, technical for code editors, professional for business apps.
```

### User Message (raw transcription)
```
yo uh anyone wanna play some games tonight like around 8 pm you know we could do that new battle royale game
```

### Expected GPT Response
```
Yo, anyone wanna play some games tonight around 8 PM? We could do that new battle royale game.
```

**Why this output?**
- GPT detected Discord (very casual gaming context)
- Kept very casual tone ("Yo", "wanna")
- Removed filler words but preserved casual language
- Still added proper punctuation
- Capitalized "PM" correctly

---

## Example 5: VS Code (Code Editor)

### System Prompt
```
[Same base rules as above]

CONTEXT: The user activated voice transcription from another application. Process name: 'Code'. Window title: 'main.py - Visual Studio Code'. Based on this context, adjust the tone, formality, and style of the cleaned text appropriately. For example: formal for emails/documents, casual for chat apps, technical for code editors, professional for business apps.
```

### User Message (raw transcription)
```
um so this function should like iterate through the array and uh return the max value you know using a for loop
```

### Expected GPT Response
```
This function should iterate through the array and return the max value using a for loop.
```

**Why this output?**
- GPT detected VS Code (code editor)
- Preserved technical terms exactly (iterate, array, max value, for loop)
- Removed filler words
- Kept technical precision
- Neutral, technical tone

---

## Example 6: Twitter in Browser

### System Prompt
```
[Same base rules as above]

CONTEXT: The user activated voice transcription from another application. Process name: 'chrome'. Window title: 'Home / X'. Based on this context, adjust the tone, formality, and style of the cleaned text appropriately. For example: formal for emails/documents, casual for chat apps, technical for code editors, professional for business apps.
```

### User Message (raw transcription)
```
just finished reading this amazing book about AI like it totally changed my perspective you know everyone should check it out
```

### Expected GPT Response
```
Just finished reading this amazing book about AI. It totally changed my perspective—everyone should check it out!
```

**Why this output?**
- GPT detected Twitter/X (social media)
- Very casual, conversational tone
- Kept "totally" (casual but not filler)
- Added enthusiasm with exclamation mark
- Concise for social media format

---

## Example 7: No Context Available

### System Prompt (no context)
```
You are a text cleaning assistant for a voice-to-text transcription application called HotkeyPaster. The user speaks into their microphone, and the audio is transcribed to text. Your job is to clean up the raw transcription. 

RULES:
1. ONLY fix issues - do NOT add new content or information that wasn't spoken
2. Remove filler words (um, uh, like, you know, I mean, sort of, kind of)
3. Fix grammar errors and add proper punctuation
4. Ensure proper capitalization
5. Remove or replace profanity and inappropriate language
6. Keep the original meaning and approximate length
7. If context about the target application is provided, adjust tone and formality accordingly
8. Return ONLY the cleaned text, no explanations or meta-commentary
```

### User Message (raw transcription)
```
um I need to buy milk eggs and bread from the store you know before dinner tonight
```

### Expected GPT Response
```
I need to buy milk, eggs, and bread from the store before dinner tonight.
```

**Why this output?**
- No context provided
- GPT defaults to neutral, professional tone
- Removes filler words
- Adds proper punctuation (Oxford comma)
- Clear and grammatically correct

---

## Key Observations

### What the LLM Does Automatically

1. **Detects Application Type**
   - Email clients → Formal tone
   - Chat apps → Casual tone
   - Code editors → Technical precision
   - Social media → Conversational, concise

2. **Understands Nuance**
   - "Cover Letter" → Very formal
   - "#engineering" → Technical but casual
   - "Gaming Server" → Very casual
   - "Budget Meeting" → Professional

3. **Adjusts Formality**
   - Formal: "I believe we should" vs Casual: "we should"
   - Formal: "remain competitive" vs Casual: "stay competitive"
   - Formal: "I am writing to express" vs Casual: "Hey"

4. **Preserves Context-Appropriate Language**
   - Technical terms in code contexts
   - Casual slang in gaming contexts
   - Professional language in business contexts

### No Hardcoded Rules Needed!

The LLM figures out all of this from just:
- Process name (e.g., "OUTLOOK", "slack", "Code")
- Window title (e.g., "RE: Budget Meeting", "#engineering", "Cover Letter")

**This is the power of LLM-first thinking.**
