import Foundation

public enum HangingTaskKind: String, Codable, Sendable {
    case image
    case idea
    case reminder
    case meeting
}

public struct HangingTask: Codable, Identifiable, Equatable, Sendable {
    public var id: UUID
    public var title: String
    public var url: String?
    public var note: String
    public var createdAt: Date
    public var completedAt: Date?
    public var dueAt: Date?
    public var previewFile: String?
    public var tint: Int
    public var captureID: String?
    public var filePath: String?
    public var appBundleIdentifier: String?
    public var appName: String?
    /// A URL that restores the captured app content, such as a Chrome page or Obsidian note.
    public var resumeURL: String?
    /// Explicitly marks quick-captured ideas so a future response harness can address them.
    public var kind: HangingTaskKind?
    /// Optional user-defined type. The type's name and border color live in task-types.json.
    public var taskTypeID: UUID?
    /// Horizontal position on the clothesline, expressed as a 0...1 fraction.
    public var lineX: Double?
    /// Increasing layer number lets overlapping cards keep the last-moved card on top.
    public var lineLayer: Int?
    /// Nil keeps legacy completed tasks on the spike; false means explicitly removed.
    public var spikeAttached: Bool?
    /// User-selected offset on the horizontal paper plane while it is pierced by the spike.
    public var spikeX: Double?
    /// User-selected height along the spike.
    public var spikeY: Double?
    public var spikeZ: Double?
    /// Per-slip size on the Done Spike. Nil keeps legacy tasks at 100%.
    public var spikeScale: Double?

    public init(id: UUID = UUID(), title: String, url: String? = nil, note: String = "",
                createdAt: Date = Date(), completedAt: Date? = nil, dueAt: Date? = nil,
                previewFile: String? = nil, tint: Int = 0, captureID: String? = nil,
                filePath: String? = nil, appBundleIdentifier: String? = nil,
                appName: String? = nil, resumeURL: String? = nil,
                kind: HangingTaskKind? = nil,
                taskTypeID: UUID? = nil,
                lineX: Double? = nil, lineLayer: Int? = nil,
                spikeAttached: Bool? = nil, spikeX: Double? = nil, spikeY: Double? = nil,
                spikeZ: Double? = nil, spikeScale: Double? = nil) {
        self.id = id; self.title = title; self.url = url; self.note = note
        self.createdAt = createdAt; self.completedAt = completedAt; self.dueAt = dueAt
        self.previewFile = previewFile; self.tint = tint
        self.captureID = captureID
        self.filePath = filePath
        self.appBundleIdentifier = appBundleIdentifier
        self.appName = appName
        self.resumeURL = resumeURL
        self.kind = kind
        self.taskTypeID = taskTypeID
        self.lineX = lineX
        self.lineLayer = lineLayer
        self.spikeAttached = spikeAttached
        self.spikeX = spikeX
        self.spikeY = spikeY
        self.spikeZ = spikeZ
        self.spikeScale = spikeScale
    }

    public var isCompleted: Bool { completedAt != nil }
    public var isOnSpike: Bool { isCompleted && (spikeAttached ?? true) }
    public var effectiveSpikeScale: Double { min(max(spikeScale ?? 1, 0.6), 1.5) }
    public var meetingPlatform: MeetingPlatform? { MeetingLink.platform(for: url) }
    public var isMeeting: Bool { kind == .meeting || meetingPlatform != nil }
    public var isReminder: Bool { kind == .reminder || (!isMeeting && dueAt != nil) }
    public var domain: String? {
        guard let url, let host = URL(string: url)?.host else { return nil }
        return host.hasPrefix("www.") ? String(host.dropFirst(4)) : host
    }
    public var isImage: Bool {
        if kind == .image { return true }
        if let filePath,
           Self.imageExtensions.contains(URL(fileURLWithPath: filePath).pathExtension.lowercased()) {
            return true
        }
        // v0.10.0 stored images dragged from browsers and other apps as preview-only tasks.
        // They have no original file path, so keep recognizing that legacy representation.
        return previewFile != nil && url == nil && filePath == nil
            && appBundleIdentifier == nil && resumeURL == nil && kind != .idea
    }

    private static let imageExtensions: Set<String> = ["png", "jpg", "jpeg", "heic", "heif", "gif", "webp", "tif", "tiff", "bmp"]
}

public enum TaskInput {
    /// Only web URLs are accepted. Text that merely contains a URL remains a note.
    public static func webURL(_ text: String) -> URL? {
        var candidate = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !candidate.isEmpty, !candidate.contains(where: { $0.isWhitespace || $0.isNewline }) else { return nil }
        if candidate.hasPrefix("www.") { candidate = "https://" + candidate }
        guard let url = URL(string: candidate), let scheme = url.scheme?.lowercased(),
              ["http", "https"].contains(scheme), let host = url.host, !host.isEmpty,
              url.user == nil, url.password == nil else { return nil }
        return url
    }

    /// Restore targets are deliberately limited to normal web pages and Obsidian deep links.
    public static func resumeURL(_ text: String) -> URL? {
        guard let url = URL(string: text), let scheme = url.scheme?.lowercased() else { return nil }
        if ["http", "https"].contains(scheme) { return webURL(text) }
        guard scheme == "obsidian", url.host == "open" else { return nil }
        return url
    }
}

public enum RepositoryError: LocalizedError {
    case emptyTitle, invalidURL, missingTask, unreadableData
    public var errorDescription: String? {
        switch self {
        case .emptyTitle: return "给这件事起个名字吧。"
        case .invalidURL: return "请输入完整的 http 或 https 网页链接。"
        case .missingTask: return "这件任务已经不存在了。"
        case .unreadableData: return "任务文件无法读取。原文件已保留，请先备份并检查它。"
        }
    }
}

/// Each mutation is saved atomically before the in-memory state changes.
/// A corrupt file is never overwritten with an empty task list.
public final class TaskRepository {
    public private(set) var tasks: [HangingTask]
    public let directory: URL
    public var dataFile: URL { directory.appendingPathComponent("tasks.json") }

    public init(directory: URL) throws {
        self.directory = directory
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let file = directory.appendingPathComponent("tasks.json")
        if FileManager.default.fileExists(atPath: file.path) {
            do { tasks = try JSONDecoder().decode([HangingTask].self, from: Data(contentsOf: file)) }
            catch { throw RepositoryError.unreadableData }
        } else { tasks = [] }
    }

    public var pending: [HangingTask] { tasks.filter { !$0.isCompleted } }
    public var completed: [HangingTask] {
        tasks.filter(\.isCompleted).sorted { $0.completedAt! > $1.completedAt! }
    }

    @discardableResult
    public func add(title: String, url: String? = nil, note: String = "", dueAt: Date? = nil,
                    previewFile: String? = nil, captureID: String? = nil,
                    filePath: String? = nil, appBundleIdentifier: String? = nil,
                    appName: String? = nil, resumeURL: String? = nil,
                    kind: HangingTaskKind? = nil, taskTypeID: UUID? = nil) throws -> HangingTask {
        let cleanTitle = title.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanTitle.isEmpty else { throw RepositoryError.emptyTitle }
        if let url, TaskInput.webURL(url) == nil { throw RepositoryError.invalidURL }
        if let resumeURL, TaskInput.resumeURL(resumeURL) == nil { throw RepositoryError.invalidURL }
        let task = HangingTask(title: cleanTitle, url: url, note: note, dueAt: dueAt,
                               previewFile: previewFile, tint: tasks.count % 4, captureID: captureID,
                               filePath: filePath, appBundleIdentifier: appBundleIdentifier,
                               appName: appName, resumeURL: resumeURL, kind: kind,
                               taskTypeID: taskTypeID)
        try commit([task] + tasks)
        return task
    }

    public func update(_ task: HangingTask) throws {
        guard !task.title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw RepositoryError.emptyTitle }
        if let url = task.url, TaskInput.webURL(url) == nil { throw RepositoryError.invalidURL }
        if let resumeURL = task.resumeURL, TaskInput.resumeURL(resumeURL) == nil { throw RepositoryError.invalidURL }
        guard let index = tasks.firstIndex(where: { $0.id == task.id }) else { throw RepositoryError.missingTask }
        var next = tasks; next[index] = task
        try commit(next)
    }

    public func complete(_ id: UUID, at date: Date = Date()) throws {
        guard var task = tasks.first(where: { $0.id == id }) else { throw RepositoryError.missingTask }
        task.completedAt = date
        task.spikeAttached = true
        try update(task)
    }

    public func completeAll(_ ids: Set<UUID>, at date: Date = Date()) throws {
        guard !ids.isEmpty else { return }
        var next = tasks
        for index in next.indices where ids.contains(next[index].id) && !next[index].isCompleted {
            next[index].completedAt = date
            next[index].spikeAttached = true
        }
        try commit(next)
    }

    public func restore(_ id: UUID) throws {
        guard var task = tasks.first(where: { $0.id == id }) else { throw RepositoryError.missingTask }
        task.completedAt = nil
        task.spikeAttached = nil
        try update(task)
    }

    public func setSpikeAttached(_ attached: Bool, for id: UUID) throws {
        guard var task = tasks.first(where: { $0.id == id }) else { throw RepositoryError.missingTask }
        guard task.isCompleted else { return }
        task.spikeAttached = attached
        try update(task)
    }

    public func placeOnSpike(_ id: UUID, x: Double? = nil, y: Double? = nil,
                             z: Double? = nil) throws {
        guard var task = tasks.first(where: { $0.id == id }) else { throw RepositoryError.missingTask }
        guard task.isCompleted else { return }
        task.spikeAttached = true
        if let x { task.spikeX = min(max(x, -0.95), 0.95) }
        if let y { task.spikeY = min(max(y, 0.72), 3.05) }
        if let z { task.spikeZ = min(max(z, -0.38), 0.38) }
        try update(task)
    }

    public func moveOnSpike(_ id: UUID, x: Double, y: Double, z: Double) throws {
        guard var task = tasks.first(where: { $0.id == id }) else { throw RepositoryError.missingTask }
        guard task.isOnSpike else { return }
        task.spikeX = min(max(x, -0.95), 0.95)
        task.spikeY = min(max(y, 0.72), 3.05)
        task.spikeZ = min(max(z, -0.38), 0.38)
        try update(task)
    }

    public func resizeOnSpike(_ id: UUID, scale: Double) throws {
        guard var task = tasks.first(where: { $0.id == id }) else { throw RepositoryError.missingTask }
        guard task.isOnSpike else { return }
        task.spikeScale = min(max(scale, 0.6), 1.5)
        try update(task)
    }

    public func delete(_ id: UUID) throws {
        try commit(tasks.filter { $0.id != id })
    }

    public func deleteAll(_ ids: Set<UUID>) throws {
        guard !ids.isEmpty else { return }
        try commit(tasks.filter { !ids.contains($0.id) })
    }

    public func retainTaskTypeAssignments(in validIDs: Set<UUID>) throws {
        var next = tasks
        var changed = false
        for index in next.indices {
            if let typeID = next[index].taskTypeID, !validIDs.contains(typeID) {
                next[index].taskTypeID = nil
                changed = true
            }
        }
        if changed { try commit(next) }
    }

    public func move(_ id: UUID, before other: UUID) throws {
        guard id != other, let from = tasks.firstIndex(where: { $0.id == id }),
              tasks.contains(where: { $0.id == other }), !tasks[from].isCompleted else { return }
        var next = tasks
        let item = next.remove(at: from)
        guard let to = next.firstIndex(where: { $0.id == other && !$0.isCompleted }) else { return }
        next.insert(item, at: to)
        try commit(next)
    }

    public func place(_ id: UUID, lineX: Double) throws {
        guard var task = tasks.first(where: { $0.id == id }), !task.isCompleted else { return }
        task.lineX = min(max(lineX, 0), 1)
        task.lineLayer = (tasks.compactMap(\.lineLayer).max() ?? 0) + 1
        try update(task)
    }

    private func commit(_ next: [HangingTask]) throws {
        let encoder = JSONEncoder(); encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try encoder.encode(next).write(to: dataFile, options: .atomic)
        tasks = next
    }
}
