using System;
using System.Diagnostics;
using System.IO;
using NAudio.Wave;
using TalkKeys.Logging;

namespace TalkKeys.Services.Audio
{
    public sealed class AudioRecordingService : IAudioRecordingService, IDisposable
    {
        public event EventHandler? RecordingStarted;
        public event EventHandler? RecordingStopped;

        private readonly ILogger? _logger;
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private string? _currentFilePath;
        private DateTime _recordingStartTime;

        public AudioRecordingService(ILogger? logger = null)
        {
            _logger = logger;
        }

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
            _recordingStartTime = DateTime.Now;
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1) // 16kHz, 16-bit, mono (required for Whisper.net)
            };

            _writer = new WaveFileWriter(_currentFilePath, _waveIn.WaveFormat);
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            var logMsg = $"Started audio recording to {Path.GetFileName(filePath)} at {_recordingStartTime:HH:mm:ss.fff}";
            _logger?.Log(logMsg);
            Debug.WriteLine($"[AudioRecording] {logMsg}");
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }

        public void StopRecording()
        {
            if (!IsRecording) return;

            var duration = DateTime.Now - _recordingStartTime;
            var stackTrace = new StackTrace(true);
            var callingMethod = stackTrace.GetFrame(1)?.GetMethod();
            var callerInfo = callingMethod != null ? $"{callingMethod.DeclaringType?.Name}.{callingMethod.Name}" : "Unknown";

            var logMsg = $"Stopping audio recording after {duration.TotalSeconds:F2}s (called from: {callerInfo})";
            _logger?.Log(logMsg);
            Debug.WriteLine($"[AudioRecording] {logMsg}");

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
