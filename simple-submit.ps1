# Simple Store submission - package only (no listing updates)
# Listing updates can be done manually in Partner Center
param([switch]$DeletePending)

Import-Module StoreBroker
$global:SBDisableTelemetry = $true

$creds = Get-Content "$env:USERPROFILE\.storebroker\credentials.json" | ConvertFrom-Json
$secureSecret = ConvertTo-SecureString $creds.ClientSecret -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential($creds.ClientId, $secureSecret)

Set-StoreBrokerAuthentication -TenantId $creds.TenantId -Credential $credential

$AppId = $creds.StoreAppId
$Version = "1.2.4.0"
$MsixPath = "msix-output\TalkKeys_$Version.msix"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TalkKeys Simple Submit" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Get app info
$app = Get-Application -AppId $AppId
Write-Host "App: $($app.primaryName)" -ForegroundColor Green

# Check for pending submission
if ($app.pendingApplicationSubmission.id) {
    $pendingId = $app.pendingApplicationSubmission.id
    Write-Host "Pending submission: $pendingId" -ForegroundColor Yellow

    if ($DeletePending) {
        Write-Host "Deleting pending submission..." -ForegroundColor Red
        Remove-ApplicationSubmission -AppId $AppId -SubmissionId $pendingId -Force
        Write-Host "Deleted. Run again without -DeletePending to create new submission." -ForegroundColor Green
        exit 0
    } else {
        Write-Host ""
        Write-Host "A pending submission already exists." -ForegroundColor Yellow
        Write-Host "Options:" -ForegroundColor Cyan
        Write-Host "  1. Complete it at Partner Center:" -ForegroundColor White
        Write-Host "     https://partner.microsoft.com/dashboard/products/$AppId/submissions/$pendingId"
        Write-Host ""
        Write-Host "  2. Delete it and create new: .\simple-submit.ps1 -DeletePending" -ForegroundColor White
        exit 1
    }
}

# Create new submission
Write-Host "Creating new submission..." -ForegroundColor Yellow
$submission = New-ApplicationSubmission -AppId $AppId -Force
Write-Host "Created: $($submission.id)" -ForegroundColor Green

Write-Host ""
Write-Host "Submission created! Complete it at:" -ForegroundColor Green
Write-Host "https://partner.microsoft.com/dashboard/products/$AppId/submissions/$($submission.id)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Upload: $MsixPath" -ForegroundColor Yellow
