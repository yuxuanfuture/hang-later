import AppKit
import HangLaterCore
import SwiftUI

enum BrowserBridge {
    static let hostName = "app.hanglater.capture"
    // Matches the public key in ChromeExtension/manifest.json.
    static let extensionID = "ahkgcekpbdbdkgmkfefnlpacglodpiji"
    static var hostDirectory: URL {
        FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Application Support/Google/Chrome/NativeMessagingHosts", isDirectory: true)
    }
    static var manifestURL: URL { hostDirectory.appendingPathComponent(hostName + ".json") }
    static func executableURL(dataDirectory: URL) -> URL {
        dataDirectory.appendingPathComponent("BrowserBridge/capture-host.py")
    }
    static func isInstalled(dataDirectory: URL) -> Bool {
        guard let data = try? Data(contentsOf: manifestURL) else { return false }
        return BrowserBridgeManifest.matches(data, expectedExecutable: executableURL(dataDirectory: dataDirectory),
                                             extensionID: extensionID)
    }
    static var extensionDirectory: URL? { Bundle.main.resourceURL?.appendingPathComponent("ChromeExtension", isDirectory: true) }

    static func install(dataDirectory: URL) throws {
        guard let resources = Bundle.main.resourceURL else { throw NSError(domain: hostName, code: 1, userInfo: [NSLocalizedDescriptionKey: "请运行打包后的 HangLater.app。"] ) }
        let source = resources.appendingPathComponent("NativeHost/capture-host.py")
        guard FileManager.default.fileExists(atPath: source.path) else {
            throw NSError(domain: hostName, code: 2, userInfo: [NSLocalizedDescriptionKey: "未找到浏览器桥接程序，请重新构建应用。"])
        }
        let bridgeDirectory = dataDirectory.appendingPathComponent("BrowserBridge", isDirectory: true)
        try FileManager.default.createDirectory(at: bridgeDirectory, withIntermediateDirectories: true)
        let destination = bridgeDirectory.appendingPathComponent("capture-host.py")
        // Atomic replacement keeps an in-flight Chrome invocation safe.
        try Data(contentsOf: source).write(to: destination, options: .atomic)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: destination.path)
        try FileManager.default.createDirectory(at: hostDirectory, withIntermediateDirectories: true)
        let manifest: [String: Any] = ["name": hostName, "description": "HangLater page capture",
                                      "path": destination.path, "type": "stdio",
                                      "allowed_origins": ["chrome-extension://\(extensionID)/"]]
        try JSONSerialization.data(withJSONObject: manifest, options: [.prettyPrinted, .sortedKeys])
            .write(to: manifestURL, options: .atomic)
    }

    /// Repairs an existing bridge that points at an older or demo data directory.
    /// Pending captures are copied first, so pages already reported as saved are recovered.
    @discardableResult
    static func repairExistingConnection(dataDirectory: URL) throws -> Int {
        guard FileManager.default.fileExists(atPath: manifestURL.path),
              let manifestData = try? Data(contentsOf: manifestURL) else { return 0 }
        if BrowserBridgeManifest.matches(manifestData,
                                         expectedExecutable: executableURL(dataDirectory: dataDirectory),
                                         extensionID: extensionID) { return 0 }

        var recovered = 0
        if let oldExecutable = BrowserBridgeManifest.executableURL(in: manifestData),
           oldExecutable.lastPathComponent == "capture-host.py",
           oldExecutable.deletingLastPathComponent().lastPathComponent == "BrowserBridge" {
            let oldInbox = oldExecutable.deletingLastPathComponent().deletingLastPathComponent()
                .appendingPathComponent("Inbox", isDirectory: true)
            let newInbox = dataDirectory.appendingPathComponent("Inbox", isDirectory: true)
            try FileManager.default.createDirectory(at: newInbox, withIntermediateDirectories: true)
            let files = (try? FileManager.default.contentsOfDirectory(at: oldInbox,
                                                                       includingPropertiesForKeys: [.fileSizeKey])) ?? []
            for source in files where source.pathExtension == "json" {
                let values = try? source.resourceValues(forKeys: [.fileSizeKey])
                guard (values?.fileSize ?? Int.max) <= 16 * 1024 * 1024 else { continue }
                var destination = newInbox.appendingPathComponent(source.lastPathComponent)
                if FileManager.default.fileExists(atPath: destination.path) {
                    guard (try? Data(contentsOf: source)) != (try? Data(contentsOf: destination)) else { continue }
                    destination = newInbox.appendingPathComponent(UUID().uuidString + ".json")
                }
                try FileManager.default.copyItem(at: source, to: destination)
                recovered += 1
            }
        }
        try install(dataDirectory: dataDirectory)
        return recovered
    }

    static func openExtensions() {
        guard let url = URL(string: "chrome://extensions"),
              let chrome = NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.google.Chrome") else { return }
        NSWorkspace.shared.open([url], withApplicationAt: chrome, configuration: NSWorkspace.OpenConfiguration())
    }
}

struct BrowserGuide: View {
    @ObservedObject var store: AppStore
    @Environment(\.dismiss) private var dismiss
    @State private var installed = false
    @State private var installationError: String?
    var body: some View {
        VStack(alignment: .leading, spacing: 22) {
            HStack {
                Text(store.text("把当前页，晾起来。", "Hang the current page.")) .font(LeLaboType.display(24)).textCase(.uppercase).tracking(0.65)
                Spacer()
                Button { dismiss() } label: { Image(systemName: "xmark") }.buttonStyle(.plain).keyboardShortcut(.cancelAction)
            }
            Text(store.text("Chrome 扩展会保存当前标签页的链接、标题和画面；视频只截取播放器。下次点卡片，直接回到网页。",
                            "The Chrome extension saves the current tab’s link, title, and view. For video, it crops to the player. Click the card to return."))
                .font(LeLaboType.copy(11)).lineSpacing(5).foregroundStyle(Palette.muted)
            step("01", title: store.text("连接这台 Mac", "Connect this Mac"), detail: store.text("安装本地桥接程序，网页会直接送到 Hang Later。", "Install the local bridge so pages go straight to Hang Later.")) {
                Button(installed ? store.text("已连接 · 重新安装", "Connected · Reinstall") : store.text("连接 Chrome", "Connect Chrome")) {
                    do {
                        try BrowserBridge.install(dataDirectory: store.directory)
                        installed = BrowserBridge.isInstalled(dataDirectory: store.directory)
                        installationError = nil
                    }
                    catch { installationError = error.localizedDescription }
                }.buttonStyle(QuietButton())
            }
            step("02", title: store.text("在 Chrome 加载扩展", "Load the extension in Chrome"), detail: store.text("打开「开发者模式」→「加载已解压的扩展程序」，选择 ChromeExtension 文件夹。已经加载过时，点卡片上的“重新加载”以启用视频裁切。", "Turn on Developer mode → Load unpacked, then choose the ChromeExtension folder. If it is already loaded, click Reload on its card to enable video cropping.")) {
                HStack {
                    Button(store.text("打开扩展管理", "Open Extensions")) { BrowserBridge.openExtensions() }.buttonStyle(QuietButton())
                    Button(store.text("显示扩展文件夹", "Show Extension Folder")) {
                        if let directory = BrowserBridge.extensionDirectory { NSWorkspace.shared.activateFileViewerSelecting([directory]) }
                    }.buttonStyle(QuietButton())
                    Button(store.text("复制路径", "Copy Path")) {
                        if let path = BrowserBridge.extensionDirectory?.path {
                            NSPasteboard.general.clearContents(); NSPasteboard.general.setString(path, forType: .string)
                        }
                    }.buttonStyle(QuietButton())
                }
            }
            step("03", title: store.text("把扩展固定到工具栏", "Pin the extension to the toolbar"), detail: store.text("在想稍后看的网页上，点一下「Hang Later」图标。也可以右键链接，选择「晾到待办绳上」。", "On a page you want to revisit, click the Hang Later icon. You can also right-click a link and hang it.")) { EmptyView() }
            if let installationError { Text(installationError).font(LeLaboType.copy(11, bold: true)).foregroundStyle(Palette.danger) }
            Text(store.text("只在你点击时保存当前页。截图和任务存到本机；不会自动读取浏览记录。拖入链接则无需安装扩展。",
                            "A page is saved only when you click. Screenshots and tasks stay on this Mac; browsing history is never read automatically."))
                .font(LeLaboType.copy(10)).lineSpacing(4).foregroundStyle(Palette.muted)
            HStack { Spacer(); Button(store.text("知道了", "Done")) { dismiss() }.buttonStyle(GreenButton()) }
        }.padding(30).frame(width: 585).background(Palette.paper).foregroundStyle(Palette.ink)
            .font(LeLaboType.copy(12))
            .preferredColorScheme(store.appearance.colorScheme)
            .tint(Palette.green)
            .onAppear { installed = BrowserBridge.isInstalled(dataDirectory: store.directory) }
    }

    private func step<Content: View>(_ number: String, title: String, detail: String, @ViewBuilder content: () -> Content) -> some View {
        HStack(alignment: .top, spacing: 17) {
            Text(number).font(LeLaboType.label(12, bold: true)).foregroundStyle(Palette.green).padding(.top, 2)
            VStack(alignment: .leading, spacing: 9) {
                Text(title).font(LeLaboType.display(14)).tracking(0.4)
                Text(detail).font(LeLaboType.copy(10)).lineSpacing(4).foregroundStyle(Palette.muted)
                content()
            }
        }
    }
}
