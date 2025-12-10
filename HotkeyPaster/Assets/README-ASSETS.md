# Store Assets for TalkKeys

Before submitting to the Microsoft Store, you need to create the following PNG images.

## Required Assets

All images should be PNG format with transparent background (except SplashScreen).

| Filename | Size (px) | Description |
|----------|-----------|-------------|
| StoreLogo.png | 50x50 | Store listing icon |
| Square44x44Logo.png | 44x44 | Taskbar, Start menu |
| Square71x71Logo.png | 71x71 | Small tile |
| Square150x150Logo.png | 150x150 | Medium tile |
| Wide310x150Logo.png | 310x150 | Wide tile |
| Square310x310Logo.png | 310x310 | Large tile |
| SplashScreen.png | 620x300 | Splash screen (purple background: #7C3AED) |

## How to Create

### Option 1: Use Visual Studio Asset Generator
1. Open the project in Visual Studio
2. Double-click Package.appxmanifest
3. Go to "Visual Assets" tab
4. Drop icon.ico in the source and generate all sizes

### Option 2: Use Online Tools
1. Go to https://www.appicon.co/ or similar
2. Upload icon.ico
3. Download Windows Store asset pack
4. Copy PNGs to this folder

### Option 3: Use ImageMagick (command line)
```powershell
# Install ImageMagick first: winget install ImageMagick.ImageMagick

magick ..\icon.ico -resize 50x50 StoreLogo.png
magick ..\icon.ico -resize 44x44 Square44x44Logo.png
magick ..\icon.ico -resize 71x71 Square71x71Logo.png
magick ..\icon.ico -resize 150x150 Square150x150Logo.png
magick ..\icon.ico -resize 310x310 Square310x310Logo.png

# For wide tile, you may want to add padding/centering
magick ..\icon.ico -resize 150x150 -gravity center -background transparent -extent 310x150 Wide310x150Logo.png

# For splash screen with purple background
magick ..\icon.ico -resize 150x150 -gravity center -background "#7C3AED" -extent 620x300 SplashScreen.png
```

## Color Reference
- Primary Purple: #7C3AED
- Dark Background: #1F2937
