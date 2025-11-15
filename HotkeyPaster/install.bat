@echo off
echo Installing TalkKeys...
echo.

set INSTALL_DIR=%LOCALAPPDATA%\TalkKeys

if not exist "%INSTALL_DIR%" (
    mkdir "%INSTALL_DIR%"
    echo Created installation directory
)

echo Copying files...
xcopy /E /I /Y publish\* "%INSTALL_DIR%"

echo Adding to Windows startup...
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "TalkKeys" /t REG_SZ /d "%INSTALL_DIR%\TalkKeys.exe" /f

echo.
echo ========================================
echo Installation complete!
echo ========================================
echo.
echo Installation location: %INSTALL_DIR%
echo.
echo The app will start automatically when you log in.
echo.
echo To start the app now, run:
echo   "%INSTALL_DIR%\TalkKeys.exe"
echo.
pause
