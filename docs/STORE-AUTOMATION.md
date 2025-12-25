# Microsoft Store Submission Automation

This guide explains how to automate TalkKeys submissions to the Microsoft Store.

## Prerequisites

1. **Partner Center Account** with your app already published (at least one manual submission)
2. **Azure AD App Registration** for API access
3. **PowerShell 5.1+** (comes with Windows 10/11)

## Step 1: Set Up Azure AD App Registration

### 1.1 Create Azure AD Application

1. Go to [Azure Portal](https://portal.azure.com) > **Azure Active Directory** > **App registrations**
2. Click **New registration**
3. Enter:
   - Name: `TalkKeys-StoreSubmission`
   - Supported account types: **Single tenant**
4. Click **Register**
5. Note down:
   - **Application (client) ID** → This is your `ClientId`
   - **Directory (tenant) ID** → This is your `TenantId`

### 1.2 Create Client Secret

1. In your app registration, go to **Certificates & secrets**
2. Click **New client secret**
3. Description: `Store Submission`
4. Expiry: Choose appropriate duration
5. Click **Add**
6. **COPY THE VALUE NOW** - it won't be shown again → This is your `ClientSecret`

### 1.3 Link Azure AD to Partner Center

1. Go to [Partner Center](https://partner.microsoft.com/dashboard)
2. Go to **Account settings** > **User management** > **Azure AD applications**
3. Click **Add Azure AD application**
4. Select the app you created (`TalkKeys-StoreSubmission`)
5. Grant it **Developer** role (minimum) or **Manager** for full access

### 1.4 Get Seller ID

1. In Partner Center, go to **Account settings** > **Organization profile** > **Legal info**
2. Find your **Seller ID** (a number)

## Step 2: Install StoreBroker

Open PowerShell as Administrator and run:

```powershell
# Install StoreBroker from PowerShell Gallery
Install-Module -Name StoreBroker -Scope CurrentUser

# Import the module
Import-Module StoreBroker
```

## Step 3: Configure Credentials

Create a credentials file (keep this secure, don't commit to git):

```powershell
# Create credentials directory
New-Item -ItemType Directory -Path "$env:USERPROFILE\.storebroker" -Force

# Save credentials (run this interactively once)
$creds = @{
    TenantId = "YOUR_TENANT_ID"
    ClientId = "YOUR_CLIENT_ID"
    ClientSecret = "YOUR_CLIENT_SECRET"
    StoreAppId = "YOUR_STORE_APP_ID"  # e.g., 9P2D7DZQS61J
}
$creds | ConvertTo-Json | Set-Content "$env:USERPROFILE\.storebroker\credentials.json"
```

## Step 4: Get Your Store App ID

1. Go to [Partner Center](https://partner.microsoft.com/dashboard)
2. Select your app > **App management** > **App identity**
3. Copy the **Store ID** (looks like `9PXXXXXXXX` or `9NXXXXXXXX`)
4. Add it to your credentials file as `StoreAppId`

## Step 5: Submit to Store

The `submit-store.ps1` script handles the complete submission workflow.

### Usage

```powershell
# Preview what will be submitted (recommended first step)
.\submit-store.ps1 -DryRun

# Full submission (build + upload + listing update)
.\submit-store.ps1

# Skip build if MSIX already exists
.\submit-store.ps1 -SkipBuild

# Update only the listing (no new package)
.\submit-store.ps1 -UpdateListingOnly

# Explicit version
.\submit-store.ps1 -Version "1.2.4.0" -SkipBuild
```

## Store Listing

All Store content is defined in `store-listing.json`:
- **Description** - Full app description with features
- **Short description** - One-liner for search results
- **Features** - Bullet points shown in Store listing
- **Keywords** - Search terms
- **Release notes** - Version-specific "What's New" entries

To update the listing for a new version:
1. Edit `store-listing.json`
2. Add release notes under `releaseNotesTemplate`
3. Run `.\submit-store.ps1 -DryRun` to preview
4. Run `.\submit-store.ps1` to submit

## Automation Flow

The script performs these steps:

1. **Load listing** from `store-listing.json`
2. **Build MSIX** (if needed)
3. **Authenticate** with Azure AD
4. **Clone submission** (preserves screenshots, etc.)
5. **Update listing** (description, features, release notes)
6. **Upload package** and submit
7. **Monitor certification** status

## Troubleshooting

### "App not found"
- Ensure Azure AD app has correct permissions in Partner Center
- Verify App ID is correct

### "Authentication failed"
- Check TenantId, ClientId, ClientSecret are correct
- Ensure client secret hasn't expired

### "Submission in progress"
- You can only have one pending submission at a time
- Delete the pending submission in Partner Center or wait for it to complete

## Manual Fallback

If automation fails, you can always submit manually:

1. Go to [Partner Center](https://partner.microsoft.com/dashboard)
2. Select TalkKeys > **Update**
3. Upload `msix-output/TalkKeys_X.X.X.X.msix`
4. Review and submit

## Resources

- [StoreBroker GitHub](https://github.com/microsoft/StoreBroker)
- [Microsoft Store Submission API](https://learn.microsoft.com/en-us/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services)
- [Partner Center](https://partner.microsoft.com/dashboard)
