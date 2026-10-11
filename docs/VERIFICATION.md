# Verification

Run all checks from the repository root.

## Core behavior

```sh
bash scripts/swift.sh run HangLaterChecks
```

The checker covers task persistence, completion and restoration, Done Spike attachment, archive grouping, free card positions and overlap order, input validation, atomic-write failure handling, URL parsing, and Chrome bridge directory matching.

## Native Messaging host

```sh
python3 -m unittest discover -s NativeHost -p 'test_*.py'
```

The tests cover Native Messaging frame parsing, atomic inbox writes, invalid input, incomplete messages, and oversized messages.

## Chrome extension

```sh
node --check ChromeExtension/popup.js
node --check ChromeExtension/background.js
```

## Release bundle

```sh
bash scripts/build-app.sh release
plutil -lint build/HangLater.app/Contents/Info.plist
codesign --verify --deep --strict build/HangLater.app
./build/HangLater.app/Contents/MacOS/HangLater --verify-adaptive-card-layout
./build/HangLater.app/Contents/MacOS/HangLater --verify-resume-targets
```

## Privacy-safe visual fixtures

Use `--sample-data` when generating documentation. It creates an isolated temporary task store with synthetic previews and never reads the user's Hang Later data.

```sh
./build/HangLater.app/Contents/MacOS/HangLater \
  --render-main-preview /tmp/hang-later-en.png --sample-data --english

./build/HangLater.app/Contents/MacOS/HangLater \
  --render-desktop-preview /tmp/clothesline.png --sample-data --english --dark

./build/HangLater.app/Contents/MacOS/HangLater \
  --render-main-preview /tmp/done-spike.png --sample-data --english --spike
```

Manual release review should still cover external window dragging, the Screen Recording permission flow, Chrome Native Messaging installation, opening real Chrome and Obsidian targets, and 3D Done Spike pointer interactions.
