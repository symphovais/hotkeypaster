# HotkeyPaster Icon

## 🎨 Design

The icon features:
- **Microphone**: Represents the audio recording functionality
- **Keyboard Key (Ctrl+Q)**: Shows the hotkey trigger
- **Sound Waves**: Indicates active audio processing
- **Modern Gradient**: Professional indigo/purple gradient background
- **Clean Design**: Scales well from 16×16 to 256×256 pixels

## 📁 Files

- `icon.svg` - Vector source file (editable, scalable)
- `preview-icon.html` - Open in browser to preview the icon at different sizes

## 🔧 Converting to ICO

### Option 1: Online Converter (Easiest)
1. Go to https://convertio.co/svg-ico/
2. Upload `icon.svg`
3. Download as `icon.ico`
4. Place in the `HotkeyPaster` folder

### Option 2: Using ImageMagick
```bash
magick convert icon.svg -define icon:auto-resize=256,128,64,48,32,16 icon.ico
```

### Option 3: Using Inkscape
```bash
inkscape icon.svg --export-type=png --export-filename=icon-256.png -w 256 -h 256
# Then use an online PNG to ICO converter
```

## 📝 Adding to Your Project

1. Place `icon.ico` in the `HotkeyPaster` folder
2. Edit `HotkeyPaster.csproj` and add inside `<PropertyGroup>`:
   ```xml
   <ApplicationIcon>icon.ico</ApplicationIcon>
   ```
3. Rebuild the project:
   ```bash
   dotnet build
   ```

## 🎨 Customization

The SVG file can be edited in:
- **Inkscape** (free, open-source)
- **Adobe Illustrator**
- **Figma** (import SVG)
- Any text editor (it's XML-based)

### Color Scheme
- Primary: `#4F46E5` (Indigo)
- Secondary: `#7C3AED` (Purple)
- Accent: `#A5B4FC` (Light Indigo)
- White: `#FFFFFF`

Feel free to modify colors, shapes, or elements to match your preferences!
