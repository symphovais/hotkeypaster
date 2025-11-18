using System;
using System.Threading.Tasks;
using TalkKeys.Logging;
using TalkKeys.Services.Clipboard;
using TalkKeys.Services.Pipeline;

namespace TalkKeys.Services.RecordingMode
{
    /// <summary>
    /// Recording mode that pastes transcribed text to the clipboard and simulates Ctrl+V
    /// </summary>
    public class ClipboardModeHandler : IRecordingModeHandler
    {
        private readonly IClipboardPasteService _clipboardService;
        private readonly ILogger _logger;

        public ClipboardModeHandler(IClipboardPasteService clipboardService, ILogger logger)
        {
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string GetModeTitle() => "Recording...";

        public string GetInstructionText() => "Press Space to finish";

        public string GetRecordingIcon() => "🎙️";

        public Task HandleTranscriptionAsync(PipelineResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Text))
            {
                throw new InvalidOperationException("No transcription text available to paste");
            }

            // Paste to clipboard - must run on UI thread (STA mode required)
            _clipboardService.PasteText(result.Text);
            _logger.Log($"Clipboard mode: Pasted {result.WordCount} words");

            return Task.CompletedTask;
        }

        public string GetSuccessMessage(PipelineResult result)
        {
            return $"{result.WordCount} words";
        }
    }
}
