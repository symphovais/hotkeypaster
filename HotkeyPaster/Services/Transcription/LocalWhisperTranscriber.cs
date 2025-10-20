using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

namespace HotkeyPaster.Services.Transcription
{
    /// <summary>
    /// Local Whisper.net implementation of audio transcription.
    /// Runs entirely offline using local Whisper models.
    /// </summary>
    public class LocalWhisperTranscriber : ITranscriber, IDisposable
    {
        private readonly string _modelPath;
        private WhisperFactory? _whisperFactory;
        private bool _disposed;

        public LocalWhisperTranscriber(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("Model path cannot be null or empty", nameof(modelPath));

            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Whisper model not found at: {modelPath}", modelPath);

            _modelPath = modelPath;
            
            // Load model immediately to catch errors early (when settings are saved)
            // rather than on first transcription attempt
            try
            {
                _whisperFactory = WhisperFactory.FromPath(_modelPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load Whisper model from '{modelPath}'. The file may be corrupted or in the wrong format.", ex);
            }
        }

        public async Task<string> TranscribeAsync(byte[] audioData)
        {
            // Validate input
            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));

            try
            {
                // Factory is already initialized in constructor
                if (_whisperFactory == null)
                {
                    throw new InvalidOperationException("Whisper factory not initialized");
                }

                // Create processor with auto language detection
                using var processor = _whisperFactory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();

                // Write audio data to a temporary file (Whisper.net requires a stream)
                var tempFile = Path.GetTempFileName();
                try
                {
                    await File.WriteAllBytesAsync(tempFile, audioData);

                    // Process the audio file
                    var transcription = new StringBuilder();
                    using var fileStream = File.OpenRead(tempFile);
                    
                    await foreach (var result in processor.ProcessAsync(fileStream))
                    {
                        transcription.Append(result.Text);
                    }

                    var text = transcription.ToString().Trim();
                    
                    if (string.IsNullOrWhiteSpace(text))
                        throw new InvalidOperationException("Whisper returned empty transcription");

                    return text;
                }
                finally
                {
                    // Clean up temp file
                    if (File.Exists(tempFile))
                    {
                        try { File.Delete(tempFile); } catch { /* Ignore cleanup errors */ }
                    }
                }
            }
            catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
            {
                throw new InvalidOperationException($"Whisper transcription failed: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _whisperFactory?.Dispose();
                _disposed = true;
            }
        }
    }
}
