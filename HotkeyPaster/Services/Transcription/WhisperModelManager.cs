using System;
using System.IO;
using System.Threading.Tasks;
using Whisper.net.Ggml;

namespace HotkeyPaster.Services.Transcription
{
    /// <summary>
    /// Manages Whisper model downloads and storage.
    /// </summary>
    public static class WhisperModelManager
    {
        private static readonly string ModelsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HotkeyPaster",
            "Models"
        );

        /// <summary>
        /// Gets the path where models are stored.
        /// </summary>
        public static string GetModelsDirectory()
        {
            if (!Directory.Exists(ModelsDirectory))
            {
                Directory.CreateDirectory(ModelsDirectory);
            }
            return ModelsDirectory;
        }

        /// <summary>
        /// Gets the full path for a specific model.
        /// </summary>
        public static string GetModelPath(GgmlType modelType)
        {
            var fileName = $"ggml-{modelType.ToString().ToLower()}.bin";
            return Path.Combine(GetModelsDirectory(), fileName);
        }

        /// <summary>
        /// Checks if a model is already downloaded.
        /// </summary>
        public static bool IsModelDownloaded(GgmlType modelType)
        {
            var modelPath = GetModelPath(modelType);
            return File.Exists(modelPath);
        }

        /// <summary>
        /// Downloads a Whisper model if it doesn't exist.
        /// </summary>
        /// <param name="modelType">The model type to download</param>
        /// <param name="onProgress">Optional progress callback (percentage 0-100)</param>
        /// <returns>The path to the downloaded model</returns>
        public static async Task<string> EnsureModelDownloadedAsync(
            GgmlType modelType, 
            Action<int>? onProgress = null)
        {
            var modelPath = GetModelPath(modelType);

            if (File.Exists(modelPath))
            {
                onProgress?.Invoke(100);
                return modelPath;
            }

            // Download the model
            using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(modelType);
            
            // Get total size if available
            var totalBytes = modelStream.CanSeek ? modelStream.Length : -1;
            var downloadedBytes = 0L;

            using var fileWriter = File.OpenWrite(modelPath);
            var buffer = new byte[81920]; // 80KB buffer
            int bytesRead;

            while ((bytesRead = await modelStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileWriter.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                if (totalBytes > 0 && onProgress != null)
                {
                    var percentage = (int)((downloadedBytes * 100) / totalBytes);
                    onProgress(percentage);
                }
            }

            onProgress?.Invoke(100);
            return modelPath;
        }

        /// <summary>
        /// Gets a user-friendly description of a model type.
        /// </summary>
        public static string GetModelDescription(GgmlType modelType)
        {
            return modelType switch
            {
                GgmlType.Tiny => "Tiny (~75 MB) - Fastest, least accurate",
                GgmlType.TinyEn => "Tiny English (~75 MB) - Fast, English only",
                GgmlType.Base => "Base (~142 MB) - Good balance of speed and accuracy",
                GgmlType.BaseEn => "Base English (~142 MB) - Good for English",
                GgmlType.Small => "Small (~466 MB) - More accurate, slower",
                GgmlType.SmallEn => "Small English (~466 MB) - Accurate for English",
                GgmlType.Medium => "Medium (~1.5 GB) - Very accurate, slow",
                GgmlType.MediumEn => "Medium English (~1.5 GB) - Very accurate for English",
                GgmlType.LargeV1 => "Large V1 (~2.9 GB) - Most accurate, very slow",
                GgmlType.LargeV2 => "Large V2 (~2.9 GB) - Most accurate, very slow",
                GgmlType.LargeV3 => "Large V3 (~2.9 GB) - Latest, most accurate",
                _ => modelType.ToString()
            };
        }

        /// <summary>
        /// Deletes a downloaded model to free up space.
        /// </summary>
        public static void DeleteModel(GgmlType modelType)
        {
            var modelPath = GetModelPath(modelType);
            if (File.Exists(modelPath))
            {
                File.Delete(modelPath);
            }
        }

        /// <summary>
        /// Gets the size of a model file in bytes, or -1 if not downloaded.
        /// </summary>
        public static long GetModelSize(GgmlType modelType)
        {
            var modelPath = GetModelPath(modelType);
            if (File.Exists(modelPath))
            {
                return new FileInfo(modelPath).Length;
            }
            return -1;
        }
    }
}
