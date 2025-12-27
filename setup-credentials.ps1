<#
.SYNOPSIS
    Sets up credentials for Microsoft Store submission automation.

.DESCRIPTION
    Creates the credentials file needed for submit-store-v2.ps1.
    You must fill in the actual values before running this script.

.NOTES
    Required credentials (get from Azure Portal / Partner Center):
    - TenantId: Azure AD Directory ID
    - ClientId: Azure AD Application ID
    - ClientSecret: Azure AD Application secret
    - StoreAppId: Microsoft Store product ID (e.g., 9PXXXXXXXX)
    - SellerId: Partner Center Seller ID (numeric)
#>

$credDir = Join-Path $env:USERPROFILE ".storebroker"
$credFile = Join-Path $credDir "credentials.json"

# Create directory
if (-not (Test-Path $credDir)) {
    New-Item -ItemType Directory -Path $credDir -Force | Out-Null
    Write-Host "Created: $credDir" -ForegroundColor Green
}

# Check if credentials already exist
if (Test-Path $credFile) {
    Write-Host "Existing credentials found at: $credFile" -ForegroundColor Yellow
    $existing = Get-Content $credFile | ConvertFrom-Json
    Write-Host "  TenantId:    $($existing.TenantId)" -ForegroundColor Gray
    Write-Host "  ClientId:    $($existing.ClientId)" -ForegroundColor Gray
    Write-Host "  StoreAppId:  $($existing.StoreAppId)" -ForegroundColor Gray
    Write-Host "  SellerId:    $($existing.SellerId)" -ForegroundColor Gray
    Write-Host ""
    $confirm = Read-Host "Overwrite? (y/N)"
    if ($confirm -ne "y") {
        Write-Host "Cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Azure AD / Partner Center credentials
# IMPORTANT: Fill in these values before running!
$creds = @{
    # Azure AD Directory (tenant) ID
    # Find in: Azure Portal > Azure Active Directory > Overview > Tenant ID
    TenantId = "YOUR_TENANT_ID"

    # Azure AD Application (client) ID
    # Find in: Azure Portal > App registrations > Your App > Application (client) ID
    ClientId = "YOUR_CLIENT_ID"

    # Azure AD Application secret (create in Azure Portal > App registrations > Certificates & secrets)
    # IMPORTANT: This is a secret! Never commit the real value to git.
    ClientSecret = "YOUR_CLIENT_SECRET"

    # Microsoft Store Product ID (Store ID like 9PXXXXXXXX, NOT the GUID)
    # Find in: Partner Center > Your App > Product identity > Store ID
    StoreAppId = "YOUR_STORE_APP_ID"

    # Partner Center Seller ID (find in Partner Center > Account settings > Legal info)
    SellerId = "YOUR_SELLER_ID"
}

# Validate required fields - check for placeholder values
$hasErrors = $false

if ($creds.TenantId -eq "YOUR_TENANT_ID" -or [string]::IsNullOrWhiteSpace($creds.TenantId)) {
    Write-Host "ERROR: TenantId is not set!" -ForegroundColor Red
    $hasErrors = $true
}

if ($creds.ClientId -eq "YOUR_CLIENT_ID" -or [string]::IsNullOrWhiteSpace($creds.ClientId)) {
    Write-Host "ERROR: ClientId is not set!" -ForegroundColor Red
    $hasErrors = $true
}

if ($creds.ClientSecret -eq "YOUR_CLIENT_SECRET" -or $creds.ClientSecret -eq "YOUR_CLIENT_SECRET_HERE" -or [string]::IsNullOrWhiteSpace($creds.ClientSecret)) {
    Write-Host "ERROR: ClientSecret is not set!" -ForegroundColor Red
    $hasErrors = $true
}

if ($creds.StoreAppId -eq "YOUR_STORE_APP_ID" -or [string]::IsNullOrWhiteSpace($creds.StoreAppId)) {
    Write-Host "ERROR: StoreAppId is not set!" -ForegroundColor Red
    $hasErrors = $true
}

if ($creds.SellerId -eq "YOUR_SELLER_ID" -or [string]::IsNullOrWhiteSpace($creds.SellerId)) {
    Write-Host "ERROR: SellerId is not set!" -ForegroundColor Red
    $hasErrors = $true
}

if ($hasErrors) {
    Write-Host ""
    Write-Host "Please edit this script and replace the placeholder values with your actual credentials." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Where to find these values:" -ForegroundColor Cyan
    Write-Host "  TenantId:     Azure Portal > Azure Active Directory > Overview" -ForegroundColor Gray
    Write-Host "  ClientId:     Azure Portal > App registrations > Your App > Overview" -ForegroundColor Gray
    Write-Host "  ClientSecret: Azure Portal > App registrations > Your App > Certificates & secrets" -ForegroundColor Gray
    Write-Host "  StoreAppId:   Partner Center > Your App > Product identity > Store ID" -ForegroundColor Gray
    Write-Host "  SellerId:     Partner Center > Account settings > Legal info" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

$creds | ConvertTo-Json | Set-Content $credFile
Write-Host ""
Write-Host "Saved credentials to: $credFile" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  TenantId:    $($creds.TenantId)" -ForegroundColor White
Write-Host "  ClientId:    $($creds.ClientId)" -ForegroundColor White
Write-Host "  StoreAppId:  $($creds.StoreAppId)" -ForegroundColor White
Write-Host "  SellerId:    $($creds.SellerId)" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Run: .\submit-store-v2.ps1 -DryRun" -ForegroundColor Gray
Write-Host "  2. If preview looks good: .\submit-store-v2.ps1" -ForegroundColor Gray
Write-Host ""
