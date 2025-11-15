using System.Collections.Generic;

namespace TalkKeys.Services.Pipeline.Configuration
{
    /// <summary>
    /// Configuration for a pipeline, loaded from JSON
    /// </summary>
    public class PipelineConfiguration
    {
        /// <summary>
        /// Unique name for this pipeline configuration
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether this pipeline is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Ordered list of stages to execute
        /// </summary>
        public List<StageConfiguration> Stages { get; set; } = new();

        /// <summary>
        /// Global settings for the entire pipeline
        /// </summary>
        public Dictionary<string, object> GlobalSettings { get; set; } = new();
    }

    /// <summary>
    /// Configuration for a single pipeline stage
    /// </summary>
    public class StageConfiguration
    {
        /// <summary>
        /// Type identifier for the stage (e.g., "AudioValidation", "OpenAIWhisperTranscription")
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name for this stage instance
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Whether this stage is enabled (allows skipping stages)
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Stage-specific settings
        /// Examples:
        /// - For TranscriptionStage: { "Provider": "OpenAI", "Model": "whisper-1" }
        /// - For NoiseRemovalStage: { "NoiseThresholdDb": -40 }
        /// - For VADStage: { "MinSilenceDurationMs": 300 }
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();
    }
}
