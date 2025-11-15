# TalkKeys Startup Installation Script
# This script copies the app to a permanent location and adds it to Windows startup

$ErrorActionPreference = "Stop"

# Define installation path
$installPath = "$env:LOCALAPPDATA\TalkKeys"
$exePath = Join-Path $installPath "TalkKeys.exe"

Write-Host "Installing TalkKeys..." -ForegroundColor Cyan

# Create installation directory
if (-not (Test-Path $installPath)) {
    New-Item -ItemType Directory -Path $installPath -Force | Out-Null
    Write-Host "Created installation directory: $installPath" -ForegroundColor Green
}

# Copy published files to installation directory
$publishPath = Join-Path $PSScriptRoot "publish"
if (-not (Test-Path $publishPath)) {
    Write-Host "ERROR: Publish folder not found. Please run 'dotnet publish' first." -ForegroundColor Red
    exit 1
}

Write-Host "Copying files to $installPath..." -ForegroundColor Yellow
Copy-Item -Path "$publishPath\*" -Destination $installPath -Recurse -Force

# Add to Windows startup (current user)
$startupRegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$appName = "TalkKeys"

Write-Host "Adding to Windows startup..." -ForegroundColor Yellow
Set-ItemProperty -Path $startupRegPath -Name $appName -Value $exePath

Write-Host ""
Write-Host "✓ Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Installation location: $installPath" -ForegroundColor Cyan
Write-Host "Executable: $exePath" -ForegroundColor Cyan
Write-Host ""
Write-Host "The app will now start automatically when you log in to Windows." -ForegroundColor Green
Write-Host ""
Write-Host "To start the app now, run:" -ForegroundColor Yellow
Write-Host "  Start-Process '$exePath'" -ForegroundColor White
Write-Host ""
Write-Host "To uninstall, run the uninstall-startup.ps1 script" -ForegroundColor Yellow
