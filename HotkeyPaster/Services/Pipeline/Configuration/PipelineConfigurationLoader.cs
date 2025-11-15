using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TalkKeys.Logging;

namespace TalkKeys.Services.Pipeline.Configuration
{
    /// <summary>
    /// Loads pipeline configurations from JSON files
    /// </summary>
    public class PipelineConfigurationLoader
    {
        private readonly string _configDirectory;
        private readonly ILogger? _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public PipelineConfigurationLoader(string configDirectory, ILogger? logger = null)
        {
            _configDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
            _logger = logger;

            // Ensure config directory exists
            Directory.CreateDirectory(_configDirectory);
        }

        /// <summary>
        /// Load all pipeline configurations from the config directory
        /// </summary>
        public List<PipelineConfiguration> LoadAll()
        {
            var configurations = new List<PipelineConfiguration>();

            try
            {
                var jsonFiles = Directory.GetFiles(_configDirectory, "*.pipeline.json");
                _logger?.Log($"Found {jsonFiles.Length} pipeline configuration files");

                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var config = LoadFromFile(file);
                        if (config != null)
                        {
                            configurations.Add(config);
                            _logger?.Log($"Loaded pipeline configuration: {config.Name} from {Path.GetFileName(file)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"Failed to load pipeline config from {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"Failed to enumerate pipeline configuration files: {ex.Message}");
            }

            return configurations;
        }

        /// <summary>
        /// Load a single configuration from a file
        /// </summary>
        public PipelineConfiguration? LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger?.Log($"Pipeline configuration file not found: {filePath}");
                return null;
            }

            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<PipelineConfiguration>(json, JsonOptions);

            return config;
        }

        /// <summary>
        /// Load a configuration by name
        /// </summary>
        public PipelineConfiguration? LoadByName(string name)
        {
            var all = LoadAll();
            return all.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Save a configuration to a file
        /// </summary>
        public void Save(PipelineConfiguration config)
        {
            var fileName = SanitizeFileName(config.Name) + ".pipeline.json";
            var filePath = Path.Combine(_configDirectory, fileName);

            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(filePath, json);

            _logger?.Log($"Saved pipeline configuration: {config.Name} to {fileName}");
        }

        /// <summary>
        /// Create default configurations if none exist
        /// </summary>
        public void EnsureDefaultConfigurations(string openAiApiKey, string? localModelPath)
        {
            var existing = LoadAll();
            if (existing.Any())
            {
                _logger?.Log($"Found {existing.Count} existing pipeline configurations, skipping defaults");
                return;
            }

            _logger?.Log("No pipeline configurations found, creating defaults");

            // Default 1: Fast Cloud Pipeline
            var fastCloud = new PipelineConfiguration
            {
                Name = "FastCloud",
                Description = "Fast cloud-based transcription using OpenAI Whisper API with GPT cleaning (with noise removal and VAD)",
                Enabled = true,
                Stages = new List<StageConfiguration>
                {
                    new() { Type = "AudioValidation", Enabled = true },
                    new() { Type = "RNNoise", Enabled = true },
                    new() { Type = "SileroVAD", Enabled = true, Settings = new() { ["Threshold"] = 0.5, ["MinSpeechDurationMs"] = 250, ["MinSilenceDurationMs"] = 100 } },
                    new() { Type = "OpenAIWhisperTranscription", Enabled = true, Settings = new() { ["ApiKey"] = openAiApiKey } },
                    new() { Type = "GPTTextCleaning", Enabled = true, Settings = new() { ["ApiKey"] = openAiApiKey } }
                }
            };

            Save(fastCloud);

            // Default 2: Local Privacy Pipeline
            if (!string.IsNullOrEmpty(localModelPath))
            {
                var localPrivacy = new PipelineConfiguration
                {
                    Name = "LocalPrivacy",
                    Description = "100% offline transcription using local Whisper model (no API calls, with noise removal and VAD)",
                    Enabled = true,
                    Stages = new List<StageConfiguration>
                    {
                        new() { Type = "AudioValidation", Enabled = true },
                        new() { Type = "RNNoise", Enabled = true },
                        new() { Type = "SileroVAD", Enabled = true, Settings = new() { ["Threshold"] = 0.5, ["MinSpeechDurationMs"] = 250, ["MinSilenceDurationMs"] = 100 } },
                        new() { Type = "LocalWhisperTranscription", Enabled = true, Settings = new() { ["ModelPath"] = localModelPath } },
                        new() { Type = "PassThroughCleaning", Enabled = true }
                    }
                };

                Save(localPrivacy);
            }

            // Default 3: Hybrid Pipeline
            if (!string.IsNullOrEmpty(localModelPath))
            {
                var hybrid = new PipelineConfiguration
                {
                    Name = "Hybrid",
                    Description = "Local transcription for privacy + cloud cleaning for quality (with noise removal and VAD)",
                    Enabled = true,
                    Stages = new List<StageConfiguration>
                    {
                        new() { Type = "AudioValidation", Enabled = true },
                        new() { Type = "RNNoise", Enabled = true },
                        new() { Type = "SileroVAD", Enabled = true, Settings = new() { ["Threshold"] = 0.5, ["MinSpeechDurationMs"] = 250, ["MinSilenceDurationMs"] = 100 } },
                        new() { Type = "LocalWhisperTranscription", Enabled = true, Settings = new() { ["ModelPath"] = localModelPath } },
                        new() { Type = "GPTTextCleaning", Enabled = true, Settings = new() { ["ApiKey"] = openAiApiKey } }
                    }
                };

                Save(hybrid);
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }
    }
}
