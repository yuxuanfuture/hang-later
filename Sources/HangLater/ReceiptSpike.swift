import HangLaterCore
import SwiftUI
import UniformTypeIdentifiers

struct ReceiptSpike: View {
    @ObservedObject var store: AppStore
    var surface: DragSurface = .main
    @State private var targeted = false

    private var desktop: Bool { surface == .desktop }
    private var isTargeted: Bool { targeted || store.isDraggingOverReceipt(on: surface) }
    private var visibleReceipts: [HangingTask] { Array(store.spiked.prefix(5)) }

    var body: some View {
        VStack(spacing: desktop ? 0 : 3) {
            HStack(spacing: 6) {
                Text(store.text("完成钉", "DONE SPIKE"))
                    .font(LeLaboType.label(desktop ? 9 : 10, bold: true))
                    .tracking(desktop ? 1.4 : 1.8)
                if !store.spiked.isEmpty {
                    Text("\(store.spiked.count)")
                        .font(LeLaboType.label(8, bold: true))
                        .padding(.horizontal, 5).padding(.vertical, 2)
                        .foregroundStyle(Palette.onAccent)
                        .background(Palette.green, in: Rectangle())
                }
            }

            spikeScene
                .frame(width: desktop ? 224 : 194, height: desktop ? 218 : 224)
                .overlay {
                    if isTargeted {
                        Rectangle()
                            .stroke(Palette.green, style: StrokeStyle(lineWidth: 2, dash: [6, 4]))
                            .shadow(color: Palette.green.opacity(0.40), radius: 12)
                    }
                }
                .contentShape(Rectangle())

            HStack(spacing: 6) {
                Image(systemName: "rotate.3d")
                Text(store.spiked.isEmpty
                     ? store.text("拖纸片来完成 · 拖动旋转", "DROP A CARD · DRAG TO ROTATE")
                     : store.text("纸片留在钉上 · 拖动旋转", "SLIPS STAY · DRAG TO ROTATE"))
            }
            .font(LeLaboType.label(desktop ? 8 : 9))
            .foregroundStyle(Palette.muted).lineLimit(1)
        }
        .frame(width: desktop ? 234 : 198)
        .contentShape(Rectangle())
        .scaleEffect(isTargeted ? 1.035 : 1)
        .animation(.spring(response: 0.25, dampingFraction: 0.72), value: isTargeted)
        .background(receiptFrameReader)
        .onDrop(of: [UTType.plainText], isTargeted: $targeted) { providers in
            store.acceptCompletionDrop(providers)
        }
        .background {
            if desktop {
                GeometryReader { proxy in
                    Color.clear.preference(key: DesktopHitRects.self,
                                           value: [proxy.frame(in: .named("desktopLine"))])
                }
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(store.text("可旋转的完成钉，已有 \(store.spiked.count) 张完成纸片",
                                       "Rotatable done spike with \(store.spiked.count) completed slips"))
        .accessibilityHint(store.text("拖入任务可完成，拖动完成钉可旋转，点按可打开完成钉页面。",
                                      "Drop a task to finish it, drag to rotate, or click to open the Done Spike."))
        .help(store.text("把任务纸片拖到尖钉上即可完成；在完成钉上拖动可以旋转 3D 模型。",
                         "Drop a task on the point to finish it. Drag the spike to rotate the 3D model."))
    }

    @ViewBuilder
    private var spikeScene: some View {
        if ProcessInfo.processInfo.arguments.contains(where: { $0.hasPrefix("--render-") }) {
            Image(nsImage: ReceiptSpike3DView.previewImage(receipts: visibleReceipts,
                                                           highlighted: isTargeted,
                                                           language: store.language,
                                                           appearance: store.appearance))
                .resizable().scaledToFit()
        } else {
            ReceiptSpike3DView(receipts: visibleReceipts, highlighted: isTargeted,
                               language: store.language, appearance: store.appearance) {
                store.destination = .spike
                store.showMainWindow?()
            } onReceiptClick: { _ in
                store.destination = .spike
                store.showMainWindow?()
            } onReceiptMove: { id, x, y, z in
                guard let task = store.spiked.first(where: { $0.id == id }) else { return }
                store.moveOnSpike(task, x: x, y: y, z: z)
            }
        }
    }

    private var receiptFrameReader: some View {
        GeometryReader { proxy in
            let frame = proxy.frame(in: .global)
            Color.clear
                .onAppear { store.setReceiptFrame(frame, on: surface) }
                .onChange(of: frame) { _, newFrame in
                    store.setReceiptFrame(newFrame, on: surface)
                }
        }
    }
}
