#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."

APP="build/HangLater.app"
if [[ ! -d "$APP" ]]; then
  bash scripts/build-app.sh release
fi

VERSION="$(plutil -extract CFBundleShortVersionString raw "$APP/Contents/Info.plist")"
RELEASES="build/releases"
DMG="$RELEASES/HangLater.dmg"
ZIP="$RELEASES/HangLater.zip"
STAGING="$(mktemp -d "${TMPDIR:-/tmp}/HangLater-dmg.XXXXXX")"

cleanup() {
  /usr/bin/python3 - "$STAGING" <<'PY'
import shutil, sys
shutil.rmtree(sys.argv[1], ignore_errors=True)
PY
}
trap cleanup EXIT

mkdir -p "$RELEASES"
cp -R "$APP" "$STAGING/HangLater.app"
ln -s /Applications "$STAGING/Applications"

hdiutil create -quiet -ov -format UDZO -volname "Hang Later $VERSION" \
  -srcfolder "$STAGING" "$DMG"
ditto -c -k --sequesterRsrc --keepParent "$APP" "$ZIP"

shasum -a 256 "$DMG" "$ZIP"
