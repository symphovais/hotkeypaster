@echo off
echo Uninstalling HotkeyPaster...
echo.

set INSTALL_DIR=%LOCALAPPDATA%\HotkeyPaster

echo Stopping HotkeyPaster if running...
taskkill /F /IM HotkeyPaster.exe 2>nul

echo Removing from Windows startup...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "HotkeyPaster" /f 2>nul

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
