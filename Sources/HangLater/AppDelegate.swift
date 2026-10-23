import AppKit
import SwiftUI
import Combine
import HangLaterCore
import UserNotifications
import UniformTypeIdentifiers

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate, UNUserNotificationCenterDelegate {
    private struct ExternalApplicationDrag {
        let bundleIdentifier: String
        let window: ExternalWindowSnapshot
        let startedAt: Date
        let startPoint: CGPoint
        var maximumDistance: CGFloat = 0
        var dropTargetEnteredAt: Date?
    }

    private var store: AppStore!
    private var window: NSWindow!
    private var panel: DesktopPanel!
    private var statusItem: NSStatusItem!
    private var ideaPopover: NSPopover?
    private var reminderPopover: NSPopover?
    private var hotKey: HotKey?
    private let desktop = DesktopState()
    private var mouseTimer: Timer?
    private var revealUntil = Date.distantPast
    private var hoverSince: Date?
    private var awaySince: Date?
    private var cancelledHover = false
    private var wasPointerDragging = false
    private var activeExternalDrag: ExternalApplicationDrag?
    private var receiveDropsUntil = Date.distantPast
    private var lastContentDropAt = Date.distantPast
    private var cancellables: Set<AnyCancellable> = []

    func applicationDidFinishLaunching(_ notification: Notification) {
        // The main window starts as a normal Mac app, then returns to menu-bar-only mode when closed.
        NSApp.setActivationPolicy(.regular)
        let arguments = ProcessInfo.processInfo.arguments
        let demo = arguments.contains("--demo")
        let directory: URL
        if demo {
            directory = FileManager.default.temporaryDirectory.appendingPathComponent("hanglater-demo")
        } else {
            directory = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
                .appendingPathComponent("HangLater", isDirectory: true)
        }
        var repairedCaptures = 0
        var bridgeRepairError: Error?
        if !demo {
            do { repairedCaptures = try BrowserBridge.repairExistingConnection(dataDirectory: directory) }
            catch { bridgeRepairError = error }
        }
        store = AppStore(directory: directory)
        UNUserNotificationCenter.current().delegate = self
        if repairedCaptures > 0 { store.notify(store.text("已找回之前晾好的 \(repairedCaptures) 个网页。", "Recovered \(repairedCaptures) previously captured pages.")) }
        if let bridgeRepairError { store.error = store.text("Chrome 连接需要重新安装：", "The Chrome connection needs to be reinstalled: ") + bridgeRepairError.localizedDescription }
        if demo && store.tasks.isEmpty { seedDemo() }
        store.showDesktopLine = { [weak self] in self?.revealLine() }
        store.showMainWindow = { [weak self] in self?.showWindow() }
        store.onExternalDropAccepted = { [weak self] in self?.lastContentDropAt = Date() }
        let host = NSHostingView(rootView: MainView(store: store))
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1160, height: 760),
                          styleMask: [.titled, .closable, .miniaturizable, .resizable, .fullSizeContentView], backing: .buffered, defer: false)
        window.title = "Hang Later"; window.titleVisibility = .hidden; window.titlebarAppearsTransparent = true
        window.isReleasedWhenClosed = false; window.delegate = self
        window.contentView = host; window.minSize = NSSize(width: 1000, height: 700)
        window.center(); window.setFrameAutosaveName(demo ? "HangLaterDemoWindow" : "HangLaterMainWindow")

        let panelHost = NSHostingView(rootView: DesktopLineView(store: store, state: desktop))
        panelHost.sizingOptions = []
        panel = DesktopPanel(content: panelHost)
        if let screen = NSScreen.main {
            panel.place(on: screen, verticalPosition: store.desktopLineVerticalPosition)
        }
        panel.orderFrontRegardless()
        store.desktopLinePreferencesChanged = { [weak self] keepVisible, verticalPosition in
            self?.applyDesktopLinePreferences(keepVisible: keepVisible,
                                              verticalPosition: verticalPosition)
        }
        setupMenu()
        store.$language.dropFirst().sink { [weak self] _ in
            guard let self else { return }
            self.window.title = "Hang Later"
            self.setupMenu(); self.setupApplicationMenu()
        }.store(in: &cancellables)
        hotKey = HotKey { [weak self] in
            if self?.desktop.revealed == true {
                self?.desktop.setRevealed(false); self?.revealUntil = .distantPast; self?.cancelledHover = true
                if self?.store.pinned == true {
                    self?.store.saveDesktopLinePreferences(
                        keepVisible: false,
                        verticalPosition: self?.store.desktopLineVerticalPosition ?? 0
                    )
                }
            } else { self?.revealLine() }
        }
        if hotKey == nil { store.notify(store.text("⌃⌥L 已被占用，可用菜单栏图标呼出晾衣绳。", "⌃⌥L is already in use; use the menu bar icon to show the clothesline.")) }
        mouseTimer = Timer.scheduledTimer(withTimeInterval: 0.08, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.trackMouse() }
        }
        setupApplicationMenu()
        showWindow()
        if arguments.contains("--verify-background-close") {
            verifyBackgroundCloseBehavior()
            return
        }
        requestStableScreenCapturePermissionIfNeeded()
        if store.pinned { revealLine() }
    }

    private func requestStableScreenCapturePermissionIfNeeded() {
        let migrationKey = "requestedStableScreenCapturePermissionV1"
        guard !CGPreflightScreenCaptureAccess(), !UserDefaults.standard.bool(forKey: migrationKey) else { return }
        UserDefaults.standard.set(true, forKey: migrationKey)
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) {
            _ = CGRequestScreenCaptureAccess()
        }
    }

    private func showWindow() {
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
        if window.isMiniaturized { window.deminiaturize(nil) }
        window.makeKeyAndOrderFront(nil)
    }

    private func moveToBackground() {
        NSApp.setActivationPolicy(.accessory)
        NSApp.deactivate()
    }

    func windowWillClose(_ notification: Notification) {
        guard let closingWindow = notification.object as? NSWindow,
              closingWindow === window else { return }
        moveToBackground()
    }

    private func verifyBackgroundCloseBehavior() {
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) { [weak self] in
            guard let self else { return }
            self.window.performClose(nil)
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) {
                let backgroundValid = !self.window.isVisible
                    && NSApp.activationPolicy() == .accessory
                    && self.statusItem.button != nil
                self.showWindow()
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) {
                    let reopenValid = self.window.isVisible
                        && NSApp.activationPolicy() == .regular
                    print(backgroundValid && reopenValid
                          ? "PASS: close keeps only the menu-bar pin and reopen restores the app window"
                          : "FAIL: background close and reopen behavior is inconsistent")
                    NSApp.terminate(nil)
                }
            }
        }
    }
    private func revealLine() {
        let pointer = NSEvent.mouseLocation
        if let screen = NSScreen.screens.first(where: { $0.frame.contains(pointer) }) ?? NSScreen.main {
            panel.place(on: screen, verticalPosition: store.desktopLineVerticalPosition)
        }
        revealUntil = Date().addingTimeInterval(5); cancelledHover = false
        desktop.setRevealed(true); panel.orderFrontRegardless()
    }

    private func applyDesktopLinePreferences(keepVisible: Bool, verticalPosition: Double) {
        let pointer = NSEvent.mouseLocation
        let screen = panel.screen
            ?? NSScreen.screens.first(where: { $0.frame.contains(pointer) })
            ?? NSScreen.main
        if let screen { panel.place(on: screen, verticalPosition: verticalPosition) }
        if keepVisible {
            revealUntil = .distantFuture
            desktop.setRevealed(true)
            panel.orderFrontRegardless()
        } else {
            revealUntil = .distantPast
            desktop.setRevealed(false)
        }
    }

    private func trackMouse() {
        let now = Date(); let point = NSEvent.mouseLocation
        guard let screen = NSScreen.screens.first(where: { $0.frame.contains(point) }) else { return }
        let onStatusPin = isPointerOnStatusPin(point)
        let inPanel = panel.frame.contains(point)
        let dragging = NSEvent.pressedMouseButtons & 1 == 1
        if dragging && !wasPointerDragging,
           let application = NSWorkspace.shared.frontmostApplication,
           let identifier = application.bundleIdentifier,
           identifier != Bundle.main.bundleIdentifier,
           identifier != "com.apple.dock", identifier != "com.apple.WindowManager",
           let window = ExternalWindowPosition.snapshot(
            at: point, processIdentifier: application.processIdentifier
           ) {
            activeExternalDrag = ExternalApplicationDrag(bundleIdentifier: identifier, window: window,
                                                          startedAt: now, startPoint: point)
        }
        if onStatusPin && !cancelledHover {
            if hoverSince == nil { hoverSince = now }
            if dragging || now.timeIntervalSince(hoverSince!) > 0.45 {
                panel.place(on: screen, verticalPosition: store.desktopLineVerticalPosition)
                desktop.setRevealed(true)
            }
        } else if !onStatusPin { hoverSince = nil; cancelledHover = false }

        if store.pinned { desktop.setRevealed(true) }
        if !store.pinned && now > revealUntil && !onStatusPin && !inPanel && !store.dragTargeted {
            if awaySince == nil { awaySince = now }
            if now.timeIntervalSince(awaySince!) > 0.65 { desktop.setRevealed(false) }
        } else { awaySince = nil }
        let local = CGPoint(x: point.x - panel.frame.minX, y: panel.frame.maxY - point.y)
        let insideDropTarget = desktop.dropTargetRect.contains(local)
        let nearDropTarget = desktop.dropTargetRect.insetBy(dx: -18, dy: -12).contains(local)
        let onControl = nearDropTarget || desktop.hitRects.contains { $0.insetBy(dx: -8, dy: -6).contains(local) }
        if dragging, var candidate = activeExternalDrag {
            candidate.maximumDistance = max(candidate.maximumDistance,
                                            hypot(point.x - candidate.startPoint.x,
                                                  point.y - candidate.startPoint.y))
            if desktop.revealed && insideDropTarget {
                if candidate.dropTargetEnteredAt == nil { candidate.dropTargetEnteredAt = now }
            } else {
                candidate.dropTargetEnteredAt = nil
            }
            activeExternalDrag = candidate
        }
        let supportedContentDrag = dragging && Self.dragPasteboardContainsSupportedContent()
        let externalDragActive = dragging && (supportedContentDrag || activeExternalDrag != nil)
        desktop.setExternalDragActive(desktop.revealed && externalDragActive)
        desktop.setExternalDragTargeted(desktop.revealed && externalDragActive && insideDropTarget)
        if dragging && inPanel { receiveDropsUntil = now.addingTimeInterval(0.5) }
        if wasPointerDragging && !dragging {
            receiveDropsUntil = now.addingTimeInterval(0.5)
            if let candidate = activeExternalDrag,
               WindowDropIntent.qualifies(
                maximumDistance: Double(candidate.maximumDistance),
                duration: now.timeIntervalSince(candidate.startedAt),
                targetDwell: candidate.dropTargetEnteredAt.map { now.timeIntervalSince($0) },
                isInsideTarget: desktop.revealed && insideDropTarget
               ) {
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) { [weak self] in
                    self?.captureWindowDropIfNeeded(candidate)
                }
            }
            activeExternalDrag = nil
            desktop.setExternalDragActive(false)
            desktop.setExternalDragTargeted(false)
        }
        wasPointerDragging = dragging
        // Clear parts of the transparent panel let the desktop receive clicks.
        panel.ignoresMouseEvents = !desktop.revealed
            || !(onControl || (dragging && inPanel) || now < receiveDropsUntil)
    }

    private func isPointerOnStatusPin(_ point: CGPoint) -> Bool {
        guard let button = statusItem.button, let statusWindow = button.window else { return false }
        let buttonInWindow = button.convert(button.bounds, to: nil)
        let buttonOnScreen = statusWindow.convertToScreen(buttonInWindow)
        return buttonOnScreen.insetBy(dx: -3, dy: -2).contains(point)
    }

    private static func dragPasteboardContainsSupportedContent() -> Bool {
        NSPasteboard(name: .drag).types?.contains {
            DropSupport.accepts(typeIdentifier: $0.rawValue)
        } == true
    }

    private func captureWindowDropIfNeeded(_ candidate: ExternalApplicationDrag) {
        // A real pasteboard drop (file, URL, image or app icon) handles itself.
        guard lastContentDropAt < candidate.startedAt else { return }
        restoreExternalWindow(candidate.window)
        Task {
            do {
                guard let captured = try await WindowCaptureService.captureBestWindow(
                    bundleIdentifier: candidate.bundleIdentifier,
                    preferredWindowID: candidate.window.windowID
                ) else {
                    store.error = store.text("没有找到这个 App 的可见窗口。", "No visible window was found for this app.")
                    return
                }
                _ = store.importAppWindow(title: captured.title, appName: captured.appName,
                                          bundleIdentifier: captured.bundleIdentifier,
                                          screenshot: captured.pngData)
            } catch {
                if error is WindowCaptureService.CaptureError {
                    store.screenCapturePermissionNeeded = true
                    store.error = store.text(
                        "当前版本还没有生效的“屏幕与系统录音”权限。请在系统设置中允许 Hang Later，然后重新打开 App。",
                        "Screen Recording permission is not active for this build. Allow Hang Later in System Settings, then reopen the app."
                    )
                } else {
                    store.error = store.text("没有截到这个 App 窗口：", "The app window could not be captured: ")
                        + error.localizedDescription
                }
            }
        }
    }

    private func restoreExternalWindow(_ snapshot: ExternalWindowSnapshot) {
        switch ExternalWindowPosition.restore(snapshot) {
        case .restored:
            break
        case .permissionRequired:
            store.notify(store.text(
                "请允许 Hang Later 使用“辅助功能”；授权后这个窗口会自动回到原位。",
                "Allow Hang Later in Accessibility; this window will return automatically after access is granted."
            ))
            retryWindowRestoration(snapshot, attemptsRemaining: 120)
        case .unavailable:
            store.notify(store.text(
                "这个 App 没有允许 Hang Later 恢复窗口位置。",
                "This app did not allow Hang Later to restore its window position."
            ))
        }
    }

    private func retryWindowRestoration(_ snapshot: ExternalWindowSnapshot, attemptsRemaining: Int) {
        guard attemptsRemaining > 0 else { return }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) { [weak self] in
            guard let self else { return }
            switch ExternalWindowPosition.restore(snapshot, promptForPermission: false) {
            case .restored:
                self.store.notify(self.store.text("窗口已回到拖动前的位置。",
                                                  "The window returned to its original position."))
            case .permissionRequired:
                self.retryWindowRestoration(snapshot, attemptsRemaining: attemptsRemaining - 1)
            case .unavailable:
                break
            }
        }
    }

    private func setupMenu() {
        if statusItem == nil { statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength) }
        statusItem.button?.image = NSImage(systemSymbolName: "pin", accessibilityDescription: "Hang Later")
        let menu = NSMenu()
        for (title, action) in [(store.text("打开 Hang Later", "Open Hang Later"), #selector(openMain)),
                                (store.text("显示晾衣绳  ⌃⌥L", "Show Clothesline  ⌃⌥L"), #selector(openLine)),
                                (store.text("晾一个想法…", "Hang an Idea…"), #selector(captureIdea)),
                                (store.text("挂一个提醒…", "Hang a Reminder…"), #selector(captureReminder))] {
            let item = NSMenuItem(title: title, action: action, keyEquivalent: ""); item.target = self; menu.addItem(item)
        }
        menu.addItem(.separator())
        let quit = NSMenuItem(title: store.text("退出 Hang Later", "Quit Hang Later"), action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        menu.addItem(quit); statusItem.menu = menu
    }

    private func setupApplicationMenu() {
        let main = NSMenu(); let appItem = NSMenuItem(); let appMenu = NSMenu()
        let settings = NSMenuItem(title: store.text("设置…", "Settings…"), action: #selector(openSettings), keyEquivalent: ",")
        settings.target = self; appMenu.addItem(settings); appMenu.addItem(.separator())
        appMenu.addItem(withTitle: store.text("退出 Hang Later", "Quit Hang Later"), action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        appItem.submenu = appMenu; main.addItem(appItem)
        let editItem = NSMenuItem(); editItem.title = store.text("编辑", "Edit"); let editMenu = NSMenu(title: store.text("编辑", "Edit"))
        editMenu.addItem(withTitle: store.text("撤销", "Undo"), action: Selector(("undo:")), keyEquivalent: "z")
        editMenu.addItem(withTitle: store.text("剪切", "Cut"), action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        editMenu.addItem(withTitle: store.text("复制", "Copy"), action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        editMenu.addItem(withTitle: store.text("粘贴", "Paste"), action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        editMenu.addItem(withTitle: store.text("全选", "Select All"), action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        editItem.submenu = editMenu; main.addItem(editItem); NSApp.mainMenu = main
    }

    @objc private func openMain() { showWindow() }
    @objc private func openLine() { revealLine() }
    @objc private func captureIdea() {
        guard let button = statusItem.button else { return }
        ideaPopover?.performClose(nil)
        let popover = NSPopover()
        popover.behavior = .transient
        popover.animates = true
        popover.contentSize = NSSize(width: 390, height: 154)
        popover.contentViewController = NSHostingController(rootView: IdeaCaptureView(
            store: store,
            onHang: { [weak self] idea in self?.store.addIdea(idea) ?? false },
            onCancel: { [weak self] in self?.ideaPopover?.performClose(nil) }
        ))
        ideaPopover = popover
        DispatchQueue.main.async {
            popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
        }
    }
    @objc private func captureReminder() {
        guard let button = statusItem.button else { return }
        reminderPopover?.performClose(nil)
        let popover = NSPopover()
        popover.behavior = .transient
        popover.animates = true
        popover.contentSize = NSSize(width: 440, height: 286)
        popover.contentViewController = NSHostingController(rootView: ReminderCaptureView(
            store: store,
            onHang: { [weak self] title, link, reminderAt in
                self?.store.addReminder(title: title, url: link, reminderAt: reminderAt) ?? false
            },
            onCancel: { [weak self] in self?.reminderPopover?.performClose(nil) }
        ))
        reminderPopover = popover
        DispatchQueue.main.async {
            popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
        }
    }
    @objc private func openSettings() { showWindow(); store.showingSettings = true }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        showWindow()
        return true
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }

    nonisolated func userNotificationCenter(_ center: UNUserNotificationCenter,
                                             willPresent notification: UNNotification,
                                             withCompletionHandler completionHandler:
                                                 @escaping (UNNotificationPresentationOptions) -> Void) {
        completionHandler([.banner, .sound])
    }

    nonisolated func userNotificationCenter(_ center: UNUserNotificationCenter,
                                             didReceive response: UNNotificationResponse,
                                             withCompletionHandler completionHandler: @escaping () -> Void) {
        let rawID = response.notification.request.content.userInfo["taskID"] as? String
        Task { @MainActor [weak self] in
            if let rawID, let id = UUID(uuidString: rawID),
               let task = self?.store.tasks.first(where: { $0.id == id }) {
                self?.store.open(task)
            }
            completionHandler()
        }
    }

    private func seedDemo() {
        // Only used with --demo, in a separate temporary data directory.
        _ = store.perform { repository in
            _ = try repository.add(title: "继续看：理解 Transformer 的注意力机制", url: "https://www.youtube.com/watch?v=wjZofJX0v4M", note: "看到 12:30，继续做笔记。")
            _ = try repository.add(title: "读完这篇关于好奇心的文章", url: "https://paulgraham.com/curiosity.html", note: "留一点完整的时间给阅读。")
            _ = try repository.add(title: "整理这周的学习笔记", note: "把零散的想法\n整理成自己的语言。")
            let done = try repository.add(title: "给阳台的绿植浇水", note: "生活的小事也值得被记住。")
            try repository.complete(done.id)
        }
    }
}
