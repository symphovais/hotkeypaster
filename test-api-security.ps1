# Simple API Security Review Test

param([string]$File = "")

Write-Host "API Security Review Test" -ForegroundColor Red

if ($File) {
    Write-Host "Reviewing file: $File"
    
    if (Test-Path $File) {
        $content = Get-Content $File -Raw
        
        $prompt = @"
Perform a security review of this API-related code. Look for:
1. Authentication vulnerabilities
2. Input validation issues  
3. Information disclosure
4. Injection vulnerabilities
5. Authorization problems

File: $File

$content
"@
        
        claude --print $prompt
    } else {
        Write-Error "File not found: $File"
    }
} else {
    Write-Host "Usage: .\test-api-security.ps1 -File <path>"
}

Write-Host "Review completed!"