import HangLaterCore
import SwiftUI

struct DoneSpikePage: View {
    @ObservedObject var store: AppStore
    @State private var spikeDropTargeted = false
    @State private var onListTargeted = false
    @State private var offListTargeted = false

    private var matchingSpiked: [HangingTask] { store.spiked.filter(matchesSearch) }
    private var matchingOffSpike: [HangingTask] { store.offSpike.filter(matchesSearch) }
    private var renderedReceipts: [HangingTask] { Array(matchingSpiked.prefix(10)) }

    var body: some View {
        HStack(alignment: .top, spacing: 24) {
            VStack(spacing: 3) {
                HStack(spacing: 8) {
                    Text(store.text("完成钉", "DONE SPIKE"))
                        .font(LeLaboType.label(10, bold: true)).tracking(2)
                    Text("\(store.spiked.count)")
                        .font(LeLaboType.label(9, bold: true))
                        .foregroundStyle(Palette.onAccent)
                        .padding(.horizontal, 6).padding(.vertical, 3)
                        .background(Palette.green, in: Rectangle())
                    Spacer()
                }
                Text(workdayWindowLine)
                    .font(LeLaboType.label(8)).foregroundStyle(Palette.muted)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.top, 3)

                GeometryReader { proxy in
                    spikeScene
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                        .contentShape(Rectangle())
                        .overlay {
                            if spikeDropTargeted {
                                Rectangle()
                                    .stroke(Palette.green, style: StrokeStyle(lineWidth: 3, dash: [8, 5]))
                                    .shadow(color: Palette.green.opacity(0.35), radius: 12)
                            }
                        }
                        .dropDestination(for: String.self) { items, location in
                            guard let task = task(from: items) else { return false }
                            let x = (Double(location.x / max(proxy.size.width, 1)) - 0.5) * 1.9
                            let verticalRatio = Double(location.y / max(proxy.size.height, 1))
                            let y = 3.05 - verticalRatio * (3.05 - 0.72)
                            let z = (0.5 - Double(location.y / max(proxy.size.height, 1))) * 0.76
                            store.putOnSpike(task, x: x, y: y, z: z)
                            return true
                        } isTargeted: { spikeDropTargeted = $0 }
                }
                .frame(height: 405)

                Label(store.text("纸片可上下、前后、左右移动 · 拖空白处旋转 · 右侧纸片可拖来串上",
                                 "Move slips up, down, forward, back, left or right · Drag empty space to rotate"),
                      systemImage: "rotate.3d")
                    .font(LeLaboType.label(9)).foregroundStyle(Palette.muted)
                    .padding(.bottom, 14)

                Text(store.text("在纸面上按住拖动可沿尖钉升降，也可改变穿钉位置。右侧纸片可拖进或拖出两个列表。",
                                "Drag a slip to slide it along the spike and change where the spike pierces it. You can also drag slips between the lists and model."))
                    .font(LeLaboType.copy(10)).foregroundStyle(Palette.muted)
                    .multilineTextAlignment(.center).lineSpacing(3)
                    .frame(maxWidth: 360)
            }
            .padding(18)
            .frame(minWidth: 370, maxWidth: .infinity)
            .background(Palette.raisedSurface.opacity(0.42), in: Rectangle())
            .overlay(Rectangle().stroke(Palette.border.opacity(0.75), lineWidth: 0.8))

            VStack(alignment: .leading, spacing: 22) {
                paperSection(title: store.text("钉上的纸片", "ON THE SPIKE"),
                             tasks: matchingSpiked, actionTitle: store.text("取下", "Take off"),
                             action: store.takeOffSpike)
                    .overlay(dropOutline(onListTargeted))
                    .dropDestination(for: String.self) { items, _ in
                        guard let task = task(from: items) else { return false }
                        store.putOnSpike(task)
                        return true
                    } isTargeted: { onListTargeted = $0 }
                paperSection(title: store.text("已取下", "OFF THE SPIKE"),
                             tasks: matchingOffSpike, actionTitle: store.text("串上去", "Put back"),
                             action: { store.putOnSpike($0) })
                    .overlay(dropOutline(offListTargeted))
                    .dropDestination(for: String.self) { items, _ in
                        guard let task = task(from: items) else { return false }
                        store.takeOffSpike(task)
                        return true
                    } isTargeted: { offListTargeted = $0 }
            }
            .frame(minWidth: 330, maxWidth: .infinity, alignment: .topLeading)
        }
    }

    @ViewBuilder
    private var spikeScene: some View {
        if ProcessInfo.processInfo.arguments.contains(where: { $0.hasPrefix("--render-") }) {
            Image(nsImage: ReceiptSpike3DView.previewImage(receipts: renderedReceipts,
                                                           highlighted: false,
                                                           language: store.language,
                                                           appearance: store.appearance,
                                                           yaw: 0.42,
                                                           pitch: -0.03))
                .resizable().scaledToFit()
        } else {
            ReceiptSpike3DView(receipts: renderedReceipts, highlighted: false,
                               language: store.language,
                               appearance: store.appearance,
                               onClick: {},
                               onReceiptClick: { id in
                                   guard let task = store.spiked.first(where: { $0.id == id }) else { return }
                                   store.takeOffSpike(task)
                               },
                               onReceiptMove: { id, x, y, z in
                                   guard let task = store.spiked.first(where: { $0.id == id }) else { return }
                                   store.moveOnSpike(task, x: x, y: y, z: z)
                               })
        }
    }

    @ViewBuilder
    private func paperSection(title: String, tasks: [HangingTask], actionTitle: String,
                              action: @escaping (HangingTask) -> Void) -> some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack {
                Text(title).font(LeLaboType.label(10, bold: true)).tracking(1.4)
                Spacer()
                Text("\(tasks.count)").font(LeLaboType.label(9)).foregroundStyle(Palette.muted)
            }
            if tasks.isEmpty {
                Text(store.search.isEmpty
                     ? store.text("这里还没有纸片。", "No slips here yet.")
                     : store.text("没有找到纸片。", "No slips matched."))
                    .font(LeLaboType.copy(10)).foregroundStyle(Palette.muted)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.vertical, 17).padding(.horizontal, 14)
                    .background(Palette.raisedSurface.opacity(0.3), in: Rectangle())
            } else {
                ForEach(tasks) { task in
                    paperRow(task, actionTitle: actionTitle, action: action)
                }
            }
        }
    }

    private func paperRow(_ task: HangingTask, actionTitle: String,
                          action: @escaping (HangingTask) -> Void) -> some View {
        HStack(spacing: 11) {
            Image(systemName: "line.3.horizontal")
                .font(.system(size: 12, weight: .medium)).foregroundStyle(Palette.muted)
                .frame(width: 12)
                .help(store.text("按住拖动纸片", "Drag this slip"))
            Button { store.open(task) } label: {
                Image(systemName: "doc.text.fill")
                    .font(.system(size: 17)).foregroundStyle(Palette.labelInk)
                    .frame(width: 34, height: 42)
                    .background(Palette.cards[abs(task.tint % 4)], in: Rectangle())
            }.buttonStyle(.plain)
            VStack(alignment: .leading, spacing: 5) {
                Text(task.title).font(LeLaboType.display(12)).tracking(0.4).lineLimit(1)
                Text(timeLine(task)).font(LeLaboType.label(8)).foregroundStyle(Palette.labelMuted).lineLimit(2)
            }.frame(maxWidth: .infinity, alignment: .leading)
            Button(actionTitle) { action(task) }.buttonStyle(PaperButton())
        }
        .padding(11)
        .foregroundStyle(Palette.labelInk)
        .background { LabelPaper() }
        .overlay(Rectangle().stroke(Palette.labelRule.opacity(0.68), lineWidth: 0.8))
        .contentShape(Rectangle())
        .draggable(task.id.uuidString) {
            paperDragPreview(task)
        }
    }

    private func paperDragPreview(_ task: HangingTask) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(task.title).font(LeLaboType.display(13)).tracking(0.45).lineLimit(1)
            Text(timeLine(task)).font(LeLaboType.label(8)).foregroundStyle(Palette.labelMuted)
        }
        .frame(width: 270, alignment: .leading)
        .padding(15)
        .foregroundStyle(Palette.labelInk)
        .background { LabelPaper() }
        .clipShape(UnevenRoundedRectangle(cornerRadii: .init(topLeading: 1, bottomLeading: 8, bottomTrailing: 2, topTrailing: 7)))
        .overlay(alignment: .top) {
            Rectangle().fill(Palette.green.opacity(0.35)).frame(height: 1)
        }
    }

    @ViewBuilder
    private func dropOutline(_ targeted: Bool) -> some View {
        if targeted {
            Rectangle()
                .stroke(Palette.green, style: StrokeStyle(lineWidth: 2, dash: [6, 4]))
                .allowsHitTesting(false)
        }
    }

    private func task(from items: [String]) -> HangingTask? {
        guard let raw = items.first, let id = UUID(uuidString: raw) else { return nil }
        return store.spikeEligibleCompleted.first(where: { $0.id == id })
    }

    private func timeLine(_ task: HangingTask) -> String {
        let formatter = DateFormatter()
        formatter.locale = store.language.locale
        formatter.dateFormat = store.language == .zh ? "M月d日 HH:mm" : "MMM d, HH:mm"
        let published = formatter.string(from: task.createdAt)
        let completed = formatter.string(from: task.completedAt ?? task.createdAt)
        return store.text("发布 \(published)  ·  完成 \(completed)",
                          "Published \(published)  ·  Completed \(completed)")
    }

    private func matchesSearch(_ task: HangingTask) -> Bool {
        guard !store.search.isEmpty else { return true }
        return [task.title, task.note, task.url ?? ""]
            .joined(separator: " ").localizedCaseInsensitiveContains(store.search)
    }

    private var workdayWindowLine: String {
        let schedule = store.workdaySchedule
        let start = String(format: "%02d:%02d", schedule.startMinute / 60, schedule.startMinute % 60)
        let end = String(format: "%02d:%02d", schedule.endMinute / 60, schedule.endMinute % 60)
        let endLabel = schedule.endsOnFollowingDay ? store.text("次日 ", "NEXT DAY ") : ""
        let prefix = store.text("仅统计发布于 ", "PUBLISHED ")
        let suffix = store.currentWorkdayInterval == nil
            ? store.text(" · 当前不在工作日时段", " · OUTSIDE WORKDAY")
            : ""
        return prefix + start + " → " + endLabel + end + suffix
    }
}
