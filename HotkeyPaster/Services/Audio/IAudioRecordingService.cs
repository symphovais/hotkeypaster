using System;

namespace TalkKeys.Services.Audio
{
    public interface IAudioRecordingService
    {
        void StartRecording(string filePath);
        void StopRecording();
        bool IsRecording { get; }
        string DeviceName { get; }
        int CurrentDeviceIndex { get; }
        string[] GetAvailableDevices();
        void SetDevice(int deviceIndex);
        event EventHandler? RecordingStarted;
        event EventHandler? RecordingStopped;
        event EventHandler? NoAudioDetected;
        event EventHandler<AudioLevelEventArgs>? AudioLevelChanged;
    }

    public class AudioLevelEventArgs : EventArgs
    {
        public float Level { get; set; } // 0.0 to 1.0
    }
}
