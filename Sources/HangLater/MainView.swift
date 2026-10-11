import SwiftUI
import HangLaterCore
import UniformTypeIdentifiers

struct MainView: View {
    @ObservedObject var store: AppStore
    @State private var targeted = false
    @State private var deleting: HangingTask?
    @State private var completionGrouping: CompletionGrouping = .day

    var body: some View {
        HStack(spacing: 0) {
            sidebar
            VStack(alignment: .leading, spacing: 0) {
                toolbar
                ScrollView {
                    VStack(alignment: .leading, spacing: 0) {
                        heading.padding(.bottom, 30)
                        switch store.destination {
                        case .line:
                            linePage
                        case .spike:
                            DoneSpikePage(store: store)
                        case .list, .completed:
                            listPage
                        }
                    }.padding(.horizontal, 38).padding(.top, 33).padding(.bottom, 32)
                }
                footer
            }.background(Palette.paper)
        }
        .foregroundStyle(Palette.ink)
        .font(LeLaboType.copy(12))
        .frame(minWidth: 970, minHeight: 720)
        .preferredColorScheme(store.appearance.colorScheme)
        .tint(Palette.green)
        .environment(\.locale, store.language.locale)
        .onDrop(of: DropSupport.acceptedContentTypes, isTargeted: $targeted) { store.acceptDrop($0) }
        .overlay {
            if targeted {
                Rectangle().stroke(Palette.green, style: StrokeStyle(lineWidth: 2, dash: [8]))
                    .padding(8).allowsHitTesting(false)
            }
        }
        .overlay(alignment: .bottom) {
            if let message = store.message {
                Label(message, systemImage: "checkmark.circle.fill").font(LeLaboType.label(11, bold: true))
                    .foregroundStyle(Palette.onAccent).padding(.horizontal, 20).padding(.vertical, 12)
                    .background(Palette.green, in: Rectangle()).padding(.bottom, 49)
                    .transition(.opacity.combined(with: .move(edge: .bottom)))
            }
        }
        .animation(.easeInOut(duration: 0.2), value: store.message)
        .sheet(isPresented: $store.showingAddChooser) { AddSourceSheet(store: store) }
        .sheet(isPresented: $store.showingComposer) { TaskEditor(store: store) }
        .sheet(item: $store.editingTask) { TaskEditor(store: store, task: $0) }
        .sheet(isPresented: $store.showingWindowCapture) { WindowCapturePicker(store: store) }
        .sheet(isPresented: $store.showingGuide) { BrowserGuide(store: store) }
        .alert(store.text("这次没能保存", "Couldn’t save this time"), isPresented: Binding(
            get: { store.error != nil },
            set: {
                if !$0 {
                    store.error = nil
                    store.screenCapturePermissionNeeded = false
                }
            }
        )) {
            Button(store.text("知道了", "OK")) {
                store.error = nil
                store.screenCapturePermissionNeeded = false
            }
            if store.screenCapturePermissionNeeded {
                Button(store.text("打开屏幕与系统录音设置", "Open Screen Recording Settings")) {
                    if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture") {
                        NSWorkspace.shared.open(url)
                    }
                    store.error = nil
                    store.screenCapturePermissionNeeded = false
                }
            } else {
                Button(store.text("查看数据文件夹", "Show Data Folder")) {
                    NSWorkspace.shared.open(store.directory)
                    store.error = nil
                }
            }
        } message: { Text(store.error ?? "") }
        .confirmationDialog(store.text("永久删除这件任务？", "Delete this task permanently?"), isPresented: Binding(get: { deleting != nil }, set: { if !$0 { deleting = nil } })) {
            Button(store.text("永久删除", "Delete Permanently"), role: .destructive) { if let deleting { store.remove(deleting) }; deleting = nil }
            Button(store.text("取消", "Cancel"), role: .cancel) { deleting = nil }
        } message: { Text(store.text("任务及缩略图会从本机删除。", "The task and its preview will be deleted from this Mac.")) }
    }

    private var sidebar: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 12) {
                ZStack {
                    Rectangle().fill(Palette.green).frame(width: 37, height: 37)
                    Image(systemName: "pin.fill").font(.system(size: 18)).rotationEffect(.degrees(18)).foregroundStyle(Palette.onAccent)
                }
                Text("HANG LATER")
                    .font(LeLaboType.display(20))
                    .tracking(0.65)
            }.padding(.bottom, 42)
            Text(store.text("我的空间", "MY SPACE")).font(LeLaboType.label(9, bold: true)).tracking(1.2).foregroundStyle(Palette.muted).padding(.leading, 12).padding(.bottom, 14)
            ForEach(Destination.allCases, id: \.self) { destination in
                Button { store.destination = destination; store.search = "" } label: {
                    HStack(spacing: 10) {
                        Image(systemName: destination.symbol).frame(width: 18)
                        Text(destination.title(store.language))
                        Spacer()
                        Text("\(destinationCount(destination))")
                            .font(LeLaboType.label(9)).foregroundStyle(Palette.muted)
                    }
                    .font(LeLaboType.copy(12, bold: store.destination == destination))
                    .foregroundStyle(store.destination == destination ? Palette.green : Palette.ink.opacity(0.75))
                    .padding(.horizontal, 12).padding(.vertical, 13)
                    .background(store.destination == destination ? Palette.raisedSurface.opacity(0.82) : .clear, in: Rectangle())
                    .overlay(alignment: .leading) {
                        if store.destination == destination { Rectangle().fill(Palette.green).frame(width: 2) }
                    }
                }.buttonStyle(.plain).padding(.bottom, 4)
            }
            Spacer()
            HStack(spacing: 14) {
                Button { store.toggleAppearance() } label: {
                    Label(store.appearance == .light ? store.text("夜间", "Dark") : store.text("白天", "Light"),
                          systemImage: store.appearance == .light ? "moon" : "sun.max")
                }.help(store.text("切换白天 / 夜间模式", "Switch light / dark mode"))
                Button { store.toggleLanguage() } label: {
                    Label(store.language == .zh ? "EN" : "中文", systemImage: "character.bubble")
                }.help(store.text("切换中文 / English", "Switch Chinese / English"))
            }.font(LeLaboType.label(11, bold: true)).foregroundStyle(Palette.muted).buttonStyle(.plain).padding(.leading, 8).padding(.bottom, 14)
            Button { store.showingGuide = true } label: { Label(store.text("连接 Chrome", "Connect Chrome"), systemImage: "puzzlepiece.extension") }
                .font(LeLaboType.label(11, bold: true)).foregroundStyle(Palette.muted).buttonStyle(.plain).padding(.leading, 8)
        }.padding(.horizontal, 20).padding(.top, 51).padding(.bottom, 23)
            .frame(width: 218).background(Palette.sidebar)
    }

    private var toolbar: some View {
        HStack {
            HStack(spacing: 7) {
                Text(Date(), format: .dateTime.month(.wide).day().weekday(.wide))
            }.font(LeLaboType.label(11)).foregroundStyle(Palette.muted)
            Spacer()
            HStack(spacing: 8) {
                Image(systemName: "magnifyingglass").foregroundStyle(Palette.ink.opacity(0.72))
                TextField(text: $store.search,
                          prompt: Text(store.text("找一件事…", "Find something…"))
                            .foregroundColor(Palette.muted.opacity(0.92))) { EmptyView() }
                    .textFieldStyle(.plain)
                    .foregroundStyle(Palette.ink.opacity(0.86))
                    .frame(width: 150)
            }.font(LeLaboType.label(11)).padding(.horizontal, 12).padding(.vertical, 9)
                .background(Palette.raisedSurface.opacity(0.72), in: Rectangle())
                .overlay(Rectangle().stroke(Palette.border.opacity(0.8), lineWidth: 0.6))
            Button { store.showDesktopLine?() } label: { Image(systemName: "menubar.arrow.up.rectangle") }
                .buttonStyle(.plain).foregroundStyle(Palette.muted).help(store.text("显示桌面晾衣绳 · ⌃⌥L", "Show desktop clothesline · ⌃⌥L")).padding(.leading, 10)
        }.padding(.horizontal, 38).padding(.top, 39).padding(.bottom, 18)
            .overlay(alignment: .bottom) { Rectangle().fill(Palette.border.opacity(0.6)).frame(height: 1) }
    }

    private var heading: some View {
        HStack(alignment: .center) {
            VStack(alignment: .leading, spacing: 11) {
                Text(headingTitle)
                    .font(LeLaboType.display(26))
                    .textCase(.uppercase)
                    .tracking(0.75)
                Text(headingSubtitle)
                    .font(LeLaboType.copy(13)).lineSpacing(3).foregroundStyle(Palette.muted)
            }
            Spacer(minLength: 12)
            Button { store.presentAddOptions() } label: { Label(store.text("晾一件事", "Hang something"), systemImage: "plus") }.buttonStyle(GreenButton())
                .keyboardShortcut("n", modifiers: .command)
        }
    }

    private var headingTitle: String {
        switch store.destination {
        case .line: return store.text("给想做的事，留一个位置。", "Give what matters a place.")
        case .list: return store.text("一件一件，慢慢来。", "One thing at a time.")
        case .completed: return store.text("那些做完的小事。", "The things you finished.")
        case .spike: return store.text("把完成的纸片，串在这里。", "Keep finished slips on the spike.")
        }
    }

    private var headingSubtitle: String {
        switch store.destination {
        case .completed:
            return store.text("每一次收起，都是一点小小的前进。", "Every finished task is a little progress.")
        case .spike:
            return store.text("拖纸片改变穿钉位置，拖空白处旋转，也可以取下再串回。",
                              "Drag slips to reposition them, drag empty space to rotate, or take one off and put it back.")
        case .line, .list:
            return store.text("想看的网页、想学的视频，还有生活里的小事。",
                              "Pages, videos, images, app windows, and everyday tasks.")
        }
    }

    private func destinationCount(_ destination: Destination) -> Int {
        switch destination {
        case .line, .list: return store.pending.count
        case .completed: return store.completed.count
        case .spike: return store.spiked.count
        }
    }

    private var linePage: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 9) {
                Circle().fill(Palette.green).frame(width: 5, height: 5)
                Text(store.text("正在晾着", "HANGING NOW")).font(LeLaboType.label(10, bold: true)).tracking(0.7)
                Text(store.language == .zh ? "\(store.pending.count) 件" : "\(store.pending.count) ITEMS").font(LeLaboType.label(11)).foregroundStyle(Palette.muted)
                Spacer()
                Text(store.text("点卡片继续 · 自由拖动位置 · 点完成归档", "Click to continue · Place cards freely · Choose Done to archive"))
                    .font(LeLaboType.label(11)).foregroundStyle(Palette.muted)
            }.padding(.bottom, 23)
            if store.filtered.isEmpty {
                emptyState
            } else {
                ClotheslineStage(tasks: store.filtered, store: store)
            }
            HStack(spacing: 25) {
                Label(store.language == .zh ? "\(store.pending.count) 件待办" : "\(store.pending.count) pending", systemImage: "circle.dotted")
                Label(store.language == .zh ? "\(store.completed.count) 件已完成" : "\(store.completed.count) completed", systemImage: "checkmark.circle")
                Spacer()
                Text(store.text("让桌面轻一点，让脑袋空一点。", "A lighter desktop, a clearer mind."))
                    .font(LeLaboType.copy(11))
            }.font(LeLaboType.label(11)).foregroundStyle(Palette.muted).padding(.top, 25)
        }
    }

    private var emptyState: some View {
        VStack(spacing: 16) {
            TendederoRope(sag: 28).frame(height: 50)
            Image(systemName: store.search.isEmpty ? "leaf" : "magnifyingglass").font(.system(size: 33, weight: .ultraLight)).foregroundStyle(Palette.green.opacity(0.6))
            Text(store.search.isEmpty
                 ? (store.destination == .completed
                    ? store.text("完成的任务会收在这里。", "Finished tasks rest here.")
                    : store.text("绳子空着，心里也轻一点。", "An empty line, a lighter mind."))
                 : store.text("没有找到这件事。", "Nothing matched."))
                .font(LeLaboType.display(17)).tracking(0.45)
            Text(store.search.isEmpty
                 ? (store.destination == .completed
                    ? store.text("去完成一件小事，再回来看看。", "Finish something small, then come back.")
                    : store.text("拖入网页或图片，也可以截取任意 App 窗口。", "Drag in a page or image, or capture any app window."))
                 : store.text("试试标题、网址或备注中的其他关键词。", "Try another word from the title, link, or note."))
                .font(LeLaboType.copy(12)).foregroundStyle(Palette.muted)
            if store.destination != .completed, store.search.isEmpty {
                Button(store.text("+  晾第一件事", "+  Hang the first thing")) { store.presentAddOptions() }.buttonStyle(QuietButton()).padding(.top, 5)
            }
            Spacer(minLength: 15)
        }.frame(maxWidth: .infinity).frame(height: 350)
    }

    private var listPage: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text(store.destination == .completed
                     ? (store.language == .zh ? "已收好 · \(store.completed.count) 件" : "Archived · \(store.completed.count)")
                     : (store.language == .zh ? "等你去做 · \(store.pending.count) 件" : "Waiting · \(store.pending.count)"))
                Spacer()
                if store.destination == .completed {
                    Picker(store.text("完成记录分组", "Group completed tasks"), selection: $completionGrouping) {
                        ForEach(CompletionGrouping.allCases, id: \.self) { grouping in
                            Text(groupingTitle(grouping)).tag(grouping)
                        }
                    }
                    .labelsHidden().pickerStyle(.segmented).frame(width: 210)
                } else {
                    Text(store.text("网页、图片、App 和文字待办都在这里", "Pages, images, apps, and tasks all live here"))
                }
            }.font(LeLaboType.label(12)).foregroundStyle(Palette.muted).padding(.bottom, 10)
            if store.filtered.isEmpty { emptyState }
            if !store.filtered.isEmpty, store.destination == .completed {
                ForEach(completionSections) { section in
                    VStack(alignment: .leading, spacing: 10) {
                        HStack {
                            Text(sectionTitle(section.start))
                                .font(LeLaboType.display(14))
                                .tracking(0.4)
                                .foregroundStyle(Palette.ink.opacity(0.82))
                            Rectangle().fill(Palette.border.opacity(0.7)).frame(height: 1)
                            Text(store.language == .zh ? "\(section.tasks.count) 件" : "\(section.tasks.count) items")
                                .font(LeLaboType.label(10)).foregroundStyle(Palette.muted)
                        }.padding(.top, 5)
                        ForEach(section.tasks) { task in taskRow(task) }
                    }
                }
            } else if !store.filtered.isEmpty {
                ForEach(store.filtered) { task in taskRow(task) }
            }
        }
    }

    private var completionSections: [CompletionSection] {
        CompletionArchive.sections(from: store.filtered, grouping: completionGrouping)
    }

    private func groupingTitle(_ grouping: CompletionGrouping) -> String {
        switch grouping {
        case .month: return store.text("月", "Month")
        case .week: return store.text("周", "Week")
        case .day: return store.text("日", "Day")
        }
    }

    private func sectionTitle(_ start: Date) -> String {
        let formatter = DateFormatter()
        formatter.locale = store.language.locale
        switch completionGrouping {
        case .month:
            formatter.dateFormat = store.language == .zh ? "yyyy年M月" : "MMMM yyyy"
            return formatter.string(from: start)
        case .week:
            let end = Calendar.current.date(byAdding: .day, value: 6, to: start) ?? start
            formatter.dateFormat = store.language == .zh ? "M月d日" : "MMM d"
            return "\(formatter.string(from: start)) – \(formatter.string(from: end))"
        case .day:
            formatter.dateFormat = store.language == .zh ? "M月d日 EEEE" : "EEEE, MMMM d"
            return formatter.string(from: start)
        }
    }

    private func taskRow(_ task: HangingTask) -> some View {
        HStack(spacing: 15) {
            Button { task.isCompleted ? store.restore(task) : store.complete(task) } label: {
                Image(systemName: task.isCompleted ? "checkmark.circle.fill" : "circle").font(.system(size: 20, weight: .light))
                    .foregroundStyle(task.isCompleted ? Palette.green : Palette.muted)
            }.buttonStyle(.plain).help(task.isCompleted ? store.text("重新挂回晾衣绳", "Hang on the clothesline again") : store.text("完成任务", "Complete task"))
            Button { store.open(task) } label: {
                ZStack {
                    Rectangle().fill(Palette.cards[abs(task.tint % 4)])
                    if let preview = store.preview(task) { Image(nsImage: preview).resizable().scaledToFill() }
                    else { Image(systemName: store.sourceSymbol(task)).foregroundStyle(Palette.accents[abs(task.tint % 4)]) }
                }.frame(width: 57, height: 45).clipped().clipShape(Rectangle())
            }.buttonStyle(.plain)
            Button { store.open(task) } label: {
                VStack(alignment: .leading, spacing: 7) {
                    Text(task.title).font(LeLaboType.display(13)).tracking(0.35).lineLimit(1)
                    HStack(spacing: 8) {
                        Text(store.sourceLabel(task))
                        if task.isCompleted, let completed = task.completedAt {
                            Text("·")
                            Text(store.text("发布", "Published"))
                            Text(task.createdAt, format: .dateTime.month().day().hour().minute())
                            Text("·")
                            Text(store.text("完成", "Completed"))
                            Text(completed, format: .dateTime.month().day().hour().minute())
                        } else if let due = task.dueAt { Text("·"); Text(due, format: .dateTime.month().day()); Text(store.text("到期", "due")) }
                    }.font(LeLaboType.label(11)).foregroundStyle(Palette.muted).lineLimit(1)
                }.frame(maxWidth: .infinity, alignment: .leading).contentShape(Rectangle())
            }.buttonStyle(.plain)
            if task.isCompleted {
                Button(store.text("再晾一次", "Hang again")) { store.restore(task) }.buttonStyle(QuietButton())
                Button { deleting = task } label: { Image(systemName: "trash").foregroundStyle(Palette.muted) }.buttonStyle(.plain).help(store.text("永久删除", "Delete permanently"))
            } else {
                Button { store.editingTask = task } label: { Image(systemName: "ellipsis").foregroundStyle(Palette.muted) }.buttonStyle(.plain).help(store.text("编辑", "Edit"))
            }
        }.padding(16).background(Palette.raisedSurface.opacity(0.75), in: Rectangle())
            .overlay(Rectangle().stroke(Palette.border.opacity(0.7), lineWidth: 0.7))
    }

    private var footer: some View {
        HStack {
            Circle().fill(Palette.green.opacity(0.7)).frame(width: 5, height: 5)
            Text(store.text("保存在这台 Mac 上", "SAVED ON THIS MAC")).font(LeLaboType.label(10)).foregroundStyle(Palette.muted)
            Spacer()
            Text(store.text("⌃⌥L  呼出桌面晾衣绳", "⌃⌥L  SHOW THE DESKTOP CLOTHESLINE")).font(LeLaboType.label(10)).foregroundStyle(Palette.muted)
        }.padding(.horizontal, 38).padding(.vertical, 14)
            .overlay(alignment: .top) { Rectangle().fill(Palette.border.opacity(0.6)).frame(height: 1) }
    }
}
