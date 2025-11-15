using System.Threading.Tasks;

namespace TalkKeys.Services.Transcription
{
    /// <summary>
    /// Interface for transcribing audio data to raw text.
    /// </summary>
    public interface ITranscriber
    {
        /// <summary>
        /// Transcribes audio data to raw text.
        /// </summary>
        /// <param name="audioData">Audio data in WAV, MP3, or other supported format</param>
        /// <returns>Raw transcribed text</returns>
        Task<string> TranscribeAsync(byte[] audioData);
    }
}
