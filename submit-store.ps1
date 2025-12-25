<#
.SYNOPSIS
    Automates Microsoft Store submission for TalkKeys.

.DESCRIPTION
    This script uses StoreBroker to submit a new version of TalkKeys to the Microsoft Store.
    It handles authentication, package upload, listing updates, and submission creation.

.PARAMETER Version
    The version to submit (e.g., "1.2.4.0"). If not specified, reads from build-msix.cmd.

.PARAMETER SkipBuild
    Skip the MSIX build step (use existing package).

.PARAMETER UpdateListingOnly
    Only update the Store listing without uploading a new package.

.PARAMETER DryRun
    Show what would be submitted without actually submitting.

.EXAMPLE
    .\submit-store.ps1
    .\submit-store.ps1 -Version "1.2.4.0" -SkipBuild
    .\submit-store.ps1 -DryRun
#>

param(
    [string]$Version,
    [switch]$SkipBuild,
    [switch]$UpdateListingOnly,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Configuration
$MsixDir = "msix-output"
$CredentialsFile = "$env:USERPROFILE\.storebroker\credentials.json"
$ListingFile = "store-listing.json"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "     TalkKeys Store Submission" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "  [DRY RUN MODE - No changes will be made]" -ForegroundColor Yellow
    Write-Host ""
}

# Get version from build script if not specified
if (-not $Version) {
    $buildScript = Get-Content "build-msix.cmd" -Raw
    if ($buildScript -match 'set VERSION=(\d+\.\d+\.\d+\.\d+)') {
        $Version = $matches[1]
    } else {
        Write-Error "Could not determine version. Please specify -Version parameter."
        exit 1
    }
}

# Extract short version (e.g., "1.2.4" from "1.2.4.0")
$ShortVersion = $Version -replace '\.0$', ''

Write-Host "  Version: $Version" -ForegroundColor Green
Write-Host ""

# ============================================
# STEP 1: Load Store Listing
# ============================================
Write-Host "[1/6] Loading store listing..." -ForegroundColor Yellow

if (-not (Test-Path $ListingFile)) {
    Write-Error "Store listing file not found: $ListingFile"
    exit 1
}

$listing = Get-Content $ListingFile -Raw | ConvertFrom-Json
Write-Host "       Loaded: $ListingFile" -ForegroundColor Gray

# Get release notes for this version
$releaseNotes = $listing.releaseNotesTemplate.$ShortVersion
if (-not $releaseNotes) {
    Write-Host "       Warning: No release notes for version $ShortVersion" -ForegroundColor Yellow
    $releaseNotes = "Bug fixes and improvements."
}

Write-Host "       Release notes found for v$ShortVersion" -ForegroundColor Green

# ============================================
# STEP 2: Build MSIX (if needed)
# ============================================
$MsixPath = Join-Path $MsixDir "TalkKeys_$Version.msix"

if (-not $UpdateListingOnly) {
    Write-Host ""
    Write-Host "[2/6] Checking MSIX package..." -ForegroundColor Yellow

    if (-not (Test-Path $MsixPath)) {
        if ($SkipBuild) {
            Write-Error "MSIX not found at $MsixPath. Run build-msix.cmd first or remove -SkipBuild."
            exit 1
        }

        if ($DryRun) {
            Write-Host "       [DRY RUN] Would build MSIX package" -ForegroundColor Gray
        } else {
            Write-Host "       Building MSIX package..." -ForegroundColor Cyan
            & cmd /c "build-msix.cmd"

            if (-not (Test-Path $MsixPath)) {
                Write-Error "Build failed. MSIX not found at $MsixPath"
                exit 1
            }
        }
    }
    Write-Host "       Package: $MsixPath" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[2/6] Skipping MSIX (listing update only)" -ForegroundColor Gray
}

# ============================================
# STEP 3: Load StoreBroker
# ============================================
Write-Host ""
Write-Host "[3/6] Loading StoreBroker module..." -ForegroundColor Yellow

if (-not (Get-Module -ListAvailable -Name StoreBroker)) {
    if ($DryRun) {
        Write-Host "       [DRY RUN] Would install StoreBroker" -ForegroundColor Gray
    } else {
        Write-Host "       Installing StoreBroker..." -ForegroundColor Cyan
        Install-Module -Name StoreBroker -Scope CurrentUser -Force
    }
}

if (-not $DryRun) {
    Import-Module StoreBroker -ErrorAction Stop
}
Write-Host "       StoreBroker ready" -ForegroundColor Green

# ============================================
# STEP 4: Authenticate
# ============================================
Write-Host ""
Write-Host "[4/6] Authenticating with Partner Center..." -ForegroundColor Yellow

if (-not (Test-Path $CredentialsFile)) {
    Write-Host ""
    Write-Host "Credentials file not found at: $CredentialsFile" -ForegroundColor Red
    Write-Host ""
    Write-Host "Run setup-credentials.ps1 or create manually." -ForegroundColor Yellow
    Write-Host "See docs/STORE-AUTOMATION.md for setup instructions." -ForegroundColor Yellow
    exit 1
}

$creds = Get-Content $CredentialsFile | ConvertFrom-Json

$AppId = $creds.StoreAppId
if (-not $AppId) {
    Write-Error "StoreAppId not found in credentials file."
    exit 1
}

if ($DryRun) {
    Write-Host "       [DRY RUN] Would authenticate with TenantId: $($creds.TenantId)" -ForegroundColor Gray
    Write-Host "       [DRY RUN] App ID: $AppId" -ForegroundColor Gray
} else {
    try {
        # Create PSCredential from ClientId (username) and ClientSecret (password)
        $secureSecret = ConvertTo-SecureString $creds.ClientSecret -AsPlainText -Force
        $credential = New-Object System.Management.Automation.PSCredential($creds.ClientId, $secureSecret)
        Set-StoreBrokerAuthentication -TenantId $creds.TenantId -Credential $credential
        Write-Host "       Authenticated successfully" -ForegroundColor Green
    } catch {
        Write-Error "Authentication failed: $_"
        exit 1
    }
}

# ============================================
# STEP 5: Create and Configure Submission
# ============================================
Write-Host ""
Write-Host "[5/6] Creating submission..." -ForegroundColor Yellow

if ($DryRun) {
    Write-Host ""
    Write-Host "  --------------------------------------------------------" -ForegroundColor Cyan
    Write-Host "  DRY RUN - Submission Preview" -ForegroundColor Cyan
    Write-Host "  --------------------------------------------------------" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  App ID:      $AppId" -ForegroundColor White
    Write-Host "  Version:     $Version" -ForegroundColor White
    Write-Host ""
    Write-Host "  -- Listing ------------------------------------------" -ForegroundColor Gray
    Write-Host "  Title:       $($listing.listing.baseListing.title)" -ForegroundColor White
    Write-Host ""
    Write-Host "  Short Description:" -ForegroundColor Gray
    Write-Host "  $($listing.listing.baseListing.shortDescription)" -ForegroundColor White
    Write-Host ""
    Write-Host "  -- Release Notes ------------------------------------" -ForegroundColor Gray
    Write-Host ""
    $releaseNotes -split "`n" | ForEach-Object { Write-Host "  $_" -ForegroundColor White }
    Write-Host ""
    Write-Host "  -- Features -----------------------------------------" -ForegroundColor Gray
    $listing.listing.baseListing.features | ForEach-Object { Write-Host "  * $_" -ForegroundColor White }
    Write-Host ""
    Write-Host "  -- Keywords -----------------------------------------" -ForegroundColor Gray
    Write-Host "  $($listing.listing.baseListing.keywords -join ', ')" -ForegroundColor White
    Write-Host ""

    if (-not $UpdateListingOnly) {
        Write-Host "  -- Package ------------------------------------------" -ForegroundColor Gray
        Write-Host "  MSIX:        $MsixPath" -ForegroundColor White
    }

    Write-Host ""
    Write-Host "  [DRY RUN] No submission created. Remove -DryRun to submit." -ForegroundColor Yellow
    Write-Host ""
    exit 0
}

try {
    # Get current app info
    $app = Get-Application -AppId $AppId
    Write-Host "       App: $($app.PrimaryName)" -ForegroundColor Cyan

    # Check for pending submissions (ignore error if none exists)
    try {
        $pendingSubmission = Get-ApplicationSubmission -AppId $AppId -SubmissionId "Pending" -ErrorAction SilentlyContinue
        if ($pendingSubmission) {
            Write-Host "       Deleting pending submission..." -ForegroundColor Yellow
            Remove-ApplicationSubmission -AppId $AppId -SubmissionId $pendingSubmission.Id -Force
        }
    } catch {
        # No pending submission - that's fine
        Write-Host "       No pending submission (OK)" -ForegroundColor Gray
    }

    # Create new submission by cloning the last published one
    Write-Host "       Cloning last published submission..." -ForegroundColor Cyan
    $submission = New-ApplicationSubmission -AppId $AppId

    Write-Host "       Submission ID: $($submission.Id)" -ForegroundColor Green

    # Update listing content
    Write-Host "       Updating listing content..." -ForegroundColor Cyan

    # Update the en-US listing
    if ($submission.listings -and $submission.listings."en-US") {
        $enListing = $submission.listings."en-US".baseListing

        # Update fields
        $enListing.description = $listing.listing.baseListing.description
        $enListing.releaseNotes = $releaseNotes
        $enListing.features = $listing.listing.baseListing.features
        $enListing.keywords = $listing.listing.baseListing.keywords
        $enListing.shortDescription = $listing.listing.baseListing.shortDescription

        Write-Host "       Updated: description, release notes, features, keywords" -ForegroundColor Gray
    }

} catch {
    Write-Error "Failed to create submission: $_"
    exit 1
}

# ============================================
# STEP 6: Upload Package and Submit
# ============================================
Write-Host ""
Write-Host "[6/6] Uploading and submitting..." -ForegroundColor Yellow

try {
    if (-not $UpdateListingOnly) {
        # Get the upload URL
        $uploadUrl = $submission.FileUploadUrl

        # Create a zip with the MSIX (StoreBroker expects a zip)
        $zipPath = Join-Path $MsixDir "TalkKeys_$Version.zip"
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

        Compress-Archive -Path $MsixPath -DestinationPath $zipPath -Force
        Write-Host "       Created upload package" -ForegroundColor Gray

        # Upload the package
        Set-SubmissionPackage -PackagePath $zipPath -UploadUrl $uploadUrl
        Write-Host "       Package uploaded" -ForegroundColor Green

        # Update submission to include the new package
        $submission.applicationPackages = @(
            @{
                fileName = "TalkKeys_$Version.msix"
                fileStatus = "PendingUpload"
                minimumDirectXVersion = "None"
                minimumSystemRam = "None"
            }
        )
    }

    # Update the submission with listing changes
    Update-ApplicationSubmission -AppId $AppId -SubmissionId $submission.Id -SubmissionData $submission

    # Commit the submission
    Write-Host "       Committing submission..." -ForegroundColor Cyan
    Complete-ApplicationSubmission -AppId $AppId -SubmissionId $submission.Id

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "      SUBMISSION SUCCESSFUL" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Submission ID: $($submission.Id)"
    Write-Host "  Version:       $Version"
    Write-Host ""
    Write-Host "  The submission is now in the certification queue."
    Write-Host ""
    Write-Host "  Monitor at:" -ForegroundColor Cyan
    Write-Host "  https://partner.microsoft.com/dashboard/products/$AppId/submissions/$($submission.Id)"
    Write-Host ""

} catch {
    Write-Error "Failed to submit: $_"
    Write-Host ""
    Write-Host "You may need to complete the submission manually at Partner Center." -ForegroundColor Yellow
    exit 1
}

# ============================================
# Monitor submission status
# ============================================
Write-Host "Monitoring submission status..." -ForegroundColor Yellow
Write-Host "(Press Ctrl+C to stop monitoring)" -ForegroundColor Gray
Write-Host ""

$maxWaitMinutes = 60
$checkIntervalSeconds = 60
$startTime = Get-Date

while ($true) {
    try {
        $status = Get-ApplicationSubmissionStatus -AppId $AppId -SubmissionId $submission.Id

        $elapsed = (Get-Date) - $startTime
        Write-Host "[$([math]::Floor($elapsed.TotalMinutes))m] Status: $($status.Status)" -ForegroundColor Cyan

        if ($status.Status -eq "Published") {
            Write-Host ""
            Write-Host "========================================" -ForegroundColor Green
            Write-Host "      APP PUBLISHED SUCCESSFULLY!" -ForegroundColor Green
            Write-Host "========================================" -ForegroundColor Green
            Write-Host ""
            break
        }

        if ($status.Status -eq "Failed") {
            Write-Host ""
            Write-Host "  SUBMISSION FAILED" -ForegroundColor Red
            Write-Host "  Errors:" -ForegroundColor Red
            $status.StatusDetails.Errors | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
            exit 1
        }

        if ($elapsed.TotalMinutes -ge $maxWaitMinutes) {
            Write-Host ""
            Write-Host "Stopped monitoring after $maxWaitMinutes minutes." -ForegroundColor Yellow
            Write-Host "Check Partner Center for final status." -ForegroundColor Yellow
            break
        }

        Start-Sleep -Seconds $checkIntervalSeconds

    } catch {
        Write-Host "Error checking status: $_" -ForegroundColor Yellow
        Start-Sleep -Seconds $checkIntervalSeconds
    }
}
