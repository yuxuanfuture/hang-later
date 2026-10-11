#!/bin/bash
# Use the stock Swift toolchain, with a workspace-only fix for CLT installations
# that contain an old private PackageDescription interface beside a new dylib.
set -euo pipefail
cd "$(dirname "$0")/.."
FLAGS=()
COMPILE_FLAGS=()
TOOLCHAIN_BIN="$(dirname "$(xcrun --find swiftc)")"
MANIFEST_API="$TOOLCHAIN_BIN/../lib/swift/pm/ManifestAPI"
TASK_ARCH="$(uname -m)"
PUBLIC_INTERFACE="$MANIFEST_API/PackageDescription.swiftmodule/$TASK_ARCH-apple-macos.swiftinterface"
PRIVATE_INTERFACE="$MANIFEST_API/PackageDescription.swiftmodule/$TASK_ARCH-apple-macos.private.swiftinterface"
if [[ -f "$PUBLIC_INTERFACE" && -f "$PRIVATE_INTERFACE" ]] &&
   grep -q 'enum SwiftLanguageMode' "$PUBLIC_INTERFACE" &&
   ! grep -q 'enum SwiftLanguageMode' "$PRIVATE_INTERFACE"; then
  mkdir -p .build/toolchain-compat
  TASK_MODULE="$PWD/.build/toolchain-compat/PackageDescription.swiftmodule"
  if [[ ! -f "$TASK_MODULE" || "$PUBLIC_INTERFACE" -nt "$TASK_MODULE" ]]; then
    swiftc -frontend -compile-module-from-interface "$PUBLIC_INTERFACE" \
      -o "$TASK_MODULE" -module-name PackageDescription \
      -target "$TASK_ARCH-apple-macosx13.0" -sdk "$(xcrun --show-sdk-path)" \
      -package-description-version 6.0.0
  fi
  FLAGS=(-Xbuild-tools-swiftc -Xfrontend -Xbuild-tools-swiftc "-swift-module-file=PackageDescription=$TASK_MODULE")
fi
LEGACY_MAP="$TOOLCHAIN_BIN/../include/swift/module.modulemap"
BRIDGING_MAP="$TOOLCHAIN_BIN/../include/swift/bridging.modulemap"
if [[ -f "$LEGACY_MAP" && -f "$BRIDGING_MAP" ]] &&
   grep -q 'module SwiftBridging' "$LEGACY_MAP" && grep -q 'module SwiftBridging' "$BRIDGING_MAP"; then
  mkdir -p .build/toolchain-compat
  TASK_OVERLAY="$PWD/.build/toolchain-compat/overlay.json"
  /usr/bin/python3 - "$TASK_OVERLAY" "$LEGACY_MAP" <<'PY'
import json, sys
from pathlib import Path
overlay = Path(sys.argv[1])
empty = overlay.with_name('empty.modulemap')
if not empty.exists():
    empty.write_text('')
content = json.dumps({'version': 0, 'roots': [
    {'type': 'file', 'name': str(Path(sys.argv[2]).resolve()), 'external-contents': str(empty)}
]})
if not overlay.exists() or overlay.read_text() != content:
    overlay.write_text(content)
PY
  COMPILE_FLAGS=(-vfsoverlay "$TASK_OVERLAY")
fi
case "${1:-}" in
  build|test|run)
    if [[ ${#COMPILE_FLAGS[@]} -gt 0 ]]; then
      FLAGS+=(-Xswiftc -vfsoverlay -Xswiftc "$TASK_OVERLAY")
    fi
    TASK_COMMAND="$1"
    shift
    exec swift "$TASK_COMMAND" "${FLAGS[@]}" "$@"
    ;;
  *) exec swift "${COMPILE_FLAGS[@]}" "$@" ;;
esac
