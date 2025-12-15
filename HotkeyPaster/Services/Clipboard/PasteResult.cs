namespace TalkKeys.Services.Clipboard
{
    /// <summary>
    /// Result of a paste operation.
    /// </summary>
    public class PasteResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }

        public static PasteResult Ok() => new() { Success = true };
        public static PasteResult Fail(string error) => new() { Success = false, ErrorMessage = error };
    }
}
