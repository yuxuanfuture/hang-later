import Foundation

public enum CompletionGrouping: String, CaseIterable, Sendable {
    case day
    case week
    case month
}

public struct CompletionSection: Identifiable, Equatable, Sendable {
    public let start: Date
    public let tasks: [HangingTask]
    public var id: Date { start }
}

public enum CompletionArchive {
    public static func sections(from tasks: [HangingTask], grouping: CompletionGrouping,
                                calendar: Calendar = .current) -> [CompletionSection] {
        let completed = tasks.filter { $0.completedAt != nil }
        let grouped = Dictionary(grouping: completed) { task -> Date in
            let date = task.completedAt!
            switch grouping {
            case .month:
                return calendar.dateInterval(of: .month, for: date)?.start
                    ?? calendar.startOfDay(for: date)
            case .week:
                return calendar.dateInterval(of: .weekOfYear, for: date)?.start
                    ?? calendar.startOfDay(for: date)
            case .day:
                return calendar.startOfDay(for: date)
            }
        }
        return grouped.map { start, tasks in
            CompletionSection(start: start,
                              tasks: tasks.sorted { $0.completedAt! > $1.completedAt! })
        }.sorted { $0.start > $1.start }
    }
}
