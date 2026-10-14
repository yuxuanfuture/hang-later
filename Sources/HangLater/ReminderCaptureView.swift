import HangLaterCore
import SwiftUI

struct ReminderCaptureView: View {
    @ObservedObject var store: AppStore
    let onHang: (String, String?, Date) -> Bool
    let onCancel: () -> Void

    @State private var title: String
    @State private var link: String
    @State private var reminderAt: Date
    @State private var validation: String?
    @FocusState private var titleFocused: Bool

    private var cleanLink: String { link.trimmingCharacters(in: .whitespacesAndNewlines) }
    private var enteredURL: URL? { cleanLink.isEmpty ? nil : TaskInput.webURL(cleanLink) }
    private var platform: MeetingPlatform? { enteredURL.flatMap(MeetingLink.platform(for:)) }
    private var linkIsValid: Bool { cleanLink.isEmpty || enteredURL != nil }

    init(store: AppStore,
         onHang: @escaping (String, String?, Date) -> Bool,
         onCancel: @escaping () -> Void,
         initialTitle: String = "",
         initialLink: String = "",
         initialReminderAt: Date? = nil) {
        self.store = store
        self.onHang = onHang
        self.onCancel = onCancel
        _title = State(initialValue: initialTitle)
        _link = State(initialValue: initialLink)
        _reminderAt = State(initialValue: initialReminderAt ?? Self.defaultReminderDate())
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 10) {
                Image(systemName: "bell.fill")
                    .font(.system(size: 14, weight: .medium))
                Text(store.text("挂一个提醒", "HANG A REMINDER"))
                    .font(LeLaboType.display(16)).tracking(0.6)
                Spacer()
                Button(action: onCancel) { Image(systemName: "xmark") }
                    .buttonStyle(.plain).keyboardShortcut(.cancelAction)
            }

            VStack(alignment: .leading, spacing: 8) {
                TextField(store.text("要提醒什么", "What do you need to remember?"), text: $title)
                    .focused($titleFocused)
                    .reminderFieldStyle()
                TextField(store.text("链接（可选）", "Link (optional)"), text: $link)
                    .reminderFieldStyle()
                HStack(spacing: 6) {
                    Image(systemName: helperSymbol)
                    Text(helperText)
                }
                .font(LeLaboType.label(9, bold: true))
                .foregroundStyle(Palette.muted)
            }

            HStack {
                Label(store.text("提醒时间", "Reminder"), systemImage: "bell")
                    .font(LeLaboType.label(10, bold: true))
                Spacer()
                DatePicker("", selection: $reminderAt, in: Date()...,
                           displayedComponents: [.date, .hourAndMinute])
                    .labelsHidden().frame(width: 230)
            }

            HStack {
                if let validation {
                    Text(validation).foregroundStyle(Palette.danger)
                } else {
                    Text(store.text("到点会收到 macOS 通知",
                                    "macOS will remind you when it’s time"))
                        .foregroundStyle(Palette.muted)
                }
                Spacer()
                Button(store.text("挂上去", "Hang it"), action: hangReminder)
                    .buttonStyle(QuietButton())
                    .disabled(title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || !linkIsValid)
            }
            .font(LeLaboType.label(9, bold: true))
        }
        .padding(20)
        .frame(width: 440)
        .background(Palette.paper)
        .foregroundStyle(Palette.ink)
        .preferredColorScheme(store.appearance.colorScheme)
        .tint(Palette.green)
        .onAppear { DispatchQueue.main.async { titleFocused = true } }
    }

    private func hangReminder() {
        let cleanTitle = title.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !cleanTitle.isEmpty else {
            validation = store.text("请输入要提醒的事情。", "Enter what you want to be reminded about.")
            return
        }
        guard linkIsValid else {
            validation = store.text("请输入完整的链接，或把链接留空。",
                                    "Enter a complete link, or leave the link empty.")
            return
        }
        if onHang(cleanTitle, cleanLink.isEmpty ? nil : cleanLink, reminderAt) {
            onCancel()
        } else {
            validation = store.error ?? store.text("这个提醒没有保存下来。", "This reminder could not be saved.")
            store.error = nil
        }
    }

    private var helperSymbol: String {
        if platform != nil || enteredURL != nil { return "checkmark.circle.fill" }
        return cleanLink.isEmpty ? "bell" : "exclamationmark.triangle"
    }

    private var helperText: String {
        if let platform {
            return store.text("已识别为 \(store.meetingPlatformName(platform))，点击纸片即可入会",
                              "Recognized as \(store.meetingPlatformName(platform)); click the card to join")
        }
        if cleanLink.isEmpty {
            return store.text("在线会议链接：点击即达 · 无链接：生活提醒",
                              "Online meeting link: click to join · No link: everyday reminder")
        }
        if enteredURL != nil {
            return store.text("提醒时点击纸片即可打开这个链接",
                              "Click the card to open this link when it’s time")
        }
        return store.text("请输入完整的 http 或 https 链接",
                          "Enter a complete http or https link")
    }

    private static func defaultReminderDate() -> Date {
        let date = Date().addingTimeInterval(3600)
        var components = Calendar.current.dateComponents([.year, .month, .day, .hour, .minute], from: date)
        components.minute = ((components.minute ?? 0) / 15 + 1) * 15
        return Calendar.current.date(from: components) ?? date
    }
}

private extension View {
    func reminderFieldStyle() -> some View {
        self
            .textFieldStyle(.plain)
            .font(LeLaboType.copy(12))
            .padding(.horizontal, 13)
            .frame(height: 40)
            .background(Palette.raisedSurface, in: Rectangle())
            .overlay(Rectangle().stroke(Palette.border, lineWidth: 1))
    }
}
