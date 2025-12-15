namespace TalkKeys.Services.Controller
{
    /// <summary>
    /// Information about an available microphone
    /// </summary>
    public class MicrophoneInfo
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Current { get; set; }
    }
}
