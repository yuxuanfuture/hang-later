# Hang Later for Windows

Version 0.10.1 · Windows 10 1809 or later / Windows 11 · x64

[Download HangLater.exe](https://github.com/yuxuanfuture/hang-later/releases/download/v0.10.1/HangLater.exe) · [Release and checksum](https://github.com/yuxuanfuture/hang-later/releases/tag/v0.10.1)

## Run

Keep `HangLater.exe` in a permanent user-writable folder and double-click it. No installation, administrator permission or separate .NET runtime is required. The filename is stable: no debug/build-number suffix. The application version is recorded in the EXE metadata.

This is an unsigned community build, not a Microsoft Store installer. Verify the release's SHA-256 against `Get-FileHash ./HangLater.exe -Algorithm SHA256`. Do not disable Windows security protections. Closing the main window keeps the tray pin running; choose Quit from its menu to exit.

## Use

- Click the tray pin or press `Ctrl+Alt+L` to reveal/hide the desktop clothesline. Optional edge hover and vertical placement are in Settings.
- Hang a task/link, reminder, image or visible app window through Hang something. Only actual dragging shows the drop target, and only after the line has already been revealed. Captured windows return to their previous position.
- Click a slip to open its URL/file or activate its app. Saved document paths/links are used for supported Typora and Obsidian targets. Apps without a document link can only be activated/relaunched; exact internal state is not guaranteed.
- Completed records group by day, week or month. The Windows Done Spike uses today's completion timestamp, not the creation/published timestamp. Drag empty space to rotate; drag a slip to reposition it along the spike; Ctrl+wheel resizes a slip, and the wheel zooms the scene.
- Light/Dark themes, English/Chinese, local reminders and named task types are supported.

## Chrome / Edge

Open Connect Chrome or Hang something → Hang a page or video from Chrome. The application extracts its bundled extension into `%APPDATA%\HangLater\BrowserBridge\ChromeExtension`; Open extension folder reveals it. Connect Chrome registers the native messaging host for the current user. Load that directory from `chrome://extensions` or `edge://extensions` in developer mode. Pin the extension and use its action on the page you wish to capture.

The bridge points to the current `HangLater.exe` location. If you move the executable, connect again. No local HTTP server is used. The extension reads/captures the active page only on a user action.

## Local data

Tasks, settings, task types, previews and the bridge queue live in `%APPDATA%\HangLater`. The EXE is portable; its task data is per-user and is not stored beside it. There is no account, cloud sync or telemetry. Back up this folder before changing machines. Source code and downloads never include personal tasks/previews.

## Build and verify

Install the .NET 8 SDK on Windows, then run from the repository root:

```powershell
git clone https://github.com/yuxuanfuture/hang-later.git
cd hang-later
./scripts/build-windows.ps1
```

The script builds the WPF app, runs eight isolated verification projects and produces only `artifacts/windows/HangLater.exe` plus its SHA-256 sidecar. Tests generate neutral fixture images and temporary repositories; they do not need private screenshots or the developer's user profile. Generated images, bin/obj files and local data are ignored by Git.

To build without tests after a verified checkout: `./scripts/build-windows.ps1 -SkipTests`. To run one check after building:

```powershell
dotnet run --project Windows/Tests/ResumeChecks/ResumeChecks.csproj -c Release
```

The checks cover window/document restoration, drag/drop lifecycle, completion grouping, current-day spike filtering, 3D controls, settings drafts, source-dialog flow, theme assets, status counts, embedded extension extraction and responsive footer spacing. They are scoped fixtures, not a guarantee that every third-party app supports document restoration. Existing nullable warnings are being retained rather than hidden.

`Windows/HangLater` contains App.xaml, Views, Controls, Models, Services and Resources. `Windows/Tests` contains the eight check projects. Shared browser source remains in the root `ChromeExtension` directory. The Windows workflow builds/verifies on a clean GitHub runner and adds the EXE to the existing v0.10.1 release without replacing its macOS assets or moving the existing tag. The release notes identify the Windows source commit on main.

## 简体中文

下载 `HangLater.exe` 后放在固定位置直接运行，不需要安装额外运行时，也无需管理员权限。版本号为 0.10.1，保存在 EXE 属性中，不放在文件名里。构建尚未签名，请使用发布页的 SHA-256 校验，不要关闭系统安全保护。

托盘图钉或 `Ctrl+Alt+L` 呼出晾衣绳。窗口关闭后仍在托盘运行，右键「退出」才会结束。浏览器扩展已内置，打开连接页面后可从「打开扩展文件夹」加载。移动 EXE 后请重新连接浏览器桥接。

Windows 完成钉只统计当天完成的任务，和任务创建时间无关。任务、预览、设置仅保存在 `%APPDATA%\HangLater`，不会包含在开源仓库或下载文件中。

安装 .NET 8 SDK 后，在仓库根目录运行 `./scripts/build-windows.ps1`，会运行八组隔离检查并生成 `artifacts/windows/HangLater.exe`。原有 macOS 下载、源码与标签保留，两个平台共用仓库。
