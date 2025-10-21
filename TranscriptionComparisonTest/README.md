# Transcription Optimization Comparison Test

This test project compares the performance of **OLD** vs **OPTIMIZED** transcription approaches for HotkeyPaster.

## What's Being Tested

### OLD Approach (Baseline)
- **Step 1**: Whisper API transcription
- **Step 2**: GPT-4.1-nano text cleaning
- **Total API Calls**: 2
- **Expected Time**: 4-7 seconds

### OPTIMIZED Approach (New)
- **Single Step**: GPT-4o-mini combined transcription + cleaning
- **Total API Calls**: 1
- **Expected Time**: 2-3 seconds
- **Expected Improvement**: 2-4 seconds faster (40-60% improvement)

## Prerequisites

1. OpenAI API key with access to:
   - Whisper API
   - GPT-4.1-nano
   - GPT-4o-mini (with audio support)

2. A test audio file (WAV format, 16kHz, 16-bit, mono)
   - You can record one using the HotkeyPaster app (saved to temp directory)
   - Or provide your own WAV file

## How to Run

### Option 1: Using Environment Variable

```bash
# Set API key
set OPENAI_API_KEY=sk-your-api-key-here

# Run with auto-detected audio file (uses most recent recording)
dotnet run --project TranscriptionComparisonTest

# Or specify a specific audio file
dotnet run --project TranscriptionComparisonTest -- "path\to\audio.wav"
```

### Option 2: Using Command Line Arguments

```bash
# Run with API key and auto-detected audio
dotnet run --project TranscriptionComparisonTest -- sk-your-api-key-here

# Run with API key and specific audio file
dotnet run --project TranscriptionComparisonTest -- sk-your-api-key-here "path\to\audio.wav"
```

### Recording Test Audio

1. Run the HotkeyPaster app
2. Press `Ctrl+Shift+Q` to start recording
3. Speak something (e.g., "This is a test recording to compare transcription performance")
4. Press `Space` to stop
5. The recording is saved to your temp directory as `HotkeyPaster_YYYYMMDD_HHMMSS.wav`

## Output

The test will display:

1. **Real-time progress** as each approach runs
2. **Detailed performance report** including:
   - Total time for each approach
   - Breakdown of transcription vs cleaning time (OLD only)
   - Word count and processing speed
   - Time saved and speedup factor
   - API calls comparison
3. **Full transcription results** for comparison
4. **Saved report file** in temp directory

### Sample Output

```
════════════════════════════════════════════════════════════════
  HotkeyPaster Transcription Optimization Test
════════════════════════════════════════════════════════════════

📂 Loading audio file: HotkeyPaster_20251021_143022.wav
   Size: 128,044 bytes (125.04 KB)

🚀 Starting comparison test...

🔬 Starting Transcription Optimization Comparison Test

Audio size: 128,044 bytes (125.04 KB)

Testing OLD approach (Whisper + GPT-4.1-nano)...
  → Calling Whisper API...
  ✓ Whisper completed in 2.34s
  → Calling GPT-4.1-nano for cleaning...
  ✓ GPT cleaning completed in 1.87s
  ✓ Total time: 4.21s | Words: 28

Testing OPTIMIZED approach (GPT-4o-mini combined)...
  → Calling GPT-4o-mini (combined)...
  ✓ Total time: 2.15s | Words: 28

╔════════════════════════════════════════════════════════════════╗
║     TRANSCRIPTION OPTIMIZATION COMPARISON REPORT               ║
╚════════════════════════════════════════════════════════════════╝

📊 OLD APPROACH (Whisper API + GPT-4.1-nano)
────────────────────────────────────────────────────────────────
  API Calls:           2
  Total Time:          4.210s
  - Transcription:     2.340s
  - Text Cleaning:     1.870s
  Word Count:          28
  Processing Speed:    6.7 words/sec

🚀 OPTIMIZED APPROACH (GPT-4o-mini Combined)
────────────────────────────────────────────────────────────────
  API Calls:           1 (single combined call)
  Total Time:          2.150s
  Word Count:          28
  Processing Speed:    13.0 words/sec

📈 PERFORMANCE IMPROVEMENT
════════════════════════════════════════════════════════════════
  Time Saved:          2.060s (48.9% faster)
  Speedup Factor:      1.96x
  API Calls Reduced:   1 fewer call(s)

  ✅ OPTIMIZED approach is 1.96x FASTER!

💾 Full report saved to: C:\Users\...\Temp\transcription_comparison_20251021_143045.txt
```

## Interpreting Results

### Success Criteria
- ✅ Optimized approach should be **1.5-2.5x faster**
- ✅ Both approaches should produce similar quality transcriptions
- ✅ Optimized approach should use **1 API call** vs 2

### What to Look For
1. **Time Savings**: 2-4 seconds improvement expected
2. **Quality**: Compare full transcriptions - should be very similar
3. **Consistency**: Run multiple tests to verify consistent improvement

### Troubleshooting

**Error: "Audio file not found"**
- Record audio using HotkeyPaster first, or provide a valid WAV path

**Error: "API key not provided"**
- Set `OPENAI_API_KEY` environment variable or pass as argument

**Error: "GPT-4o-mini transcription error"**
- Ensure your API key has access to GPT-4o-mini with audio support
- This feature might be in beta - check OpenAI docs

**Slower than expected**
- Network latency can affect results
- Run multiple tests and average the results
- Check API status at status.openai.com

## Files Created

- `OptimizationComparisonService.cs` - Comparison test logic
- `GPT4oMiniCombinedTranscriber.cs` - New optimized transcriber
- `OptimizedTranscriptionService.cs` - Service wrapper
- Test results saved to: `%TEMP%\transcription_comparison_*.txt`

## Cost Estimate

Per transcription (60 seconds audio):
- **OLD**: ~$0.006 (Whisper) + ~$0.0001 (GPT) = ~$0.0061
- **OPTIMIZED**: ~$0.00015 (GPT-4o-mini audio) + tokens = ~$0.0003-0.0005

**Cost savings**: ~90-95% cheaper while being 2x faster!

## Next Steps

After verifying the improvement:
1. The optimized approach is **already enabled** for cloud mode in HotkeyPaster
2. Just rebuild and run the main app to use the new faster transcription
3. Monitor performance in real-world usage
4. Collect user feedback on transcription quality

## Technical Details

### Optimizations Implemented

1. **Single API Call** - Biggest win (2-4s saved)
   - Eliminated separate Whisper API call
   - GPT-4o-mini handles both transcription and cleaning

2. **Optimized Streaming Parser** - Medium win (~50-100ms)
   - Pre-filters SSE responses before JSON parsing
   - Reduces unnecessary JsonDocument.Parse() calls

3. **Eliminated Duplicate Word Counting** - Small win (~10-20ms)
   - Word count calculated once during progress updates

4. **Added Audio Duration Calculation**
   - Better UX, no performance cost
   - Calculated from WAV format metadata

### Performance Breakdown

```
OLD Approach:
┌─────────────────┐
│ Whisper API     │  2-4s (network + processing)
├─────────────────┤
│ GPT-4.1-nano    │  1-2s (network + processing)
├─────────────────┤
│ Word counting   │  <20ms
└─────────────────┘
Total: 3-6+ seconds

OPTIMIZED Approach:
┌─────────────────┐
│ GPT-4o-mini     │  2-3s (network + processing)
├─────────────────┤
│ Word counting   │  <10ms (optimized)
└─────────────────┘
Total: 2-3 seconds
```

## License

Part of the HotkeyPaster project.
