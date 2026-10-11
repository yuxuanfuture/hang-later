import AppKit
import ScreenCaptureKit
import SwiftUI

struct CapturableWindow: Identifiable {
    let window: SCWindow
    let appName: String
    let bundleIdentifier: String
    let icon: NSImage

    var id: CGWindowID { window.windowID }
    var title: String {
        let clean = window.title?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return clean.isEmpty ? appName : clean
    }
}

enum WindowCaptureService {
    enum CaptureError: LocalizedError {
        case permissionDenied

        var errorDescription: String? {
            switch self {
            case .permissionDenied:
                return "Screen Recording permission is not active for this build of Hang Later."
            }
        }
    }

    struct Result {
        let title: String
        let appName: String
        let bundleIdentifier: String
        let pngData: Data
    }

    static func availableWindows() async throws -> [CapturableWindow] {
        guard CGPreflightScreenCaptureAccess() || CGRequestScreenCaptureAccess() else {
            throw CaptureError.permissionDenied
        }
        let content = try await SCShareableContent.excludingDesktopWindows(true, onScreenWindowsOnly: true)
        let ownBundle = Bundle.main.bundleIdentifier ?? "app.hanglater.mac"
        return content.windows.compactMap { window in
            guard window.windowLayer == 0, window.frame.width >= 180, window.frame.height >= 120,
                  let app = window.owningApplication, app.bundleIdentifier != ownBundle,
                  app.bundleIdentifier != "com.apple.dock", app.bundleIdentifier != "com.apple.WindowManager" else { return nil }
            let icon: NSImage
            if let appURL = NSWorkspace.shared.urlForApplication(withBundleIdentifier: app.bundleIdentifier) {
                icon = NSWorkspace.shared.icon(forFile: appURL.path)
            } else { icon = NSImage(systemSymbolName: "macwindow", accessibilityDescription: nil) ?? NSImage() }
            return CapturableWindow(window: window, appName: app.applicationName,
                                    bundleIdentifier: app.bundleIdentifier, icon: icon)
        }
        .sorted {
            if $0.window.isActive != $1.window.isActive { return $0.window.isActive }
            return $0.appName.localizedCaseInsensitiveCompare($1.appName) == .orderedAscending
        }
    }

    static func capture(_ item: CapturableWindow) async throws -> Result {
        let filter = SCContentFilter(desktopIndependentWindow: item.window)
        let configuration = SCStreamConfiguration()
        let scale = min(2, 1600 / max(item.window.frame.width, 1))
        configuration.width = Int(max(1, item.window.frame.width * scale))
        configuration.height = Int(max(1, item.window.frame.height * scale))
        configuration.scalesToFit = true
        configuration.preservesAspectRatio = true
        configuration.captureResolution = .best
        configuration.showsCursor = false
        configuration.ignoreShadowsSingleWindow = false
        let image = try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: configuration)
        let representation = NSBitmapImageRep(cgImage: image)
        guard let data = representation.representation(using: .png, properties: [:]) else {
            throw CocoaError(.fileWriteUnknown)
        }
        return Result(title: item.title, appName: item.appName,
                      bundleIdentifier: item.bundleIdentifier, pngData: data)
    }

    static func captureBestWindow(bundleIdentifier: String) async throws -> Result? {
        let windows = try await availableWindows().filter { $0.bundleIdentifier == bundleIdentifier }
        guard let best = windows.max(by: { $0.window.frame.width * $0.window.frame.height < $1.window.frame.width * $1.window.frame.height }) else { return nil }
        return try await capture(best)
    }
}

@MainActor
final class WindowCaptureModel: ObservableObject {
    @Published var windows: [CapturableWindow] = []
    @Published var loading = true
    @Published var capturing: CGWindowID?
    @Published var failure: String?

    func load(language: AppLanguage) async {
        loading = true; failure = nil
        do { windows = try await WindowCaptureService.availableWindows() }
        catch {
            failure = language.text("无法读取窗口。请在“系统设置 → 隐私与安全性 → 屏幕录制”中允许 Hang Later，然后再试。",
                                    "Windows could not be read. Allow Hang Later in System Settings → Privacy & Security → Screen Recording, then try again.")
        }
        loading = false
    }
}

struct WindowCapturePicker: View {
    @ObservedObject var store: AppStore
    @Environment(\.dismiss) private var dismiss
    @StateObject private var model = WindowCaptureModel()

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 7) {
                    Text(store.text("晾起一个 App 窗口。", "Hang an app window."))
                        .font(LeLaboType.display(24))
                        .textCase(.uppercase)
                        .tracking(0.65)
                    Text(store.text("选择窗口后，会保存当前画面；点纸片会重新唤起这个 App。",
                                    "Choose a window to save its current view. Clicking the card brings the app back."))
                        .font(LeLaboType.copy(11)).foregroundStyle(Palette.muted)
                }
                Spacer()
                Button { dismiss() } label: { Image(systemName: "xmark") }.buttonStyle(.plain)
            }

            if model.loading {
                VStack(spacing: 12) {
                    ProgressView()
                    Text(store.text("正在读取可见窗口…", "Finding visible windows…"))
                        .font(LeLaboType.copy(11)).foregroundStyle(Palette.muted)
                }.frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if let failure = model.failure {
                VStack(spacing: 14) {
                    Image(systemName: "rectangle.dashed.badge.record").font(.system(size: 30)).foregroundStyle(Palette.danger)
                    Text(failure).font(LeLaboType.copy(11)).multilineTextAlignment(.center).frame(maxWidth: 390)
                    HStack {
                        Button(store.text("打开屏幕录制设置", "Open Screen Recording Settings")) { openPrivacySettings() }.buttonStyle(QuietButton())
                        Button(store.text("重试", "Try Again")) { Task { await model.load(language: store.language) } }.buttonStyle(GreenButton())
                    }
                }.frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if model.windows.isEmpty {
                VStack(spacing: 12) {
                    Image(systemName: "macwindow").font(.system(size: 30)).foregroundStyle(Palette.muted)
                    Text(store.text("没有找到可晾起的窗口。先打开一个 App 再试。",
                                    "No available windows were found. Open an app and try again."))
                        .font(LeLaboType.copy(11)).foregroundStyle(Palette.muted)
                }.frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVStack(spacing: 9) {
                        ForEach(model.windows) { item in
                            Button { capture(item) } label: {
                                HStack(spacing: 13) {
                                    Image(nsImage: item.icon).resizable().scaledToFit().frame(width: 34, height: 34)
                                    VStack(alignment: .leading, spacing: 5) {
                                        Text(item.title).font(LeLaboType.display(13)).tracking(0.4).lineLimit(1)
                                        Text("\(item.appName) · \(Int(item.window.frame.width)) × \(Int(item.window.frame.height))")
                                            .font(LeLaboType.label(9)).foregroundStyle(Palette.muted)
                                    }
                                    Spacer()
                                    if model.capturing == item.id { ProgressView().controlSize(.small) }
                                    else { Image(systemName: "camera").foregroundStyle(Palette.green) }
                                }
                                .padding(13).background(Palette.raisedSurface, in: Rectangle())
                                .overlay(Rectangle().stroke(Palette.border, lineWidth: 1))
                            }.buttonStyle(.plain).disabled(model.capturing != nil)
                        }
                    }
                }
            }
        }
        .padding(28).frame(width: 560, height: 560)
        .background(Palette.paper).foregroundStyle(Palette.ink)
        .font(LeLaboType.copy(12))
        .preferredColorScheme(store.appearance.colorScheme)
        .tint(Palette.green)
        .task { await model.load(language: store.language) }
    }

    private func capture(_ item: CapturableWindow) {
        model.capturing = item.id; model.failure = nil
        Task {
            do {
                let result = try await WindowCaptureService.capture(item)
                if store.importAppWindow(title: result.title, appName: result.appName,
                                         bundleIdentifier: result.bundleIdentifier, screenshot: result.pngData) {
                    dismiss()
                }
            } catch {
                model.failure = store.text("没有截到这个窗口。检查屏幕录制权限后再试。",
                                           "This window could not be captured. Check Screen Recording permission and try again.")
            }
            model.capturing = nil
        }
    }

    private func openPrivacySettings() {
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture") {
            NSWorkspace.shared.open(url)
        }
    }
}
