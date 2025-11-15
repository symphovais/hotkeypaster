@echo off
echo Uninstalling TalkKeys...
echo.

set INSTALL_DIR=%LOCALAPPDATA%\TalkKeys

echo Stopping TalkKeys if running...
taskkill /F /IM TalkKeys.exe 2>nul

echo Removing from Windows startup...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "TalkKeys" /f 2>nul

echo Deleting installation files...
if exist "%INSTALL_DIR%" (
    rmdir /S /Q "%INSTALL_DIR%"
    echo Installation directory removed
) else (
    echo Installation directory not found
)

echo.
echo ========================================
echo Uninstallation complete!
echo ========================================
echo.
pause
