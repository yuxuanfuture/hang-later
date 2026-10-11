import HangLaterCore
import SwiftUI

struct SettingsView: View {
    @ObservedObject var store: AppStore
    @Environment(\.dismiss) private var dismiss
    @State private var startMinute: Int
    @State private var endMinute: Int

    init(store: AppStore) {
        self.store = store
        _startMinute = State(initialValue: store.workdayStartMinute)
        _endMinute = State(initialValue: store.workdayEndMinute)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                VStack(alignment: .leading, spacing: 6) {
                    Text(store.text("设置", "SETTINGS"))
                        .font(LeLaboType.display(23)).tracking(0.7)
                    Text(store.text("按照你的工作习惯定义一天。", "Define a day around the way you work."))
                        .font(LeLaboType.copy(11)).foregroundStyle(Palette.muted)
                }
                Spacer()
                Button { dismiss() } label: { Image(systemName: "xmark") }
                    .buttonStyle(.plain).foregroundStyle(Palette.muted)
            }
            .padding(.bottom, 22)

            Rectangle().fill(Palette.border).frame(height: 1)

            VStack(alignment: .leading, spacing: 18) {
                VStack(alignment: .leading, spacing: 8) {
                    Text(store.text("工作日", "WORKDAY"))
                        .font(LeLaboType.label(10, bold: true)).tracking(1.5)
                    Text(store.text("完成钉只统计 Publish 时间落在这个区间内的任务。结束时间早于或等于开始时间时，会自动算作第二天。",
                                    "Done Spike only counts tasks published inside this window. An end time at or before the start is treated as the following day."))
                        .font(LeLaboType.copy(11)).foregroundStyle(Palette.muted).lineSpacing(3)
                }

                HStack(spacing: 12) {
                    timeField(title: store.text("开始", "START"), selection: startBinding)
                    Image(systemName: "arrow.right")
                        .font(.system(size: 13)).foregroundStyle(Palette.muted)
                    timeField(title: store.text("结束", "END"), selection: endBinding,
                              detail: schedule.endsOnFollowingDay
                              ? store.text("第二天", "NEXT DAY")
                              : store.text("当天", "SAME DAY"))
                }

                HStack(alignment: .top, spacing: 11) {
                    Image(systemName: "clock")
                        .font(.system(size: 15)).foregroundStyle(Palette.ink.opacity(0.72))
                    VStack(alignment: .leading, spacing: 5) {
                        Text(rangeText).font(LeLaboType.label(12, bold: true))
                        Text(activeWindowText).font(LeLaboType.copy(10)).foregroundStyle(Palette.muted)
                    }
                }
                .padding(14).frame(maxWidth: .infinity, alignment: .leading)
                .background(Palette.raisedSurface.opacity(0.72), in: Rectangle())
                .overlay(Rectangle().stroke(Palette.border.opacity(0.8), lineWidth: 0.8))
            }
            .padding(.vertical, 24)

            Rectangle().fill(Palette.border).frame(height: 1)

            HStack {
                Spacer()
                Button(store.text("取消", "Cancel")) { dismiss() }.buttonStyle(QuietButton())
                Button(store.text("保存", "Save")) {
                    store.updateWorkday(startMinute: startMinute, endMinute: endMinute)
                    dismiss()
                }.buttonStyle(GreenButton())
            }
            .padding(.top, 18)
        }
        .padding(28)
        .frame(width: 500)
        .background(Palette.paper)
        .foregroundStyle(Palette.ink)
        .preferredColorScheme(store.appearance.colorScheme)
        .environment(\.locale, store.language.locale)
    }

    private func timeField(title: String, selection: Binding<Date>, detail: String? = nil) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(title).font(LeLaboType.label(9, bold: true)).tracking(1.1)
                Spacer()
                if let detail {
                    Text(detail).font(LeLaboType.label(8, bold: true)).foregroundStyle(Palette.muted)
                }
            }
            DatePicker("", selection: selection, displayedComponents: .hourAndMinute)
                .labelsHidden().datePickerStyle(.field).frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(14).frame(maxWidth: .infinity)
        .background(Palette.raisedSurface.opacity(0.72), in: Rectangle())
        .overlay(Rectangle().stroke(Palette.border.opacity(0.8), lineWidth: 0.8))
    }

    private var schedule: WorkdaySchedule {
        WorkdaySchedule(startMinute: startMinute, endMinute: endMinute)
    }

    private var rangeText: String {
        let nextDay = schedule.endsOnFollowingDay ? store.text("次日 ", "NEXT DAY ") : ""
        return "\(timeText(startMinute))  →  \(nextDay)\(timeText(endMinute))"
    }

    private var activeWindowText: String {
        guard let interval = schedule.interval(containing: Date()) else {
            return store.text("当前不在这个工作日时间段内。", "The current time is outside this workday window.")
        }
        let formatter = DateFormatter()
        formatter.locale = store.language.locale
        formatter.dateFormat = store.language == .zh ? "M月d日 HH:mm" : "MMM d, HH:mm"
        return store.text("当前工作日：", "Current workday: ")
            + formatter.string(from: interval.start) + "  –  " + formatter.string(from: interval.end)
    }

    private var startBinding: Binding<Date> {
        Binding(get: { date(for: startMinute) }, set: { startMinute = minute(of: $0) })
    }

    private var endBinding: Binding<Date> {
        Binding(get: { date(for: endMinute) }, set: { endMinute = minute(of: $0) })
    }

    private func date(for minute: Int) -> Date {
        Calendar.current.date(from: DateComponents(year: 2001, month: 1, day: 1,
                                                   hour: minute / 60, minute: minute % 60)) ?? Date()
    }

    private func minute(of date: Date) -> Int {
        let components = Calendar.current.dateComponents([.hour, .minute], from: date)
        return (components.hour ?? 0) * 60 + (components.minute ?? 0)
    }

    private func timeText(_ minute: Int) -> String {
        String(format: "%02d:%02d", minute / 60, minute % 60)
    }
}
