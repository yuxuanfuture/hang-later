import Foundation

/// A configurable daily work window. The end can explicitly belong to either
/// the same calendar day or the following one.
public struct WorkdaySchedule: Equatable, Sendable {
    public let startMinute: Int
    public let endMinute: Int
    public let endsOnFollowingDay: Bool

    public init(startMinute: Int, endMinute: Int, endsOnFollowingDay: Bool? = nil) {
        let clampedStart = min(max(startMinute, 0), 1439)
        let clampedEnd = min(max(endMinute, 0), 1439)
        self.startMinute = clampedStart
        self.endMinute = clampedEnd
        self.endsOnFollowingDay = endsOnFollowingDay ?? (clampedEnd <= clampedStart)
    }

    public func interval(containing date: Date,
                         calendar: Calendar = .autoupdatingCurrent) -> DateInterval? {
        let today = calendar.startOfDay(for: date)
        let yesterday = calendar.date(byAdding: .day, value: -1, to: today) ?? today
        for workday in [today, yesterday] {
            guard let interval = interval(startingOn: workday, calendar: calendar) else { continue }
            if date >= interval.start && date < interval.end { return interval }
        }
        return nil
    }

    public func contains(_ publishedAt: Date, inWorkdayContaining reference: Date,
                         calendar: Calendar = .autoupdatingCurrent) -> Bool {
        guard let interval = interval(containing: reference, calendar: calendar) else { return false }
        return publishedAt >= interval.start && publishedAt < interval.end
    }

    public func interval(startingOn day: Date,
                         calendar: Calendar = .autoupdatingCurrent) -> DateInterval? {
        let startDay = calendar.startOfDay(for: day)
        guard let start = time(startMinute, on: startDay, calendar: calendar) else { return nil }
        let endDay: Date
        if endsOnFollowingDay {
            guard let nextDay = calendar.date(byAdding: .day, value: 1, to: startDay) else { return nil }
            endDay = nextDay
        } else {
            endDay = startDay
        }
        guard let end = time(endMinute, on: endDay, calendar: calendar), end > start else { return nil }
        return DateInterval(start: start, end: end)
    }

    private func time(_ minute: Int, on day: Date, calendar: Calendar) -> Date? {
        calendar.date(bySettingHour: minute / 60, minute: minute % 60, second: 0, of: day)
            ?? calendar.date(byAdding: .minute, value: minute, to: calendar.startOfDay(for: day))
    }
}
