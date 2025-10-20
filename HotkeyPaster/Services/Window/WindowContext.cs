namespace HotkeyPaster.Services.Windowing
{
    /// <summary>
    /// Represents the context of the active window where transcription was triggered.
    /// Contains raw information that will be sent to the LLM for interpretation.
    /// </summary>
    public class WindowContext
    {
        /// <summary>
        /// The process name (e.g., "OUTLOOK", "WINWORD", "chrome")
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// The full window title (e.g., "RE: Budget Meeting - Message (HTML) - Outlook", "Document1.docx - Word")
        /// </summary>
        public string WindowTitle { get; set; } = string.Empty;

        /// <summary>
        /// Whether context detection was successful
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(ProcessName) || !string.IsNullOrEmpty(WindowTitle);

        /// <summary>
        /// Gets a prompt that provides context information to the LLM.
        /// The LLM will interpret what application and context the user is in.
        /// </summary>
        public string GetContextPrompt()
        {
            if (!IsValid)
                return string.Empty;

            var prompt = "\n\n=== CONTEXT INFORMATION ===\n";
            prompt += "The user was working in another application when they pressed a hotkey to activate voice transcription. ";
            prompt += "They spoke into their microphone, and now you're cleaning up that transcribed text. ";
            prompt += "The transcribed text will be automatically pasted back into the application they were using.\n\n";
            
            prompt += "Application details:\n";
            
            if (!string.IsNullOrEmpty(ProcessName))
            {
                prompt += $"- Process name: '{ProcessName}'\n";
            }

            if (!string.IsNullOrEmpty(WindowTitle))
            {
                prompt += $"- Window title: '{WindowTitle}'\n";
            }

            prompt += "\nYour task: Analyze the process name and window title to understand what application the user is in and what they're doing. ";
            prompt += "Then adjust the tone, formality, and style of the cleaned text to match that context.\n\n";
            
            prompt += "Examples of how to adapt:\n";
            prompt += "- Email clients (Outlook, Gmail): Use formal, professional language\n";
            prompt += "- Chat apps (Slack, Teams, Discord): Keep it casual and conversational\n";
            prompt += "- Documents (Word, Google Docs): Use structured, clear writing\n";
            prompt += "- Code editors (VS Code, Visual Studio): Preserve technical terms precisely\n";
            prompt += "- Social media (Twitter, Facebook): Keep it brief and conversational\n";
            prompt += "- Professional contexts (LinkedIn, cover letters): Very formal and polished\n";

            return prompt;
        }
    }
}
