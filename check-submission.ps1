# Check if submission was created
Import-Module StoreBroker

$creds = Get-Content "$env:USERPROFILE\.storebroker\credentials.json" | ConvertFrom-Json
$secureSecret = ConvertTo-SecureString $creds.ClientSecret -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential($creds.ClientId, $secureSecret)

Set-StoreBrokerAuthentication -TenantId $creds.TenantId -Credential $credential

$AppId = $creds.StoreAppId

Write-Host "Getting app details..." -ForegroundColor Cyan
$app = Get-Application -AppId $AppId
Write-Host "App: $($app.primaryName)" -ForegroundColor Green
Write-Host "Last Published Submission: $($app.lastPublishedApplicationSubmission.id)" -ForegroundColor Yellow
Write-Host "Pending Submission: $($app.pendingApplicationSubmission.id)" -ForegroundColor Yellow

if ($app.pendingApplicationSubmission.id) {
    Write-Host ""
    Write-Host "Getting pending submission details..." -ForegroundColor Cyan
    $submission = Get-ApplicationSubmission -AppId $AppId -SubmissionId $app.pendingApplicationSubmission.id
    Write-Host "Status: $($submission.status)" -ForegroundColor Green
}
