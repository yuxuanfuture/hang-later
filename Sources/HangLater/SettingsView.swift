import HangLaterCore
import SwiftUI

private enum SettingsSection: String, CaseIterable, Identifiable {
    case desktopClothesline
    case workday
    case taskTypes

    var id: Self { self }
}

private final class SettingsWindowProbeView: NSView {
    var configureWindow: ((NSWindow) -> Void)?

    override func viewDidMoveToWindow() {
        super.viewDidMoveToWindow()
        if let window { configureWindow?(window) }
    }
}

private struct ResizableSettingsWindow: NSViewRepresentable {
    final class Coordinator {
        var configuredWindowNumber: Int?
    }

    func makeCoordinator() -> Coordinator { Coordinator() }

    func makeNSView(context: Context) -> SettingsWindowProbeView {
        let view = SettingsWindowProbeView(frame: .zero)
        view.configureWindow = { window in
            configure(window, coordinator: context.coordinator)
        }
        return view
    }

    func updateNSView(_ view: SettingsWindowProbeView, context: Context) {
        guard let window = view.window else { return }
        configure(window, coordinator: context.coordinator)
    }

    private func configure(_ window: NSWindow, coordinator: Coordinator) {
        window.styleMask.insert(.resizable)
        window.contentMinSize = NSSize(width: 720, height: 560)
        window.minSize = NSSize(width: 720, height: 560)

        guard coordinator.configuredWindowNumber != window.windowNumber else { return }
        coordinator.configuredWindowNumber = window.windowNumber
        window.setContentSize(NSSize(width: 820, height: 700))
    }
}

private struct SettingsPrimaryButton: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(LeLaboType.label(15, bold: true))
            .tracking(0.35)
            .foregroundStyle(Palette.onAccent)
            .padding(.horizontal, 22)
            .padding(.vertical, 13)
            .background(Palette.green.opacity(configuration.isPressed ? 0.78 : 1), in: Rectangle())
            .overlay(Rectangle().stroke(Palette.green, lineWidth: 1))
    }
}

private struct SettingsQuietButton: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(LeLaboType.label(13.75, bold: true))
            .tracking(0.3)
            .foregroundStyle(Palette.ink)
            .padding(.horizontal, 17)
            .padding(.vertical, 12)
            .background(Palette.raisedSurface.opacity(configuration.isPressed ? 0.55 : 0.82), in: Rectangle())
            .overlay(Rectangle().stroke(Palette.border, lineWidth: 1))
    }
}

struct SettingsView: View {
    @ObservedObject var store: AppStore
    @Environment(\.dismiss) private var dismiss
    @State private var startMinute: Int
    @State private var endMinute: Int
    @State private var endsOnFollowingDay: Bool
    @State private var taskTypes: [TaskTypeDefinition]
    @State private var editingColorTypeID: UUID?
    @State private var keepDesktopLineVisible: Bool
    @State private var desktopLineVerticalPosition: Double
    @State private var selectedSection: SettingsSection = .desktopClothesline
    @State private var saved = false
    private let originalKeepDesktopLineVisible: Bool
    private let originalDesktopLineVerticalPosition: Double

    private let paletteColors: [UInt32] = [
        0xAA2651, 0xBF2B54, 0xE8365F, 0xEF678F, 0xF491AF,
        0xC33026, 0xD1382B, 0xE1543E, 0xE4736E, 0xEE9A96,
        0xEC6D20, 0xF37C23, 0xFD9727, 0xFCB550, 0xFECA80,
        0xF6A532, 0xF9BD3A, 0xFCE748, 0xFDEE79, 0xFDF29E,
        0x20752E, 0x2B8537, 0x42A74D, 0x7BC082, 0xA0D2A4,
        0x006055, 0x067265, 0x098276, 0x47B1A8, 0x7CC8C1,
        0x107D8A, 0x1591A1, 0x1FB7D2, 0x4BCCDD, 0x7DDBE8,
        0x2663BB, 0x2973CE, 0x2F87E1, 0x3394EF, 0x65B3F4,
        0x27328E, 0x2F3D9A, 0x3F51B2, 0x7684C6, 0x9DA6D8,
        0x422A99, 0x4F30A1, 0x663DB3, 0x9275C9, 0xB19DD8,
        0x662694, 0x792B9D, 0x9934AA, 0xB96AC4, 0xCE95D5,
        0x2F4048, 0x3E545D, 0x597786, 0x8CA0AA, 0xADBCC1,
        0x462B24, 0x56392F, 0x745041, 0x9C8479, 0xB9A8A0,
        0x0A0A0A, 0x5A5A5A, 0x9B9B9B, 0xDFDFDF, 0xFFFFFF
    ]

    private let newTypeColors: [UInt32] = [
        0xE8365F, 0xFD9727, 0x42A74D, 0x1FB7D2,
        0x2F87E1, 0x663DB3, 0x597786, 0x745041
    ]

    init(store: AppStore) {
        self.store = store
        _startMinute = State(initialValue: store.workdayStartMinute)
        _endMinute = State(initialValue: store.workdayEndMinute)
        _endsOnFollowingDay = State(initialValue: store.workdayEndsOnFollowingDay)
        _taskTypes = State(initialValue: store.taskTypes)
        _keepDesktopLineVisible = State(initialValue: store.pinned)
        _desktopLineVerticalPosition = State(initialValue: store.desktopLineVerticalPosition)
        originalKeepDesktopLineVisible = store.pinned
        originalDesktopLineVerticalPosition = store.desktopLineVerticalPosition
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                VStack(alignment: .leading, spacing: 6) {
                    Text(store.text("设置", "SETTINGS"))
                        .font(settingsDisplay(23)).tracking(0.9)
                    Text(store.text("定义你的工作日，也为不同类型的任务做上标记。",
                                    "Define your workday and mark different kinds of work."))
                        .font(settingsCopy(11)).foregroundStyle(Palette.muted)
                }
                Spacer()
                Button { dismiss() } label: { Image(systemName: "xmark") }
                    .buttonStyle(.plain).foregroundStyle(Palette.muted)
            }
            .padding(.bottom, 22)

            Rectangle().fill(Palette.border).frame(height: 1)

            ScrollViewReader { proxy in
                HStack(alignment: .top, spacing: 0) {
                    settingsSidebar(proxy: proxy)
                        .frame(width: 198)
                        .padding(.trailing, 24)

                    Rectangle().fill(Palette.border).frame(width: 1)

                    ScrollView {
                        VStack(alignment: .leading, spacing: 24) {
                            desktopClotheslineSection
                                .id(SettingsSection.desktopClothesline)
                            Rectangle().fill(Palette.border).frame(height: 1)
                            workdaySection
                                .id(SettingsSection.workday)
                            Rectangle().fill(Palette.border).frame(height: 1)
                            taskTypeSection
                                .id(SettingsSection.taskTypes)
                        }
                        .padding(.leading, 28)
                        .padding(.trailing, 30)
                        .padding(.vertical, 28)
                    }
                }
            }

            Rectangle().fill(Palette.border).frame(height: 1)

            HStack {
                Spacer()
                Button(store.text("取消", "Cancel")) { dismiss() }.buttonStyle(SettingsQuietButton())
                Button(store.text("保存", "Save")) { save() }.buttonStyle(SettingsPrimaryButton())
            }
            .padding(.top, 20)
        }
        .padding(30)
        .frame(minWidth: 720, idealWidth: 820, maxWidth: .infinity,
               minHeight: 560, idealHeight: 700, maxHeight: .infinity)
        .background(Palette.paper)
        .background(ResizableSettingsWindow())
        .foregroundStyle(Palette.ink)
        .preferredColorScheme(store.appearance.colorScheme)
        .environment(\.locale, store.language.locale)
        .onChange(of: keepDesktopLineVisible) { _, keepVisible in
            store.previewDesktopLinePreferences(
                keepVisible: keepVisible,
                verticalPosition: desktopLineVerticalPosition
            )
        }
        .onChange(of: desktopLineVerticalPosition) { _, position in
            store.previewDesktopLineHeight(position)
        }
        .onDisappear {
            if !saved {
                store.previewDesktopLinePreferences(
                    keepVisible: originalKeepDesktopLineVisible,
                    verticalPosition: originalDesktopLineVerticalPosition
                )
            }
        }
    }

    private func settingsSidebar(proxy: ScrollViewProxy) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(store.text("设置栏目", "SECTIONS"))
                .font(settingsLabel(9, bold: true))
                .tracking(1.3)
                .foregroundStyle(Palette.muted)
                .padding(.bottom, 4)

            ForEach(SettingsSection.allCases) { section in
                Button {
                    selectedSection = section
                    withAnimation(.easeInOut(duration: 0.24)) {
                        proxy.scrollTo(section, anchor: .top)
                    }
                } label: {
                    HStack(spacing: 10) {
                        Image(systemName: sectionSymbol(section))
                            .font(.system(size: 15, weight: .medium))
                            .frame(width: 22)
                        Text(sectionTitle(section))
                            .font(settingsLabel(10, bold: selectedSection == section))
                            .lineLimit(2)
                        Spacer(minLength: 0)
                    }
                    .foregroundStyle(selectedSection == section ? Palette.ink : Palette.muted)
                    .padding(.horizontal, 14)
                    .padding(.vertical, 14)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background {
                        if selectedSection == section {
                            Palette.raisedSurface.opacity(0.82)
                        }
                    }
                    .overlay(alignment: .leading) {
                        if selectedSection == section {
                            Rectangle().fill(Palette.ink).frame(width: 2)
                        }
                    }
                }
                .buttonStyle(.plain)
            }

            Spacer()
        }
        .padding(.vertical, 28)
    }

    private func sectionTitle(_ section: SettingsSection) -> String {
        switch section {
        case .desktopClothesline:
            store.text("桌面晾衣绳", "Desktop clothesline")
        case .workday:
            store.text("工作日时间", "Workday hours")
        case .taskTypes:
            store.text("任务类型", "Task types")
        }
    }

    private func sectionSymbol(_ section: SettingsSection) -> String {
        switch section {
        case .desktopClothesline: "macwindow"
        case .workday: "clock"
        case .taskTypes: "tag"
        }
    }

    private var desktopClotheslineSection: some View {
        VStack(alignment: .leading, spacing: 14) {
            VStack(alignment: .leading, spacing: 8) {
                Text(store.text("桌面晾衣绳", "DESKTOP CLOTHESLINE"))
                    .font(settingsLabel(10, bold: true)).tracking(1.5)
                Text(store.text("决定晾衣绳是否一直显示，以及它在屏幕上的上下位置。",
                                "Keep the clothesline visible and choose its vertical position on screen."))
                    .font(settingsCopy(11)).foregroundStyle(Palette.muted).lineSpacing(5)
            }

            HStack(spacing: 16) {
                VStack(alignment: .leading, spacing: 4) {
                    Text(store.text("始终显示在桌面", "Always show on desktop"))
                        .font(settingsLabel(11, bold: true))
                    Text(store.text("打开后，晾衣绳不会自动收起。",
                                    "The clothesline stays open instead of hiding automatically."))
                        .font(settingsCopy(9)).foregroundStyle(Palette.muted)
                }
                Spacer()
                Toggle("", isOn: $keepDesktopLineVisible)
                    .labelsHidden()
                    .toggleStyle(.switch)
                    .controlSize(.regular)
                    .tint(Palette.ink.opacity(0.72))
            }
            .padding(14)
            .background(Palette.raisedSurface.opacity(0.72), in: Rectangle())
            .overlay(Rectangle().stroke(Palette.border.opacity(0.8), lineWidth: 0.8))

            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Text(store.text("屏幕高度", "SCREEN HEIGHT"))
                        .font(settingsLabel(9, bold: true)).tracking(1.1)
                    Spacer()
                    Text("\(Int((desktopLineVerticalPosition * 100).rounded()))%")
                        .font(settingsLabel(9, bold: true)).foregroundStyle(Palette.muted)
                }
                Slider(value: $desktopLineVerticalPosition, in: 0...1)
                    .tint(Palette.ink)
                HStack {
                    Text(store.text("顶部", "TOP"))
                    Spacer()
                    Text(store.text("底部", "BOTTOM"))
                }
                .font(settingsLabel(8, bold: true)).foregroundStyle(Palette.muted)
            }
            .padding(14)
            .background(Palette.raisedSurface.opacity(0.72), in: Rectangle())
            .overlay(Rectangle().stroke(Palette.border.opacity(0.8), lineWidth: 0.8))
        }
    }

    private var workdaySection: some View {
        VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 8) {
                Text(store.text("工作日", "WORKDAY"))
                    .font(settingsLabel(10, bold: true)).tracking(1.5)
                Text(store.text("完成钉只统计 Publish 时间落在这个区间内的任务。结束日期可以选择当天或第二天。",
                                "Done Spike only counts tasks published inside this window. Choose whether the end belongs to the same day or the following day."))
                    .font(settingsCopy(11)).foregroundStyle(Palette.muted).lineSpacing(5)
            }

            HStack(spacing: 12) {
                timeField(title: store.text("开始", "START"), selection: startBinding,
                          fixedDayLabel: store.text("当天", "SAME DAY"))
                Image(systemName: "arrow.right")
                    .font(.system(size: 16.25)).foregroundStyle(Palette.muted)
                timeField(title: store.text("结束", "END"), selection: endBinding,
                          endDaySelection: $endsOnFollowingDay)
            }

            HStack(alignment: .top, spacing: 11) {
                Image(systemName: "clock")
                    .font(.system(size: 18.75)).foregroundStyle(Palette.ink.opacity(0.72))
                VStack(alignment: .leading, spacing: 5) {
                    Text(rangeText).font(settingsLabel(12, bold: true))
                    Text(activeWindowText).font(settingsCopy(10)).foregroundStyle(Palette.muted)
                }
            }
            .padding(14).frame(maxWidth: .infinity, alignment: .leading)
            .background(Palette.raisedSurface.opacity(0.72), in: Rectangle())
            .overlay(Rectangle().stroke(Palette.border.opacity(0.8), lineWidth: 0.8))
        }
    }

    private var taskTypeSection: some View {
        VStack(alignment: .leading, spacing: 14) {
            VStack(alignment: .leading, spacing: 8) {
                Text(store.text("任务类型", "TASK TYPES"))
                    .font(settingsLabel(10, bold: true)).tracking(1.5)
                Text(store.text("添加并命名类型，再从色板中选择颜色。只有便签底部的“完成”一行会使用对应颜色。",
                                "Add and name your own types, then choose from the palette. Only the Done row uses it."))
                    .font(settingsCopy(11)).foregroundStyle(Palette.muted).lineSpacing(5)
            }

            HStack(spacing: 12) {
                typePreview(color: nil)
                VStack(alignment: .leading, spacing: 3) {
                    Text(store.text("默认", "Default")).font(settingsLabel(11, bold: true))
                    Text(store.text("保持现在的纸张颜色", "Keep the current paper row"))
                        .font(settingsCopy(9)).foregroundStyle(Palette.muted)
                }
                Spacer()
            }
            .padding(12)
            .background(Palette.raisedSurface.opacity(0.55), in: Rectangle())
            .overlay(Rectangle().stroke(Palette.border.opacity(0.75), lineWidth: 0.8))

            VStack(spacing: 9) {
                ForEach($taskTypes) { $type in
                    HStack(spacing: 11) {
                        typePreview(color: Color(hex: type.colorHex))
                        TextField(store.text("类型名称", "Type name"), text: $type.name)
                            .textFieldStyle(.plain)
                            .font(settingsCopy(11))
                            .padding(.horizontal, 10).padding(.vertical, 8)
                            .background(Palette.raisedSurface, in: Rectangle())
                            .overlay(Rectangle().stroke(Palette.border, lineWidth: 0.8))
                        Button {
                            editingColorTypeID = type.id
                        } label: {
                            HStack(spacing: 5) {
                                Circle().fill(Color(hex: type.colorHex)).frame(width: 18, height: 18)
                                    .overlay(Circle().stroke(Palette.border, lineWidth: 0.8))
                                Image(systemName: "chevron.down")
                                    .font(.system(size: 10, weight: .bold)).foregroundStyle(Palette.muted)
                            }
                            .padding(.horizontal, 7).padding(.vertical, 6)
                            .background(Palette.raisedSurface, in: Rectangle())
                            .overlay(Rectangle().stroke(Palette.border, lineWidth: 0.8))
                        }
                        .buttonStyle(.plain)
                        .popover(isPresented: paletteBinding(for: type.id), arrowEdge: .trailing) {
                            colorPalette(for: type.id)
                        }
                        Button {
                            taskTypes.removeAll { $0.id == type.id }
                        } label: {
                            Image(systemName: "trash")
                        }
                        .buttonStyle(.plain)
                        .foregroundStyle(Palette.muted)
                        .frame(width: 30, height: 32)
                        .contentShape(Rectangle())
                        .help(store.text("删除类型；使用它的任务会恢复默认边框",
                                         "Delete type; assigned tasks return to the default border"))
                    }
                }
            }

            Button { addTaskType() } label: {
                Label(store.text("添加任务类型", "Add task type"), systemImage: "plus")
            }
            .buttonStyle(SettingsQuietButton())
        }
    }

    private func timeField(title: String, selection: Binding<Date>,
                           fixedDayLabel: String? = nil,
                           endDaySelection: Binding<Bool>? = nil) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(title).font(settingsLabel(9, bold: true)).tracking(1.1)
                Spacer()
                if let endDaySelection {
                    Picker("", selection: endDaySelection) {
                        Text(store.text("当天", "Same day")).tag(false)
                        Text(store.text("第二天", "Next day")).tag(true)
                    }
                    .labelsHidden()
                    .pickerStyle(.menu)
                    .controlSize(.small)
                    .fixedSize()
                } else if let fixedDayLabel {
                    Text(fixedDayLabel)
                        .font(settingsLabel(8, bold: true))
                        .foregroundStyle(Palette.muted)
                }
            }
            DatePicker("", selection: selection, displayedComponents: .hourAndMinute)
                .labelsHidden().datePickerStyle(.field).controlSize(.large)
                .font(settingsCopy(11))
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(14).frame(maxWidth: .infinity)
        .background(Palette.raisedSurface.opacity(0.72), in: Rectangle())
        .overlay(Rectangle().stroke(Palette.border.opacity(0.8), lineWidth: 0.8))
    }

    private var schedule: WorkdaySchedule {
        WorkdaySchedule(startMinute: startMinute, endMinute: endMinute,
                        endsOnFollowingDay: endsOnFollowingDay)
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

    private func typePreview(color: Color?) -> some View {
        ZStack(alignment: .bottom) {
            Rectangle().fill(Palette.labelPaper)
            if let color { Rectangle().fill(color.opacity(0.82)).frame(height: 11) }
        }
        .frame(width: 42, height: 29)
        .overlay(Rectangle().stroke(Palette.labelRule.opacity(0.72), lineWidth: 1))
    }

    private func paletteBinding(for id: UUID) -> Binding<Bool> {
        Binding(get: { editingColorTypeID == id }, set: { shown in
            if !shown, editingColorTypeID == id { editingColorTypeID = nil }
        })
    }

    private func colorPalette(for id: UUID) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(store.text("选择颜色", "CHOOSE A COLOR"))
                .font(settingsLabel(10, bold: true)).tracking(1.2)
            LazyVGrid(columns: Array(repeating: GridItem(.fixed(24), spacing: 8), count: 10), spacing: 8) {
                ForEach(paletteColors, id: \.self) { colorHex in
                    Button {
                        guard let index = taskTypes.firstIndex(where: { $0.id == id }) else { return }
                        taskTypes[index].colorHex = colorHex
                        editingColorTypeID = nil
                    } label: {
                        Circle()
                            .fill(Color(hex: colorHex))
                            .frame(width: 23, height: 23)
                            .overlay(Circle().stroke(Palette.border, lineWidth: colorHex == 0xFFFFFF ? 1 : 0))
                            .overlay {
                                if taskTypes.first(where: { $0.id == id })?.colorHex == colorHex {
                                    Image(systemName: "checkmark")
                                        .font(.system(size: 11.25, weight: .black))
                                        .foregroundStyle(contrastInk(for: colorHex))
                                }
                            }
                    }
                    .buttonStyle(.plain)
                    .help(String(format: "#%06X", colorHex))
                }
            }
        }
        .padding(16)
        .background(Palette.paper)
        .preferredColorScheme(store.appearance.colorScheme)
    }

    private func contrastInk(for colorHex: UInt32) -> Color {
        let red = Double((colorHex >> 16) & 255)
        let green = Double((colorHex >> 8) & 255)
        let blue = Double(colorHex & 255)
        return red * 0.299 + green * 0.587 + blue * 0.114 < 145 ? .white : .black
    }

    private func settingsDisplay(_ size: CGFloat) -> Font {
        LeLaboType.display(size * 1.25)
    }

    private func settingsLabel(_ size: CGFloat, bold: Bool = false) -> Font {
        LeLaboType.label(size * 1.25, bold: bold)
    }

    private func settingsCopy(_ size: CGFloat, bold: Bool = false) -> Font {
        LeLaboType.copy(size * 1.25, bold: bold)
    }

    private func addTaskType() {
        var number = taskTypes.count + 1
        var name = store.text("新类型 \(number)", "New type \(number)")
        while taskTypes.contains(where: { $0.name.localizedCaseInsensitiveCompare(name) == .orderedSame }) {
            number += 1
            name = store.text("新类型 \(number)", "New type \(number)")
        }
        let color = newTypeColors[taskTypes.count % newTypeColors.count]
        taskTypes.append(TaskTypeDefinition(name: name, colorHex: color))
    }

    private func save() {
        guard store.updateTaskTypes(taskTypes) else { return }
        store.updateWorkday(startMinute: startMinute, endMinute: endMinute,
                            endsOnFollowingDay: endsOnFollowingDay)
        store.saveDesktopLinePreferences(
            keepVisible: keepDesktopLineVisible,
            verticalPosition: desktopLineVerticalPosition
        )
        saved = true
        dismiss()
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
