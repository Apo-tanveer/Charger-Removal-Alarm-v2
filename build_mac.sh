#!/bin/bash
set -e

echo ""
echo "============================================"
echo "  Charger Removal Alarm - macOS Build"
echo "============================================"
echo ""

# ── Step 1: Check .NET SDK ────────────────────────────────────────────────────
echo "[1/3] Checking .NET SDK..."
if ! command -v dotnet &> /dev/null; then
    echo "  ERROR: .NET SDK not found."
    echo "  Install from: https://dotnet.microsoft.com/download"
    exit 1
fi
echo "       Found $(dotnet --version)"

# ── Step 2: Build ─────────────────────────────────────────────────────────────
echo ""
echo "[2/3] Building self-contained app..."
rm -rf obj bin publish

dotnet publish ChargerRemovalAlarm.Mac.csproj \
    -c Release \
    -r osx-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o publish

echo ""
echo "       Binary ready: publish/ChargerRemovalAlarm"

# ── Step 3: Create .app bundle ────────────────────────────────────────────────
echo ""
echo "[3/3] Creating .app bundle..."

APP="publish/ChargerRemovalAlarm.app"
MACOS="$APP/Contents/MacOS"
RESOURCES="$APP/Contents/Resources"

mkdir -p "$MACOS" "$RESOURCES"

# Copy binary
cp publish/ChargerRemovalAlarm "$MACOS/ChargerRemovalAlarm"
chmod +x "$MACOS/ChargerRemovalAlarm"

# Copy icon if present
if [ -f "icon.icns" ]; then
    cp icon.icns "$RESOURCES/AppIcon.icns"
    ICON_LINE='<key>CFBundleIconFile</key><string>AppIcon</string>'
else
    ICON_LINE=''
    echo "       Note: No icon.icns found. Add icon.icns for a custom dock icon."
fi

# Write Info.plist
cat > "$APP/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Charger Removal Alarm</string>
    <key>CFBundleDisplayName</key>
    <string>Charger Removal Alarm</string>
    <key>CFBundleIdentifier</key>
    <string>com.tanveer.chargerremovalalarm</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundleExecutable</key>
    <string>ChargerRemovalAlarm</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    $ICON_LINE
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
</dict>
</plist>
PLIST

# Create DMG for distribution
if command -v hdiutil &> /dev/null; then
    echo ""
    echo "       Creating DMG installer..."
    mkdir -p dmg_staging
    cp -r "$APP" dmg_staging/
    ln -sf /Applications dmg_staging/Applications
    hdiutil create \
        -volname "Charger Removal Alarm" \
        -srcfolder dmg_staging \
        -ov -format UDZO \
        publish/ChargerRemovalAlarm.dmg
    rm -rf dmg_staging
    echo "       DMG ready: publish/ChargerRemovalAlarm.dmg"
fi

echo ""
echo "============================================"
echo "  BUILD COMPLETE!"
echo "============================================"
echo ""
echo "  App bundle : publish/ChargerRemovalAlarm.app"
echo "  DMG        : publish/ChargerRemovalAlarm.dmg"
echo ""
echo "  To install: drag ChargerRemovalAlarm.app"
echo "  to your Applications folder."
echo ""
