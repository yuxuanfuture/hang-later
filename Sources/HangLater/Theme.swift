import SwiftUI
import AppKit

enum AppLanguage: String, CaseIterable {
    case zh, en

    var locale: Locale { Locale(identifier: self == .zh ? "zh_CN" : "en_US") }
    func text(_ zh: String, _ en: String) -> String { self == .zh ? zh : en }
}

enum AppAppearance: String, CaseIterable {
    case light, dark

    var colorScheme: ColorScheme { self == .light ? .light : .dark }
}

enum Palette {
    /// Le Labo's quiet black-and-off-white canvas. Dark mode keeps the softer
    /// charcoal requested for the app while the physical slips stay unchanged.
    static let paper = adaptive(light: 0xF7F8F7, dark: 0x212121)
    static let sidebar = adaptive(light: 0xECEDE9, dark: 0x171717)
    static let surface = adaptive(light: 0xFBFBF8, dark: 0x2B2B2B)
    static let raisedSurface = adaptive(light: 0xF3F4F0, dark: 0x292929)
    static let ink = adaptive(light: 0x252522, dark: 0xEFEFE9)
    static let muted = adaptive(light: 0x6C6C66, dark: 0xB5B5AE)
    static let green = adaptive(light: 0x171715, dark: 0xEFEFE9)
    static let onAccent = adaptive(light: 0xF7F8F7, dark: 0x212121)
    static let border = adaptive(light: 0xBFC0BA, dark: 0x464641)
    static let danger = adaptive(light: 0x171715, dark: 0xEFEFE9)

    /// The label stock is intentionally non-adaptive: it should look like a
    /// real, slightly aged piece of paper in both appearances.
    static let labelPaper = Color(hex: 0xE5E0D2)
    static let labelInk = Color(hex: 0x171713)
    static let labelMuted = Color(hex: 0x5F5B50)
    static let labelRule = Color(hex: 0x292821)
    static let labelAccents: [Color] = [
        Color(hex: 0x171713), Color(hex: 0x353229),
        Color(hex: 0x4A463B), Color(hex: 0x625D50)
    ]
    static let accents: [Color] = [
        adaptive(light: 0x171715, dark: 0xEFEFE9), adaptive(light: 0x393934, dark: 0xD4D4CD),
        adaptive(light: 0x55554E, dark: 0xBCBCB4), adaptive(light: 0x727269, dark: 0xA4A49C)
    ]
    static let cards: [Color] = [
        Color(hex: 0xE8E3D6), Color(hex: 0xDED8C8),
        Color(hex: 0xD8D1BF), Color(hex: 0xCEC6B4)
    ]

    private static func adaptive(light: UInt32, dark: UInt32) -> Color {
        Color(nsColor: NSColor(name: nil) { appearance in
            let match = appearance.bestMatch(from: [.darkAqua, .aqua])
            return NSColor(hex: match == .darkAqua ? dark : light)
        })
    }
}

enum LeLaboType {
    /// Bell Gothic is Le Labo's web face but is not installed with macOS.
    /// Arial Narrow has the same compact industrial silhouette; Courier New
    /// supplies the typewriter voice of the product labels and reference copy.
    static func display(_ size: CGFloat) -> Font {
        .custom("ArialNarrow-Bold", fixedSize: size)
    }

    static func label(_ size: CGFloat, bold: Bool = false) -> Font {
        .custom(bold ? "CourierNewPS-BoldMT" : "CourierNewPSMT", fixedSize: size)
    }

    static func copy(_ size: CGFloat, bold: Bool = false) -> Font {
        label(size, bold: bold)
    }

    static func nsDisplay(_ size: CGFloat) -> NSFont {
        NSFont(name: "ArialNarrow-Bold", size: size) ?? .systemFont(ofSize: size, weight: .bold)
    }

    static func nsLabel(_ size: CGFloat, bold: Bool = false) -> NSFont {
        NSFont(name: bold ? "CourierNewPS-BoldMT" : "CourierNewPSMT", size: size)
            ?? .monospacedSystemFont(ofSize: size, weight: bold ? .bold : .regular)
    }
}

enum PaperTextureAsset {
    static let image: NSImage? = {
        if let url = Bundle.main.url(forResource: "le-labo-paper", withExtension: "png"),
           let image = NSImage(contentsOf: url) {
            return image
        }
        let source = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .appendingPathComponent("Resources/le-labo-paper.png")
        return NSImage(contentsOf: source)
    }()
}

struct LabelPaper: View {
    var tint: Color? = nil

    var body: some View {
        ZStack {
            tint ?? Palette.labelPaper
            if let image = PaperTextureAsset.image {
                Image(nsImage: image)
                    .resizable(resizingMode: .tile)
                    .opacity(tint == nil ? 0.45 : 0.28)
                    .blendMode(.multiply)
            }
        }
        .allowsHitTesting(false)
        .accessibilityHidden(true)
    }
}

enum TaskTypePaperStyle {
    static func usesLightInk(_ hex: UInt32) -> Bool {
        let red = Double((hex >> 16) & 255) / 255
        let green = Double((hex >> 8) & 255) / 255
        let blue = Double(hex & 255) / 255
        return red * 0.299 + green * 0.587 + blue * 0.114 < 0.52
    }

    static func paper(_ hex: UInt32?) -> Color {
        hex.map(Color.init(hex:)) ?? Palette.labelPaper
    }

    static func ink(_ hex: UInt32?) -> Color {
        guard let hex else { return Palette.labelInk }
        return usesLightInk(hex) ? Color.white.opacity(0.96) : Color.black.opacity(0.88)
    }

    static func mutedInk(_ hex: UInt32?) -> Color {
        guard let hex else { return Palette.labelMuted }
        return usesLightInk(hex) ? Color.white.opacity(0.72) : Color.black.opacity(0.62)
    }

    static func nsPaper(_ hex: UInt32?) -> NSColor {
        hex.map(NSColor.init(hex:))
            ?? NSColor(srgbRed: 229 / 255, green: 224 / 255, blue: 210 / 255, alpha: 1)
    }

    static func nsInk(_ hex: UInt32?) -> NSColor {
        guard let hex else {
            return NSColor(srgbRed: 23 / 255, green: 23 / 255, blue: 19 / 255, alpha: 1)
        }
        return usesLightInk(hex)
            ? NSColor.white.withAlphaComponent(0.96)
            : NSColor.black.withAlphaComponent(0.88)
    }

    static func nsMutedInk(_ hex: UInt32?) -> NSColor {
        guard let hex else {
            return NSColor(srgbRed: 74 / 255, green: 70 / 255, blue: 59 / 255, alpha: 1)
        }
        return usesLightInk(hex)
            ? NSColor.white.withAlphaComponent(0.72)
            : NSColor.black.withAlphaComponent(0.62)
    }
}

private extension NSColor {
    convenience init(hex: UInt32) {
        self.init(srgbRed: CGFloat((hex >> 16) & 255) / 255,
                  green: CGFloat((hex >> 8) & 255) / 255,
                  blue: CGFloat(hex & 255) / 255, alpha: 1)
    }
}

extension Color {
    init(hex: UInt32) {
        self.init(.sRGB, red: Double((hex >> 16) & 255) / 255,
                  green: Double((hex >> 8) & 255) / 255, blue: Double(hex & 255) / 255, opacity: 1)
    }
}

struct GreenButton: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.font(LeLaboType.label(12, bold: true)).tracking(0.25)
            .foregroundStyle(Palette.onAccent).padding(.horizontal, 18).padding(.vertical, 11)
            .background(Palette.green.opacity(configuration.isPressed ? 0.78 : 1), in: Rectangle())
            .overlay(Rectangle().stroke(Palette.green, lineWidth: 1))
    }
}

struct QuietButton: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.font(LeLaboType.label(11, bold: true)).tracking(0.2)
            .foregroundStyle(Palette.ink).padding(.horizontal, 13).padding(.vertical, 9)
            .background(Palette.raisedSurface.opacity(configuration.isPressed ? 0.55 : 0.82), in: Rectangle())
            .overlay(Rectangle().stroke(Palette.border, lineWidth: 1))
    }
}

struct PaperButton: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.font(LeLaboType.label(10, bold: true))
            .foregroundStyle(Palette.labelInk)
            .padding(.horizontal, 12).padding(.vertical, 8)
            .background(Palette.labelPaper.opacity(configuration.isPressed ? 0.45 : 0.16), in: Rectangle())
            .overlay(Rectangle().stroke(Palette.labelRule.opacity(0.72), lineWidth: 0.8))
    }
}

struct ClothesRope: Shape {
    var sag: CGFloat = 28
    func path(in rect: CGRect) -> Path {
        Path { p in
            p.move(to: CGPoint(x: -10, y: 12))
            p.addQuadCurve(to: CGPoint(x: rect.width + 10, y: 12),
                           control: CGPoint(x: rect.width / 2, y: 12 + sag * 2))
        }
    }
}

/// Tendedero's fixed neutral rope remains readable on light and dark backgrounds.
struct TendederoRope: View {
    var sag: CGFloat = 28

    var body: some View {
        ZStack {
            ClothesRope(sag: sag)
                .stroke(Color.black.opacity(0.22), lineWidth: 1.4)
                .offset(y: 1.2).blur(radius: 1.2)
            ClothesRope(sag: sag).stroke(Color(white: 0.55), lineWidth: 1.2)
            ClothesRope(sag: sag)
                .stroke(Color.white.opacity(0.45), lineWidth: 0.4)
                .offset(y: -0.35)
        }
        .mask(
            LinearGradient(stops: [
                .init(color: .clear, location: 0),
                .init(color: .black, location: 0.08),
                .init(color: .black, location: 0.92),
                .init(color: .clear, location: 1)
            ], startPoint: .leading, endPoint: .trailing)
        )
        .allowsHitTesting(false)
    }
}

struct WoodenPeg: View {
    let tint: Color
    var body: some View {
        RoundedRectangle(cornerRadius: 2)
            .fill(LinearGradient(colors: [Palette.ink.opacity(0.72), Palette.ink.opacity(0.42), Palette.ink.opacity(0.82)], startPoint: .leading, endPoint: .trailing))
            .frame(width: 13, height: 36)
            .overlay(Rectangle().fill(Palette.ink.opacity(0.8)).frame(width: 10, height: 2).offset(y: -4))
            .overlay(Rectangle().fill(Palette.onAccent.opacity(0.3)).frame(width: 1, height: 30).offset(x: -3))
            .shadow(color: .black.opacity(0.15), radius: 2, x: 1, y: 2)
            .allowsHitTesting(false)
    }
}
