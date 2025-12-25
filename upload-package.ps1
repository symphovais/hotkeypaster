# Upload package to existing pending submission
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
Write-Host "Upload Package to Pending Submission" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Get app and pending submission
$app = Get-Application -AppId $AppId
Write-Host "App: $($app.primaryName)" -ForegroundColor Green

if (-not $app.pendingApplicationSubmission.id) {
    Write-Host "No pending submission found!" -ForegroundColor Red
    exit 1
}

$submissionId = $app.pendingApplicationSubmission.id
Write-Host "Pending Submission: $submissionId" -ForegroundColor Yellow

# Get submission details
Write-Host "Getting submission details..." -ForegroundColor Cyan
$submission = Get-ApplicationSubmission -AppId $AppId -SubmissionId $submissionId
Write-Host "Status: $($submission.status)" -ForegroundColor Green
Write-Host "Upload URL exists: $($null -ne $submission.fileUploadUrl)" -ForegroundColor Gray

if ($submission.fileUploadUrl) {
    Write-Host ""
    Write-Host "Uploading package..." -ForegroundColor Yellow

    # Create zip (StoreBroker requires zip)
    $zipPath = "msix-output\TalkKeys_$Version.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path $MsixPath -DestinationPath $zipPath -Force
    Write-Host "Created: $zipPath" -ForegroundColor Gray

    # Upload
    Set-SubmissionPackage -PackagePath $zipPath -UploadUrl $submission.fileUploadUrl
    Write-Host "Package uploaded!" -ForegroundColor Green

    Write-Host ""
    Write-Host "Now complete at Partner Center:" -ForegroundColor Cyan
    Write-Host "https://partner.microsoft.com/dashboard/products/$AppId/submissions/$submissionId"
} else {
    Write-Host "No upload URL available." -ForegroundColor Red
    Write-Host "You may need to upload manually at Partner Center." -ForegroundColor Yellow
}
