@echo off
setlocal EnableDelayedExpansion

echo ========================================
echo TalkKeys MSIX Build Script
echo For Microsoft Store Submission
echo ========================================
echo.

:: Configuration
set VERSION=1.2.4.0
set APP_NAME=TalkKeys
set PROJECT_PATH=HotkeyPaster\HotkeyPaster.csproj
set PUBLISH_DIR=HotkeyPaster\bin\msix-publish
set OUTPUT_DIR=msix-output

:: Parse command line arguments
:parse_args
if "%~1"=="" goto :done_args
if /I "%~1"=="--version" (
    set VERSION=%~2
    shift
    shift
    goto :parse_args
)
shift
goto :parse_args
:done_args

echo Version: %VERSION%
echo.

:: Step 1: Clean previous builds
echo [1/5] Cleaning previous builds...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%"

:: Step 2: Check for required assets
echo [2/5] Checking store assets...
set ASSETS_DIR=HotkeyPaster\Assets
set MISSING_ASSETS=0

if not exist "%ASSETS_DIR%\StoreLogo.png" (
    echo WARNING: Missing StoreLogo.png
    set MISSING_ASSETS=1
)
if not exist "%ASSETS_DIR%\Square44x44Logo.png" (
    echo WARNING: Missing Square44x44Logo.png
    set MISSING_ASSETS=1
)
if not exist "%ASSETS_DIR%\Square150x150Logo.png" (
    echo WARNING: Missing Square150x150Logo.png
    set MISSING_ASSETS=1
)

if %MISSING_ASSETS%==1 (
    echo.
    echo Some store assets are missing. See HotkeyPaster\Assets\README-ASSETS.md
    echo for instructions on creating them.
    echo.
    choice /C YN /M "Continue anyway"
    if errorlevel 2 exit /b 1
)

:: Step 3: Build and publish
echo [3/5] Building TalkKeys (Release, x64, self-contained)...
dotnet publish %PROJECT_PATH% ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=false ^
    -p:PublishReadyToRun=true ^
    -p:Version=%VERSION% ^
    -o %PUBLISH_DIR%

if errorlevel 1 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

:: Step 4: Copy manifest and assets
echo [4/5] Copying manifest and assets...
copy /Y "HotkeyPaster\Package.appxmanifest" "%PUBLISH_DIR%\AppxManifest.xml"
if exist "%ASSETS_DIR%" xcopy /E /I /Y "%ASSETS_DIR%" "%PUBLISH_DIR%\Assets"

:: Also copy icon
copy /Y "HotkeyPaster\icon.ico" "%PUBLISH_DIR%\"

:: Step 5: Create MSIX package
echo [5/5] Creating MSIX package...

:: Find Windows SDK makeappx.exe
set SDK_FOUND=0
for /d %%i in ("%ProgramFiles(x86)%\Windows Kits\10\bin\10.0.*") do (
    if exist "%%i\x64\makeappx.exe" (
        set MAKEAPPX_PATH=%%i\x64
        set SDK_FOUND=1
    )
)

if %SDK_FOUND%==0 (
    echo.
    echo ERROR: Windows SDK not found!
    echo Please install Windows 10/11 SDK from Visual Studio Installer.
    echo.
    echo The build output is ready at: %PUBLISH_DIR%
    echo You can manually package it using Visual Studio or Partner Center.
    pause
    exit /b 1
)

echo Using SDK: %MAKEAPPX_PATH%

:: Create the MSIX package
"%MAKEAPPX_PATH%\makeappx.exe" pack /d "%PUBLISH_DIR%" /p "%OUTPUT_DIR%\%APP_NAME%_%VERSION%.msix" /o

if errorlevel 1 (
    echo.
    echo ERROR: MSIX packaging failed!
    echo.
    echo Common issues:
    echo 1. Missing or invalid Package.appxmanifest
    echo 2. Missing required assets in Assets folder
    echo 3. Publisher ID mismatch
    echo.
    echo The build output is available at: %PUBLISH_DIR%
    pause
    exit /b 1
)

echo.
echo ========================================
echo BUILD SUCCESSFUL
echo ========================================
echo.
echo MSIX Package: %OUTPUT_DIR%\%APP_NAME%_%VERSION%.msix
echo.
echo NEXT STEPS:
echo.
echo 1. TEST LOCALLY:
echo    Double-click the MSIX to install (requires signing for sideload)
echo    Or use: Add-AppPackage -Path "%OUTPUT_DIR%\%APP_NAME%_%VERSION%.msix"
echo.
echo 2. RUN WACK (Windows App Cert Kit):
echo    "%ProgramFiles(x86)%\Windows Kits\10\App Certification Kit\appcert.exe"
echo    test -appxpackagepath "%OUTPUT_DIR%\%APP_NAME%_%VERSION%.msix" ^
echo         -reportoutputpath "%OUTPUT_DIR%\wack-report.xml"
echo.
echo 3. SUBMIT TO STORE:
echo    a. Go to https://partner.microsoft.com/dashboard
echo    b. Create new app submission
echo    c. Upload %OUTPUT_DIR%\%APP_NAME%_%VERSION%.msix
echo    d. Fill in store listing, screenshots, privacy policy
echo    e. Submit for certification
echo.
echo NOTE: For Store submission, Microsoft will sign the package.
echo       Local testing requires a developer certificate or developer mode.
echo.
pause
