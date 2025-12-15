namespace TalkKeys.Services.Controller
{
    /// <summary>
    /// Represents the current state of TalkKeys
    /// </summary>
    public class TalkKeysStatus
    {
        public bool Success { get; set; } = true;
        public string Status { get; set; } = "idle"; // "idle", "recording", "processing"
        public bool Recording { get; set; }
        public bool Processing { get; set; }
        public string Version { get; set; } = "1.0.0";
        public bool Authenticated { get; set; }
        public string? Message { get; set; }
    }
}
