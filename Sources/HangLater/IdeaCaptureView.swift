import SwiftUI

struct IdeaCaptureView: View {
    @ObservedObject var store: AppStore
    let onHang: (String) -> Bool
    let onCancel: () -> Void

    @State private var idea = ""
    @State private var validation: String?
    @FocusState private var ideaFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 10) {
                Image(systemName: "pin.fill")
                    .font(.system(size: 14, weight: .medium))
                    .rotationEffect(.degrees(18))
                Text(store.text("晾一个想法", "HANG AN IDEA"))
                    .font(LeLaboType.display(16))
                    .tracking(0.6)
                Spacer()
                Button(action: onCancel) {
                    Image(systemName: "xmark")
                }
                .buttonStyle(.plain)
                .keyboardShortcut(.cancelAction)
            }

            TextField(text: $idea,
                      prompt: Text(store.text("写下刚刚冒出来的想法…", "Type the idea that just appeared…"))
                        .foregroundColor(Palette.muted)) { EmptyView() }
                .focused($ideaFocused)
                .textFieldStyle(.plain)
                .font(LeLaboType.copy(13))
                .padding(.horizontal, 13)
                .frame(height: 43)
                .background(Palette.raisedSurface, in: Rectangle())
                .overlay(Rectangle().stroke(Palette.border, lineWidth: 1))
                .onSubmit(hangIdea)

            HStack {
                if let validation {
                    Text(validation).foregroundStyle(Palette.danger)
                } else {
                    Text(store.text("按 Return 挂到晾衣绳", "PRESS RETURN TO HANG IT"))
                        .tracking(0.45)
                }
                Spacer()
                Button(store.text("挂上去", "Hang it"), action: hangIdea)
                    .buttonStyle(QuietButton())
                    .disabled(idea.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
            .font(LeLaboType.label(9, bold: true))
            .foregroundStyle(Palette.muted)
        }
        .padding(20)
        .frame(width: 390)
        .background(Palette.paper)
        .foregroundStyle(Palette.ink)
        .preferredColorScheme(store.appearance.colorScheme)
        .tint(Palette.green)
        .onAppear {
            DispatchQueue.main.async { ideaFocused = true }
        }
    }

    private func hangIdea() {
        let clean = idea.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !clean.isEmpty else {
            validation = store.text("先写下一个想法。", "Write an idea first.")
            return
        }
        if onHang(clean) {
            onCancel()
        } else {
            validation = store.error ?? store.text("这个想法没有保存下来。", "This idea could not be saved.")
            store.error = nil
        }
    }
}
