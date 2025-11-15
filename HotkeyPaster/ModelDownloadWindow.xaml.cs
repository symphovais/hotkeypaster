using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TalkKeys.Logging;
using TalkKeys.Services.Transcription;

namespace TalkKeys
{
    public partial class ModelDownloadWindow : Window
    {
        private readonly ILogger _logger;

        public bool ModelsDownloaded { get; private set; }

        public ModelDownloadWindow(ILogger logger)
        {
            InitializeComponent();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            PopulateDownloadModels();
        }

        private void PopulateDownloadModels()
        {
            DownloadModelComboBox.Items.Clear();

            // Add common models that users might want
            var modelsToOffer = new[]
            {
                Whisper.net.Ggml.GgmlType.TinyEn,
                Whisper.net.Ggml.GgmlType.BaseEn,
                Whisper.net.Ggml.GgmlType.SmallEn,
                Whisper.net.Ggml.GgmlType.Base,
                Whisper.net.Ggml.GgmlType.Small,
                Whisper.net.Ggml.GgmlType.Medium
            };

            foreach (var modelType in modelsToOffer)
            {
                var isDownloaded = WhisperModelManager.IsModelDownloaded(modelType);
                var description = WhisperModelManager.GetModelDescription(modelType);

                if (isDownloaded)
                {
                    description = $"✓ {description} - Already downloaded";
                }

                DownloadModelComboBox.Items.Add(new ModelDownloadItem
                {
                    ModelType = modelType,
                    Description = description,
                    IsDownloaded = isDownloaded
                });
            }

            if (DownloadModelComboBox.Items.Count > 0)
            {
                DownloadModelComboBox.SelectedIndex = 0;
            }
        }

        private async void DownloadModel_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadModelComboBox.SelectedItem is not ModelDownloadItem selectedModel)
            {
                return;
            }

            if (selectedModel.IsDownloaded)
            {
                MessageBox.Show(
                    $"This model is already downloaded.\n\nYou can select it from the 'Select Local Model' dropdown in Settings.",
                    "Model Already Downloaded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Disable download controls during download (but keep Close button enabled)
            DownloadModelButton.IsEnabled = false;
            DownloadModelComboBox.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            DownloadStatusText.Text = "Starting download...";

            try
            {
                _logger.Log($"Starting download of model: {selectedModel.ModelType}");

                var modelPath = await WhisperModelManager.EnsureModelDownloadedAsync(
                    selectedModel.ModelType,
                    progress =>
                    {
                        // Use BeginInvoke to avoid blocking the download thread
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            DownloadProgressBar.Value = progress;
                            DownloadStatusText.Text = $"Downloading... {progress}%";
                        }));
                    });

                _logger.Log($"Model downloaded successfully to: {modelPath}");

                // Update status
                DownloadStatusText.Text = "✓ Download complete!";
                DownloadStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));

                // Mark that models were downloaded
                ModelsDownloaded = true;

                // Refresh the download list to show updated status
                PopulateDownloadModels();

                MessageBox.Show(
                    $"Model downloaded successfully!\n\nThe model is now available in Settings.",
                    "Download Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.Log($"Model download failed: {ex.Message}");

                DownloadStatusText.Text = "Download failed";
                DownloadStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));

                MessageBox.Show(
                    $"Failed to download model:\n\n{ex.Message}\n\nPlease check your internet connection and try again.",
                    "Download Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable download controls
                DownloadModelButton.IsEnabled = true;
                DownloadModelComboBox.IsEnabled = true;

                // Hide progress after a delay if successful
                if (DownloadStatusText.Text.StartsWith("✓"))
                {
                    await Task.Delay(3000);
                    ProgressPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private class ModelDownloadItem
        {
            public required Whisper.net.Ggml.GgmlType ModelType { get; init; }
            public required string Description { get; init; }
            public required bool IsDownloaded { get; init; }
        }
    }
}
