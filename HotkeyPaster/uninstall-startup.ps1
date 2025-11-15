# TalkKeys Startup Uninstallation Script
# This script removes the app from Windows startup and deletes installation files

$ErrorActionPreference = "Stop"

# Define installation path
$installPath = "$env:LOCALAPPDATA\TalkKeys"
$startupRegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$appName = "TalkKeys"

Write-Host "Uninstalling TalkKeys..." -ForegroundColor Cyan

# Stop running process if exists
$process = Get-Process -Name "TalkKeys" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "Stopping running TalkKeys process..." -ForegroundColor Yellow
    Stop-Process -Name "TalkKeys" -Force
    Start-Sleep -Seconds 1
}

# Remove from Windows startup
try {
    Remove-ItemProperty -Path $startupRegPath -Name $appName -ErrorAction SilentlyContinue
    Write-Host "Removed from Windows startup" -ForegroundColor Green
} catch {
    Write-Host "Not found in startup registry" -ForegroundColor Yellow
}

# Remove installation directory
if (Test-Path $installPath) {
    Write-Host "Removing installation directory..." -ForegroundColor Yellow
    Remove-Item -Path $installPath -Recurse -Force
    Write-Host "Installation directory removed" -ForegroundColor Green
} else {
    Write-Host "Installation directory not found" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✓ Uninstallation complete!" -ForegroundColor Green
