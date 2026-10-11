import SwiftUI
import HangLaterCore
import UniformTypeIdentifiers

struct HangingCardDimensions {
    let width: CGFloat
    let previewHeight: CGFloat

    static func make(image: NSImage?, compact: Bool) -> Self {
        guard let image, image.size.width > 0, image.size.height > 0 else {
            return Self(width: compact ? 168 : 222, previewHeight: compact ? 94 : 150)
        }
        let bounds: (minWidth: CGFloat, maxWidth: CGFloat, minHeight: CGFloat, maxHeight: CGFloat, area: CGFloat) = compact
            ? (126, 238, 94, 180, 19_000)
            : (145, 300, 120, 220, 33_000)
        let minimumAspect = bounds.minWidth / bounds.maxHeight
        let maximumAspect = bounds.maxWidth / bounds.minHeight
        let aspect = min(max(image.size.width / image.size.height, minimumAspect), maximumAspect)
        var width = sqrt(bounds.area * aspect)
        var height = width / aspect
        let lowerScale = max(bounds.minWidth / width, bounds.minHeight / height)
        let upperScale = min(bounds.maxWidth / width, bounds.maxHeight / height)
        let scale = min(max(1, lowerScale), upperScale)
        width *= scale
        height *= scale
        return Self(width: width.rounded(), previewHeight: height.rounded())
    }
}

struct HangingCard: View {
    let task: HangingTask
    @ObservedObject var store: AppStore
    var compact = false
    var surface: DragSurface = .main
    var currentCenterX: CGFloat = 0
    var stageWidth: CGFloat = 1
    var dimensions = HangingCardDimensions(width: 222, previewHeight: 150)

    @State private var hovering = false
    @State private var appeared = false
    @State private var dropTarget = false
    @State private var isDragging = false
    @State private var dragTranslation: CGSize = .zero
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    private var accent: Color { Palette.labelAccents[abs(task.tint % 4)] }
    private var width: CGFloat { dimensions.width }
    private var tilt: Double { [Double(-2.0), 1.6, -1.2, 2.1][abs(task.tint % 4)] }

    var body: some View {
        TimelineView(.animation(minimumInterval: 1.0 / 30.0, paused: !isDragging || reduceMotion)) { timeline in
            let time = timeline.date.timeIntervalSinceReferenceDate
            let wind = isDragging && !reduceMotion ? sin(time * 7.1) : 0
            let ripple = isDragging && !reduceMotion ? cos(time * 5.3 + Double(task.tint)) : 0
            card(wind: wind, ripple: ripple)
        }
    }

    private func card(wind: Double, ripple: Double) -> some View {
        let shape = UnevenRoundedRectangle(
            topLeadingRadius: 1 + max(0, wind) * 5,
            bottomLeadingRadius: 1 + max(0, -ripple) * 10,
            bottomTrailingRadius: 1 + max(0, ripple) * 9,
            topTrailingRadius: 1 + max(0, -wind) * 6
        )

        return VStack(spacing: -12) {
            WoodenPeg(tint: accent).zIndex(1)
            VStack(alignment: .leading, spacing: 0) {
                VStack(alignment: .leading, spacing: 0) {
                    preview
                    VStack(alignment: .leading, spacing: compact ? 4 : 6) {
                        Text(task.title)
                            .font(LeLaboType.display(compact ? 12 : 14))
                            .textCase(.uppercase)
                            .tracking(compact ? 0.45 : 0.55)
                            .lineLimit(1)
                            .truncationMode(.tail)
                            .foregroundStyle(Palette.labelInk)
                            .frame(maxWidth: .infinity, alignment: .leading)
                        HStack(spacing: 5) {
                            Image(systemName: store.sourceSymbol(task))
                            Text(store.sourceLabel(task)).lineLimit(1)
                            if task.url != nil || task.filePath != nil || task.appBundleIdentifier != nil {
                                Spacer(minLength: 0); Image(systemName: "arrow.up.right")
                            }
                        }
                        .font(LeLaboType.label(compact ? 10 : 11)).foregroundStyle(Palette.labelMuted)
                    }.padding(.horizontal, compact ? 10 : 13).padding(.vertical, compact ? 9 : 11)
                }
                .contentShape(Rectangle())
                .onTapGesture { store.open(task) }
                .help(store.text("点按继续；拖动整张纸片可自由摆放，点“完成”即可归档。",
                                 "Click to continue. Drag the card anywhere on the line, or choose Done to archive it."))

                Rectangle().fill(Palette.labelRule.opacity(0.72)).frame(height: 1)
                HStack(spacing: 7) {
                    Button { store.complete(task) } label: {
                        HStack(spacing: 6) { Image(systemName: "circle"); Text(store.text("完成", "Done")) }
                    }.buttonStyle(.plain).help(store.text("完成并收进已完成页面", "Complete and move to the archive"))
                    Spacer(minLength: 0)
                    if let due = task.dueAt {
                        Text(due, format: .dateTime.month().day())
                            .foregroundStyle(Palette.labelMuted)
                    }
                    Menu {
                        Button(store.text("编辑", "Edit")) {
                            store.editingTask = task
                            store.showMainWindow?()
                        }
                        Divider()
                        Button(store.text("删除", "Delete"), role: .destructive) {
                            store.remove(task)
                        }
                    } label: {
                        Image(systemName: "ellipsis")
                    }
                    .menuStyle(.borderlessButton).menuIndicator(.hidden)
                    .fixedSize()
                    .help(store.text("更多操作", "More actions"))
                }
                .font(LeLaboType.label(compact ? 9 : 10, bold: true)).foregroundStyle(Palette.labelInk)
                .padding(.horizontal, compact ? 10 : 13).padding(.vertical, compact ? 8 : 10)
            }
            .background { LabelPaper().clipShape(shape) }
            .clipShape(shape)
            .overlay(shape.stroke(dropTarget ? Palette.labelInk : Palette.labelRule.opacity(0.72), lineWidth: dropTarget ? 2 : 0.8))
            .shadow(color: Color.black.opacity(isDragging ? 0.24 : (hovering ? 0.17 : 0.09)),
                    radius: isDragging ? 20 : (hovering ? 15 : 9), x: isDragging ? 5 : 1, y: isDragging ? 15 : (hovering ? 10 : 6))
        }
        .frame(width: width)
        .rotationEffect(.degrees((hovering && !isDragging ? 0 : tilt) + wind * (isDragging ? 2.8 : 0)), anchor: .top)
        .rotation3DEffect(.degrees(ripple * (isDragging ? 3.2 : 0)), axis: (x: 0.15, y: 1, z: 0), anchor: .top)
        .scaleEffect(isDragging ? 1.025 : 1, anchor: .top)
        .offset(x: dragTranslation.width, y: dragTranslation.height + (appeared ? (hovering && !isDragging ? -3 : 0) : -16))
        .opacity(appeared ? 1 : 0)
        .animation(reduceMotion ? nil : .spring(response: 0.5, dampingFraction: 0.62), value: hovering)
        .animation(reduceMotion ? nil : .spring(response: 0.24, dampingFraction: 0.78), value: isDragging)
        .onAppear { withAnimation(reduceMotion ? nil : .spring(response: 0.7, dampingFraction: 0.65)) { appeared = true } }
        .onHover { hovering = $0 }
        .simultaneousGesture(dragGesture)
        .onDrop(of: DropSupport.acceptedContentTypes, isTargeted: $dropTarget) {
            store.acceptDrop($0)
        }
        .contextMenu {
            Button(store.text("继续这件事", "Continue this task")) { store.open(task) }
            Button(store.text("编辑", "Edit")) { store.editingTask = task; store.showMainWindow?() }
            Button(store.text("完成，收起来", "Complete and archive")) { store.complete(task) }
            Divider()
            Button(store.text("删除", "Delete"), role: .destructive) { store.remove(task) }
        }
    }

    private var dragGesture: some Gesture {
        DragGesture(minimumDistance: 4, coordinateSpace: .global)
            .onChanged { value in
                if !isDragging {
                    isDragging = true
                    store.beginDragging(task, on: surface)
                }
                dragTranslation = value.translation
                store.updateDragging(at: value.location, on: surface)
            }
            .onEnded { value in
                let translation = value.translation
                dragTranslation = .zero
                isDragging = false
                store.finishDragging(task, on: surface, at: value.location,
                                     translation: translation,
                                     currentCenterX: currentCenterX, stageWidth: stageWidth)
            }
    }

    private var preview: some View {
        ZStack(alignment: .topLeading) {
            if let image = store.preview(task) {
                GeometryReader { geo in
                    Group {
                        if task.filePath?.lowercased().hasSuffix(".pdf") == true {
                            Image(nsImage: image).resizable().scaledToFit()
                        } else {
                            Image(nsImage: image).resizable().scaledToFill()
                        }
                    }
                        .frame(width: geo.size.width, height: geo.size.height, alignment: .top).clipped()
                }
            } else {
                LabelPaper()
                if task.url != nil || task.filePath != nil || task.appBundleIdentifier != nil {
                    VStack(alignment: .leading, spacing: 10) {
                        HStack(spacing: 4) {
                            ForEach(0..<3) { _ in Circle().fill(accent.opacity(0.5)).frame(width: 4, height: 4) }
                            Rectangle().fill(Palette.labelInk.opacity(0.16)).frame(height: 7).padding(.leading, 5)
                        }
                        Spacer(minLength: 0)
                        Image(systemName: store.sourceSymbol(task)).font(.system(size: compact ? 24 : 33, weight: .ultraLight))
                            .foregroundStyle(accent)
                        Text(store.sourceLabel(task)).font(LeLaboType.label(compact ? 10 : 12, bold: true))
                            .foregroundStyle(accent).lineLimit(1)
                    }.padding(compact ? 15 : 22)
                } else {
                    VStack(alignment: .leading, spacing: compact ? 10 : 14) {
                        Text(store.text("待 办", "TO DO")).font(LeLaboType.label(9, bold: true)).tracking(3).foregroundStyle(accent)
                        if !task.note.isEmpty {
                            Text(task.note).font(LeLaboType.copy(compact ? 11 : 14))
                                .lineSpacing(4).foregroundStyle(Palette.labelInk.opacity(0.78)).lineLimit(compact ? 2 : 3)
                        } else {
                            ForEach(0..<3) { index in
                                HStack(spacing: 9) {
                                    RoundedRectangle(cornerRadius: 2).stroke(accent.opacity(0.5), lineWidth: 1).frame(width: 9, height: 9)
                                    Rectangle().fill(accent.opacity(0.2)).frame(width: CGFloat([104, 82, 96][index]), height: 1)
                                }
                            }
                        }
                        Spacer(minLength: 0)
                    }.padding(compact ? 15 : 22)
                }
            }
        }.frame(height: dimensions.previewHeight).clipped()
    }
}

struct ClotheslineStage: View {
    let tasks: [HangingTask]
    @ObservedObject var store: AppStore
    var compact = false
    var desktop = false

    var body: some View {
        GeometryReader { geometry in
            let surface: DragSurface = desktop ? .desktop : .main
            ZStack(alignment: .topLeading) {
                TendederoRope(sag: compact ? 13 : 28)
                ForEach(Array(tasks.enumerated()), id: \.element.id) { index, task in
                    let dimensions = HangingCardDimensions.make(image: store.preview(task), compact: compact)
                    let half = dimensions.width / 2
                    let fraction = task.lineX ?? defaultPosition(index: index, count: tasks.count)
                    let desiredCenter = CGFloat(fraction) * geometry.size.width
                    let centerX = min(max(desiredCenter, half + 4), max(half + 4, geometry.size.width - half - 4))
                    let visibleFraction = geometry.size.width > 0 ? centerX / geometry.size.width : 0.5
                    let sag = 4 * (compact ? CGFloat(13) : CGFloat(28)) * visibleFraction * (1 - visibleFraction)
                    HangingCard(task: task, store: store, compact: compact, surface: surface,
                                currentCenterX: centerX, stageWidth: geometry.size.width,
                                dimensions: dimensions)
                        .background {
                            if desktop {
                                GeometryReader { proxy in
                                    Color.clear.preference(key: DesktopHitRects.self,
                                                           value: [proxy.frame(in: .named("desktopLine"))])
                                }
                            }
                        }
                        .padding(.top, 3 + sag)
                        .offset(x: centerX - half)
                        .zIndex(store.draggingTaskID == task.id ? 100_000 : Double(task.lineLayer ?? index))
                }
            }
        }.frame(height: compact ? 246 : 390)
    }

    private func defaultPosition(index: Int, count: Int) -> Double {
        guard count > 1 else { return 0.5 }
        if count <= 3 {
            return 0.5 + (Double(index) - Double(count - 1) / 2) * 0.18
        }
        return 0.14 + Double(index) * 0.72 / Double(count - 1)
    }
}

struct DesktopHitRects: PreferenceKey {
    static var defaultValue: [CGRect] = []
    static func reduce(value: inout [CGRect], nextValue: () -> [CGRect]) { value += nextValue() }
}
