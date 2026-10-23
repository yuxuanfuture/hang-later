import AppKit
import Combine
import HangLaterCore
import SwiftUI

MainActor.assumeIsolated {
    let app = NSApplication.shared
    let arguments = ProcessInfo.processInfo.arguments

    if arguments.contains("--verify-resume-targets") {
        let target = AppResumeTarget.resolve(bundleIdentifier: "md.obsidian", appName: "Obsidian",
                                             windowTitle: "Reviews - Obsidian Vault - Obsidian 1.13.7")
        let expected = "obsidian://open?vault=Obsidian%20Vault&file=Reviews"
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Typora-QA-\(UUID().uuidString)", isDirectory: true)
        let olderDirectory = directory.appendingPathComponent("older", isDirectory: true)
        let newerDirectory = directory.appendingPathComponent("newer", isDirectory: true)
        let filename = "Specific document.md"
        let older = olderDirectory.appendingPathComponent(filename)
        let newer = newerDirectory.appendingPathComponent(filename)
        try? FileManager.default.createDirectory(at: olderDirectory, withIntermediateDirectories: true)
        try? FileManager.default.createDirectory(at: newerDirectory, withIntermediateDirectories: true)
        try? Data("older".utf8).write(to: older)
        try? Data("newer".utf8).write(to: newer)
        try? FileManager.default.setAttributes([.modificationDate: Date(timeIntervalSince1970: 1)],
                                               ofItemAtPath: older.path)
        try? FileManager.default.setAttributes([.modificationDate: Date(timeIntervalSince1970: 2)],
                                               ofItemAtPath: newer.path)
        let typoraTarget = AppResumeTarget.resolveDocumentFile(
            bundleIdentifier: "abnerworks.Typora", appName: "Typora",
            windowTitle: filename, candidates: [older, newer]
        )
        try? FileManager.default.removeItem(at: directory)
        if target?.absoluteString == expected && typoraTarget == newer {
            print("PASS: Obsidian and Typora windows resolve to their exact saved content")
            exit(0)
        }
        print("FAIL: unexpected targets \(target?.absoluteString ?? "nil"), \(typoraTarget?.path ?? "nil")")
        exit(2)
    }

    if let typoraIndex = arguments.firstIndex(of: "--verify-typora-resume"),
       arguments.indices.contains(typoraIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let source = URL(fileURLWithPath: arguments[typoraIndex + 1])
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Typora-Resume-QA-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        let added = store.add(title: source.lastPathComponent, url: nil, note: "", dueAt: nil,
                              appBundleIdentifier: "abnerworks.Typora", appName: "Typora")
        if let task = store.tasks.first { store.open(task) }
        let deadline = Date().addingTimeInterval(5)
        while store.tasks.first?.filePath == nil && Date() < deadline {
            RunLoop.main.run(until: Date().addingTimeInterval(0.05))
        }
        let savedPath = store.tasks.first?.filePath
        let typora = NSRunningApplication.runningApplications(
            withBundleIdentifier: "abnerworks.Typora"
        ).first
        let visibleWindow = ((CGWindowListCopyWindowInfo(
            [.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID
        ) as? [[String: Any]]) ?? []).contains { window in
            let ownerPID = window[kCGWindowOwnerPID as String] as? pid_t
            let title = window[kCGWindowName as String] as? String
            return ownerPID == typora?.processIdentifier && title == source.lastPathComponent
        }
        let valid = added && savedPath == source.path && typora != nil && visibleWindow
        print(valid
              ? "PASS: Typora card restored and raised \(source.lastPathComponent)"
              : "FAIL: Typora card saved \(savedPath ?? "nil"); visible=\(visibleWindow)")
        try? FileManager.default.removeItem(at: directory)
        exit(valid ? 0 : 2)
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

    if arguments.contains("--verify-card-drag-update-rate") {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Card-Drag-QA-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        _ = store.add(title: "Drag performance", url: nil, note: "", dueAt: nil)
        guard let task = store.pending.first else {
            print("FAIL: could not create a card for drag performance verification")
            exit(2)
        }

        store.setReceiptFrame(CGRect(x: 300, y: 200, width: 160, height: 90), on: .main)
        var publishes = 0
        let subscription = store.objectWillChange.sink { publishes += 1 }
        store.beginDragging(task, on: .main)
        for index in 0..<240 {
            store.updateDragging(at: CGPoint(x: 20 + index % 120, y: 40), on: .main)
        }
        for index in 0..<240 {
            store.updateDragging(at: CGPoint(x: 320 + index % 100, y: 230), on: .main)
        }
        let enteredReceipt = store.isDraggingOverReceipt(on: .main)
        for index in 0..<240 {
            store.updateDragging(at: CGPoint(x: 330 + index % 80, y: 240), on: .main)
        }
        store.updateDragging(at: CGPoint(x: 40, y: 40), on: .main)
        let exitedReceipt = !store.isDraggingOverReceipt(on: .main)
        withExtendedLifetime(subscription) {}

        let valid = enteredReceipt && exitedReceipt && publishes <= 4
        print(valid
              ? "PASS: 721 pointer moves produced only \(publishes) store updates"
              : "FAIL: card drag produced \(publishes) store updates")
        try? FileManager.default.removeItem(at: directory)
        exit(valid ? 0 : 2)
    }

    if arguments.contains("--verify-idea-capture") {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Idea-QA-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        let idea = "A small tool that lets unfinished thoughts mature"
        let added = store.addIdea(idea)
        let saved = store.pending.first
        let valid = added && saved?.title == idea && saved?.url == nil
            && saved?.note.isEmpty == true && saved?.kind == .idea
        print(valid ? "PASS: quick idea capture creates a local clothesline card"
                    : "FAIL: quick idea capture did not preserve the idea")
        try? FileManager.default.removeItem(at: directory)
        exit(valid ? 0 : 2)
    }

    if arguments.contains("--verify-image-preview-routing") {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Image-Preview-QA-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        var showedMainWindow = false
        var openedPreviewURLs: [URL] = []
        store.showMainWindow = { showedMainWindow = true }
        store.openImagePreview = { openedPreviewURLs.append($0); return true }
        let image = NSImage(size: NSSize(width: 640, height: 400))
        image.lockFocus()
        NSColor.systemBlue.setFill()
        NSRect(origin: .zero, size: image.size).fill()
        image.unlockFocus()
        let data = image.tiffRepresentation
        let legacyAdded = store.add(title: "Legacy dropped image", url: nil, note: "", dueAt: nil,
                                    screenshot: data)
        let legacy = store.pending.first
        if let legacy { store.open(legacy) }
        let legacyRoutesOutsideHangLater = store.editingTask == nil
            && !showedMainWindow && openedPreviewURLs.last == legacy.flatMap(store.imagePreviewURL)

        store.importImage(image, title: "New dropped image")
        let imported = store.pending.first(where: { $0.title == "New dropped image" })
        if let imported { store.open(imported) }
        let newImageRoutesOutsideHangLater = imported?.kind == .image
            && store.editingTask == nil && !showedMainWindow
            && openedPreviewURLs.last == imported.flatMap(store.imagePreviewURL)

        let originalURL = directory.appendingPathComponent("original-image.png")
        try? data?.write(to: originalURL, options: .atomic)
        store.importImage(image, title: "Image with original", originalURL: originalURL)
        let sourceBacked = store.pending.first(where: { $0.title == "Image with original" })
        if let sourceBacked { store.open(sourceBacked) }
        let originalRoutesOutsideHangLater = openedPreviewURLs.last == originalURL && !showedMainWindow
        let valid = legacyAdded && legacyRoutesOutsideHangLater
            && newImageRoutesOutsideHangLater && originalRoutesOutsideHangLater
        print(valid ? "PASS: clicking an image bypasses the Hang Later window"
                    : "FAIL: clicking an image still shows the Hang Later window")
        try? FileManager.default.removeItem(at: directory)
        exit(valid ? 0 : 2)
    }

    if arguments.contains("--verify-image-editing") {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Image-Edit-QA-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        var showedMainWindow = false
        store.showMainWindow = { showedMainWindow = true }
        let image = NSImage(size: NSSize(width: 640, height: 400))
        image.lockFocus()
        NSColor.systemOrange.setFill()
        NSRect(origin: .zero, size: image.size).fill()
        image.unlockFocus()
        store.importImage(image, title: "Editable image")
        let task = store.pending.first
        if let task { store.edit(task) }
        let valid = showedMainWindow && store.editingTask?.id == task?.id
            && task.flatMap(store.preview) != nil
        print(valid ? "PASS: the to-do edit action opens an image task with its preview"
                    : "FAIL: the to-do edit action did not prepare the image editor")
        try? FileManager.default.removeItem(at: directory)
        exit(valid ? 0 : 2)
    }

    if arguments.contains("--verify-task-thumbnail-preview") {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Thumbnail-Preview-QA-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        var showedMainWindow = false
        var openedPreviewURLs: [URL] = []
        store.showMainWindow = { showedMainWindow = true }
        store.openImagePreview = { openedPreviewURLs.append($0); return true }
        let image = NSImage(size: NSSize(width: 640, height: 400))
        image.lockFocus()
        NSColor.systemTeal.setFill()
        NSRect(origin: .zero, size: image.size).fill()
        image.unlockFocus()
        let added = store.add(title: "Captured web page", url: "https://example.com", note: "", dueAt: nil,
                              screenshot: image.tiffRepresentation)
        let task = store.pending.first
        if let task { store.openPreview(task) }
        let storedPreview = task?.previewFile.map { store.previewDirectory.appendingPathComponent($0) }
        let valid = added && task?.isImage == false && !showedMainWindow
            && store.editingTask == nil && openedPreviewURLs.last == storedPreview
        print(valid ? "PASS: clicking a task thumbnail enlarges its saved preview"
                    : "FAIL: the task thumbnail did not open its saved preview")
        try? FileManager.default.removeItem(at: directory)
        exit(valid ? 0 : 2)
    }

    if let thumbnailIndex = arguments.firstIndex(of: "--verify-document-thumbnail"),
       arguments.indices.contains(thumbnailIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let url = URL(fileURLWithPath: arguments[thumbnailIndex + 1])
        var thumbnail: NSImage?
        var finished = false
        Task { @MainActor in
            thumbnail = await DocumentThumbnail.image(for: url)
            finished = true
        }
        let deadline = Date().addingTimeInterval(12)
        while !finished && Date() < deadline {
            RunLoop.main.run(until: Date().addingTimeInterval(0.05))
        }
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Document-QA-\(UUID().uuidString)", isDirectory: true)
        let legacyPreview = "legacy-icon.png"
        try? FileManager.default.createDirectory(at: directory.appendingPathComponent("Previews"),
                                                 withIntermediateDirectories: true)
        try? Data([0]).write(to: directory.appendingPathComponent("Previews").appendingPathComponent(legacyPreview))
        if let repository = try? TaskRepository(directory: directory) {
            _ = try? repository.add(title: url.lastPathComponent, previewFile: legacyPreview,
                                    filePath: url.path)
        }
        let store = AppStore(directory: directory)
        let refreshDeadline = Date().addingTimeInterval(12)
        while store.tasks.first?.previewFile == legacyPreview && Date() < refreshDeadline {
            RunLoop.main.run(until: Date().addingTimeInterval(0.05))
        }
        let refreshed = store.tasks.first?.previewFile != legacyPreview
        let provider = NSItemProvider(contentsOf: url)
        let accepted = provider.map { store.acceptDrop([$0]) } ?? false
        let importDeadline = Date().addingTimeInterval(12)
        while store.tasks.filter({ $0.filePath == url.path }).count < 2 && Date() < importDeadline {
            RunLoop.main.run(until: Date().addingTimeInterval(0.05))
        }
        let imported = store.tasks.first(where: { $0.filePath == url.path && $0.previewFile != legacyPreview })
        let storedPreview = imported.flatMap(store.preview)
        let valid = thumbnail.map { $0.size.width > 0 && $0.size.height > 0 } == true
            && refreshed && accepted && store.tasks.filter({ $0.filePath == url.path }).count == 2
            && storedPreview.map { $0.size.width > 0 && $0.size.height > 0 } == true
        if valid, let thumbnail {
            print("PASS: Quick Look rendered and imported \(url.lastPathComponent) at \(Int(thumbnail.size.width))x\(Int(thumbnail.size.height))")
            try? FileManager.default.removeItem(at: directory)
            exit(0)
        }
        print("FAIL: Quick Look did not render and import \(url.lastPathComponent)")
        try? FileManager.default.removeItem(at: directory)
        exit(2)
    }

    if arguments.contains("--verify-external-drop") {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Drop-QA-\(UUID().uuidString)", isDirectory: true)
        let fixture = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater Drop Test \(UUID().uuidString).txt")
        do {
            try Data("Hang Later drop zone verification".utf8).write(to: fixture, options: .atomic)
            guard let provider = NSItemProvider(contentsOf: fixture) else {
                throw CocoaError(.fileReadUnknown)
            }
            let store = AppStore(directory: directory)
            let accepted = store.acceptDrop([provider])
            let deadline = Date().addingTimeInterval(3)
            while store.pending.isEmpty && Date() < deadline {
                RunLoop.main.run(until: Date().addingTimeInterval(0.05))
            }
            let imported = store.pending.contains { $0.filePath == fixture.path }
            print(accepted && imported
                  ? "PASS: external file drop creates a clothesline list"
                  : "FAIL: external file drop was not imported")
            try? FileManager.default.removeItem(at: fixture)
            try? FileManager.default.removeItem(at: directory)
            exit(accepted && imported ? 0 : 2)
        } catch {
            print("FAIL: external drop verification could not run: \(error.localizedDescription)")
            try? FileManager.default.removeItem(at: fixture)
            try? FileManager.default.removeItem(at: directory)
            exit(2)
        }
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
            for name in ["tasks.json", "task-types.json", "Previews"] {
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
        if arguments.contains("--workday-example") {
            store.updateWorkday(startMinute: 8 * 60, endMinute: 2 * 60, persist: false)
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

    if arguments.contains("--verify-desktop-drop-routing") {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Drop-Routing-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        let state = DesktopState(); state.setRevealed(true)
        let size = NSSize(width: 1400, height: 304)
        let host = NSHostingView(rootView: DesktopLineView(store: store, state: state)
            .frame(width: size.width, height: size.height).background(Color.clear))
        host.frame = NSRect(origin: .zero, size: size)
        let window = NSWindow(contentRect: NSRect(origin: NSPoint(x: -4000, y: -4000), size: size),
                              styleMask: [.borderless], backing: .buffered, defer: false)
        window.contentView = host
        window.orderFrontRegardless()
        RunLoop.main.run(until: Date().addingTimeInterval(1))
        host.layoutSubtreeIfNeeded()

        var registeredDropRects: [CGRect] = []
        func collectDropRects(from view: NSView) {
            if !view.registeredDraggedTypes.isEmpty {
                let appKitRect = view.convert(view.bounds, to: host)
                registeredDropRects.append(CGRect(x: appKitRect.minX,
                                                  y: appKitRect.minY,
                                                  width: appKitRect.width,
                                                  height: appKitRect.height))
            }
            view.subviews.forEach(collectDropRects)
        }
        collectDropRects(from: host)

        let routed = state.dropTargetRect
        let matchingRect = registeredDropRects.contains { actual in
            abs(actual.minX - routed.minX) < 1 && abs(actual.minY - routed.minY) < 1
                && abs(actual.width - routed.width) < 1 && abs(actual.height - routed.height) < 1
        }
        let valid = routed.width >= 219 && routed.height >= 47 && matchingRect
        print(valid
              ? "PASS: visible desktop target and registered drop destination share one frame"
              : "FAIL: routed=\(routed) registered=\(registeredDropRects)")
        window.orderOut(nil)
        try? FileManager.default.removeItem(at: directory)
        exit(valid ? 0 : 2)
    }

    if let ideaIndex = arguments.firstIndex(of: "--render-idea-preview"),
       arguments.indices.contains(ideaIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Idea-Preview-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        store.language = arguments.contains("--english") ? .en : .zh
        store.appearance = arguments.contains("--dark") ? .dark : .light
        let rendered = IdeaCaptureView(store: store, onHang: { _ in true }, onCancel: {})
            .frame(width: 390, height: 154)
        let succeeded = writeSnapshot(rendered, size: NSSize(width: 390, height: 154),
                                      to: arguments[ideaIndex + 1])
        try? FileManager.default.removeItem(at: directory)
        exit(succeeded ? 0 : 2)
    }

    if let ideaCardIndex = arguments.firstIndex(of: "--render-idea-card-preview"),
       arguments.indices.contains(ideaCardIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Idea-Card-Preview-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        store.language = arguments.contains("--english") ? .en : .zh
        store.appearance = arguments.contains("--dark") ? .dark : .light
        _ = store.addIdea("I want to eat")
        if let task = store.pending.first {
            if arguments.contains("--colored-type") {
                let ideaType = TaskTypeDefinition(name: "Personal", colorHex: 0xE8365F)
                _ = store.updateTaskTypes([ideaType])
                store.setTaskType(ideaType.id, for: task)
            }
            if let current = store.pending.first { store.place(current, lineX: 0.25) }
        }
        store.message = nil
        let state = DesktopState(); state.setRevealed(true)
        let rendered = DesktopLineView(store: store, state: state)
            .frame(width: 760, height: 304).background(Color.clear)
        let succeeded = writeSnapshot(rendered, size: NSSize(width: 760, height: 304),
                                      to: arguments[ideaCardIndex + 1])
        try? FileManager.default.removeItem(at: directory)
        exit(succeeded ? 0 : 2)
    }

    if let reminderIndex = arguments.firstIndex(where: {
        $0 == "--render-reminder-preview" || $0 == "--render-meeting-preview"
    }), arguments.indices.contains(reminderIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Reminder-Preview-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        store.language = arguments.contains("--english") ? .en : .zh
        store.appearance = arguments.contains("--dark") ? .dark : .light
        let rendered = ReminderCaptureView(
            store: store,
            onHang: { _, _, _ in true },
            onCancel: {},
            initialTitle: store.text("生活提醒", "Everyday reminder"),
            initialLink: ""
        ).frame(width: 440, height: 286)
        let succeeded = writeSnapshot(rendered, size: NSSize(width: 440, height: 286),
                                      to: arguments[reminderIndex + 1])
        try? FileManager.default.removeItem(at: directory)
        exit(succeeded ? 0 : 2)
    }

    if let editorIndex = arguments.firstIndex(of: "--render-reminder-editor-preview"),
       arguments.indices.contains(editorIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Reminder-Editor-Preview-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        store.language = arguments.contains("--english") ? .en : .zh
        store.appearance = arguments.contains("--dark") ? .dark : .light
        let task = HangingTask(title: store.text("准备下午的事情", "Prepare the afternoon task"))
        let rendered = ReminderEditorView(store: store, task: task)
            .frame(width: 440, height: 310)
        let succeeded = writeSnapshot(rendered, size: NSSize(width: 440, height: 310),
                                      to: arguments[editorIndex + 1])
        try? FileManager.default.removeItem(at: directory)
        exit(succeeded ? 0 : 2)
    }

    if let imageEditorIndex = arguments.firstIndex(of: "--render-image-editor-preview"),
       arguments.indices.contains(imageEditorIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Image-Editor-Preview-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        store.language = arguments.contains("--english") ? .en : .zh
        store.appearance = arguments.contains("--dark") ? .dark : .light
        let image = samplePreview(kind: "app", title: "DROPPED IMAGE", size: NSSize(width: 1200, height: 760))
            .flatMap(NSImage.init(data:)) ?? NSImage(size: NSSize(width: 1200, height: 760))
        store.importImage(image, title: store.text("拖入的图片", "Dropped image"))
        guard let task = store.pending.first else { exit(2) }
        let rendered = TaskEditor(store: store, task: task).frame(width: 540, height: 720)
        let succeeded = writeSnapshot(rendered, size: NSSize(width: 540, height: 720),
                                      to: arguments[imageEditorIndex + 1])
        try? FileManager.default.removeItem(at: directory)
        exit(succeeded ? 0 : 2)
    }

    if let spikeIndex = arguments.firstIndex(of: "--render-spike-preview"), arguments.indices.contains(spikeIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let (store, directory) = makePreviewStore()
        let image = ReceiptSpike3DView.previewImage(receipts: Array(store.spiked.prefix(10)),
                                                    highlighted: false, language: store.language,
                                                    appearance: store.appearance,
                                                    taskTypeColors: Dictionary(uniqueKeysWithValues:
                                                        store.taskTypes.map { ($0.id, $0.colorHex) }),
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
        if arguments.contains("--drag-active") { state.setExternalDragActive(true) }
        if arguments.contains("--drop-targeted") {
            state.setExternalDragActive(true)
            state.setExternalDragTargeted(true)
        }
        let rendered = DesktopLineView(store: store, state: state)
            .frame(width: 1400, height: 304).background(Color.clear)
        let succeeded = writeSnapshot(rendered, size: NSSize(width: 1400, height: 304),
                                      to: arguments[desktopIndex + 1])
        try? FileManager.default.removeItem(at: directory)
        exit(succeeded ? 0 : 2)
    }

    if let settingsIndex = arguments.firstIndex(of: "--render-settings-preview"), arguments.indices.contains(settingsIndex + 1) {
        app.setActivationPolicy(.prohibited)
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Settings-Preview-\(UUID().uuidString)", isDirectory: true)
        let store = AppStore(directory: directory)
        store.language = arguments.contains("--english") ? .en : .zh
        store.appearance = arguments.contains("--dark") ? .dark : .light
        store.updateWorkday(startMinute: 8 * 60, endMinute: 2 * 60, persist: false)
        _ = store.updateTaskTypes([
            TaskTypeDefinition(name: store.text("深度工作", "Deep work"), colorHex: 0x2F87E1),
            TaskTypeDefinition(name: store.text("个人", "Personal"), colorHex: 0xE8365F)
        ])
        let rendered = SettingsView(store: store).frame(width: 820, height: 700)
        let succeeded = writeSnapshot(rendered, size: NSSize(width: 820, height: 700),
                                      to: arguments[settingsIndex + 1])
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
            let scales = [0.72, 1.0, 1.35]
            for ((task, position), scale) in zip(zip(store.spiked.prefix(3), positions), scales) {
                _ = store.perform {
                    try $0.moveOnSpike(task.id, x: position.0, y: position.1, z: position.2)
                    try $0.resizeOnSpike(task.id, scale: scale)
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
    // Start as a regular app so Dock creates a full-size tile. The delegate
    // switches to accessory mode only after the main window is closed.
    app.setActivationPolicy(.regular)
    withExtendedLifetime(delegate) { app.run() }
}
