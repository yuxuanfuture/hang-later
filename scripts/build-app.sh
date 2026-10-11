#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
CONFIG="${1:-release}"
if [[ "$CONFIG" != "debug" && "$CONFIG" != "release" ]]; then
  echo "Usage: scripts/build-app.sh [debug|release]" >&2
  exit 1
fi
bash scripts/swift.sh build -c "$CONFIG"
BIN_DIRECTORY="$(bash scripts/swift.sh build -c "$CONFIG" --show-bin-path)"
APP="build/HangLater.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
rm -f "$APP/Contents/Resources/receipt-spike.png"
cp Sources/HangLater/Resources/le-labo-paper.png "$APP/Contents/Resources/le-labo-paper.png"
cp "$BIN_DIRECTORY/HangLater" "$APP/Contents/MacOS/HangLater.new"
mv "$APP/Contents/MacOS/HangLater.new" "$APP/Contents/MacOS/HangLater"
bash scripts/swift.sh scripts/make-icon.swift "$APP/Contents/Resources"
sips -z 48 48 "$APP/Contents/Resources/icon.png" --out ChromeExtension/icon48.png >/dev/null
sips -z 128 128 "$APP/Contents/Resources/icon.png" --out ChromeExtension/icon128.png >/dev/null
cp -R ChromeExtension "$APP/Contents/Resources/"
mkdir -p "$APP/Contents/Resources/NativeHost"
cp NativeHost/capture-host.py "$APP/Contents/Resources/NativeHost/"
cp THIRD_PARTY_NOTICES.md "$APP/Contents/Resources/"
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>HangLater</string>
  <key>CFBundleDisplayName</key><string>Hang Later</string>
  <key>CFBundleIdentifier</key><string>app.hanglater.mac</string>
  <key>CFBundleExecutable</key><string>HangLater</string>
  <key>CFBundleIconFile</key><string>HangLater</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>0.9.7</string>
  <key>CFBundleVersion</key><string>28</string>
  <key>CFBundleDevelopmentRegion</key><string>zh_CN</string>
  <key>LSMinimumSystemVersion</key><string>14.0</string>
  <key>LSUIElement</key><true/>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSScreenCaptureUsageDescription</key><string>Hang Later 需要在你选择 App 窗口时保存一张本地预览图。</string>
  <key>NSAppleEventsUsageDescription</key><string>Hang Later 需要读取你主动晾起的 Chrome 标签页地址，以便稍后返回原页面。</string>
</dict></plist>
PLIST
# Keep one local designated requirement across rebuilds so macOS can associate
# privacy grants with Hang Later instead of an individual ad-hoc binary hash.
codesign --force --deep --sign - \
  --requirements '=designated => identifier "app.hanglater.mac"' "$APP"
echo "Built $APP"
