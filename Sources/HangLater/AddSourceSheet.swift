import SwiftUI

struct AddSourceSheet: View {
    @ObservedObject var store: AppStore
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 7) {
                    Text(store.text("晾一件什么？", "What would you like to hang?"))
                        .font(LeLaboType.display(24))
                        .textCase(.uppercase)
                        .tracking(0.65)
                    Text(store.text("网页、图片、App 窗口，或一件普通待办。",
                                    "A web page, image, app window, or a regular task."))
                        .font(LeLaboType.copy(11)).foregroundStyle(Palette.muted)
                }
                Spacer()
                Button { dismiss() } label: { Image(systemName: "xmark") }.buttonStyle(.plain)
            }

            VStack(spacing: 10) {
                sourceButton(symbol: "square.and.pencil", title: store.text("写待办或贴链接", "Write a task or paste a link"),
                             detail: store.text("记下一件事，也可以粘贴网页地址。", "Write something down or paste a web address.")) {
                    next { store.showingComposer = true }
                }
                sourceButton(symbol: "photo", title: store.text("选择图片", "Choose an image"),
                             detail: store.text("也可以直接把图片拖到晾衣绳上。", "You can also drag an image straight onto the clothesline.")) {
                    next { store.chooseImage() }
                }
                sourceButton(symbol: "macwindow", title: store.text("截取 App 窗口", "Capture an app window"),
                             detail: store.text("适用于任意正在显示的 App；点击纸片可重新唤起它。",
                                                "Works with any visible app; clicking the card brings it back.")) {
                    next { store.showingWindowCapture = true }
                }
                sourceButton(symbol: "play.rectangle", title: store.text("从 Chrome 晾网页或视频", "Hang a page or video from Chrome"),
                             detail: store.text("视频会自动只保留播放器画面。", "Videos automatically keep only the player frame.")) {
                    next { store.showingGuide = true }
                }
            }
        }
        .padding(28).frame(width: 510)
        .background(Palette.paper).foregroundStyle(Palette.ink)
        .font(LeLaboType.copy(12))
        .preferredColorScheme(store.appearance.colorScheme)
        .tint(Palette.green)
    }

    private func sourceButton(symbol: String, title: String, detail: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack(spacing: 15) {
                Image(systemName: symbol).font(.system(size: 20, weight: .light)).foregroundStyle(Palette.green)
                    .frame(width: 38, height: 38).background(Palette.raisedSurface, in: Rectangle())
                    .overlay(Rectangle().stroke(Palette.border, lineWidth: 0.7))
                VStack(alignment: .leading, spacing: 5) {
                    Text(title).font(LeLaboType.display(13)).tracking(0.4)
                    Text(detail).font(LeLaboType.copy(9)).foregroundStyle(Palette.muted).lineLimit(2)
                }
                Spacer()
                Image(systemName: "chevron.right").font(.system(size: 10, weight: .semibold)).foregroundStyle(Palette.muted)
            }
            .padding(14).background(Palette.raisedSurface, in: Rectangle())
            .overlay(Rectangle().stroke(Palette.border, lineWidth: 1))
        }.buttonStyle(.plain)
    }

    private func next(_ action: @escaping () -> Void) {
        dismiss()
        DispatchQueue.main.async(execute: action)
    }
}
