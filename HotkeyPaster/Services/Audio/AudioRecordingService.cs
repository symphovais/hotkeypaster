using System;
using System.IO;
using NAudio.Wave;

namespace HotkeyPaster.Services.Audio
{
    public sealed class AudioRecordingService : IAudioRecordingService, IDisposable
    {
        public event EventHandler? RecordingStarted;
        public event EventHandler? RecordingStopped;

        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private string? _currentFilePath;

        public bool IsRecording => _waveIn != null;
        
        public string DeviceName
        {
            get
            {
                try
                {
                    // Get the default recording device (device number 0)
                    int deviceNumber = 0;
                    if (deviceNumber < WaveInEvent.DeviceCount)
                    {
                        var capabilities = WaveInEvent.GetCapabilities(deviceNumber);
                        return capabilities.ProductName;
                    }
                    return "Default Microphone";
                }
                catch
                {
                    return "Unknown Device";
                }
            }
        }

        public void StartRecording(string filePath)
        {
            if (IsRecording) return;

            _currentFilePath = filePath;
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1) // 16kHz, 16-bit, mono (required for Whisper.net)
            };

            _writer = new WaveFileWriter(_currentFilePath, _waveIn.WaveFormat);
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }

        public void StopRecording()
        {
            if (!IsRecording) return;

            _waveIn?.StopRecording();
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _writer?.Dispose();
            _writer = null;
            _waveIn?.Dispose();
            _waveIn = null;
            RecordingStopped?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            StopRecording();
        }
    }
}
