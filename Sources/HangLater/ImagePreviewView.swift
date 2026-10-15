import SwiftUI
import HangLaterCore

struct ImagePreviewView: View {
    @ObservedObject var store: AppStore
    let task: HangingTask
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 14) {
                Image(systemName: "photo")
                    .font(.system(size: 17, weight: .medium))
                Text(task.title)
                    .font(LeLaboType.display(16))
                    .tracking(0.4)
                    .lineLimit(1)
                Spacer(minLength: 18)
                if let path = task.filePath,
                   FileManager.default.fileExists(atPath: path) {
                    Button {
                        NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: path)])
                    } label: {
                        Label(store.text("在访达中显示", "Show in Finder"), systemImage: "folder")
                    }
                    .buttonStyle(QuietButton())
                }
                Button { dismiss() } label: {
                    Image(systemName: "xmark")
                        .foregroundStyle(Palette.muted)
                        .frame(width: 28, height: 28)
                }
                .buttonStyle(.plain)
                .keyboardShortcut(.cancelAction)
            }
            .padding(.horizontal, 24)
            .padding(.vertical, 18)

            Rectangle().fill(Palette.border).frame(height: 1)

            ZStack {
                Palette.raisedSurface
                if let image = store.fullImage(for: task) {
                    Image(nsImage: image)
                        .resizable()
                        .scaledToFit()
                        .padding(24)
                } else {
                    VStack(spacing: 12) {
                        Image(systemName: "photo.badge.exclamationmark")
                            .font(.system(size: 34, weight: .light))
                        Text(store.text("图片预览已经不可用。", "The image preview is no longer available."))
                            .font(LeLaboType.copy(12))
                    }
                    .foregroundStyle(Palette.muted)
                }
            }
        }
        .frame(minWidth: 680, idealWidth: 860, minHeight: 500, idealHeight: 650)
        .background(Palette.paper)
        .foregroundStyle(Palette.ink)
        .preferredColorScheme(store.appearance.colorScheme)
        .tint(Palette.green)
    }
}
