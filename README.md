<p align="center">
  <img src="docs/images/hang-later-icon.png" width="132" alt="Hang Later app icon">
</p>

<h1 align="center">Hang Later</h1>

<p align="center">
  A clothesline for pages, files, app windows, and the things you want to return to.
</p>

<p align="center">
  <strong>English</strong> • <a href="docs/README.zh-CN.md">简体中文</a>
</p>

<p align="center">
  Free and open source. Native macOS 14+.
</p>

<p align="center">
  <a href="https://github.com/yuxuanfuture/hang-later/releases/latest/download/HangLater.dmg"><strong>Download for macOS</strong></a>
  &nbsp;·&nbsp;
  <a href="#build-from-source">Build from source</a>
</p>

![Hang Later main window](docs/images/hang-later-en.png)

## Leave it where you can return to it.

Hang Later turns postponed work into something spatial. Hang a web page, video, image, PDF, app window, or plain task on the clothesline. When you have time again, click the slip and return to the original page, file, or app.

The desktop clothesline stays tucked above the screen. Rest the pointer on the Hang Later menu-bar pin, or press `⌃⌥L`, and it drops into view.

![Desktop clothesline](docs/images/clothesline-dark-en.png)

## Hang almost anything.

- **Web pages and videos** — save the title, exact URL, and visible page. When a visible video player is present, the Chrome extension crops the preview to the largest player.
- **Images and PDFs** — drag them from Finder or another app. PDFs keep the first page as a proportional thumbnail.
- **App windows** — drag a window title bar onto “Drag something here,” or choose a visible window from **Hang something**. Hang Later keeps a local screenshot and the app identity.
- **Plain tasks** — write a traditional to-do with notes and an optional due date.

Cards keep the proportions of their previews within a practical size range. Move them anywhere along the line, overlap them, and bring the last card you touched to the front.

The desktop clothesline includes a dashed drop target, so a file, image, link, or app window is added as soon as you release it inside the marked area.

## One click back.

Clicking a slip returns to its source:

- Chrome pages reopen from the saved URL, even after the original tab is closed.
- Obsidian windows resolve to their vault and note when the window title contains enough information.
- Images, PDFs, and files open in their default app.
- Apps without a document deep link, including terminals and remote-desktop clients, are reopened and brought to the front.

Some apps do not expose an internal document or scroll-position URL. For those apps, Hang Later restores the app rather than an exact internal state.

## Finish it. Keep the record.

Choose **Done** on a slip to move it into the archive. Use **Complete all** or **Delete all** to clear every slip currently visible on the clothesline in one action. Completed work can be grouped by day, week, or month, restored to the line, or deleted permanently.

The **Done Spike** turns the archive into a rotatable 3D receipt spike. It only shows slips published during the current workday. In **Settings**, define a workday that can cross midnight—such as 15:00 to 03:00—so late-night work stays with the day it belongs to. Drag slips onto or off the spike, move them up, down, forward, back, left, or right, and keep their published and completed times.

![Done Spike](docs/images/done-spike-en.png)

## A gesture for each action.

Action | Result
--- | ---
Click a slip | Return to its page, file, or app.
Drag a slip | Place it freely on the clothesline. Slips may overlap.
Choose **Done** | Move it to Completed and the Done Spike.
Choose **Complete all** | Complete every slip currently visible on the clothesline.
Choose **Delete all** | Permanently remove every slip currently visible on the clothesline.
Open `…` | Edit or delete a mistakenly added task.
Rest the pointer on the menu-bar pin | Reveal the desktop clothesline.
`⌃⌥L` | Show or hide the desktop clothesline.
`⌘N` | Open the source picker.

## Private by design.

Hang Later has no account, cloud sync, analytics, or telemetry. Tasks and previews stay in:

```text
~/Library/Application Support/HangLater/
```

Link previews may contact the page you add through macOS Link Presentation. The Chrome extension reads and captures the active page only when you click the extension or use its context-menu action. It does not automatically read browser history.

Window capture requires macOS Screen Recording permission. The captured preview remains local.

## Tech specs

 | |
--- | ---
Compatibility | macOS 14 Sonoma or later
Languages | English, Chinese (Simplified)
Built with | Swift, AppKit, SwiftUI, SceneKit, ScreenCaptureKit, PDFKit
Storage | Local JSON and PNG files
Network | Page metadata and user-initiated browser capture; no analytics
Price | Free
License | MIT

## Install

Download [HangLater.dmg](https://github.com/yuxuanfuture/hang-later/releases/latest/download/HangLater.dmg), open it, and drag Hang Later into **Applications**. A ZIP archive is also available on the [latest release](https://github.com/yuxuanfuture/hang-later/releases/latest) page.

The current community build is ad hoc signed and has not been notarized by Apple. On first launch, Control-click Hang Later in Applications, choose **Open**, then confirm **Open**. If macOS still blocks it, go to **System Settings → Privacy & Security** and choose **Open Anyway**.

## Build from source

Install Swift 6 through Xcode or the Command Line Tools, then run:

```sh
git clone https://github.com/yuxuanfuture/hang-later.git
cd hang-later
bash scripts/build-app.sh release
open build/HangLater.app
```

The build script creates `build/HangLater.app`, generates the complete macOS icon set, bundles the Chrome extension and native host, and applies a stable local ad hoc signature. macOS may ask for Screen Recording permission after a rebuild.

The app remains in the menu bar after its main window closes. Choose **Quit Hang Later** from the menu-bar menu to exit it.

## Connect Chrome

1. Open Hang Later and choose **Connect Chrome** in the lower-left corner. Install the native messaging host.
2. Open `chrome://extensions`, enable **Developer mode**, and choose **Load unpacked**.
3. Select the repository's `ChromeExtension` directory, or use **Show extension folder** in Hang Later.
4. Pin the Hang Later extension. Click it on a page to hang the current title, URL, and visible content.

The extension uses Chrome Native Messaging. There is no local HTTP server. Captures remain queued locally if Hang Later is closed and import on the next launch.

## Develop and verify

```sh
bash scripts/swift.sh run HangLaterChecks
python3 -m unittest discover -s NativeHost -p 'test_*.py'
node --check ChromeExtension/popup.js
node --check ChromeExtension/background.js
```

Generate privacy-safe README previews with sample data:

```sh
./build/HangLater.app/Contents/MacOS/HangLater \
  --render-main-preview /tmp/hang-later.png --sample-data --english
```

See [docs/VERIFICATION.md](docs/VERIFICATION.md) for the complete validation checklist.

## Project map

File | Role
--- | ---
`AppDelegate.swift` | Windows, menu bar, desktop reveal behavior, global shortcut
`DesktopLine.swift` | Transparent desktop panel and drop target
`HangingCard.swift` | Adaptive paper cards, free positioning, overlap, drag motion
`AppStore.swift` | Tasks, previews, opening targets, Chrome inbox, persistence glue
`ReceiptSpike3D.swift` | Interactive SceneKit Done Spike and receipt textures
`WindowCapture.swift` | ScreenCaptureKit window picker and local previews
`HangLaterCore/` | Persistence, completion grouping, drag input, bridge manifest
`ChromeExtension/` | Manifest V3 extension for user-initiated page capture
`NativeHost/` | Chrome Native Messaging host

## Acknowledgments

The desktop clothesline interaction is inspired by [Alejandro Buján's Tendedero](https://github.com/alejandrobujan/tendedero). The floating desktop panel, rope geometry, and global hotkey implementation are adapted from or informed by its MIT-licensed source. Hang Later uses its own name, icon, task model, interface, and artwork. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## License

[MIT](LICENSE) © 2026 Yuxuan Fu.
