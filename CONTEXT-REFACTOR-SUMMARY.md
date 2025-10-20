# Context Detection Refactor Summary

## What Changed

We refactored the window context detection feature from a **hardcoded parsing approach** to an **LLM-first approach**.

## Before: Hardcoded Parsing (Initial Implementation)

### Problems
- **200+ lines** of complex parsing logic
- Hardcoded `ApplicationType` enum with 15+ app types
- Multiple parsing methods: `ParseOutlookContext()`, `ParseWordContext()`, `ParseBrowserContext()`, etc.
- Fragile string matching that breaks when apps update
- Limited to predefined applications
- Required code changes to support new apps
- Made assumptions about what context means

### Example Code (Old)
```csharp
context.ApplicationType = processName switch
{
    "OUTLOOK" => ApplicationType.MicrosoftOutlook,
    "WINWORD" => ApplicationType.MicrosoftWord,
    "EXCEL" => ApplicationType.MicrosoftExcel,
    // ... 15 more cases
};

context.ContextDescription = context.ApplicationType switch
{
    ApplicationType.MicrosoftOutlook => ParseOutlookContext(windowTitle),
    ApplicationType.MicrosoftWord => ParseWordContext(windowTitle),
    // ... more parsing methods
};

private string ParseOutlookContext(string windowTitle)
{
    if (titleLower.Contains("message (html)"))
    {
        if (titleLower.StartsWith("re:"))
            return "email reply";
        return "composing email";
    }
    // ... 20 more lines of parsing
}
```

## After: LLM-First Approach (Current Implementation)

### Solution
- **~80 lines** of simple code
- No enums, no parsing methods, no assumptions
- Capture raw information: process name + window title
- Send directly to LLM with clear instructions
- Let GPT interpret the context

### Example Code (New)
```csharp
// Capture raw information
var context = new WindowContext
{
    ProcessName = process.ProcessName,  // e.g., "OUTLOOK"
    WindowTitle = GetWindowTitle(handle) // e.g., "RE: Budget - Message (HTML)"
};

// Generate prompt for LLM
public string GetContextPrompt()
{
    return $"CONTEXT: The user activated voice transcription from another application. " +
           $"Process name: '{ProcessName}'. " +
           $"Window title: '{WindowTitle}'. " +
           $"Based on this context, adjust the tone, formality, and style appropriately.";
}
```

## Key Insight

**We were trying to do the LLM's job.** 

The LLM is already excellent at:
- Understanding application context from names
- Interpreting window titles
- Detecting formality levels
- Adjusting tone appropriately

Why write 200 lines of fragile parsing code when GPT can do it better in zero lines?

## Benefits of LLM-First Approach

### 1. Simplicity
- 60% less code
- No complex parsing logic
- Easy to understand and maintain

### 2. Flexibility
- Works with ANY application (not just hardcoded ones)
- Handles new apps automatically
- Robust to UI changes

### 3. Intelligence
- Understands nuance ("Cover Letter.docx" vs "Grocery List.docx")
- Adapts to context automatically
- No maintenance needed

### 4. Future-Proof
- No code changes when apps update
- No need to add new applications
- Self-maintaining

## Files Changed

### Simplified
- `WindowContext.cs` - Removed enum, removed parsing methods, simplified to raw data
- `ActiveWindowContextService.cs` - Removed all parsing logic (~150 lines deleted)
- `OpenAIGPTTextCleaner.cs` - Enhanced system prompt with better instructions

### Updated
- `MainWindow.xaml.cs` - Simplified logging
- `WINDOW-CONTEXT-FEATURE.md` - Updated documentation to reflect new approach

## Example: What the LLM Sees

**Input to LLM:**
```
System: You are a text cleaning assistant for HotkeyPaster...
[cleaning rules]

CONTEXT: The user activated voice transcription from another application. 
Process name: 'OUTLOOK'. 
Window title: 'RE: Q4 Budget Meeting - Message (HTML) - Outlook'. 
Based on this context, adjust the tone, formality, and style appropriately.

User: um so basically I think we should like increase the budget by 10 percent you know
```

**LLM Output:**
```
I believe we should increase the budget by 10%.
```

The LLM automatically:
- Recognized it's Outlook (email client)
- Saw it's a reply about a budget meeting
- Applied formal, professional tone
- Removed filler words
- Made it concise and business-appropriate

**No hardcoded rules needed.**

## Lesson Learned

When working with LLMs:
1. **Don't overthink it** - Let the LLM do what it's good at
2. **Provide raw information** - Don't pre-process or interpret
3. **Give clear instructions** - Tell the LLM what to do with the context
4. **Trust the model** - It's better at understanding context than our code

This is a perfect example of **LLM-first thinking**: Instead of writing complex logic to interpret data, we provide the raw data to the LLM and let it figure out what to do.
