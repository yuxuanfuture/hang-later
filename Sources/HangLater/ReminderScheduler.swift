import Foundation
import HangLaterCore
import UserNotifications

enum ReminderScheduler {
    private static let identifierPrefix = "hanglater.task."
    private static var isAvailable: Bool {
        Bundle.main.bundleURL.pathExtension == "app" && Bundle.main.bundleIdentifier != nil
    }

    static func schedule(_ task: HangingTask, language: AppLanguage,
                         requestPermission: Bool = true,
                         onPermissionDenied: (() -> Void)? = nil) {
        guard isAvailable else { return }
        guard let dueAt = task.dueAt, dueAt > Date(), !task.isCompleted else {
            cancel(task.id)
            return
        }
        let center = UNUserNotificationCenter.current()
        center.getNotificationSettings { settings in
            switch settings.authorizationStatus {
            case .authorized, .provisional:
                add(task, dueAt: dueAt, language: language, center: center)
            case .notDetermined where requestPermission:
                center.requestAuthorization(options: [.alert, .sound]) { granted, _ in
                    if granted {
                        add(task, dueAt: dueAt, language: language, center: center)
                    } else {
                        DispatchQueue.main.async { onPermissionDenied?() }
                    }
                }
            case .denied:
                DispatchQueue.main.async { onPermissionDenied?() }
            default:
                break
            }
        }
    }

    static func cancel(_ id: UUID) {
        guard isAvailable else { return }
        UNUserNotificationCenter.current()
            .removePendingNotificationRequests(withIdentifiers: [identifier(for: id)])
    }

    static func synchronize(_ tasks: [HangingTask], language: AppLanguage) {
        guard isAvailable else { return }
        let center = UNUserNotificationCenter.current()
        center.getNotificationSettings { settings in
            guard [.authorized, .provisional].contains(settings.authorizationStatus) else { return }
            center.getPendingNotificationRequests { requests in
                let stale = requests.map(\.identifier).filter { $0.hasPrefix(identifierPrefix) }
                center.removePendingNotificationRequests(withIdentifiers: stale)
                for task in tasks where !task.isCompleted {
                    guard let dueAt = task.dueAt, dueAt > Date() else { continue }
                    add(task, dueAt: dueAt, language: language, center: center)
                }
            }
        }
    }

    private static func add(_ task: HangingTask, dueAt: Date, language: AppLanguage,
                            center: UNUserNotificationCenter) {
        let content = UNMutableNotificationContent()
        content.title = task.isMeeting
            ? language.text("会议时间到了", "It’s meeting time")
            : language.text("提醒时间到了", "It’s time")
        content.body = task.title
        content.sound = .default
        content.userInfo = ["taskID": task.id.uuidString]
        let components = Calendar.current.dateComponents([.year, .month, .day, .hour, .minute],
                                                         from: dueAt)
        let trigger = UNCalendarNotificationTrigger(dateMatching: components, repeats: false)
        let request = UNNotificationRequest(identifier: identifier(for: task.id),
                                            content: content, trigger: trigger)
        center.add(request)
    }

    private static func identifier(for id: UUID) -> String {
        identifierPrefix + id.uuidString
    }
}
