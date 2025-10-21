# How to Run the Transcription Optimization Comparison

## Quick Start (3 Steps)

### Step 1: Record Test Audio
1. Run HotkeyPaster: `dotnet run --project HotkeyPaster`
2. Press `Ctrl+Shift+Q` to start recording
3. Speak clearly for 10-20 seconds (e.g., "This is a test of the new optimized transcription system. I'm comparing the old approach which uses Whisper and GPT separately, versus the new optimized approach which uses GPT-4o-mini for both transcription and cleaning in a single API call.")
4. Press `Space` to stop recording
5. Audio is saved to: `%TEMP%\HotkeyPaster_YYYYMMDD_HHMMSS.wav`

### Step 2: Set Your API Key
```cmd
set OPENAI_API_KEY=sk-your-openai-api-key-here
```

### Step 3: Run Comparison Test
```cmd
cd C:\projects\hotkeypaster
dotnet run --project TranscriptionComparisonTest
```

That's it! The test will:
- Auto-detect your most recent recording
- Run BOTH approaches (old and optimized)
- Show detailed performance comparison
- Save full report to temp directory

## Expected Output

```
════════════════════════════════════════════════════════════════
  HotkeyPaster Transcription Optimization Test
════════════════════════════════════════════════════════════════

ℹ️  No audio file specified, using most recent recording:
   C:\Users\YourName\AppData\Local\Temp\HotkeyPaster_20251021_143022.wav

📂 Loading audio file: HotkeyPaster_20251021_143022.wav
   Size: 256,044 bytes (250.04 KB)

🚀 Starting comparison test...

🔬 Starting Transcription Optimization Comparison Test

Audio size: 256,044 bytes (250.04 KB)

Testing OLD approach (Whisper + GPT-4.1-nano)...
  → Calling Whisper API...
  ✓ Whisper completed in 3.42s
  → Calling GPT-4.1-nano for cleaning...
  ✓ GPT cleaning completed in 2.18s
  ✓ Total time: 5.60s | Words: 52

Testing OPTIMIZED approach (GPT-4o-mini combined)...
  → Calling GPT-4o-mini (combined)...
  ✓ Total time: 2.87s | Words: 52

╔════════════════════════════════════════════════════════════════════════════╗
║          TRANSCRIPTION OPTIMIZATION COMPARISON REPORT                     ║
╚════════════════════════════════════════════════════════════════════════════╝

📊 OLD APPROACH (Whisper API + GPT-4.1-nano)
────────────────────────────────────────────────────────────────────────────
  API Calls:           2
  Total Time:          5.600s
  - Transcription:     3.420s
  - Text Cleaning:     2.180s
  Word Count:          52
  Processing Speed:    9.3 words/sec
  Result Preview:      This is a test of the new optimized transcription system. I'm comparing...

🚀 OPTIMIZED APPROACH (GPT-4o-mini Combined)
────────────────────────────────────────────────────────────────────────────
  API Calls:           1 (single combined call)
  Total Time:          2.870s
  Word Count:          52
  Processing Speed:    18.1 words/sec
  Result Preview:      This is a test of the new optimized transcription system. I'm comparing...

📈 PERFORMANCE IMPROVEMENT
════════════════════════════════════════════════════════════════════════════
  Time Saved:          2.730s (48.8% faster)
  Speedup Factor:      1.95x
  API Calls Reduced:   1 fewer call(s)

  ✅ OPTIMIZED approach is 1.95x FASTER!

💰 ESTIMATED COST COMPARISON
────────────────────────────────────────────────────────────────────────────
  OLD:        Whisper ($0.006/min) + GPT-4.1-nano (~$0.0001/request)
  OPTIMIZED:  GPT-4o-mini audio (~$0.00015/min) + text generation
  Note: Actual costs depend on audio length and text output tokens

💡 RECOMMENDATIONS
────────────────────────────────────────────────────────────────────────────
  ✅ Use OPTIMIZED approach for:
     • Faster response times
     • Reduced API complexity
     • Better user experience

════════════════════════════════════════════════════════════════════════════

📝 FULL TRANSCRIPTION COMPARISON

OLD Approach Result:
────────────────────────────────────────────────────────────────
This is a test of the new optimized transcription system. I'm comparing
the old approach which uses Whisper and GPT separately, versus the new
optimized approach which uses GPT-4o-mini for both transcription and
cleaning in a single API call.

OPTIMIZED Approach Result:
────────────────────────────────────────────────────────────────
This is a test of the new optimized transcription system. I'm comparing
the old approach which uses Whisper and GPT separately, versus the new
optimized approach which uses GPT-4o-mini for both transcription and
cleaning in a single API call.

💾 Full report saved to: C:\Users\...\Temp\transcription_comparison_20251021_143052.txt

✅ Test completed successfully!

Press any key to exit...
```

## Alternative: Specify Audio File

If you have a specific WAV file you want to test:

```cmd
dotnet run --project TranscriptionComparisonTest -- sk-your-api-key "C:\path\to\audio.wav"
```

## Troubleshooting

### "Audio file not found"
**Solution**: Record audio using HotkeyPaster first, or check the temp directory:
```cmd
dir %TEMP%\HotkeyPaster_*.wav
```

### "API key not provided"
**Solution**: Set the environment variable:
```cmd
set OPENAI_API_KEY=sk-your-key
```

### "GPT-4o-mini transcription error (401)"
**Solution**: Your API key is invalid or expired. Get a new one from OpenAI.

### "GPT-4o-mini transcription error (404)"
**Solution**: Your account may not have access to GPT-4o-mini with audio support yet. This feature might be in beta. Check OpenAI's documentation or use the old approach in the meantime.

### Slow performance on both approaches
**Solution**:
- Check your internet connection
- Try again during non-peak hours
- Check OpenAI API status: https://status.openai.com

## What to Look For

### Success Indicators ✅
- Optimized is **1.5-2.5x faster** than old
- Both produce similar/identical transcriptions
- Time saved is **2-4 seconds** or more
- No errors during execution

### Potential Issues ⚠️
- If optimized is slower, check network latency
- If transcriptions differ significantly, compare quality
- If errors occur, check API key and account status

## Running Multiple Tests

For more accurate results, run the test 3-5 times and average:

```cmd
# Run 1
dotnet run --project TranscriptionComparisonTest

# Run 2 (press Ctrl+C and run again)
dotnet run --project TranscriptionComparisonTest

# Run 3
dotnet run --project TranscriptionComparisonTest
```

Average the results to account for network variability.

## Next Steps After Testing

If the test shows good results:

1. ✅ The optimization is **already active** in HotkeyPaster cloud mode
2. ✅ Just use the app normally - it's using the fast approach
3. ✅ Monitor performance in real-world usage
4. ✅ Collect feedback on transcription quality

If you want to revert to the old approach temporarily:
- Change `TranscriptionMode` to `Local` in settings, or
- Modify `App.xaml.cs` to use the old cloud service

## Files Generated

The test creates:
- `%TEMP%\transcription_comparison_YYYYMMDD_HHMMSS.txt` - Full report

## Support

For issues or questions:
- Check `TranscriptionComparisonTest/README.md` for detailed docs
- Check `OPTIMIZATION_SUMMARY.md` for technical details
- Review code in `OptimizationComparisonService.cs`

---

**Estimated time to run**: 10-15 seconds (depends on audio length and network)
**Recommended test audio length**: 10-30 seconds for quick but meaningful results
