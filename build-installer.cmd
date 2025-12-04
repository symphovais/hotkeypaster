@echo off
setlocal

echo ========================================
echo TalkKeys Installer Build Script
echo ========================================
echo.

:: Check for Inno Setup
set ISCC_PATH=
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=C:\Program Files\Inno Setup 6\ISCC.exe"
)

:: Step 1: Clean previous build
echo [1/4] Cleaning previous build...
if exist "HotkeyPaster\bin\publish" rmdir /s /q "HotkeyPaster\bin\publish"
if exist "installer\output" rmdir /s /q "installer\output"
mkdir "installer\output" 2>nul

:: Step 2: Build and publish the application
echo [2/4] Building and publishing TalkKeys...
dotnet publish HotkeyPaster\HotkeyPaster.csproj -c Release -r win-x64 --self-contained false -o HotkeyPaster\bin\publish
if errorlevel 1 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

:: Step 3: Build installer (if Inno Setup is available)
echo [3/4] Building installer...
if defined ISCC_PATH (
    "%ISCC_PATH%" installer\TalkKeys.iss
    if errorlevel 1 (
        echo ERROR: Installer build failed!
        pause
        exit /b 1
    )
) else (
    echo WARNING: Inno Setup not found. Skipping installer creation.
    echo Download from: https://jrsoftware.org/isinfo.php
    echo.
    echo The published files are available in: HotkeyPaster\bin\publish
)

:: Step 4: Done
echo.
echo [4/4] Build complete!
echo.
if exist "installer\output\TalkKeys-Setup-*.exe" (
    echo Installer created: installer\output\
    dir /b installer\output\*.exe
) else (
    echo Published files: HotkeyPaster\bin\publish\
)
echo.

pause
