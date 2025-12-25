# List apps accessible via StoreBroker
Import-Module StoreBroker

$creds = Get-Content "$env:USERPROFILE\.storebroker\credentials.json" | ConvertFrom-Json
$secureSecret = ConvertTo-SecureString $creds.ClientSecret -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential($creds.ClientId, $secureSecret)

Set-StoreBrokerAuthentication -TenantId $creds.TenantId -Credential $credential

Write-Host "Listing accessible apps..." -ForegroundColor Cyan
Get-Applications | Format-Table -Property id, primaryName
