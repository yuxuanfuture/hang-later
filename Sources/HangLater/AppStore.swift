import AppKit
import Combine
import HangLaterCore
import LinkPresentation
import PDFKit
import UniformTypeIdentifiers

enum Destination: String, CaseIterable {
    case line, list, completed, spike
    var symbol: String {
        switch self {
        case .line: return "line.3.horizontal"
        case .list: return "checklist"
        case .completed: return "checkmark.seal"
        case .spike: return "pin.fill"
        }
    }
    func title(_ language: AppLanguage) -> String {
        switch self {
        case .line: return language.text("晾衣绳", "Clothesline")
        case .list: return language.text("待办清单", "To-do list")
        case .completed: return language.text("已完成", "Completed")
        case .spike: return language.text("完成钉", "Done Spike")
        }
    }
}

enum DragSurface: Hashable {
    case main
    case desktop
}

@MainActor
final class AppStore: ObservableObject {
    @Published var tasks: [HangingTask] = []
    @Published var destination = Destination.line
    @Published var search = ""
    @Published var showingComposer = false
    @Published var showingReminderComposer = false
    @Published var showingAddChooser = false
    @Published var showingWindowCapture = false
    @Published var editingTask: HangingTask?
    @Published var reminderEditingTask: HangingTask?
    @Published var showingGuide = false
    @Published var message: String?
    @Published var error: String?
    @Published var screenCapturePermissionNeeded = false
    @Published private(set) var pinned: Bool
    @Published private(set) var desktopLineVerticalPosition: Double
    @Published var showingSettings = false
    @Published var dragTargeted = false
    @Published var draggingTaskID: UUID?
    @Published var dragLocation: CGPoint?
    @Published var dragSurface: DragSurface?
    @Published var language = AppLanguage(rawValue: UserDefaults.standard.string(forKey: "appLanguage") ?? "zh") ?? .zh
    @Published var appearance = AppAppearance(rawValue: UserDefaults.standard.string(forKey: "appAppearance") ?? "light") ?? .light
    @Published private(set) var workdayStartMinute: Int
    @Published private(set) var workdayEndMinute: Int
    @Published private(set) var workdayEndsOnFollowingDay: Bool
    @Published private(set) var workdayNow = Date()
    @Published private(set) var taskTypes: [TaskTypeDefinition] = []
    var showDesktopLine: (() -> Void)?
    var showMainWindow: (() -> Void)?
    var openImagePreview: ((URL) -> Bool)?
    var onExternalDropAccepted: (() -> Void)?
    var desktopLinePreferencesChanged: ((Bool, Double) -> Void)?
    let directory: URL
    private var repository: TaskRepository?
    private var taskTypeRepository: TaskTypeRepository?
    private var metadataProviders: [UUID: LPMetadataProvider] = [:]
    private var messageTimer: Timer?
    private var inboxTimer: Timer?
    private var workdayTimer: Timer?
    private var previewCache: [String: NSImage] = [:]
    private var receiptFrames: [DragSurface: CGRect] = [:]
    private var recentWindowImports: [String: Date] = [:]

    init(directory: URL) {
        self.directory = directory
        pinned = UserDefaults.standard.bool(forKey: "linePinned")
        desktopLineVerticalPosition = DesktopLineLayout.clampedPosition(
            UserDefaults.standard.object(forKey: "desktopLineVerticalPosition") as? Double ?? 0
        )
        let savedWorkdayStart = UserDefaults.standard.object(forKey: "workdayStartMinute") as? Int ?? 0
        let savedWorkdayEnd = UserDefaults.standard.object(forKey: "workdayEndMinute") as? Int ?? 0
        workdayStartMinute = savedWorkdayStart
        workdayEndMinute = savedWorkdayEnd
        workdayEndsOnFollowingDay = UserDefaults.standard.object(forKey: "workdayEndsOnFollowingDay") as? Bool
            ?? (savedWorkdayEnd <= savedWorkdayStart)
        do { repository = try TaskRepository(directory: directory); reload() }
        catch { self.error = error.localizedDescription }
        do {
            taskTypeRepository = try TaskTypeRepository(directory: directory)
            taskTypes = taskTypeRepository?.taskTypes ?? []
        } catch {
            self.error = localized(error)
        }
        try? FileManager.default.createDirectory(at: previewDirectory, withIntermediateDirectories: true)
        try? FileManager.default.createDirectory(at: inboxDirectory, withIntermediateDirectories: true)
        inboxTimer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.importInbox() }
        }
        workdayTimer = Timer.scheduledTimer(withTimeInterval: 30, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.workdayNow = Date() }
        }
        importInbox()
        Task { [weak self] in await self?.refreshDocumentThumbnails() }
        ReminderScheduler.synchronize(tasks, language: language)
    }

    var previewDirectory: URL { directory.appendingPathComponent("Previews", isDirectory: true) }
    var inboxDirectory: URL { directory.appendingPathComponent("Inbox", isDirectory: true) }
    var pending: [HangingTask] { tasks.filter { !$0.isCompleted } }
    var completed: [HangingTask] { tasks.filter(\.isCompleted).sorted { $0.completedAt! > $1.completedAt! } }
    var workdaySchedule: WorkdaySchedule {
        WorkdaySchedule(startMinute: workdayStartMinute, endMinute: workdayEndMinute,
                        endsOnFollowingDay: workdayEndsOnFollowingDay)
    }
    var currentWorkdayInterval: DateInterval? { workdaySchedule.interval(containing: workdayNow) }
    var spikeEligibleCompleted: [HangingTask] {
        guard let interval = currentWorkdayInterval else { return [] }
        return completed.filter { $0.createdAt >= interval.start && $0.createdAt < interval.end }
    }
    var spiked: [HangingTask] { spikeEligibleCompleted.filter(\.isOnSpike) }
    var offSpike: [HangingTask] { spikeEligibleCompleted.filter { !$0.isOnSpike } }
    var filtered: [HangingTask] {
        let source: [HangingTask]
        switch destination {
        case .completed, .spike: source = completed
        case .line, .list: source = pending
        }
        guard !search.isEmpty else { return source }
        return source.filter { [$0.title, $0.note, $0.url ?? ""].joined(separator: " ").localizedCaseInsensitiveContains(search) }
    }

    func text(_ zh: String, _ en: String) -> String { language.text(zh, en) }

    func toggleLanguage() {
        language = language == .zh ? .en : .zh
        UserDefaults.standard.set(language.rawValue, forKey: "appLanguage")
        ReminderScheduler.synchronize(tasks, language: language)
    }

    func toggleAppearance() {
        appearance = appearance == .light ? .dark : .light
        UserDefaults.standard.set(appearance.rawValue, forKey: "appAppearance")
    }

    func updateWorkday(startMinute: Int, endMinute: Int,
                       endsOnFollowingDay: Bool? = nil, persist: Bool = true) {
        let schedule = WorkdaySchedule(startMinute: startMinute, endMinute: endMinute,
                                       endsOnFollowingDay: endsOnFollowingDay)
        workdayStartMinute = schedule.startMinute
        workdayEndMinute = schedule.endMinute
        workdayEndsOnFollowingDay = schedule.endsOnFollowingDay
        workdayNow = Date()
        if persist {
            UserDefaults.standard.set(schedule.startMinute, forKey: "workdayStartMinute")
            UserDefaults.standard.set(schedule.endMinute, forKey: "workdayEndMinute")
            UserDefaults.standard.set(schedule.endsOnFollowingDay,
                                      forKey: "workdayEndsOnFollowingDay")
            notify(text("工作日时间已更新。", "Workday hours updated."))
        }
    }

    func previewDesktopLinePreferences(keepVisible: Bool, verticalPosition: Double) {
        pinned = keepVisible
        desktopLineVerticalPosition = DesktopLineLayout.clampedPosition(verticalPosition)
        desktopLinePreferencesChanged?(pinned, desktopLineVerticalPosition)
    }

    func previewDesktopLineHeight(_ verticalPosition: Double) {
        desktopLineVerticalPosition = DesktopLineLayout.clampedPosition(verticalPosition)
        desktopLinePreferencesChanged?(true, desktopLineVerticalPosition)
    }

    func saveDesktopLinePreferences(keepVisible: Bool, verticalPosition: Double) {
        previewDesktopLinePreferences(keepVisible: keepVisible, verticalPosition: verticalPosition)
        UserDefaults.standard.set(pinned, forKey: "linePinned")
        UserDefaults.standard.set(desktopLineVerticalPosition, forKey: "desktopLineVerticalPosition")
        notify(text("桌面晾衣绳设置已更新。", "Desktop clothesline settings updated."))
    }

    @discardableResult
    func updateTaskTypes(_ definitions: [TaskTypeDefinition]) -> Bool {
        guard let taskTypeRepository else {
            error = text("任务类型文件无法读取，已停止写入以保留原始数据。",
                         "The task type file could not be read. Writing is paused to preserve the original data.")
            return false
        }
        do {
            try taskTypeRepository.replace(with: definitions)
            taskTypes = taskTypeRepository.taskTypes
            let validIDs = Set(taskTypes.map(\.id))
            if let repository {
                try repository.retainTaskTypeAssignments(in: validIDs)
                reload()
            }
            notify(text("任务类型已更新。", "Task types updated."))
            return true
        } catch {
            self.error = localized(error)
            return false
        }
    }

    func taskType(for task: HangingTask) -> TaskTypeDefinition? {
        guard let id = task.taskTypeID else { return nil }
        return taskTypes.first { $0.id == id }
    }

    func setTaskType(_ typeID: UUID?, for task: HangingTask) {
        guard typeID == nil || taskTypes.contains(where: { $0.id == typeID }),
              var current = tasks.first(where: { $0.id == task.id }) else { return }
        current.taskTypeID = typeID
        if perform({ try $0.update(current) }) {
            notify(text("任务类型已更新。", "Task type updated."))
        }
    }

    func edit(_ task: HangingTask) {
        editingTask = tasks.first(where: { $0.id == task.id }) ?? task
        showMainWindow?()
    }

    func editReminder(_ task: HangingTask) {
        reminderEditingTask = tasks.first(where: { $0.id == task.id }) ?? task
        showMainWindow?()
    }

    func presentAddOptions() {
        showingAddChooser = true
        showMainWindow?()
    }

    private func reload() { tasks = repository?.tasks ?? [] }

    @discardableResult
    func perform(_ action: (TaskRepository) throws -> Void) -> Bool {
        guard let repository else {
            error = text("任务文件无法读取，已停止写入以保留原始数据。请在数据文件夹中检查 tasks.json。",
                         "The task file could not be read. Writing is paused to protect the original data; inspect tasks.json in the data folder.")
            return false
        }
        do { try action(repository); reload(); return true }
        catch { self.error = localized(error); return false }
    }

    @discardableResult
    func add(title: String, url: String?, note: String, dueAt: Date?, screenshot: Data? = nil,
             usePageTitle: Bool = false, captureID: String? = nil, filePath: String? = nil,
             appBundleIdentifier: String? = nil, appName: String? = nil,
             resumeURL: String? = nil, kind: HangingTaskKind? = nil,
             taskTypeID: UUID? = nil) -> Bool {
        var previewFile: String?
        if let screenshot, let image = NSImage(data: screenshot), let data = pngData(image) {
            let filename = UUID().uuidString + ".png"
            do { try data.write(to: previewDirectory.appendingPathComponent(filename), options: .atomic); previewFile = filename }
            catch { self.error = text("缩略图未能保存：", "Could not save the preview: ") + error.localizedDescription; return false }
        }
        var added: HangingTask?
        let success = perform {
            added = try $0.add(title: title, url: url, note: note, dueAt: dueAt,
                               previewFile: previewFile, captureID: captureID, filePath: filePath,
                               appBundleIdentifier: appBundleIdentifier, appName: appName,
                               resumeURL: resumeURL, kind: kind, taskTypeID: taskTypeID)
        }
        if success, let task = added {
            notify(text("已经晾好了，等你有空再继续。", "Hung up for later."))
            if task.previewFile == nil, task.url != nil { fetchMetadata(task, updateTitle: usePageTitle) }
            scheduleReminder(for: task)
            showDesktopLine?()
        } else if let previewFile { try? FileManager.default.removeItem(at: previewDirectory.appendingPathComponent(previewFile)) }
        return success
    }

    func addText(_ rawText: String) {
        if let url = TaskInput.webURL(rawText) {
            _ = add(title: url.host ?? text("稍后打开的网页", "Web page for later"), url: url.absoluteString, note: "", dueAt: nil, usePageTitle: true)
        } else {
            let trimmed = rawText.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !trimmed.isEmpty else { return }
            if trimmed.contains("://") { error = text("目前支持 http / https 网页链接，也可以直接添加文字待办。", "Use an http/https link, or add it as a text task."); return }
            _ = add(title: String(trimmed.prefix(160)), url: nil, note: trimmed.count > 160 ? trimmed : "", dueAt: nil)
        }
    }

    @discardableResult
    func addIdea(_ rawIdea: String) -> Bool {
        let clean = rawIdea.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !clean.isEmpty else { return false }
        let firstLine = clean.split(whereSeparator: \Character.isNewline).first.map(String.init) ?? clean
        let title = String(firstLine.prefix(160))
        let note = clean == title ? "" : clean
        return add(title: title, url: nil, note: note, dueAt: nil, kind: .idea)
    }

    @discardableResult
    func addReminder(title rawTitle: String, url rawURL: String?, reminderAt: Date) -> Bool {
        let cleanTitle = rawTitle.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanTitle.isEmpty else {
            error = text("请输入要提醒的事情。", "Enter what you want to be reminded about.")
            return false
        }
        let cleanURL = rawURL?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let url: URL?
        if cleanURL.isEmpty {
            url = nil
        } else if let parsed = TaskInput.webURL(cleanURL) {
            url = parsed
        } else {
            error = text("请输入完整的链接，或把链接留空。",
                         "Enter a complete link, or leave the link empty.")
            return false
        }
        let kind: HangingTaskKind = url.flatMap { MeetingLink.platform(for: $0) } == nil
            ? .reminder : .meeting
        return add(title: cleanTitle, url: url?.absoluteString, note: "",
                   dueAt: reminderAt, kind: kind)
    }

    @discardableResult
    func updateTask(_ task: HangingTask) -> Bool {
        guard perform({ try $0.update(task) }) else { return false }
        scheduleReminder(for: task)
        return true
    }

    func complete(_ task: HangingTask) {
        if perform({ try $0.complete(task.id) }) {
            ReminderScheduler.cancel(task.id)
            notify(text("做完了，已收进「已完成」。", "Completed and moved to the archive."))
        }
    }

    func completeAll(_ candidates: [HangingTask]) {
        let selected = candidates.filter { !$0.isCompleted }
        let ids = Set(selected.map(\.id))
        guard !ids.isEmpty else { return }
        if perform({ try $0.completeAll(ids) }) {
            ids.forEach(ReminderScheduler.cancel)
            notify(text("已完成当前全部 \(selected.count) 件任务。",
                        "Completed all \(selected.count) visible tasks."))
        }
    }

    func place(_ task: HangingTask, lineX: Double) {
        _ = perform { try $0.place(task.id, lineX: lineX) }
    }

    func setReceiptFrame(_ frame: CGRect, on surface: DragSurface) {
        receiptFrames[surface] = frame
    }

    func beginDragging(_ task: HangingTask, on surface: DragSurface) {
        draggingTaskID = task.id
        dragSurface = surface
    }

    func updateDragging(at location: CGPoint, on surface: DragSurface) {
        dragLocation = location
        dragSurface = surface
    }

    func isDraggingOverReceipt(on surface: DragSurface) -> Bool {
        guard draggingTaskID != nil, dragSurface == surface, let dragLocation,
              let frame = receiptFrames[surface] else { return false }
        return frame.insetBy(dx: -10, dy: -8).contains(dragLocation)
    }

    func finishDragging(_ task: HangingTask, on surface: DragSurface, at location: CGPoint,
                        translation: CGSize, currentCenterX: CGFloat, stageWidth: CGFloat) {
        defer {
            draggingTaskID = nil
            dragLocation = nil
            dragSurface = nil
        }
        if let frame = receiptFrames[surface], frame.insetBy(dx: -10, dy: -8).contains(location) {
            complete(task)
            return
        }
        guard stageWidth > 0 else { return }
        place(task, lineX: Double(min(max((currentCenterX + translation.width) / stageWidth, 0.02), 0.98)))
    }
    func restore(_ task: HangingTask) {
        if perform({ try $0.restore(task.id) }) {
            if let restored = tasks.first(where: { $0.id == task.id }) { scheduleReminder(for: restored) }
            notify(text("已重新挂回晾衣绳。", "Hung back on the clothesline."))
        }
    }
    func takeOffSpike(_ task: HangingTask) {
        if perform({ try $0.setSpikeAttached(false, for: task.id) }) {
            notify(text("纸片已从完成钉上取下，仍保留在已完成。", "Removed from the spike and kept in Completed."))
        }
    }
    func putOnSpike(_ task: HangingTask, x: Double? = nil, y: Double? = nil, z: Double? = nil) {
        if perform({ try $0.placeOnSpike(task.id, x: x, y: y, z: z) }) {
            notify(text("纸片已重新串上完成钉。", "Placed back on the Done Spike."))
        }
    }
    func moveOnSpike(_ task: HangingTask, x: Double, y: Double, z: Double) {
        _ = perform { try $0.moveOnSpike(task.id, x: x, y: y, z: z) }
    }
    func resizeOnSpike(_ task: HangingTask, scale: Double) {
        _ = perform { try $0.resizeOnSpike(task.id, scale: scale) }
    }
    func remove(_ task: HangingTask) {
        guard perform({ try $0.delete(task.id) }) else { return }
        if let file = task.previewFile {
            previewCache[file] = nil
            try? FileManager.default.removeItem(at: previewDirectory.appendingPathComponent(file))
        }
        ReminderScheduler.cancel(task.id)
    }

    func removeAll(_ candidates: [HangingTask]) {
        let selected = candidates.filter { !$0.isCompleted }
        let ids = Set(selected.map(\.id))
        guard !ids.isEmpty else { return }
        if perform({ try $0.deleteAll(ids) }) {
            ids.forEach(ReminderScheduler.cancel)
            for file in selected.compactMap(\.previewFile) {
                previewCache[file] = nil
                try? FileManager.default.removeItem(at: previewDirectory.appendingPathComponent(file))
            }
            notify(text("已删除当前全部 \(selected.count) 件任务。",
                        "Deleted all \(selected.count) visible tasks."))
        }
    }
    func open(_ task: HangingTask) {
        if task.isImage {
            guard let previewURL = imagePreviewURL(for: task) else {
                error = text("图片预览已经不可用。", "The image preview is no longer available.")
                return
            }
            if !openImageOutsideHangLater(previewURL) {
                error = text("无法打开图片预览。", "The image preview could not be opened.")
            }
            return
        }
        // Meeting links should be handed back to macOS first so an installed meeting app
        // can claim its universal link. This also applies to links imported from Chrome,
        // which may carry a browser-specific resume URL.
        if task.isMeeting, let link = task.url, let url = TaskInput.webURL(link) {
            if !NSWorkspace.shared.open(url) {
                error = text("无法打开这个会议链接。", "This meeting link could not be opened.")
            }
            return
        }
        if let target = task.resumeURL, let url = TaskInput.resumeURL(target) {
            openResumeURL(url)
            return
        }
        if let link = task.url, let url = TaskInput.webURL(link) {
            if let chrome = NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.google.Chrome") {
                NSWorkspace.shared.open([url], withApplicationAt: chrome, configuration: NSWorkspace.OpenConfiguration()) { _, error in
                    if let error { Task { @MainActor in self.error = self.text("Chrome 无法打开网页：", "Chrome could not open the page: ") + error.localizedDescription } }
                }
            } else if !NSWorkspace.shared.open(url) {
                error = text("没有找到能打开此网页的浏览器。", "No browser was found to open this page.")
            }
            return
        }
        if let path = task.filePath {
            let fileURL = URL(fileURLWithPath: path)
            guard FileManager.default.fileExists(atPath: path) else {
                error = text("原文件已经移动或删除。任务和预览图仍然保留。", "The original file was moved or deleted. The task and its preview are still here.")
                return
            }
            if !NSWorkspace.shared.open(fileURL) { error = text("无法打开这个文件。", "This file could not be opened.") }
            return
        }
        if let bundleIdentifier = task.appBundleIdentifier {
            if let recovered = AppResumeTarget.resolve(bundleIdentifier: bundleIdentifier,
                                                       appName: task.appName,
                                                       windowTitle: task.title) {
                var updated = task
                updated.resumeURL = recovered.absoluteString
                _ = perform { try $0.update(updated) }
                openResumeURL(recovered)
                return
            }
            activateApplication(bundleIdentifier: bundleIdentifier)
            return
        }
        editingTask = task
        showMainWindow?()
    }

    func openPreview(_ task: HangingTask) {
        let previewURL: URL?
        if task.isImage {
            previewURL = imagePreviewURL(for: task)
        } else {
            previewURL = storedPreviewURL(for: task)
        }
        guard let previewURL else {
            error = text("缩略图预览已经不可用。", "The thumbnail preview is no longer available.")
            return
        }
        if !openImageOutsideHangLater(previewURL) {
            error = text("无法打开缩略图预览。", "The thumbnail preview could not be opened.")
        }
    }

    private func activateApplication(bundleIdentifier: String) {
        let workspace = NSWorkspace.shared
        let running = NSRunningApplication.runningApplications(withBundleIdentifier: bundleIdentifier).first
        let applicationURL = running?.bundleURL ?? workspace.urlForApplication(withBundleIdentifier: bundleIdentifier)
        guard let applicationURL else {
            error = text("没有找到这个 App。", "This app could not be found.")
            return
        }

        // Some Electron and menu-bar apps ignore a lone activate call. Unhide immediately,
        // then open the bundle with activation enabled so macOS also sends a reopen event.
        running?.unhide()
        _ = running?.activate(options: [.activateAllWindows])
        let configuration = NSWorkspace.OpenConfiguration()
        configuration.activates = true
        configuration.addsToRecentItems = false
        configuration.createsNewApplicationInstance = false
        workspace.openApplication(at: applicationURL, configuration: configuration) { application, failure in
            Task { @MainActor in
                if let failure {
                    self.error = self.text("无法打开这个 App：", "Could not open this app: ") + failure.localizedDescription
                    return
                }
                guard let application else {
                    self.error = self.text("这个 App 没有响应打开请求。", "This app did not respond to the open request.")
                    return
                }
                application.unhide()
                _ = application.activate(options: [.activateAllWindows])
            }
        }
    }

    private func openResumeURL(_ url: URL) {
        if let scheme = url.scheme?.lowercased(), ["http", "https"].contains(scheme) {
            if let chrome = NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.google.Chrome") {
                NSWorkspace.shared.open([url], withApplicationAt: chrome,
                                        configuration: NSWorkspace.OpenConfiguration()) { _, error in
                    if let error { Task { @MainActor in self.error = self.text("Chrome 无法打开网页：", "Chrome could not open the page: ") + error.localizedDescription } }
                }
                return
            }
        }
        if !NSWorkspace.shared.open(url) {
            error = text("无法回到这张纸片保存的位置。", "The saved location could not be opened.")
        }
    }

    func preview(_ task: HangingTask) -> NSImage? {
        guard let filename = task.previewFile, filename == URL(fileURLWithPath: filename).lastPathComponent else { return nil }
        if let cached = previewCache[filename] { return cached }
        if let path = task.filePath, URL(fileURLWithPath: path).pathExtension.lowercased() == "pdf",
           let thumbnail = pdfThumbnail(at: URL(fileURLWithPath: path)) {
            previewCache[filename] = thumbnail
            return thumbnail
        }
        if let loaded = NSImage(contentsOf: previewDirectory.appendingPathComponent(filename)) {
            let preview = task.appBundleIdentifier == nil ? loaded : normalizedAppPreview(loaded)
            previewCache[filename] = preview; return preview
        }
        return nil
    }

    func imagePreviewURL(for task: HangingTask) -> URL? {
        if let path = task.filePath,
           FileManager.default.fileExists(atPath: path) {
            return URL(fileURLWithPath: path)
        }
        return storedPreviewURL(for: task)
    }

    private func storedPreviewURL(for task: HangingTask) -> URL? {
        guard let filename = task.previewFile,
              filename == URL(fileURLWithPath: filename).lastPathComponent else { return nil }
        let storedPreview = previewDirectory.appendingPathComponent(filename)
        return FileManager.default.fileExists(atPath: storedPreview.path) ? storedPreview : nil
    }

    private func openImageOutsideHangLater(_ url: URL) -> Bool {
        if let openImagePreview { return openImagePreview(url) }
        let workspace = NSWorkspace.shared
        guard let previewApplication = workspace.urlForApplication(withBundleIdentifier: "com.apple.Preview") else {
            return workspace.open(url)
        }
        let configuration = NSWorkspace.OpenConfiguration()
        configuration.activates = true
        configuration.addsToRecentItems = false
        workspace.open([url], withApplicationAt: previewApplication, configuration: configuration) { _, failure in
            guard let failure else { return }
            Task { @MainActor in
                self.error = self.text("无法打开图片预览：", "The image preview could not be opened: ")
                    + failure.localizedDescription
            }
        }
        return true
    }

    /// Older ScreenCaptureKit captures sometimes placed a 1x window in the top-left of a 2x
    /// black canvas. Detect that geometry and crop only the synthetic padding at display time.
    private func normalizedAppPreview(_ image: NSImage) -> NSImage {
        var proposed = CGRect(origin: .zero, size: image.size)
        guard let source = image.cgImage(forProposedRect: &proposed, context: nil, hints: nil),
              source.width >= 240, source.height >= 180 else { return image }
        let width = source.width, height = source.height, bytesPerRow = width * 4
        var pixels = [UInt8](repeating: 0, count: bytesPerRow * height)
        guard let context = CGContext(data: &pixels, width: width, height: height,
                                      bitsPerComponent: 8, bytesPerRow: bytesPerRow,
                                      space: CGColorSpaceCreateDeviceRGB(),
                                      bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return image }
        context.draw(source, in: CGRect(x: 0, y: 0, width: width, height: height))

        func pixel(_ x: Int, _ y: Int) -> (Int, Int, Int) {
            let offset = y * bytesPerRow + x * 4
            return (Int(pixels[offset]), Int(pixels[offset + 1]), Int(pixels[offset + 2]))
        }
        let backgrounds = [pixel(0, 0), pixel(width - 1, 0),
                           pixel(0, height - 1), pixel(width - 1, height - 1)]
        let background = backgrounds[0]
        guard backgrounds.allSatisfy({ abs($0.0 - background.0) <= 5 && abs($0.1 - background.1) <= 5 && abs($0.2 - background.2) <= 5 }) else { return image }

        var minX = width, minY = height, maxX = -1, maxY = -1
        for y in 0..<height {
            for x in 0..<width {
                let value = pixel(x, y)
                let difference = max(max(abs(value.0 - background.0), abs(value.1 - background.1)),
                                     abs(value.2 - background.2))
                if difference > 9 {
                    minX = min(minX, x); minY = min(minY, y)
                    maxX = max(maxX, x); maxY = max(maxY, y)
                }
            }
        }
        guard maxX >= minX, maxY >= minY else { return image }
        let contentWidth = maxX - minX + 1, contentHeight = maxY - minY + 1
        let widthRatio = CGFloat(contentWidth) / CGFloat(width)
        let heightRatio = CGFloat(contentHeight) / CGFloat(height)
        guard widthRatio < 0.82, heightRatio < 0.82, abs(widthRatio - heightRatio) < 0.14 else { return image }
        let inset = 8
        let cropX = max(0, minX - inset), cropY = max(0, minY - inset)
        let cropWidth = min(width - cropX, contentWidth + inset * 2)
        let cropHeight = min(height - cropY, contentHeight + inset * 2)
        let crop = CGRect(x: CGFloat(cropX), y: CGFloat(cropY),
                          width: CGFloat(cropWidth), height: CGFloat(cropHeight))
        guard let cropped = source.cropping(to: crop) else { return image }
        return NSImage(cgImage: cropped, size: NSSize(width: cropped.width, height: cropped.height))
    }

    private func pdfThumbnail(at url: URL) -> NSImage? {
        guard let document = PDFDocument(url: url), let page = document.page(at: 0) else { return nil }
        let bounds = page.bounds(for: .mediaBox)
        guard bounds.width > 0, bounds.height > 0 else { return nil }
        let width: CGFloat = 720
        return page.thumbnail(of: NSSize(width: width, height: width * bounds.height / bounds.width),
                              for: .mediaBox)
    }

    func notify(_ text: String) {
        messageTimer?.invalidate(); message = text
        messageTimer = Timer.scheduledTimer(withTimeInterval: 4, repeats: false) { [weak self] _ in
            MainActor.assumeIsolated { self?.message = nil }
        }
    }

    func meetingPlatformName(_ platform: MeetingPlatform) -> String {
        switch platform {
        case .zoom: return "Zoom"
        case .tencentMeeting: return text("腾讯会议", "Tencent Meeting")
        case .feishu: return text("飞书会议", "Feishu Meetings")
        case .lark: return "Lark Meetings"
        }
    }

    private func scheduleReminder(for task: HangingTask) {
        ReminderScheduler.schedule(task, language: language) { [weak self] in
            self?.notify(self?.text("任务已保存；请在系统设置中允许 Hang Later 通知。",
                                    "Saved. Allow Hang Later notifications in System Settings.") ?? "")
        }
    }

    func sourceLabel(_ task: HangingTask) -> String {
        if task.kind == .idea { return text("一个想法", "An idea") }
        if let platform = task.meetingPlatform { return meetingPlatformName(platform) }
        if task.kind == .meeting { return text("在线会议", "Online meeting") }
        if task.isReminder { return text("提醒", "Reminder") }
        if let appName = task.appName { return appName }
        if task.isImage { return text("图片", "Image") }
        if let path = task.filePath { return URL(fileURLWithPath: path).pathExtension.uppercased() }
        return task.domain ?? text("一件小事", "A small thing")
    }

    func sourceSymbol(_ task: HangingTask) -> String {
        if task.isMeeting { return "video.fill" }
        if task.isReminder { return "bell.fill" }
        if task.appBundleIdentifier != nil { return "macwindow" }
        if task.isImage { return "photo" }
        if task.filePath != nil { return "doc" }
        return task.url == nil ? "pencil.line" : "globe"
    }

    func chooseImage() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.image]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.title = text("选择一张要晾起来的图片", "Choose an image to hang")
        panel.prompt = text("晾起来", "Hang")
        guard panel.runModal() == .OK, let url = panel.url else { return }
        importFile(url)
    }

    func importImage(_ image: NSImage, title: String, originalURL: URL? = nil) {
        guard let data = pngData(image) else {
            error = text("这张图片无法读取。", "This image could not be read.")
            return
        }
        _ = add(title: title, url: nil, note: "", dueAt: nil, screenshot: data,
                filePath: originalURL?.path, kind: .image)
    }

    func importAppWindow(title: String, appName: String, bundleIdentifier: String, screenshot: Data) -> Bool {
        let key = bundleIdentifier + "\u{1F}" + title
        let now = Date()
        if let previous = recentWindowImports[key], now.timeIntervalSince(previous) < 8 {
            notify(text("这个窗口刚刚已经挂好了。", "This window was just hung up."))
            return true
        }
        recentWindowImports[key] = now
        let resumeURL = AppResumeTarget.resolve(bundleIdentifier: bundleIdentifier,
                                                appName: appName, windowTitle: title)?.absoluteString
        let imported = add(title: title, url: nil, note: "", dueAt: nil, screenshot: screenshot,
                           appBundleIdentifier: bundleIdentifier, appName: appName,
                           resumeURL: resumeURL)
        if !imported { recentWindowImports[key] = nil }
        return imported
    }

    func acceptCompletionDrop(_ providers: [NSItemProvider]) -> Bool {
        guard let provider = providers.first(where: { $0.hasItemConformingToTypeIdentifier(UTType.plainText.identifier) }) else { return false }
        provider.loadItem(forTypeIdentifier: UTType.plainText.identifier, options: nil) { value, _ in
            let text = Self.itemText(value)
            Task { @MainActor in
                guard let text, let id = Self.taskID(from: text), let task = self.tasks.first(where: { $0.id == id && !$0.isCompleted }) else { return }
                self.complete(task)
            }
        }
        return true
    }

    func acceptDrop(_ providers: [NSItemProvider], before target: UUID? = nil) -> Bool {
        let supported = providers.compactMap { provider in
            DropSupport.preferredKind(for: provider).map { (provider, $0) }
        }
        if !supported.isEmpty { onExternalDropAccepted?() }
        for (provider, kind) in supported {
            switch kind {
            case .fileURL:
                loadFile(provider)
            case let .application(typeIdentifier):
                loadApplication(provider, typeIdentifier: typeIdentifier)
            case .image:
                let suggestedName = provider.suggestedName
                provider.loadObject(ofClass: NSImage.self) { value, error in
                    Task { @MainActor in
                        guard let image = value as? NSImage else { if let error { self.error = error.localizedDescription }; return }
                        self.importImage(image, title: suggestedName ?? self.text("拖入的图片", "Dropped image"))
                    }
                }
            case .url, .plainText:
                let type = kind == .url ? UTType.url.identifier : UTType.plainText.identifier
                provider.loadItem(forTypeIdentifier: type, options: nil) { value, error in
                    let text = Self.itemText(value)
                    Task { @MainActor in
                        guard let text else { if let error { self.error = error.localizedDescription }; return }
                        if let id = Self.taskID(from: text) {
                            if let target { _ = self.perform { try $0.move(id, before: target) } }
                        } else { self.addText(text) }
                    }
                }
            }
        }
        return !supported.isEmpty
    }

    private func loadApplication(_ provider: NSItemProvider, typeIdentifier: String) {
        let suggestedName = provider.suggestedName
        provider.loadInPlaceFileRepresentation(forTypeIdentifier: typeIdentifier) { url, _, error in
            Task { @MainActor in
                if let url { self.importFile(url); return }
                if let resolved = Self.applicationURL(named: suggestedName) {
                    self.importFile(resolved)
                } else {
                    self.error = error?.localizedDescription
                        ?? self.text("无法读取拖入的 App。", "The dropped app could not be read.")
                }
            }
        }
    }

    private func loadFile(_ provider: NSItemProvider) {
        provider.loadItem(forTypeIdentifier: UTType.fileURL.identifier, options: nil) { value, error in
            let url = Self.itemURL(value)
            Task { @MainActor in
                guard let url else { if let error { self.error = error.localizedDescription }; return }
                self.importFile(url)
            }
        }
    }

    private func importFile(_ url: URL) {
        let type = UTType(filenameExtension: url.pathExtension)
        if type?.conforms(to: .image) == true, let image = NSImage(contentsOf: url) {
            importImage(image, title: url.deletingPathExtension().lastPathComponent, originalURL: url)
            return
        }
        if type?.conforms(to: .pdf) == true, let image = pdfThumbnail(at: url), let data = pngData(image) {
            _ = add(title: url.lastPathComponent, url: nil, note: "", dueAt: nil,
                    screenshot: data, filePath: url.path)
            return
        }
        if DocumentPreviewSupport.usesQuickLook(url) {
            Task {
                let image = await DocumentThumbnail.image(for: url)
                    ?? NSWorkspace.shared.icon(forFile: url.path)
                guard let data = self.pngData(image) else { return }
                _ = self.add(title: url.lastPathComponent, url: nil, note: "", dueAt: nil,
                             screenshot: data, filePath: url.path)
            }
            return
        }
        if url.pathExtension.lowercased() == "app", let bundle = Bundle(url: url) {
            let identifier = bundle.bundleIdentifier
            let name = (bundle.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String)
                ?? (bundle.object(forInfoDictionaryKey: "CFBundleName") as? String)
                ?? url.deletingPathExtension().lastPathComponent
            if let identifier {
                Task {
                    if let captured = try? await WindowCaptureService.captureBestWindow(bundleIdentifier: identifier) {
                        _ = self.importAppWindow(title: captured.title, appName: captured.appName,
                                                 bundleIdentifier: identifier, screenshot: captured.pngData)
                    } else {
                        let icon = NSWorkspace.shared.icon(forFile: url.path)
                        guard let data = self.pngData(icon) else { return }
                        _ = self.add(title: name, url: nil, note: "", dueAt: nil,
                                     screenshot: data, appBundleIdentifier: identifier, appName: name)
                    }
                }
            }
            return
        }
        let icon = NSWorkspace.shared.icon(forFile: url.path)
        guard let data = pngData(icon) else { return }
        _ = add(title: url.lastPathComponent, url: nil, note: "", dueAt: nil,
                screenshot: data, filePath: url.path)
    }

    /// Replaces the small generic icons saved by older versions with content thumbnails.
    private func refreshDocumentThumbnails() async {
        let candidates = tasks.compactMap { task -> (HangingTask, URL)? in
            guard let path = task.filePath else { return nil }
            let url = URL(fileURLWithPath: path)
            guard DocumentPreviewSupport.usesQuickLook(url),
                  FileManager.default.fileExists(atPath: url.path),
                  needsDocumentThumbnail(task) else { return nil }
            return (task, url)
        }
        for (task, url) in candidates {
            guard let image = await DocumentThumbnail.image(for: url) else { continue }
            replacePreview(for: task, with: image)
        }
    }

    private func needsDocumentThumbnail(_ task: HangingTask) -> Bool {
        guard let filename = task.previewFile,
              filename == URL(fileURLWithPath: filename).lastPathComponent,
              let image = NSImage(contentsOf: previewDirectory.appendingPathComponent(filename)) else {
            return true
        }
        // Older imports saved the generic 32- or 64-point file icon. Content thumbnails are
        // requested at 960 points, so this cleanly distinguishes legacy cards without a schema change.
        return image.size.width <= 128 && image.size.height <= 128
    }

    private func replacePreview(for task: HangingTask, with image: NSImage) {
        guard var current = tasks.first(where: { $0.id == task.id }),
              current.filePath == task.filePath,
              let data = pngData(image) else { return }
        let oldFilename = current.previewFile
        let filename = UUID().uuidString + ".png"
        let destination = previewDirectory.appendingPathComponent(filename)
        do {
            try data.write(to: destination, options: .atomic)
            current.previewFile = filename
            if perform({ try $0.update(current) }) {
                previewCache[filename] = image
                if let oldFilename, oldFilename != filename {
                    previewCache[oldFilename] = nil
                    try? FileManager.default.removeItem(at: previewDirectory.appendingPathComponent(oldFilename))
                }
            } else {
                try? FileManager.default.removeItem(at: destination)
            }
        } catch {
            // The original icon remains usable when Quick Look's optional preview cannot be saved.
        }
    }

    nonisolated private static func itemText(_ value: NSSecureCoding?) -> String? {
        if let url = value as? URL { return url.absoluteString }
        if let string = value as? String { return string }
        if let data = value as? Data { return String(data: data, encoding: .utf8) }
        return nil
    }

    nonisolated private static func itemURL(_ value: NSSecureCoding?) -> URL? {
        if let url = value as? URL { return url }
        let text: String?
        if let data = value as? Data { text = String(data: data, encoding: .utf8) }
        else { text = value as? String }
        guard let clean = text?.trimmingCharacters(in: .whitespacesAndNewlines), !clean.isEmpty else { return nil }
        return clean.hasPrefix("/") ? URL(fileURLWithPath: clean) : URL(string: clean)
    }

    nonisolated private static func applicationURL(named suggestedName: String?) -> URL? {
        guard var name = suggestedName?.trimmingCharacters(in: .whitespacesAndNewlines), !name.isEmpty else { return nil }
        if name.lowercased().hasSuffix(".app") { name.removeLast(4) }
        let roots = ["/Applications", "/System/Applications", "/System/Applications/Utilities",
                     FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Applications").path]
        return roots.lazy.map { URL(fileURLWithPath: $0).appendingPathComponent(name + ".app") }
            .first { FileManager.default.fileExists(atPath: $0.path) }
    }

    nonisolated private static func taskID(from text: String) -> UUID? {
        guard text.hasPrefix("hanglater-task:") else { return nil }
        return UUID(uuidString: String(text.dropFirst(15)))
    }

    func refreshMetadata(_ task: HangingTask) { fetchMetadata(task) }

    private func fetchMetadata(_ task: HangingTask, updateTitle: Bool = false) {
        guard let link = task.url, let url = TaskInput.webURL(link) else { return }
        let provider = LPMetadataProvider(); provider.timeout = 10
        metadataProviders[task.id] = provider
        provider.startFetchingMetadata(for: url) { [weak self] metadata, _ in
            Task { @MainActor in
                guard let self else { return }
                self.metadataProviders[task.id] = nil
                // Keep a title the user has edited while metadata was loading.
                if updateTitle, let title = metadata?.title, !title.isEmpty,
                   var current = self.tasks.first(where: { $0.id == task.id }), current.title == task.title, current.url == link {
                    current.title = title
                    _ = self.perform { try $0.update(current) }
                }
                if let imageProvider = metadata?.imageProvider ?? metadata?.iconProvider {
                    imageProvider.loadObject(ofClass: NSImage.self) { image, _ in
                        Task { @MainActor in
                            guard let image = image as? NSImage, let data = self.pngData(image),
                                  var current = self.tasks.first(where: { $0.id == task.id }), current.previewFile == nil, current.url == link else { return }
                            let filename = UUID().uuidString + ".png"
                            do {
                                try data.write(to: self.previewDirectory.appendingPathComponent(filename), options: .atomic)
                                current.previewFile = filename
                                if !self.perform({ try $0.update(current) }) {
                                    try? FileManager.default.removeItem(at: self.previewDirectory.appendingPathComponent(filename))
                                }
                            } catch { /* A URL remains usable when its optional preview cannot be fetched. */ }
                        }
                    }
                }
            }
        }
    }

    private func pngData(_ image: NSImage) -> Data? {
        // Keep screenshots small enough for a long-lived local task collection.
        let width = min(960, image.size.width)
        guard width > 0, image.size.height > 0 else { return nil }
        let size = NSSize(width: width, height: width * image.size.height / image.size.width)
        let scaled = NSImage(size: size)
        scaled.lockFocus(); image.draw(in: NSRect(origin: .zero, size: size)); scaled.unlockFocus()
        guard let tiff = scaled.tiffRepresentation, let bitmap = NSBitmapImageRep(data: tiff) else { return nil }
        return bitmap.representation(using: .png, properties: [:])
    }

    private func localized(_ error: Error) -> String {
        guard language == .en else { return error.localizedDescription }
        if let repositoryError = error as? RepositoryError {
            switch repositoryError {
            case .emptyTitle: return "Give this task a name."
            case .invalidURL: return "Enter a complete http or https web link."
            case .missingTask: return "This task no longer exists."
            case .unreadableData: return "The task file could not be read. The original file was preserved."
            }
        }
        if let taskTypeError = error as? TaskTypeRepositoryError {
            switch taskTypeError {
            case .emptyName: return "Give every task type a name."
            case .duplicateName: return "Task type names must be unique."
            case .unreadableData: return "The task type file could not be read. The original file was preserved."
            }
        }
        return error.localizedDescription
    }

    private struct BrowserCapture: Decodable { let title: String; let url: String; let screenshot: String? }
    private func importInbox() {
        guard repository != nil, let files = try? FileManager.default.contentsOfDirectory(at: inboxDirectory, includingPropertiesForKeys: nil) else { return }
        for file in files.filter({ $0.pathExtension == "json" }).sorted(by: { $0.lastPathComponent < $1.lastPathComponent }) {
            do {
                // A crash after saving a task but before deleting its inbox file must not duplicate it.
                if tasks.contains(where: { $0.captureID == file.lastPathComponent }) {
                    try FileManager.default.removeItem(at: file)
                    continue
                }
                let data = try Data(contentsOf: file)
                guard data.count <= 16 * 1024 * 1024 else { throw RepositoryError.invalidURL }
                let capture = try JSONDecoder().decode(BrowserCapture.self, from: data)
                guard let url = TaskInput.webURL(capture.url) else { throw RepositoryError.invalidURL }
                let screenshot = capture.screenshot.flatMap { Data(base64Encoded: $0) }
                if add(title: capture.title.isEmpty ? url.host! : capture.title, url: url.absoluteString,
                       note: "", dueAt: nil, screenshot: screenshot,
                       usePageTitle: capture.title == url.host, captureID: file.lastPathComponent) {
                    try FileManager.default.removeItem(at: file)
                }
            } catch {
                // Quarantine invalid captures so a bad file cannot repeat every second.
                try? FileManager.default.moveItem(at: file, to: file.appendingPathExtension("invalid"))
                self.error = text("有一个网页未能导入：", "A web page could not be imported: ") + localized(error)
            }
        }
    }
}
