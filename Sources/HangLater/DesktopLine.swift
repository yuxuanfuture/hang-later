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
    func place(on screen: NSScreen) {
        let visible = screen.visibleFrame
        let next = NSRect(x: visible.minX, y: visible.maxY - 304, width: visible.width, height: 304)
        if frame != next { setFrame(next, display: true) }
    }
}

@MainActor
final class DesktopState: ObservableObject {
    @Published private(set) var revealed = false
    var hitRects: [CGRect] = []
    var dropTargetRect: CGRect = .zero
    func setRevealed(_ value: Bool) { if revealed != value { revealed = value } }
}

struct DesktopLineView: View {
    @ObservedObject var store: AppStore
    @ObservedObject var state: DesktopState
    @State private var targeted = false

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
                Label("Drag something here", systemImage: "pin")
                    .font(LeLaboType.label(11, bold: true))
                    .foregroundStyle(Palette.labelInk)
                    .textCase(.uppercase)
                    .padding(.horizontal, 17).padding(.vertical, 12)
                    .background { LabelPaper() }
                    .overlay(Rectangle().stroke(targeted ? Palette.labelInk : Palette.labelRule.opacity(0.72),
                                                lineWidth: targeted ? 2 : 0.8))
                    .background(GeometryReader { proxy in
                        Color.clear.preference(key: DesktopDropTargetRect.self,
                                               value: proxy.frame(in: .named("desktopLine")))
                    })
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
        .onDrop(of: DropSupport.acceptedContentTypes, isTargeted: $targeted) { store.acceptDrop($0) }
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
