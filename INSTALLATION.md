# HotkeyPaster Installation Guide

## Quick Install

1. **Publish the app** (if not already done):
   ```powershell
   cd HotkeyPaster
   dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
   ```

2. **Run the installer**:
   ```powershell
   .\install.bat
   ```

3. **Done!** The app will:
   - Be installed to `%LOCALAPPDATA%\HotkeyPaster`
   - Start automatically when you log in to Windows
   - Run in the system tray

## Manual Start

To start the app immediately without logging out:
```powershell
& "$env:LOCALAPPDATA\HotkeyPaster\HotkeyPaster.exe"
```

Or just double-click:
```
C:\Users\<YourUsername>\AppData\Local\HotkeyPaster\HotkeyPaster.exe
```

## Usage

- **Hotkey**: Press `Ctrl+Shift+Q` to activate voice transcription
- **Recording**: The UI appears at the bottom center of your screen
- **Stop**: Press `Space` or click the stop button
- **Transcription**: Text is automatically transcribed and pasted

## Features

✅ Works across all screen configurations (laptop, docked, multiple monitors)  
✅ Self-healing multi-screen support  
✅ DPI-aware positioning  
✅ Auto-repositions when displays change  
✅ Runs at Windows startup  

## Uninstall

Run the uninstall script:
```powershell
.\uninstall.bat
```

This will:
- Stop the running app
- Remove from Windows startup
- Delete all installation files

## Troubleshooting

### App not starting at login
Check Windows startup registry:
```powershell
Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "HotkeyPaster"
```

### Hotkey not working
- Make sure no other app is using `Ctrl+Shift+Q`
- Check the system tray - the app should be running
- Check logs at: `%LOCALAPPDATA%\HotkeyPaster\logs.txt`

### UI not visible
- The app now handles all screen configurations automatically
- If you still have issues, check the logs for positioning information
- Try pressing the hotkey on different screens

## Configuration

Settings can be accessed by:
1. Right-click the tray icon
2. Select "Settings"
3. Configure OpenAI API key or local Whisper model

## Logs

Logs are stored at:
```
%LOCALAPPDATA%\HotkeyPaster\logs.txt
```

View recent logs:
```powershell
Get-Content "$env:LOCALAPPDATA\HotkeyPaster\logs.txt" -Tail 50
```
