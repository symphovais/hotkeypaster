@echo off
setlocal EnableDelayedExpansion

echo ========================================
echo TalkKeys Installer Build Script
echo ========================================
echo.
echo Uses Velopack for packaging and auto-updates.
echo.

:: Configuration
set VERSION=1.0.3
set APP_NAME=TalkKeys
set PUBLISH_DIR=HotkeyPaster\bin\publish
set RELEASES_DIR=releases
set DO_PUBLISH=0

:: Parse command line arguments
:parse_args
if "%~1"=="" goto :done_args
if /I "%~1"=="--version" (
    set VERSION=%~2
    shift
    shift
    goto :parse_args
)
if /I "%~1"=="-v" (
    set VERSION=%~2
    shift
    shift
    goto :parse_args
)
if /I "%~1"=="--publish" (
    set DO_PUBLISH=1
    shift
    goto :parse_args
)
if /I "%~1"=="-p" (
    set DO_PUBLISH=1
    shift
    goto :parse_args
)
shift
goto :parse_args
:done_args

echo Version: %VERSION%
echo.

:: Check for vpk tool
where vpk >nul 2>&1
if errorlevel 1 (
    echo Velopack CLI not found. Installing...
    dotnet tool install -g vpk
    if errorlevel 1 (
        echo ERROR: Failed to install Velopack CLI tool!
        echo.
        echo Please install manually: dotnet tool install -g vpk
        pause
        exit /b 1
    )
    echo Velopack CLI installed successfully.
    echo.
)

:: Step 1: Clean previous build
echo [1/5] Cleaning previous build...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%RELEASES_DIR%" rmdir /s /q "%RELEASES_DIR%"
mkdir "%RELEASES_DIR%"

:: Step 2: Build and publish the application
echo [2/5] Building TalkKeys (Release, win-x64)...
dotnet publish HotkeyPaster\HotkeyPaster.csproj -c Release -r win-x64 --self-contained false -o %PUBLISH_DIR% -p:PublishSingleFile=false
if errorlevel 1 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)

:: Step 3: Verify main executable exists
echo [3/5] Verifying build output...
if not exist "%PUBLISH_DIR%\TalkKeys.exe" (
    echo.
    echo ERROR: TalkKeys.exe not found in publish directory!
    pause
    exit /b 1
)

:: Step 4: Copy icon to publish directory
echo [4/5] Preparing assets...
copy HotkeyPaster\icon.ico %PUBLISH_DIR%\icon.ico >nul 2>&1

:: Step 5: Create Velopack package
echo [5/5] Creating installer package...
vpk pack ^
    --packId %APP_NAME% ^
    --packVersion %VERSION% ^
    --packDir %PUBLISH_DIR% ^
    --mainExe TalkKeys.exe ^
    --icon HotkeyPaster\icon.ico ^
    --outputDir %RELEASES_DIR%

if errorlevel 1 (
    echo.
    echo ERROR: Velopack packaging failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo BUILD SUCCESSFUL
echo ========================================
echo.
echo Output files in %RELEASES_DIR%\:
echo.
dir /b %RELEASES_DIR%
echo.

:: Check if we should publish to GitHub
if "%DO_PUBLISH%"=="0" goto :skip_publish

echo ----------------------------------------
echo PUBLISHING TO GITHUB RELEASES
echo ----------------------------------------
echo.

:: Check for gh CLI
where gh >nul 2>&1
if !errorlevel! neq 0 (
    echo ERROR: GitHub CLI not found!
    echo.
    echo Please install from: https://cli.github.com/
    pause
    exit /b 1
)

:: Check if authenticated
gh auth status >nul 2>&1
if !errorlevel! neq 0 (
    echo ERROR: Not authenticated with GitHub CLI!
    echo.
    echo Please run: gh auth login
    pause
    exit /b 1
)

:: Create release and upload all files
echo Creating GitHub release v%VERSION%...
gh release create v%VERSION% %RELEASES_DIR%\* --title "TalkKeys v%VERSION%" --generate-notes
if !errorlevel! neq 0 (
    echo.
    echo ERROR: Failed to create GitHub release!
    echo.
    echo This could be because:
    echo   - Tag v%VERSION% already exists
    echo   - Network issues
    echo   - Permission issues
    echo.
    pause
    exit /b 1
)

echo.
echo ========================================
echo RELEASE PUBLISHED SUCCESSFULLY
echo ========================================
echo.
echo Release URL: https://github.com/symphovais/hotkeypaster/releases/tag/v%VERSION%
echo Update source: https://github.com/symphovais/hotkeypaster/releases
echo Users will auto-update on next launch.
echo.
goto :done

:skip_publish
echo ----------------------------------------
echo INSTALLATION:
echo   Run: %RELEASES_DIR%\TalkKeys-Setup.exe
echo.
echo PUBLISHING TO GITHUB:
echo   Re-run with --publish flag:
echo   build-velopack.cmd --version %VERSION% --publish
echo.
echo   Or manually:
echo   1. Create a new release with tag: v%VERSION%
echo   2. Upload ALL files from %RELEASES_DIR%\
echo   3. Users will auto-update on next launch
echo ----------------------------------------
echo.

:done
pause
