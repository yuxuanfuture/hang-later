import AppKit
import HangLaterCore
import SwiftUI
import UniformTypeIdentifiers

final class DesktopPanel: NSPanel {
    init(content: NSView) {
        super.init(contentRect: .zero, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        isOpaque = false; backgroundColor = .clear; hasShadow = false; level = .floating
        collectionBehavior = [.canJoinAllSpaces, .stationary, .ignoresCycle]
        hidesOnDeactivate = false; isMovable = false; becomesKeyOnlyIfNeeded = true
        ignoresMouseEvents = true; contentView = content
    }
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
    func place(on screen: NSScreen, verticalPosition: Double) {
        let visible = screen.visibleFrame
        let height: CGFloat = 304
        let originY = DesktopLineLayout.verticalOrigin(
            visibleMinY: Double(visible.minY), visibleHeight: Double(visible.height),
            panelHeight: Double(height), position: verticalPosition
        )
        let next = NSRect(x: visible.minX, y: CGFloat(originY), width: visible.width, height: height)
        if frame != next { setFrame(next, display: true) }
    }
}

@MainActor
final class DesktopState: ObservableObject {
    @Published private(set) var revealed = false
    @Published private(set) var externalDragTargeted = false
    var hitRects: [CGRect] = []
    var dropTargetRect: CGRect = .zero
    func setRevealed(_ value: Bool) { if revealed != value { revealed = value } }
    func setExternalDragTargeted(_ value: Bool) {
        if externalDragTargeted != value { externalDragTargeted = value }
    }
}

struct DesktopLineView: View {
    @ObservedObject var store: AppStore
    @ObservedObject var state: DesktopState
    @State private var targeted = false

    private var dropHighlighted: Bool { targeted || state.externalDragTargeted }

    var body: some View {
        ZStack(alignment: .topLeading) {
            VStack(spacing: 0) {
                if store.pending.isEmpty {
                    TendederoRope(sag: 13).frame(height: 30)
                        .frame(height: 246, alignment: .top)
                } else {
                    ClotheslineStage(tasks: store.pending, store: store, compact: true, desktop: true)
                }
                Spacer(minLength: 0)
            }
            HStack {
                Spacer()
                HStack(spacing: 12) {
                    Image(systemName: "pin")
                        .font(.system(size: 13, weight: .medium))
                        .rotationEffect(.degrees(-28))
                    Text("Drag something here")
                        .font(LeLaboType.label(11, bold: true))
                        .textCase(.uppercase)
                }
                .foregroundStyle(Palette.labelInk)
                .frame(width: 220, height: 48)
                .background {
                    ZStack {
                        LabelPaper()
                        Palette.labelInk.opacity(dropHighlighted ? 0.18 : 0)
                    }
                }
                .overlay {
                    Rectangle().stroke(
                        Palette.labelInk.opacity(dropHighlighted ? 0.98 : 0.80),
                        style: StrokeStyle(lineWidth: dropHighlighted ? 2 : 1.2,
                                           dash: [7, 5])
                    )
                }
                .shadow(color: .black.opacity(dropHighlighted ? 0.34 : 0.22), radius: 5, y: 2)
                .scaleEffect(dropHighlighted ? 1.015 : 1)
                .animation(.easeOut(duration: 0.14), value: dropHighlighted)
                .contentShape(Rectangle())
                .background(GeometryReader { proxy in
                    Color.clear.preference(key: DesktopDropTargetRect.self,
                                           value: proxy.frame(in: .named("desktopLine")))
                })
                .onDrop(of: DropSupport.acceptedContentTypes, isTargeted: $targeted) {
                    store.acceptDrop($0)
                }
                .accessibilityLabel(store.text("把内容拖到这里", "Drop something here"))
                Spacer()
            }
            .padding(.top, 36)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .coordinateSpace(name: "desktopLine")
        .onPreferenceChange(DesktopHitRects.self) { rects in
            Task { @MainActor in state.hitRects = rects }
        }
        .onPreferenceChange(DesktopDropTargetRect.self) { rect in
            Task { @MainActor in state.dropTargetRect = rect }
        }
        .onChange(of: targeted) { _, isTargeted in
            store.dragTargeted = isTargeted
            if isTargeted { state.setRevealed(true) }
        }
        .onDisappear { store.dragTargeted = false }
        .offset(y: state.revealed ? 0 : -320)
        .animation(.spring(response: 0.4, dampingFraction: 0.84), value: state.revealed)
        .preferredColorScheme(store.appearance.colorScheme)
        .tint(Palette.green)
    }
}

struct DesktopDropTargetRect: PreferenceKey {
    static var defaultValue: CGRect = .zero
    static func reduce(value: inout CGRect, nextValue: () -> CGRect) { value = nextValue() }
}
