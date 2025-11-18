using System;
using System.Threading.Tasks;
using TalkKeys.Logging;
using TalkKeys.Services.Diary;
using TalkKeys.Services.Pipeline;

namespace TalkKeys.Services.RecordingMode
{
    /// <summary>
    /// Recording mode that saves transcribed text as a diary entry
    /// </summary>
    public class DiaryModeHandler : IRecordingModeHandler
    {
        private readonly IDiaryService _diaryService;
        private readonly ILogger _logger;

        public DiaryModeHandler(IDiaryService diaryService, ILogger logger)
        {
            _diaryService = diaryService ?? throw new ArgumentNullException(nameof(diaryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string GetModeTitle() => "Recording Diary Entry...";

        public string GetInstructionText() => "Press Space to save";

        public string GetRecordingIcon() => "📔";

        public async Task HandleTranscriptionAsync(PipelineResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Text))
            {
                throw new InvalidOperationException("No transcription text available for diary entry");
            }

            // Save to diary
            var entry = await _diaryService.AddEntryAsync(
                result.Text,
                result.WordCount,
                result.Language);

            _logger.Log($"Diary mode: Saved entry with {result.WordCount} words at {entry.Timestamp:yyyy-MM-dd HH:mm:ss}");
        }

        public string GetSuccessMessage(PipelineResult result)
        {
            return $"Diary saved • {result.WordCount} words";
        }
    }
}
