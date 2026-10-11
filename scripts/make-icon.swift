import AppKit

// A macOS app icon drawn from the same Le Labo-inspired label system used by
// the app: charcoal field, warm paper stock, condensed display type, thin
// rules, and typewriter metadata.
let output = URL(fileURLWithPath: CommandLine.arguments[1])
let image = NSImage(size: NSSize(width: 1024, height: 1024))

private func color(_ hex: UInt32, alpha: CGFloat = 1) -> NSColor {
    NSColor(srgbRed: CGFloat((hex >> 16) & 255) / 255,
            green: CGFloat((hex >> 8) & 255) / 255,
            blue: CGFloat(hex & 255) / 255,
            alpha: alpha)
}

private func font(_ name: String, size: CGFloat, fallbackWeight: NSFont.Weight = .regular) -> NSFont {
    NSFont(name: name, size: size) ?? .systemFont(ofSize: size, weight: fallbackWeight)
}

private func drawText(_ text: String, in rect: NSRect, font: NSFont,
                      color: NSColor, kern: CGFloat = 0) {
    let paragraph = NSMutableParagraphStyle()
    paragraph.lineBreakMode = .byClipping
    (text as NSString).draw(in: rect, withAttributes: [
        .font: font,
        .foregroundColor: color,
        .kern: kern,
        .paragraphStyle: paragraph
    ])
}

image.lockFocus()
NSGraphicsContext.current?.imageInterpolation = .high

let outer = NSBezierPath(roundedRect: NSRect(x: 48, y: 48, width: 928, height: 928),
                         xRadius: 205, yRadius: 205)
color(0x171715).setFill()
outer.fill()

color(0xE5E0D2, alpha: 0.42).setStroke()
let keyline = NSBezierPath(roundedRect: NSRect(x: 79, y: 79, width: 866, height: 866),
                           xRadius: 174, yRadius: 174)
keyline.lineWidth = 3
keyline.stroke()

let ropeShadow = NSBezierPath()
ropeShadow.move(to: NSPoint(x: 78, y: 785))
ropeShadow.curve(to: NSPoint(x: 946, y: 785),
                 controlPoint1: NSPoint(x: 320, y: 700),
                 controlPoint2: NSPoint(x: 704, y: 700))
ropeShadow.lineWidth = 13
color(0x000000, alpha: 0.42).setStroke()
ropeShadow.stroke()

let rope = ropeShadow.copy() as! NSBezierPath
rope.lineWidth = 8
color(0xA9A79E).setStroke()
rope.stroke()

let paperRect = NSRect(x: 199, y: 284, width: 626, height: 460)
NSGraphicsContext.saveGraphicsState()
let rotation = NSAffineTransform()
rotation.translateX(by: paperRect.midX, yBy: paperRect.midY)
rotation.rotate(byDegrees: -3)
rotation.translateX(by: -paperRect.midX, yBy: -paperRect.midY)
rotation.concat()

let shadow = NSShadow()
shadow.shadowColor = NSColor.black.withAlphaComponent(0.38)
shadow.shadowBlurRadius = 34
shadow.shadowOffset = NSSize(width: 8, height: -18)
shadow.set()

let paper = NSBezierPath(roundedRect: paperRect, xRadius: 7, yRadius: 7)
color(0xE5E0D2).setFill()
paper.fill()

NSGraphicsContext.saveGraphicsState()
paper.addClip()
let textureURL = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
    .appendingPathComponent("Sources/HangLater/Resources/le-labo-paper.png")
if let texture = NSImage(contentsOf: textureURL) {
    texture.draw(in: paperRect, from: .zero, operation: .sourceOver, fraction: 0.38)
}
NSGraphicsContext.restoreGraphicsState()

color(0x171713).setStroke()
paper.lineWidth = 5
paper.stroke()

for y: CGFloat in [536, 438] {
    let rule = NSBezierPath()
    rule.move(to: NSPoint(x: paperRect.minX, y: y))
    rule.line(to: NSPoint(x: paperRect.maxX, y: y))
    rule.lineWidth = 4
    rule.stroke()
}

let ink = color(0x171713)
drawText("HANG LATER", in: NSRect(x: 232, y: 566, width: 560, height: 104),
         font: font("ArialNarrow-Bold", size: 91, fallbackWeight: .bold), color: ink, kern: 1.5)
drawText("SAVE NOW  /  OPEN LATER", in: NSRect(x: 236, y: 500, width: 550, height: 34),
         font: font("CourierNewPS-BoldMT", size: 27, fallbackWeight: .bold), color: ink, kern: 1.2)

drawText("DESKTOP TASK LABEL", in: NSRect(x: 236, y: 391, width: 550, height: 34),
         font: font("CourierNewPS-BoldMT", size: 27, fallbackWeight: .bold), color: ink, kern: 0.8)
drawText("STATUS: HANGING", in: NSRect(x: 236, y: 345, width: 550, height: 34),
         font: font("CourierNewPSMT", size: 26), color: ink, kern: 0.5)

NSGraphicsContext.restoreGraphicsState()

let pegShadow = NSShadow()
pegShadow.shadowColor = NSColor.black.withAlphaComponent(0.38)
pegShadow.shadowBlurRadius = 14
pegShadow.shadowOffset = NSSize(width: 5, height: -7)
pegShadow.set()
let peg = NSBezierPath(roundedRect: NSRect(x: 480, y: 676, width: 64, height: 166),
                       xRadius: 8, yRadius: 8)
color(0x777771).setFill()
peg.fill()
color(0xC8C7C0, alpha: 0.62).setFill()
NSBezierPath(roundedRect: NSRect(x: 489, y: 686, width: 13, height: 145),
             xRadius: 5, yRadius: 5).fill()

image.unlockFocus()

let bitmap = NSBitmapImageRep(data: image.tiffRepresentation!)!
let png = bitmap.representation(using: .png, properties: [:])!
try png.write(to: output.appendingPathComponent("icon.png"))

let iconset = output.appendingPathComponent("HangLater.iconset", isDirectory: true)
try FileManager.default.createDirectory(at: iconset, withIntermediateDirectories: true)
for size in [16, 32, 128, 256, 512] {
    for scale in [1, 2] {
        let actual = size * scale
        let resized = NSImage(size: NSSize(width: actual, height: actual))
        resized.lockFocus()
        NSGraphicsContext.current?.imageInterpolation = .high
        image.draw(in: NSRect(x: 0, y: 0, width: actual, height: actual))
        resized.unlockFocus()
        let representation = NSBitmapImageRep(data: resized.tiffRepresentation!)!
            .representation(using: .png, properties: [:])!
        let name = "icon_\(size)x\(size)\(scale == 2 ? "@2x" : "").png"
        try representation.write(to: iconset.appendingPathComponent(name))
    }
}

let process = Process()
process.executableURL = URL(fileURLWithPath: "/usr/bin/iconutil")
process.arguments = ["-c", "icns", iconset.path,
                     "-o", output.appendingPathComponent("HangLater.icns").path]
try process.run()
process.waitUntilExit()
guard process.terminationStatus == 0 else { exit(1) }
try FileManager.default.removeItem(at: iconset)
