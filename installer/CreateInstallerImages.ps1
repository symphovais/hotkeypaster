# PowerShell script to generate installer wizard images
# Run this script to create the BMP files needed for the installer

Add-Type -AssemblyName System.Drawing

# Create WizardImage (164x314) - Left side panel image
$wizardWidth = 164
$wizardHeight = 314

$wizardBitmap = New-Object System.Drawing.Bitmap($wizardWidth, $wizardHeight)
$graphics = [System.Drawing.Graphics]::FromImage($wizardBitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

# Dark gradient background (#1F2937 to #111827)
$gradientBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 0)),
    (New-Object System.Drawing.Point(0, $wizardHeight)),
    [System.Drawing.ColorTranslator]::FromHtml("#1F2937"),
    [System.Drawing.ColorTranslator]::FromHtml("#111827")
)
$graphics.FillRectangle($gradientBrush, 0, 0, $wizardWidth, $wizardHeight)

# Draw decorative circles (like the audio visualizer bars abstracted)
$purple = [System.Drawing.ColorTranslator]::FromHtml("#6366F1")
$purpleBrush = New-Object System.Drawing.SolidBrush($purple)

# Decorative dots pattern
$dotPositions = @(
    @{X=30; Y=80; Size=8},
    @{X=50; Y=90; Size=12},
    @{X=70; Y=75; Size=6},
    @{X=90; Y=95; Size=14},
    @{X=110; Y=85; Size=10},
    @{X=130; Y=70; Size=8}
)
foreach ($dot in $dotPositions) {
    $graphics.FillEllipse($purpleBrush, $dot.X, $dot.Y, $dot.Size, $dot.Size)
}

# App name at bottom
$font = New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.FontStyle]::Bold)
$whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$textFormat = New-Object System.Drawing.StringFormat
$textFormat.Alignment = [System.Drawing.StringAlignment]::Center

$graphics.DrawString("TalkKeys", $font, $whiteBrush, ($wizardWidth / 2), 260, $textFormat)

# Tagline
$smallFont = New-Object System.Drawing.Font("Segoe UI", 8)
$grayBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml("#9CA3AF"))
$graphics.DrawString("Voice to Text", $smallFont, $grayBrush, ($wizardWidth / 2), 285, $textFormat)

$graphics.Dispose()
$wizardBitmap.Save("$PSScriptRoot\WizardImage.bmp", [System.Drawing.Imaging.ImageFormat]::Bmp)
$wizardBitmap.Dispose()
Write-Host "Created WizardImage.bmp (164x314)"

# Create WizardSmallImage (55x55) - Top right corner image
$smallWidth = 55
$smallHeight = 55

$smallBitmap = New-Object System.Drawing.Bitmap($smallWidth, $smallHeight)
$graphics = [System.Drawing.Graphics]::FromImage($smallBitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

# Dark background
$darkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml("#1F2937"))
$graphics.FillRectangle($darkBrush, 0, 0, $smallWidth, $smallHeight)

# Draw microphone-like icon (simplified)
$purple = [System.Drawing.ColorTranslator]::FromHtml("#6366F1")
$purpleBrush = New-Object System.Drawing.SolidBrush($purple)

# Mic body (rounded rect approximated with ellipse)
$graphics.FillEllipse($purpleBrush, 18, 12, 18, 24)

# Mic stand
$pen = New-Object System.Drawing.Pen($purple, 2)
$graphics.DrawArc($pen, 14, 25, 26, 20, 0, 180)
$graphics.DrawLine($pen, 27, 35, 27, 42)
$graphics.DrawLine($pen, 20, 42, 34, 42)

$graphics.Dispose()
$smallBitmap.Save("$PSScriptRoot\WizardSmallImage.bmp", [System.Drawing.Imaging.ImageFormat]::Bmp)
$smallBitmap.Dispose()
Write-Host "Created WizardSmallImage.bmp (55x55)"

Write-Host "`nInstaller images created successfully!"
