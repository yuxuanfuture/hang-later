import SwiftUI
import HangLaterCore

struct TaskEditor: View {
    @ObservedObject var store: AppStore
    var task: HangingTask?
    @Environment(\.dismiss) private var dismiss
    @State private var title = ""
    @State private var url = ""
    @State private var note = ""
    @State private var hasDueDate = false
    @State private var dueAt = Date()
    @State private var selectedTaskTypeID: UUID?
    @State private var validation: String?
    @FocusState private var titleFocused: Bool

    private var enteredMeetingPlatform: MeetingPlatform? {
        MeetingLink.platform(for: url.trimmingCharacters(in: .whitespacesAndNewlines))
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            HStack {
                VStack(alignment: .leading, spacing: 7) {
                    Text(task == nil ? store.text("晾一件事。", "Hang something.") : store.text("照顾一下这件事。", "Take care of this task."))
                        .font(LeLaboType.display(24))
                        .textCase(.uppercase)
                        .tracking(0.65)
                    Text(store.text("记下来，给它一个位置。", "Write it down and give it a place."))
                        .font(LeLaboType.copy(11)).foregroundStyle(Palette.muted)
                }
                Spacer()
                Button { dismiss() } label: { Image(systemName: "xmark").foregroundStyle(Palette.muted) }.buttonStyle(.plain).keyboardShortcut(.cancelAction)
            }
            field(store.text("这件事叫什么", "What is it called?")) {
                TextField(store.text("例如：继续学习那个视频", "For example: continue that course"), text: $title).focused($titleFocused)
            }
            if task?.filePath == nil, task?.appBundleIdentifier == nil, task?.isImage != true {
                field(store.text("网页链接（可选）", "Web link (optional)")) {
                    TextField("https://…", text: $url)
                }
                if let platform = enteredMeetingPlatform {
                    Label(store.text("已识别为 \(store.meetingPlatformName(platform)) 会议链接",
                                     "Recognized as a \(store.meetingPlatformName(platform)) link"),
                          systemImage: "video.fill")
                        .font(LeLaboType.label(9, bold: true))
                        .foregroundStyle(Palette.muted)
                }
            }
            field(store.text("给未来的自己留句话（可选）", "Leave your future self a note (optional)")) {
                TextField(store.text("例如：从 12:30 开始，记一下关键步骤", "For example: resume at 12:30 and note the key steps"), text: $note, axis: .vertical).lineLimit(3...5)
            }
            field(store.text("任务类型", "Task type")) {
                Picker("", selection: $selectedTaskTypeID) {
                    Text(store.text("默认边框", "Default border")).tag(nil as UUID?)
                    ForEach(store.taskTypes) { type in
                        HStack {
                            Circle().fill(Color(hex: type.colorHex)).frame(width: 9, height: 9)
                            Text(type.name)
                        }
                        .tag(type.id as UUID?)
                    }
                }
                .labelsHidden()
                .pickerStyle(.menu)
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            HStack {
                Toggle(store.text("添加提醒时间", "Add reminder"), isOn: $hasDueDate)
                    .toggleStyle(.checkbox).font(LeLaboType.copy(11))
                Spacer()
                if hasDueDate {
                    DatePicker("", selection: $dueAt, displayedComponents: [.date, .hourAndMinute])
                        .labelsHidden().frame(width: 230)
                }
            }
            if let validation { Text(validation).font(LeLaboType.copy(11, bold: true)).foregroundStyle(Palette.danger) }
            HStack {
                if let task, !task.isCompleted {
                    Button(store.text("这件事已做完", "Mark as done")) { store.complete(task); dismiss() }.buttonStyle(QuietButton())
                }
                Spacer()
                Button(store.text("取消", "Cancel")) { dismiss() }.buttonStyle(QuietButton())
                Button(task == nil ? store.text("挂上晾衣绳", "Hang it up") : store.text("保存", "Save")) { save() }.buttonStyle(GreenButton()).keyboardShortcut(.defaultAction)
            }.padding(.top, 7)
        }
        .textFieldStyle(.plain)
        .font(LeLaboType.copy(12))
        .padding(30).frame(width: 490).background(Palette.paper).foregroundStyle(Palette.ink)
        .preferredColorScheme(store.appearance.colorScheme)
        .tint(Palette.green)
        .onAppear {
            if let task {
                title = task.title
                url = task.url ?? ""
                note = task.note
                hasDueDate = task.dueAt != nil
                dueAt = task.dueAt ?? Date()
                selectedTaskTypeID = task.taskTypeID
            }
            titleFocused = true
        }
    }

    private func field<Content: View>(_ label: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 9) {
            Text(label).font(LeLaboType.label(10, bold: true)).foregroundStyle(Palette.muted)
            content().font(LeLaboType.copy(12)).padding(12)
                .background(Palette.raisedSurface, in: Rectangle())
                .overlay(Rectangle().stroke(Palette.border, lineWidth: 1))
        }
    }

    private func save() {
        guard !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { validation = store.text("先给这件事起个名字吧。", "Give this task a name first."); return }
        let cleanURL = url.trimmingCharacters(in: .whitespacesAndNewlines)
        guard cleanURL.isEmpty || TaskInput.webURL(cleanURL) != nil else { validation = store.text("请输入完整的 http 或 https 网页链接。", "Enter a complete http or https web link."); return }
        let link = cleanURL.isEmpty ? nil : TaskInput.webURL(cleanURL)?.absoluteString
        if var task {
            // Start from the current item so asynchronous preview updates are preserved.
            task = store.tasks.first(where: { $0.id == task.id }) ?? task
            let changedURL = task.url != link
            let oldPreview = task.previewFile
            task.title = title
            task.url = link
            task.note = note
            task.dueAt = hasDueDate ? dueAt : nil
            if task.kind != .idea && task.kind != .image {
                if enteredMeetingPlatform != nil {
                    task.kind = .meeting
                } else if hasDueDate {
                    task.kind = .reminder
                } else if task.kind == .meeting || task.kind == .reminder {
                    task.kind = nil
                }
            }
            task.taskTypeID = selectedTaskTypeID
            if changedURL { task.previewFile = nil }
            if store.updateTask(task) {
                if changedURL, let oldPreview { try? FileManager.default.removeItem(at: store.previewDirectory.appendingPathComponent(oldPreview)) }
                if changedURL { store.refreshMetadata(task) }
                dismiss()
            } else { validation = store.error; store.error = nil }
        } else if store.add(title: title, url: link, note: note,
                            dueAt: hasDueDate ? dueAt : nil,
                            kind: enteredMeetingPlatform != nil ? .meeting : (hasDueDate ? .reminder : nil),
                            taskTypeID: selectedTaskTypeID) { dismiss() }
        else { validation = store.error; store.error = nil }
    }
}
