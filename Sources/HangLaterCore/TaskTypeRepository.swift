import Foundation

public struct TaskTypeDefinition: Codable, Identifiable, Equatable, Sendable {
    public var id: UUID
    public var name: String
    public var colorHex: UInt32

    public init(id: UUID = UUID(), name: String, colorHex: UInt32) {
        self.id = id
        self.name = name
        self.colorHex = colorHex & 0xFFFFFF
    }
}

public enum TaskTypeRepositoryError: LocalizedError {
    case emptyName
    case duplicateName
    case unreadableData

    public var errorDescription: String? {
        switch self {
        case .emptyName: return "请为每个任务类型命名。"
        case .duplicateName: return "任务类型的名称不能重复。"
        case .unreadableData: return "任务类型文件无法读取。原文件已保留。"
        }
    }
}

public final class TaskTypeRepository {
    public private(set) var taskTypes: [TaskTypeDefinition]
    public let dataFile: URL

    public init(directory: URL) throws {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        dataFile = directory.appendingPathComponent("task-types.json")
        if FileManager.default.fileExists(atPath: dataFile.path) {
            do {
                taskTypes = try JSONDecoder().decode([TaskTypeDefinition].self,
                                                     from: Data(contentsOf: dataFile))
            } catch {
                throw TaskTypeRepositoryError.unreadableData
            }
        } else {
            taskTypes = []
        }
    }

    public func replace(with definitions: [TaskTypeDefinition]) throws {
        let cleaned = definitions.map {
            TaskTypeDefinition(id: $0.id,
                               name: $0.name.trimmingCharacters(in: .whitespacesAndNewlines),
                               colorHex: $0.colorHex)
        }
        guard cleaned.allSatisfy({ !$0.name.isEmpty }) else {
            throw TaskTypeRepositoryError.emptyName
        }
        let normalized = cleaned.map { $0.name.folding(options: [.caseInsensitive, .diacriticInsensitive],
                                                        locale: .current) }
        guard Set(normalized).count == normalized.count else {
            throw TaskTypeRepositoryError.duplicateName
        }
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try encoder.encode(cleaned).write(to: dataFile, options: .atomic)
        taskTypes = cleaned
    }
}
