using System;

namespace HotkeyPaster.Services.Audio
{
    public interface IAudioRecordingService
    {
        void StartRecording(string filePath);
        void StopRecording();
        bool IsRecording { get; }
        event EventHandler? RecordingStarted;
        event EventHandler? RecordingStopped;
    }
}
