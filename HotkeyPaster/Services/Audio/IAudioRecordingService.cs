using System;

namespace TalkKeys.Services.Audio
{
    public interface IAudioRecordingService
    {
        void StartRecording(string filePath);
        void StopRecording();
        bool IsRecording { get; }
        string DeviceName { get; }
        event EventHandler? RecordingStarted;
        event EventHandler? RecordingStopped;
        event EventHandler? NoAudioDetected;
    }
}
