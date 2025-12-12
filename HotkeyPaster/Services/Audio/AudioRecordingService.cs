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
        public event EventHandler<AudioLevelEventArgs>? AudioLevelChanged;
        public event EventHandler<RecordingFailedEventArgs>? RecordingFailed;

        private readonly ILogger? _logger;
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private string? _currentFilePath;
        private DateTime _recordingStartTime;
        private bool _hasNonZeroSample;
        private int _totalBytesRecorded;

        private int _currentDeviceIndex = 0;

        public AudioRecordingService(ILogger? logger = null)
        {
            _logger = logger;
        }

        public bool IsRecording => _waveIn != null;
        
        public int CurrentDeviceIndex => _currentDeviceIndex;

        public string DeviceName
        {
            get
            {
                try
                {
                    if (_currentDeviceIndex < WaveInEvent.DeviceCount)
                    {
                        var capabilities = WaveInEvent.GetCapabilities(_currentDeviceIndex);
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

        public string[] GetAvailableDevices()
        {
            var devices = new System.Collections.Generic.List<string>();
            
            // Add System Default option
            devices.Add("System Default");

            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                try
                {
                    var capabilities = WaveInEvent.GetCapabilities(i);
                    devices.Add(capabilities.ProductName);
                }
                catch
                {
                    devices.Add($"Device {i}");
                }
            }
            return devices.ToArray();
        }

        public void SetDevice(int deviceIndex)
        {
            // Index 0 is "System Default", which maps to device 0 for now
            // Real devices start at index 1 (Device 0), index 2 (Device 1), etc.
            
            if (deviceIndex == 0)
            {
                _currentDeviceIndex = 0; // Default to first device
                _logger?.Log("Audio device set to System Default (using device 0)");
            }
            else if (deviceIndex > 0 && deviceIndex <= WaveInEvent.DeviceCount)
            {
                _currentDeviceIndex = deviceIndex - 1;
                _logger?.Log($"Audio device set to index {_currentDeviceIndex}: {DeviceName}");
            }
            else
            {
                _logger?.Log($"Invalid audio device index: {deviceIndex}. Keeping current: {_currentDeviceIndex}");
            }
        }

        public void StartRecording(string filePath)
        {
            if (IsRecording) return;

            _currentFilePath = filePath;
            _recordingStartTime = DateTime.Now;
            _hasNonZeroSample = false;
            _totalBytesRecorded = 0;

            try
            {
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = _currentDeviceIndex,
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
            catch (Exception ex)
            {
                // Clean up any partial initialization
                _writer?.Dispose();
                _writer = null;
                _waveIn?.Dispose();
                _waveIn = null;

                var errorMsg = $"Failed to start recording: {ex.Message}";
                _logger?.Log(errorMsg);
                Debug.WriteLine($"[AudioRecording] {errorMsg}");

                // Fire failure event instead of crashing
                RecordingFailed?.Invoke(this, new RecordingFailedEventArgs(ex.Message, GetUserFriendlyErrorMessage(ex)));
            }
        }

        private static string GetUserFriendlyErrorMessage(Exception ex)
        {
            // NAudio throws MmException with "UnspecifiedError" for various audio device issues
            if (ex.Message.Contains("waveInOpen") || ex.Message.Contains("UnspecifiedError"))
            {
                return "Could not access the microphone. Please check that:\n" +
                       "• Your microphone is connected\n" +
                       "• No other app is using the microphone exclusively\n" +
                       "• Microphone permissions are enabled in Windows Settings";
            }
            if (ex.Message.Contains("NoDriver"))
            {
                return "No audio recording device found. Please connect a microphone.";
            }
            if (ex.Message.Contains("BadDeviceId"))
            {
                return "The selected microphone is no longer available. Please check Settings.";
            }
            return $"Microphone error: {ex.Message}";
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

                // Calculate audio level (RMS)
                float sum = 0;
                int sampleCount = 0;
                for (int index = 0; index + 1 < e.BytesRecorded; index += 2)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, index);
                    sum += sample * sample;
                    sampleCount++;

                    if (!_hasNonZeroSample)
                    {
                        const int silenceThreshold = 500;
                        if (sample > silenceThreshold || sample < -silenceThreshold)
                        {
                            _hasNonZeroSample = true;
                        }
                    }
                }

                // Fire audio level event
                if (sampleCount > 0)
                {
                    float rms = (float)Math.Sqrt(sum / sampleCount);
                    // Normalize to 0.0-1.0 range (16-bit audio max sample value is 32767)
                    float normalizedLevel = Math.Min(1.0f, rms / 32767f);
                    // Apply scaling for better visual response
                    normalizedLevel = (float)Math.Pow(normalizedLevel * 10, 0.5);
                    normalizedLevel = Math.Min(1.0f, normalizedLevel);

                    AudioLevelChanged?.Invoke(this, new AudioLevelEventArgs { Level = normalizedLevel });
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

    public class RecordingFailedEventArgs : EventArgs
    {
        public string TechnicalError { get; }
        public string UserMessage { get; }

        public RecordingFailedEventArgs(string technicalError, string userMessage)
        {
            TechnicalError = technicalError;
            UserMessage = userMessage;
        }
    }
}
