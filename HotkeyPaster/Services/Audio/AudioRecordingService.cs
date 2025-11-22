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
        public event EventHandler? NoAudioDetected;

        private readonly ILogger? _logger;
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private string? _currentFilePath;
        private DateTime _recordingStartTime;
        private bool _hasNonZeroSample;
        private int _totalBytesRecorded;

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
            _hasNonZeroSample = false;
            _totalBytesRecorded = 0;
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
            if (e.BytesRecorded > 0)
            {
                _totalBytesRecorded += e.BytesRecorded;

                if (!_hasNonZeroSample)
                {
                    for (int index = 0; index + 1 < e.BytesRecorded; index += 2)
                    {
                        short sample = BitConverter.ToInt16(e.Buffer, index);
                        const int silenceThreshold = 500;
                        if (sample > silenceThreshold || sample < -silenceThreshold)
                        {
                            _hasNonZeroSample = true;
                            break;
                        }
                    }
                }
            }

            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _writer?.Dispose();
            _writer = null;
            _waveIn?.Dispose();
            _waveIn = null;

            bool noAudio = !_hasNonZeroSample || _totalBytesRecorded <= 0;

            if (noAudio || e.Exception != null)
            {
                var reason = e.Exception != null
                    ? $"Recording stopped due to device error: {e.Exception.Message}"
                    : "Recording completed but no non-zero audio samples were detected";
                _logger?.Log(reason);
                Debug.WriteLine($"[AudioRecording] {reason}");
                NoAudioDetected?.Invoke(this, EventArgs.Empty);
            }

            RecordingStopped?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            StopRecording();
        }
    }
}
