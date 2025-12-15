namespace TalkKeys.Services.Controller
{
    /// <summary>
    /// Result of a controller action
    /// </summary>
    public class ControllerActionResult
    {
        public bool Success { get; set; }
        public string Status { get; set; } = "idle";
        public string? Message { get; set; }

        public static ControllerActionResult Ok(string status, string? message = null) => new()
        {
            Success = true,
            Status = status,
            Message = message
        };

        public static ControllerActionResult Fail(string status, string message) => new()
        {
            Success = false,
            Status = status,
            Message = message
        };
    }
}
