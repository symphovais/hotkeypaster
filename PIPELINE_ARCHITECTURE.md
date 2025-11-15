# Pipeline Architecture - HotkeyPaster

## Overview

HotkeyPaster now uses a **pipeline-based architecture** for audio processing, replacing the previous `IAudioTranscriptionService` with a flexible, composable pipeline system.

## Key Benefits

✅ **Composable Stages**: Build pipelines from discrete, reusable stages
✅ **Multiple Configurations**: Run different pipeline configs side-by-side for A/B testing
✅ **Detailed Metrics**: Each stage collects timing and custom metrics
✅ **Easy Extensibility**: Add new stages (noise removal, VAD trimming, etc.) without changing core code
✅ **JSON Configuration**: Pipelines stored as JSON files for easy editing
✅ **Testable**: `PipelineTestRunner` compares multiple configurations on the same audio

---

## Architecture

```
┌──────────────────────────────────────────────────────┐
│                   Pipeline Flow                       │
├──────────────────────────────────────────────────────┤
│                                                       │
│  Audio Input (byte[])                                │
│         ↓                                             │
│  ┌─────────────────────────────────┐                │
│  │ Stage 1: Audio Validation        │                │
│  │ - Validates size, format         │                │
│  │ - Calculates duration            │                │
│  │ - Metrics: Size, Duration        │                │
│  └─────────────────────────────────┘                │
│         ↓                                             │
│  ┌─────────────────────────────────┐                │
│  │ Stage 2: Transcription           │                │
│  │ - OpenAI Whisper / Local        │                │
│  │ - Metrics: Model, WordCount      │                │
│  └─────────────────────────────────┘                │
│         ↓                                             │
│  ┌─────────────────────────────────┐                │
│  │ Stage 3: Text Cleaning           │                │
│  │ - GPT / PassThrough             │                │
│  │ - Metrics: Before/After words    │                │
│  └─────────────────────────────────┘                │
│         ↓                                             │
│  Final Text + Metrics                                │
│                                                       │
└──────────────────────────────────────────────────────┘
```

---

## Core Components

### 1. Pipeline Interfaces

**`IPipelineStage`**
```csharp
public interface IPipelineStage
{
    string Name { get; }
    string StageType { get; }
    Task<StageResult> ExecuteAsync(PipelineContext context);
}
```

**`PipelineContext`**
- Shared data dictionary between stages
- Progress reporting
- Metrics collection
- Common keys: `"AudioData"`, `"RawTranscription"`, `"CleanedText"`, `"WindowContext"`

### 2. Metrics System

**`PipelineMetrics`**
- Total pipeline duration
- Per-stage metrics
- Global metrics (word count, cost estimation, etc.)

**`StageMetrics`**
- Stage name and duration
- Custom metrics dictionary
- Examples: `"WordCount"`, `"ModelUsed"`, `"ApiCalls"`, `"BytesProcessed"`

### 3. Pipeline Executor

**`Pipeline`** class
- Executes stages sequentially
- Handles errors and cancellation
- Collects aggregated metrics
- Reports progress at each stage

### 4. Configuration System

**`PipelineConfiguration`** (JSON)
```json
{
  "Name": "FastCloud",
  "Description": "Fast cloud-based transcription",
  "Enabled": true,
  "Stages": [
    {
      "Type": "AudioValidation",
      "Enabled": true
    },
    {
      "Type": "OpenAIWhisperTranscription",
      "Enabled": true,
      "Settings": {
        "ApiKey": "sk-..."
      }
    },
    {
      "Type": "GPTTextCleaning",
      "Enabled": true,
      "Settings": {
        "ApiKey": "sk-..."
      }
    }
  ]
}
```

**Location**: `%APPDATA%\HotkeyPaster\Pipelines\*.pipeline.json`

---

## Built-in Stages

### AudioValidationStage
- **Type**: `AudioValidation`
- **Purpose**: Validates audio data and calculates duration
- **Metrics**: `AudioSizeBytes`, `AudioSizeMB`, `AudioDurationSeconds`

### OpenAIWhisperTranscriptionStage
- **Type**: `OpenAIWhisperTranscription`
- **Purpose**: Cloud-based transcription using OpenAI Whisper API
- **Metrics**: `Provider`, `Model`, `WordCount`, `CharacterCount`
- **Settings**: `ApiKey`

### LocalWhisperTranscriptionStage
- **Type**: `LocalWhisperTranscription`
- **Purpose**: Offline transcription using local Whisper.net model
- **Metrics**: `Provider`, `Model`, `WordCount`, `CharacterCount`
- **Settings**: `ModelPath`

### GPTTextCleaningStage
- **Type**: `GPTTextCleaning`
- **Purpose**: AI-powered text cleanup using GPT-4.1-nano
- **Metrics**: `Model`, `BeforeWordCount`, `AfterWordCount`, `WordCountChange`
- **Settings**: `ApiKey`

### PassThroughCleaningStage
- **Type**: `PassThroughCleaning`
- **Purpose**: No-op stage that passes text through unchanged
- **Metrics**: `WordCount`, `CharacterCount`, `Modified: false`

---

## Default Pipeline Configurations

### 1. FastCloud
```
AudioValidation → OpenAIWhisperTranscription → GPTTextCleaning
```
- **Best for**: Speed and quality
- **Requirements**: OpenAI API key
- **Privacy**: Data sent to OpenAI

### 2. LocalPrivacy
```
AudioValidation → LocalWhisperTranscription → PassThroughCleaning
```
- **Best for**: Privacy and offline use
- **Requirements**: Local Whisper model
- **Privacy**: 100% local, no API calls

### 3. Hybrid
```
AudioValidation → LocalWhisperTranscription → GPTTextCleaning
```
- **Best for**: Private transcription + quality cleanup
- **Requirements**: Local model + OpenAI API key
- **Privacy**: Transcription local, cleaning uses API

---

## Usage

### For End Users

The app automatically uses the default pipeline. Switch pipelines in Settings (when UI is added).

### For Developers

**Execute default pipeline**:
```csharp
var result = await _pipelineService.ExecuteAsync(
    audioData,
    windowContext,
    progress);

Console.WriteLine($"Success: {result.IsSuccess}");
Console.WriteLine($"Text: {result.Text}");
Console.WriteLine($"Duration: {result.Metrics.TotalDurationMs}ms");
```

**Execute specific pipeline**:
```csharp
var result = await _pipelineService.ExecuteAsync(
    "LocalPrivacy",  // pipeline name
    audioData,
    windowContext,
    progress);
```

**A/B testing multiple pipelines**:
```csharp
var testRunner = new PipelineTestRunner(_logger);

var pipeline1 = _registry.GetPipeline("FastCloud");
var pipeline2 = _registry.GetPipeline("LocalPrivacy");
var pipeline3 = _registry.GetPipeline("Hybrid");

var comparison = await testRunner.RunComparisonAsync(
    audioData,
    pipeline1,
    pipeline2,
    pipeline3);

Console.WriteLine(comparison.GetSummary());
// Shows detailed comparison with timings for each pipeline
```

---

## Adding New Stages

### Example: Noise Removal Stage

```csharp
public class NoiseRemovalStage : IPipelineStage
{
    public string Name => "Noise Removal";
    public string StageType => "NoiseRemoval";

    public async Task<StageResult> ExecuteAsync(PipelineContext context)
    {
        var startTime = DateTime.UtcNow;
        var metrics = new StageMetrics
        {
            StageName = Name,
            StartTime = startTime
        };

        // Get audio from context
        var audioData = context.GetData<byte[]>("AudioData");

        // Apply noise removal algorithm
        var cleanedAudio = await RemoveNoiseAsync(audioData);

        // Update context with cleaned audio
        context.SetData("AudioData", cleanedAudio);

        // Add metrics
        metrics.EndTime = DateTime.UtcNow;
        metrics.AddMetric("OriginalSize", audioData.Length);
        metrics.AddMetric("CleanedSize", cleanedAudio.Length);
        metrics.AddMetric("NoiseReductionDb", 15.5);

        return StageResult.Success(metrics);
    }
}

// Factory
public class NoiseRemovalStageFactory : IPipelineStageFactory
{
    public string StageType => "NoiseRemoval";

    public IPipelineStage CreateStage(
        StageConfiguration config,
        PipelineBuildContext buildContext)
    {
        // Extract settings
        var thresholdDb = config.Settings.TryGetValue("ThresholdDb", out var val)
            ? Convert.ToDouble(val)
            : -40.0;

        return new NoiseRemovalStage(thresholdDb);
    }
}
```

### Register the stage

In `App.xaml.cs`, add:
```csharp
factory.RegisterStageFactory(new NoiseRemovalStageFactory());
```

### Use in pipeline config

Create `%APPDATA%\HotkeyPaster\Pipelines\EnhancedCloud.pipeline.json`:
```json
{
  "Name": "EnhancedCloud",
  "Description": "Cloud transcription with noise removal",
  "Enabled": true,
  "Stages": [
    {
      "Type": "AudioValidation",
      "Enabled": true
    },
    {
      "Type": "NoiseRemoval",
      "Enabled": true,
      "Settings": {
        "ThresholdDb": -35
      }
    },
    {
      "Type": "OpenAIWhisperTranscription",
      "Enabled": true
    },
    {
      "Type": "GPTTextCleaning",
      "Enabled": true
    }
  ]
}
```

---

## Metrics Output Example

```
Pipeline: FastCloud
Total Duration: 2847.35ms
Stages: 3

Pipeline stage metrics:
  Audio Validation: 2.14ms (AudioSizeMB=1.2, AudioDurationSeconds=12.5)
  OpenAI Whisper Transcription: 1521.77ms (Provider=OpenAI, Model=whisper-1, WordCount=142)
  GPT Text Cleaning: 1323.44ms (Model=gpt-4.1-nano, BeforeWordCount=142, AfterWordCount=138, WordCountChange=-4)

Global Metrics:
  TotalWordCount: 138
```

---

## Testing Multiple Configurations

```bash
# Create test audio file
$audio = [System.IO.File]::ReadAllBytes("test.wav")

# Compare all pipelines
$comparison = await testRunner.RunComparisonAsync($audio,
    $pipelines["FastCloud"],
    $pipelines["LocalPrivacy"],
    $pipelines["Hybrid"])

# Output:
# Pipeline Comparison Summary
# ===========================
# Total Comparison Duration: 8543.21ms
# Pipelines Tested: 3
# Successful: 3
# Failed: 0
#
# Pipeline: FastCloud
#   Status: SUCCESS
#   Duration: 2847.35ms
#   Word Count: 138
#   ...
#
# Pipeline: LocalPrivacy
#   Status: SUCCESS
#   Duration: 4521.88ms
#   Word Count: 142
#   ...
#
# Pipeline: Hybrid
#   Status: SUCCESS
#   Duration: 5174.98ms
#   Word Count: 138
#   ...
#
# Performance:
#   Fastest: FastCloud (2847.35ms)
#   Slowest: Hybrid (5174.98ms)
#   Speedup: 1.82x
```

---

## Migration Notes

### What Changed

**Old System**:
```csharp
IAudioTranscriptionService _transcriptionService;
var result = await _transcriptionService.TranscribeStreamingAsync(audioData, onProgress, windowContext);
```

**New System**:
```csharp
IPipelineService _pipelineService;
var result = await _pipelineService.ExecuteAsync(audioData, windowContext, progress);
```

### Backwards Compatibility

No backwards compatibility was maintained. The entire transcription system was refactored to use pipelines.

### Files Modified

- `App.xaml.cs`: Now creates `IPipelineService` instead of `IAudioTranscriptionService`
- `MainWindow.xaml.cs`: Updated to use `IPipelineService`

### New Files Created

**Core Infrastructure**:
- `Services/Pipeline/IPipelineStage.cs`
- `Services/Pipeline/PipelineContext.cs`
- `Services/Pipeline/StageResult.cs`
- `Services/Pipeline/PipelineMetrics.cs`
- `Services/Pipeline/StageMetrics.cs`
- `Services/Pipeline/Pipeline.cs`
- `Services/Pipeline/PipelineResult.cs`

**Configuration**:
- `Services/Pipeline/Configuration/PipelineConfiguration.cs`
- `Services/Pipeline/Configuration/PipelineConfigurationLoader.cs`

**Factory & Registry**:
- `Services/Pipeline/IPipelineStageFactory.cs`
- `Services/Pipeline/PipelineBuildContext.cs`
- `Services/Pipeline/PipelineFactory.cs`
- `Services/Pipeline/PipelineRegistry.cs`
- `Services/Pipeline/PipelineTestRunner.cs`

**Service**:
- `Services/Pipeline/IPipelineService.cs`
- `Services/Pipeline/PipelineService.cs`

**Stages**:
- `Services/Pipeline/Stages/AudioValidationStage.cs`
- `Services/Pipeline/Stages/OpenAIWhisperTranscriptionStage.cs`
- `Services/Pipeline/Stages/LocalWhisperTranscriptionStage.cs`
- `Services/Pipeline/Stages/GPTTextCleaningStage.cs`
- `Services/Pipeline/Stages/PassThroughCleaningStage.cs`

---

## Next Steps

1. **Build the project**: `dotnet build`
2. **Test default pipeline**: Run the app and press Ctrl+Shift+Q
3. **Check logs**: `%LOCALAPPDATA%\HotkeyPaster\logs.txt` for detailed metrics
4. **View configurations**: `%APPDATA%\HotkeyPaster\Pipelines\*.pipeline.json`
5. **Add custom stages**: Create new `IPipelineStage` implementations (e.g., NoiseRemovalStage, VADTrimStage)
6. **A/B test**: Use `PipelineTestRunner` to compare pipeline performance
7. **Create UI**: Add pipeline selection to SettingsWindow (future enhancement)

---

## Future Enhancements

- [ ] Add noise removal stage using NAudio
- [ ] Add VAD (Voice Activity Detection) trimming stage
- [ ] Add profanity filter stage
- [ ] Add language detection stage
- [ ] Create settings UI for pipeline selection
- [ ] Add pipeline metrics visualization window
- [ ] Add export metrics to CSV for analysis
- [ ] Add custom pipeline editor UI
- [ ] Add pipeline templates for common scenarios

---

## Support

For questions or issues with the pipeline system:
1. Check logs at `%LOCALAPPDATA%\HotkeyPaster\logs.txt`
2. Review pipeline configs at `%APPDATA%\HotkeyPaster\Pipelines\`
3. Ensure all required settings (API keys, model paths) are configured
4. Verify stages are enabled in pipeline configuration

## License

Same as HotkeyPaster project
