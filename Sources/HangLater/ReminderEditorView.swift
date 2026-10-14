import HangLaterCore
import SwiftUI

struct ReminderEditorView: View {
    @ObservedObject var store: AppStore
    let task: HangingTask

    @Environment(\.dismiss) private var dismiss
    @State private var reminderAt: Date
    @State private var validation: String?

    init(store: AppStore, task: HangingTask) {
        self.store = store
        self.task = task
        let proposed = task.dueAt ?? Self.defaultReminderDate()
        _reminderAt = State(initialValue: max(proposed, Date()))
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            HStack(spacing: 10) {
                Image(systemName: "alarm.fill")
                    .font(.system(size: 15, weight: .medium))
                Text(task.dueAt == nil
                     ? store.text("添加提醒", "ADD REMINDER")
                     : store.text("修改提醒", "CHANGE REMINDER"))
                    .font(LeLaboType.display(18)).tracking(0.6)
                Spacer()
                Button { dismiss() } label: { Image(systemName: "xmark") }
                    .buttonStyle(.plain).keyboardShortcut(.cancelAction)
            }

            VStack(alignment: .leading, spacing: 7) {
                Text(store.text("这件事", "TASK"))
                    .font(LeLaboType.label(9, bold: true)).foregroundStyle(Palette.muted)
                Text(task.title)
                    .font(LeLaboType.copy(13, bold: true)).lineLimit(2)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .padding(14)
            .background(Palette.raisedSurface, in: Rectangle())
            .overlay(Rectangle().stroke(Palette.border, lineWidth: 1))

            HStack {
                Label(store.text("提醒时间", "Reminder"), systemImage: "bell")
                    .font(LeLaboType.label(10, bold: true))
                Spacer()
                DatePicker("", selection: $reminderAt, in: Date()...,
                           displayedComponents: [.date, .hourAndMinute])
                    .labelsHidden().frame(width: 230)
            }

            if let validation {
                Text(validation)
                    .font(LeLaboType.copy(10, bold: true)).foregroundStyle(Palette.danger)
            }

            HStack {
                if task.dueAt != nil {
                    Button(store.text("移除提醒", "Remove reminder"), role: .destructive) {
                        removeReminder()
                    }
                    .buttonStyle(QuietButton())
                }
                Spacer()
                Button(store.text("取消", "Cancel")) { dismiss() }
                    .buttonStyle(QuietButton())
                Button(store.text("保存提醒", "Save reminder")) { saveReminder() }
                    .buttonStyle(GreenButton()).keyboardShortcut(.defaultAction)
            }
        }
        .padding(22)
        .frame(width: 440)
        .background(Palette.paper)
        .foregroundStyle(Palette.ink)
        .preferredColorScheme(store.appearance.colorScheme)
        .tint(Palette.green)
    }

    private func saveReminder() {
        guard var latest = store.tasks.first(where: { $0.id == task.id }) else {
            validation = store.text("没有找到这件事。", "This task could not be found.")
            return
        }
        latest.dueAt = reminderAt
        if latest.kind != .idea {
            latest.kind = latest.meetingPlatform == nil ? .reminder : .meeting
        }
        if store.updateTask(latest) {
            dismiss()
        } else {
            validation = store.error
            store.error = nil
        }
    }

    private func removeReminder() {
        guard var latest = store.tasks.first(where: { $0.id == task.id }) else {
            validation = store.text("没有找到这件事。", "This task could not be found.")
            return
        }
        latest.dueAt = nil
        if latest.kind == .reminder { latest.kind = nil }
        if store.updateTask(latest) {
            dismiss()
        } else {
            validation = store.error
            store.error = nil
        }
    }

    private static func defaultReminderDate() -> Date {
        let date = Date().addingTimeInterval(3600)
        var components = Calendar.current.dateComponents([.year, .month, .day, .hour, .minute],
                                                         from: date)
        components.minute = ((components.minute ?? 0) / 15 + 1) * 15
        return Calendar.current.date(from: components) ?? date
    }
}
