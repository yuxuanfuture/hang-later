import AppKit
import HangLaterCore
import SwiftUI

MainActor.assumeIsolated {
    let app = NSApplication.shared
    let arguments = ProcessInfo.processInfo.arguments

    if arguments.contains("--verify-resume-targets") {
        let target = AppResumeTarget.resolve(bundleIdentifier: "md.obsidian", appName: "Obsidian",
                                             windowTitle: "Reviews - Obsidian Vault - Obsidian 1.13.7")
        let expected = "obsidian://open?vault=Obsidian%20Vault&file=Reviews"
        if target?.absoluteString == expected {
            print("PASS: Obsidian window title resolves to its vault and note")
            exit(0)
        }
        print("FAIL: unexpected target \(target?.absoluteString ?? "nil")")
        exit(2)
    }

    if arguments.contains("--verify-adaptive-card-layout") {
        let pdf = HangingCardDimensions.make(image: NSImage(size: NSSize(width: 720, height: 1018)), compact: true)
        let appWindow = HangingCardDimensions.make(image: NSImage(size: NSSize(width: 960, height: 640)), compact: true)
        let pdfRatio = pdf.width / pdf.previewHeight
        let appRatio = appWindow.width / appWindow.previewHeight
        let valid = (126...238).contains(pdf.width) && (94...180).contains(pdf.previewHeight)
            && (126...238).contains(appWindow.width) && (94...180).contains(appWindow.previewHeight)
            && abs(pdfRatio - 720 / 1018) < 0.02 && abs(appRatio - 1.5) < 0.02
        print(valid ? "PASS: card sizes preserve PDF and app preview proportions within bounds"
                    : "FAIL: adaptive card dimensions are outside the expected range")
        exit(valid ? 0 : 2)
    }

    if let activationIndex = arguments.firstIndex(of: "--verify-app-activation"),
       arguments.indices.contains(activationIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let bundleIdentifier = arguments[activationIndex + 1]
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Activation-QA-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        _ = store.add(title: "Activation QA", url: nil, note: "", dueAt: nil,
                      appBundleIdentifier: bundleIdentifier, appName: "Activation QA")
        if let task = store.tasks.first { store.open(task) }
        RunLoop.main.run(until: Date().addingTimeInterval(2))
        let active = NSWorkspace.shared.frontmostApplication?.bundleIdentifier == bundleIdentifier
        print(active ? "PASS: \(bundleIdentifier) activated" : "FAIL: \(bundleIdentifier) did not activate")
        try? FileManager.default.removeItem(at: directory)
        exit(active ? 0 : 2)
    }

    @MainActor func samplePreview(kind: String, title: String, size: NSSize) -> Data? {
        let image = NSImage(size: size)
        image.lockFocus()
        NSGraphicsContext.current?.imageInterpolation = .high

        let bounds = NSRect(origin: .zero, size: size)
        let dark = NSColor(srgbRed: 0.105, green: 0.105, blue: 0.10, alpha: 1)
        let paper = NSColor(srgbRed: 0.90, green: 0.88, blue: 0.82, alpha: 1)
        let ink = NSColor(srgbRed: 0.08, green: 0.08, blue: 0.07, alpha: 1)
        let muted = NSColor(srgbRed: 0.46, green: 0.45, blue: 0.42, alpha: 1)
        (kind == "pdf" ? paper : dark).setFill()
        bounds.fill()

        if kind == "video" {
            NSColor(srgbRed: 0.16, green: 0.17, blue: 0.16, alpha: 1).setFill()
            NSRect(x: size.width * 0.07, y: size.height * 0.09,
                   width: size.width * 0.86, height: size.height * 0.82).fill()
            let circle = NSBezierPath(ovalIn: NSRect(x: size.width / 2 - 54, y: size.height / 2 - 54,
                                                     width: 108, height: 108))
            NSColor.white.withAlphaComponent(0.16).setFill(); circle.fill()
            let play = NSBezierPath()
            play.move(to: NSPoint(x: size.width / 2 - 13, y: size.height / 2 - 27))
            play.line(to: NSPoint(x: size.width / 2 - 13, y: size.height / 2 + 27))
            play.line(to: NSPoint(x: size.width / 2 + 34, y: size.height / 2))
            play.close(); NSColor.white.withAlphaComponent(0.84).setFill(); play.fill()
        } else if kind == "app" {
            NSColor(srgbRed: 0.14, green: 0.14, blue: 0.135, alpha: 1).setFill()
            NSRect(x: 0, y: 0, width: size.width * 0.24, height: size.height).fill()
            NSColor.white.withAlphaComponent(0.28).setFill()
            for index in 0..<6 {
                NSRect(x: size.width * 0.30, y: size.height * (0.72 - CGFloat(index) * 0.095),
                       width: size.width * CGFloat(index.isMultiple(of: 2) ? 0.54 : 0.43), height: 7).fill()
            }
        } else {
            ink.setStroke()
            for y in stride(from: size.height * 0.18, through: size.height * 0.76, by: size.height * 0.12) {
                let line = NSBezierPath(); line.lineWidth = 2
                line.move(to: NSPoint(x: size.width * 0.11, y: y))
                line.line(to: NSPoint(x: size.width * 0.89, y: y)); line.stroke()
            }
            muted.setFill()
            NSRect(x: size.width * 0.11, y: size.height * 0.08,
                   width: size.width * 0.31, height: size.height * 0.025).fill()
        }

        let textColor = kind == "pdf" ? ink : NSColor.white.withAlphaComponent(0.9)
        let display = NSFont(name: "ArialNarrow-Bold", size: max(24, size.width * 0.044))
            ?? .systemFont(ofSize: max(24, size.width * 0.044), weight: .bold)
        (title as NSString).draw(in: NSRect(x: size.width * 0.07, y: size.height * 0.82,
                                           width: size.width * 0.86, height: size.height * 0.1),
                                 withAttributes: [.font: display, .foregroundColor: textColor, .kern: 1.2])

        image.unlockFocus()
        guard let tiff = image.tiffRepresentation,
              let bitmap = NSBitmapImageRep(data: tiff) else { return nil }
        return bitmap.representation(using: .png, properties: [:])
    }

    @MainActor func makePreviewStore() -> (AppStore, URL) {
        let sampleData = arguments.contains("--sample-data")
        let liveDirectory = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("HangLater", isDirectory: true)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Preview-\(UUID().uuidString)", isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        if !sampleData {
            for name in ["tasks.json", "Previews"] {
                let source = liveDirectory.appendingPathComponent(name)
                let destination = directory.appendingPathComponent(name)
                if FileManager.default.fileExists(atPath: source.path) {
                    try? FileManager.default.copyItem(at: source, to: destination)
                }
            }
        }
        let store = AppStore(directory: directory)
        store.language = arguments.contains("--english") ? .en : .zh
        store.appearance = arguments.contains("--dark") ? .dark : .light
        if sampleData {
            _ = store.add(title: "LEARN SWIFTUI MOTION", url: "https://example.com/course", note: "",
                          dueAt: nil, screenshot: samplePreview(kind: "video", title: "LESSON 07", size: NSSize(width: 1280, height: 720)))
            _ = store.add(title: "REVIEW PRODUCT NOTES", url: nil, note: "", dueAt: nil,
                          screenshot: samplePreview(kind: "app", title: "NOTES", size: NSSize(width: 1200, height: 760)),
                          appBundleIdentifier: "com.apple.Notes", appName: "Notes")
            _ = store.add(title: "PROJECT BRIEF.PDF", url: nil, note: "", dueAt: nil,
                          screenshot: samplePreview(kind: "pdf", title: "PROJECT BRIEF", size: NSSize(width: 800, height: 1120)),
                          filePath: "/tmp/Hang Later Sample Project Brief.pdf")
            _ = store.add(title: "WRITE THE WEEKLY PLAN", url: nil,
                          note: "Three small priorities for the week.", dueAt: nil)
            store.perform { repository in
                let firstReceipt = try repository.add(title: "CLEAR THE DESKTOP")
                let secondReceipt = try repository.add(title: "SEND THE PROJECT UPDATE")
                try repository.complete(firstReceipt.id, at: Date().addingTimeInterval(-9 * 24 * 3600))
                try repository.complete(secondReceipt.id, at: Date().addingTimeInterval(-2 * 24 * 3600))
                for (task, position) in zip(repository.pending, [0.17, 0.40, 0.63, 0.82]) {
                    try repository.place(task.id, lineX: position)
                }
            }
        } else {
            store.perform { repository in
                let firstReceipt = try repository.add(title: "整理今天的学习笔记")
                let secondReceipt = try repository.add(title: "看完设计访谈")
                try repository.complete(firstReceipt.id, at: Date().addingTimeInterval(-40 * 24 * 3600))
                try repository.complete(secondReceipt.id, at: Date().addingTimeInterval(-10 * 24 * 3600))
                let pending = repository.pending
                if let first = pending.first { try repository.place(first.id, lineX: 0.46) }
                if pending.count > 1 { try repository.place(pending[1].id, lineX: 0.55) }
            }
        }
        store.message = nil
        return (store, directory)
    }

    @MainActor func writeSnapshot<Content: View>(_ content: Content, size: NSSize, to output: String) -> Bool {
        let host = NSHostingView(rootView: content)
        host.frame = NSRect(origin: .zero, size: size)
        let snapshotWindow = NSWindow(contentRect: NSRect(origin: NSPoint(x: -4000, y: -4000), size: size),
                                      styleMask: [.borderless], backing: .buffered, defer: false)
        snapshotWindow.isOpaque = false
        snapshotWindow.backgroundColor = .clear
        snapshotWindow.contentView = host
        snapshotWindow.orderFrontRegardless()
        RunLoop.main.run(until: Date().addingTimeInterval(1))
        host.layoutSubtreeIfNeeded()
        host.displayIfNeeded()
        guard let bitmap = host.bitmapImageRepForCachingDisplay(in: host.bounds) else { return false }
        host.cacheDisplay(in: host.bounds, to: bitmap)
        snapshotWindow.orderOut(nil)
        guard let png = bitmap.representation(using: .png, properties: [:]) else { return false }
        do {
            try png.write(to: URL(fileURLWithPath: output), options: .atomic)
            return true
        } catch { return false }
    }

    if let spikeIndex = arguments.firstIndex(of: "--render-spike-preview"), arguments.indices.contains(spikeIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let (store, directory) = makePreviewStore()
        let image = ReceiptSpike3DView.previewImage(receipts: Array(store.spiked.prefix(10)),
                                                    highlighted: false, language: store.language,
                                                    appearance: store.appearance,
                                                    yaw: 1.15, pitch: -0.12)
        let succeeded: Bool
        if let tiff = image.tiffRepresentation, let bitmap = NSBitmapImageRep(data: tiff),
           let png = bitmap.representation(using: .png, properties: [:]) {
            do {
                try png.write(to: URL(fileURLWithPath: arguments[spikeIndex + 1]), options: .atomic)
                succeeded = true
            } catch { succeeded = false }
        } else { succeeded = false }
        try? FileManager.default.removeItem(at: directory)
        exit(succeeded ? 0 : 2)
    }

    if let desktopIndex = arguments.firstIndex(of: "--render-desktop-preview"), arguments.indices.contains(desktopIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let (store, directory) = makePreviewStore()
        let state = DesktopState(); state.setRevealed(true)
        let rendered = DesktopLineView(store: store, state: state)
            .frame(width: 1400, height: 304).background(Color.clear)
        let succeeded = writeSnapshot(rendered, size: NSSize(width: 1400, height: 304),
                                      to: arguments[desktopIndex + 1])
        try? FileManager.default.removeItem(at: directory)
        exit(succeeded ? 0 : 2)
    }

    if let mainIndex = arguments.firstIndex(of: "--render-main-preview"), arguments.indices.contains(mainIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let (store, directory) = makePreviewStore()
        if arguments.contains("--completed") { store.destination = .completed }
        if arguments.contains("--spike") {
            store.destination = .spike
            let positions = [(-0.62, 1.05, -0.20), (0.52, 1.82, 0.18), (-0.18, 2.48, 0.31)]
            for (task, position) in zip(store.spiked.prefix(3), positions) {
                _ = store.perform {
                    try $0.moveOnSpike(task.id, x: position.0, y: position.1, z: position.2)
                }
            }
        }
        if arguments.contains("--detached"), let task = store.spiked.first {
            _ = store.perform { try $0.setSpikeAttached(false, for: task.id) }
        }
        let rendered = MainView(store: store).frame(width: 1100, height: 720)
        let succeeded = writeSnapshot(rendered, size: NSSize(width: 1100, height: 720),
                                      to: arguments[mainIndex + 1])
        try? FileManager.default.removeItem(at: directory)
        exit(succeeded ? 0 : 2)
    }

    let delegate = AppDelegate()
    app.delegate = delegate
    app.setActivationPolicy(.accessory)
    withExtendedLifetime(delegate) { app.run() }
}
