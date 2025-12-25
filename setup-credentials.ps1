# Setup Store submission credentials
$credDir = Join-Path $env:USERPROFILE ".storebroker"
$credFile = Join-Path $credDir "credentials.json"

# Create directory
if (-not (Test-Path $credDir)) {
    New-Item -ItemType Directory -Path $credDir -Force | Out-Null
    Write-Host "Created: $credDir" -ForegroundColor Green
}

# Your Azure AD credentials
$creds = @{
    TenantId = "0bd713c4-83f8-4e33-b1aa-e4fc949d1b86"
    ClientId = "be17c0dc-096c-45a2-a2d8-97bfec53a9e7"
    ClientSecret = "YOUR_CLIENT_SECRET_HERE"  # <-- Replace with actual secret!
    StoreAppId = "9P2D7DZQS61J"
}

$creds | ConvertTo-Json | Set-Content $credFile
Write-Host "Saved credentials to: $credFile" -ForegroundColor Green
Write-Host ""
Write-Host "Store App ID: 9P2D7DZQS61J" -ForegroundColor Cyan
