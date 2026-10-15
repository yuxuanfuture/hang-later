# Hang Later for Windows

Hang Later for Windows carries the complete v0.10.0 workflow to Windows 10 and Windows 11: clothesline cards, to-do and completed views, collapsible completion groups, Done Spike, ideas, reminders, task types, workday rules, browser capture, window capture, bulk actions, and local-only storage.

**English** • [简体中文](#简体中文)

## Windows entry points

- **System tray:** left-click the pin to show or hide the clothesline. Right-click it for Open Hang Later, Hang an Idea, Hang a Reminder, Settings, and Quit.
- **Global shortcut:** `Ctrl+Alt+L` by default. Change it in Settings with any supported Ctrl, Alt, Shift, or Win combination.
- **Top-edge hover:** optional. Rest the pointer for 0.5 seconds in a 3-pixel strip at the top of a display. Choose the full edge, left third, center third, or right third. The clothesline opens on the display under the pointer.

The 3-pixel height and 0.5-second dwell time are fixed to prevent a quick pass over browser tabs or window controls from opening the clothesline accidentally.

## Windows-specific behavior

- Closing the main window removes it from the taskbar while the tray pin keeps Hang Later running.
- Word, PowerPoint, Excel, CSV, PDF, and image files use the Windows Explorer shell thumbnail shown by the system.
- Visible app windows can be selected from **Hang Something**. Hang Later stores a local preview and reopens or focuses the application later.
- Reminders use Windows tray notifications. Online meeting links open from the task card.
- Tasks and previews stay in `%APPDATA%\HangLater`. There is no account, analytics, or cloud sync.
- Chrome and Edge use the same Manifest V3 extension and the Windows Native Messaging registry under the current user.

## Build and package

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and, for the installer, Inno Setup 6. Then run in PowerShell:

```powershell
./scripts/build-windows.ps1 -Runtime win-x64
```

The script runs the Windows core checks, publishes a self-contained app, creates `HangLater-Windows-win-x64.zip`, and creates `HangLater-Windows-x64-Setup.exe` when Inno Setup is available.

For Windows on ARM:

```powershell
./scripts/build-windows.ps1 -Runtime win-arm64 -SkipInstaller
```

Run verification only:

```powershell
dotnet run --project Windows/HangLater.Windows.Checks/HangLater.Windows.Checks.csproj -c Release
dotnet build Windows/HangLater.Windows/HangLater.Windows.csproj -c Release
```

---

## 简体中文

Hang Later Windows 版完整保留 v0.10.0 的工作流：晾衣绳纸片、待办清单、可折叠的完成记录、完成钉、想法、提醒、任务类型、工作日规则、浏览器截取、窗口截取、批量操作和纯本地存储。

### Windows 呼出方式

- **系统托盘：**左键单击图钉即可显示或收起晾衣绳。右键菜单提供「打开 Hang Later」「挂一个想法」「挂一个提醒」「设置」和「退出」。
- **全局快捷键：**默认是 `Ctrl+Alt+L`。可以在设置中改成带 Ctrl、Alt、Shift 或 Win 的其他组合。
- **屏幕顶边悬停：**这是可选功能。鼠标停在屏幕顶边 3 像素高的区域 0.5 秒后呼出。用户可以选择整个顶边、左侧三分之一、中间三分之一或右侧三分之一。绳子会显示在鼠标所在的显示器上。

3 像素高度和 0.5 秒停留时间固定不变，用来避免鼠标经过浏览器标签页或窗口按钮时误触。

### Windows 特有行为

- 关闭主窗口后，它会从任务栏消失，但系统托盘图钉会让 Hang Later 继续在后台运行。
- Word、PowerPoint、Excel、CSV、PDF 和图片使用 Windows 资源管理器提供的系统缩略图。
- 可以在「晾一件事」中选择当前可见的 App 窗口。Hang Later 会保存本地预览，之后重新打开或唤醒对应程序。
- 提醒通过 Windows 系统托盘通知显示；在线会议链接可以从纸片直接打开。
- 任务与预览保存在 `%APPDATA%\HangLater`，没有账号、统计或云同步。
- Chrome 与 Edge 共用 Manifest V3 扩展，并在当前 Windows 用户下注册 Native Messaging。

### 构建与打包

安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。如需生成安装程序，再安装 Inno Setup 6。然后在 PowerShell 中运行：

```powershell
./scripts/build-windows.ps1 -Runtime win-x64
```

脚本会运行 Windows 核心检查、发布自带运行时的程序、生成 `HangLater-Windows-win-x64.zip`，并在找到 Inno Setup 时生成 `HangLater-Windows-x64-Setup.exe`。
