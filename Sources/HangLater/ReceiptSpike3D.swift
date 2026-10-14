import AppKit
import HangLaterCore
import SceneKit
import SwiftUI

final class RotatableSpikeSceneView: SCNView {
    var rotationNode: SCNNode?
    var onClick: (() -> Void)?
    var onReceiptClick: ((UUID) -> Void)?
    var onReceiptMove: ((UUID, Double, Double, Double) -> Void)?
    var onReceiptResize: ((UUID, Double) -> Void)?
    var receiptCoordinateNode: SCNNode?
    private var dragDistance: CGFloat = 0
    private var activeReceiptNode: SCNNode?
    private var activeReceiptID: UUID?
    private var receiptDragDepth: CGFloat?
    private var receiptGrabOffset = SCNVector3Zero
    private var resizingReceiptNode: SCNNode?
    private var resizingReceiptID: UUID?

    override var acceptsFirstResponder: Bool { true }

    override func mouseDown(with event: NSEvent) {
        dragDistance = 0
        window?.makeFirstResponder(self)
        let point = convert(event.locationInWindow, from: nil)
        for result in hitTest(point, options: nil) {
            guard let (node, id) = receiptNode(from: result.node), let receiptCoordinateNode else { continue }
            activeReceiptNode = node
            activeReceiptID = id
            receiptDragDepth = projectPoint(result.worldCoordinates).z
            let localHit = receiptCoordinateNode.convertPosition(result.worldCoordinates, from: nil)
            receiptGrabOffset = SCNVector3(node.position.x - localHit.x,
                                           node.position.y - localHit.y,
                                           node.position.z - localHit.z)
            NSCursor.closedHand.set()
            return
        }
    }

    override func mouseDragged(with event: NSEvent) {
        dragDistance += abs(event.deltaX) + abs(event.deltaY)
        if let node = activeReceiptNode, let depth = receiptDragDepth, let receiptCoordinateNode {
            let point = convert(event.locationInWindow, from: nil)
            let world = unprojectPoint(SCNVector3(point.x, point.y, depth))
            let local = receiptCoordinateNode.convertPosition(world, from: nil)
            node.position.x = min(max(local.x + receiptGrabOffset.x, -0.95), 0.95)
            node.position.y = min(max(local.y + receiptGrabOffset.y, 0.72), 3.05)
            node.position.z = min(max(local.z + receiptGrabOffset.z, -0.38), 0.38)
            setNeedsDisplay(bounds)
            return
        }
        guard let rotationNode else { return }
        rotationNode.eulerAngles.y = rotationNode.eulerAngles.y + event.deltaX * 0.012
        let nextPitch = rotationNode.eulerAngles.x + event.deltaY * 0.0045
        rotationNode.eulerAngles.x = min(max(nextPitch, -0.12), 0.18)
        setNeedsDisplay(bounds)
    }

    override func mouseUp(with event: NSEvent) {
        if let node = activeReceiptNode, let id = activeReceiptID {
            activeReceiptNode = nil
            activeReceiptID = nil
            receiptDragDepth = nil
            NSCursor.openHand.set()
            if dragDistance < 4 { onReceiptClick?(id) }
            else {
                onReceiptMove?(id, Double(node.position.x), Double(node.position.y),
                               Double(node.position.z))
            }
            return
        }
        guard dragDistance < 4 else { return }
        let point = convert(event.locationInWindow, from: nil)
        for result in hitTest(point, options: nil) {
            var node: SCNNode? = result.node
            while let current = node {
                if let name = current.name, name.hasPrefix("receipt:"),
                   let id = UUID(uuidString: String(name.dropFirst(8))) {
                    onReceiptClick?(id)
                    return
                }
                node = current.parent
            }
        }
        onClick?()
    }

    private func receiptNode(from hitNode: SCNNode) -> (SCNNode, UUID)? {
        var node: SCNNode? = hitNode
        while let current = node {
            if let name = current.name, name.hasPrefix("receipt:"),
               let id = UUID(uuidString: String(name.dropFirst(8))) { return (current, id) }
            node = current.parent
        }
        return nil
    }

    override func scrollWheel(with event: NSEvent) {
        guard let camera = pointOfView?.camera else { return }
        camera.fieldOfView = min(max(camera.fieldOfView + event.scrollingDeltaY * 0.22, 29), 44)
        setNeedsDisplay(bounds)
    }

    override func magnify(with event: NSEvent) {
        if event.phase == .began {
            let point = convert(event.locationInWindow, from: nil)
            for result in hitTest(point, options: nil) {
                guard let (node, id) = receiptNode(from: result.node) else { continue }
                resizingReceiptNode = node
                resizingReceiptID = id
                break
            }
        }

        if let node = resizingReceiptNode, [.began, .changed].contains(event.phase) {
            let factor = max(0.5, 1 + event.magnification)
            let scale = min(max(CGFloat(node.scale.x) * factor, 0.6), 1.5)
            node.scale = SCNVector3(scale, scale, scale)
            setNeedsDisplay(bounds)
        }

        if event.phase == .ended || event.phase == .cancelled {
            if let node = resizingReceiptNode, let id = resizingReceiptID {
                onReceiptResize?(id, Double(node.scale.x))
            }
            resizingReceiptNode = nil
            resizingReceiptID = nil
        }
    }

    override func resetCursorRects() {
        addCursorRect(bounds, cursor: .openHand)
    }
}

struct ReceiptSpike3DView: NSViewRepresentable {
    let receipts: [HangingTask]
    let highlighted: Bool
    let language: AppLanguage
    let appearance: AppAppearance
    let taskTypeColors: [UUID: UInt32]
    let onClick: () -> Void
    let onReceiptClick: (UUID) -> Void
    let onReceiptMove: (UUID, Double, Double, Double) -> Void
    let onReceiptResize: (UUID, Double) -> Void

    func makeCoordinator() -> Coordinator { Coordinator() }

    func makeNSView(context: Context) -> RotatableSpikeSceneView {
        let view = RotatableSpikeSceneView(frame: .zero)
        view.scene = context.coordinator.scene
        view.pointOfView = context.coordinator.camera
        view.rotationNode = context.coordinator.rotationRoot
        view.onClick = onClick
        view.onReceiptClick = onReceiptClick
        view.onReceiptMove = onReceiptMove
        view.onReceiptResize = onReceiptResize
        view.receiptCoordinateNode = context.coordinator.receiptRoot
        view.backgroundColor = .clear
        view.wantsLayer = true
        view.layer?.backgroundColor = NSColor.clear.cgColor
        view.allowsCameraControl = false
        view.autoenablesDefaultLighting = false
        view.antialiasingMode = .multisampling4X
        view.preferredFramesPerSecond = 60
        return view
    }

    func updateNSView(_ view: RotatableSpikeSceneView, context: Context) {
        view.onClick = onClick
        view.onReceiptClick = onReceiptClick
        view.onReceiptMove = onReceiptMove
        view.onReceiptResize = onReceiptResize
        context.coordinator.update(receipts: receipts, highlighted: highlighted,
                                   language: language, appearance: appearance,
                                   taskTypeColors: taskTypeColors)
    }

    @MainActor
    static func previewImage(receipts: [HangingTask], highlighted: Bool,
                             language: AppLanguage = .zh,
                             appearance: AppAppearance = .light,
                             taskTypeColors: [UUID: UInt32] = [:],
                             yaw: CGFloat = 0, pitch: CGFloat = 0) -> NSImage {
        let coordinator = Coordinator()
        coordinator.update(receipts: receipts, highlighted: highlighted,
                           language: language, appearance: appearance,
                           taskTypeColors: taskTypeColors)
        coordinator.rotationRoot.eulerAngles.y = yaw
        coordinator.rotationRoot.eulerAngles.x = min(max(pitch, -0.12), 0.18)
        let renderer = SCNRenderer(device: nil, options: nil)
        renderer.scene = coordinator.scene
        renderer.pointOfView = coordinator.camera
        return renderer.snapshot(atTime: 0, with: NSSize(width: 760, height: 760),
                                 antialiasingMode: .multisampling4X)
    }

    @MainActor
    final class Coordinator {
        let scene = SCNScene()
        let camera = SCNNode()
        let rotationRoot = SCNNode()
        private let modelContent = SCNNode()
        let receiptRoot = SCNNode()
        private let baseMaterial = SCNMaterial()
        private let topMaterial = SCNMaterial()
        private var receiptSignature = ""

        init() {
            buildScene()
        }

        func update(receipts: [HangingTask], highlighted: Bool, language: AppLanguage,
                    appearance: AppAppearance, taskTypeColors: [UUID: UInt32]) {
            let dark = appearance == .dark
            baseMaterial.diffuse.contents = NSColor(calibratedWhite: dark ? 0.78 : 0.015, alpha: 1)
            topMaterial.diffuse.contents = NSColor(calibratedWhite: dark ? 0.94 : 0.04, alpha: 1)
            baseMaterial.emission.contents = highlighted
                ? NSColor(calibratedWhite: 1, alpha: 0.38)
                : NSColor.black

            let signature = receipts.map {
                let color = $0.taskTypeID.flatMap { taskTypeColors[$0] }
                return "\($0.id.uuidString)|\($0.title)|\($0.completedAt?.timeIntervalSince1970 ?? 0)|\($0.spikeX ?? 0)|\($0.spikeY ?? 0)|\($0.spikeZ ?? 0)|\($0.effectiveSpikeScale)|\(color.map(String.init) ?? "default")"
            }.joined(separator: ";") + "|\(language.rawValue)|\(appearance.rawValue)"
            guard signature != receiptSignature else { return }
            receiptSignature = signature
            receiptRoot.childNodes.forEach { $0.removeFromParentNode() }

            let displayed = Array(receipts.prefix(10).reversed())
            let spacing = displayed.count > 1 ? min(0.18, 1.28 / CGFloat(displayed.count - 1)) : 0
            for (index, task) in displayed.enumerated() {
                let colorHex = task.taskTypeID.flatMap { taskTypeColors[$0] }
                let paper = SCNNode(geometry: Self.paperGeometry(task: task, index: index,
                                                                 language: language,
                                                                 colorHex: colorHex))
                paper.name = "receipt:\(task.id.uuidString)"
                paper.position = SCNVector3(CGFloat(task.spikeX ?? 0),
                                            CGFloat(task.spikeY ?? Double(1.34 + CGFloat(index) * spacing)),
                                            CGFloat(task.spikeZ ?? 0))
                let scale = CGFloat(task.effectiveSpikeScale)
                paper.scale = SCNVector3(scale, scale, scale)
                paper.eulerAngles = SCNVector3(0,
                                               Float([-0.23, 0.14, -0.09, 0.21, -0.16][index % 5]),
                                               Float([0.025, -0.018, 0.014, -0.025, 0.018][index % 5]))
                paper.castsShadow = true
                receiptRoot.addChildNode(paper)
            }
        }

        private func buildScene() {
            scene.background.contents = NSColor.clear
            rotationRoot.position.y = 1.55
            modelContent.position.y = -1.55
            rotationRoot.addChildNode(modelContent)
            modelContent.addChildNode(receiptRoot)
            scene.rootNode.addChildNode(rotationRoot)

            let base = SCNCylinder(radius: 1.18, height: 0.28)
            base.radialSegmentCount = 96
            base.firstMaterial = baseMaterial
            baseMaterial.lightingModel = .physicallyBased
            baseMaterial.metalness.contents = 0.72
            baseMaterial.roughness.contents = 0.27
            let baseNode = SCNNode(geometry: base)
            baseNode.position.y = 0.16
            baseNode.castsShadow = true
            modelContent.addChildNode(baseNode)

            let rim = SCNTorus(ringRadius: 1.125, pipeRadius: 0.052)
            rim.ringSegmentCount = 96
            rim.pipeSegmentCount = 20
            rim.firstMaterial = baseMaterial
            let rimNode = SCNNode(geometry: rim)
            rimNode.position.y = 0.285
            modelContent.addChildNode(rimNode)

            let top = SCNCylinder(radius: 1.10, height: 0.055)
            top.radialSegmentCount = 96
            topMaterial.lightingModel = .physicallyBased
            topMaterial.metalness.contents = 0.82
            topMaterial.roughness.contents = 0.18
            top.firstMaterial = topMaterial
            let topNode = SCNNode(geometry: top)
            topNode.position.y = 0.325
            modelContent.addChildNode(topNode)

            let metal = SCNMaterial()
            metal.lightingModel = .physicallyBased
            metal.diffuse.contents = NSColor(calibratedWhite: 0.78, alpha: 1)
            metal.metalness.contents = 0.96
            metal.roughness.contents = 0.13

            let shaft = SCNCylinder(radius: 0.055, height: 2.66)
            shaft.radialSegmentCount = 48
            shaft.firstMaterial = metal
            let shaftNode = SCNNode(geometry: shaft)
            shaftNode.position.y = 1.68
            modelContent.addChildNode(shaftNode)

            let point = SCNCone(topRadius: 0, bottomRadius: 0.069, height: 0.42)
            point.radialSegmentCount = 48
            point.firstMaterial = metal
            let pointNode = SCNNode(geometry: point)
            pointNode.position.y = 3.22
            modelContent.addChildNode(pointNode)

            let collar = SCNTorus(ringRadius: 0.09, pipeRadius: 0.028)
            collar.ringSegmentCount = 48
            collar.pipeSegmentCount = 16
            collar.firstMaterial = metal
            let collarNode = SCNNode(geometry: collar)
            collarNode.position.y = 0.37
            modelContent.addChildNode(collarNode)

            let target = SCNNode()
            target.position = SCNVector3(0, 1.58, 0)
            scene.rootNode.addChildNode(target)

            let cameraObject = SCNCamera()
            cameraObject.fieldOfView = 35
            cameraObject.zNear = 0.1
            cameraObject.zFar = 100
            camera.camera = cameraObject
            // A higher three-quarter angle keeps the receipt text readable while preserving depth.
            camera.position = SCNVector3(3.55, 4.25, 4.35)
            let look = SCNLookAtConstraint(target: target)
            look.isGimbalLockEnabled = true
            camera.constraints = [look]
            scene.rootNode.addChildNode(camera)

            let key = SCNLight()
            key.type = .area
            key.intensity = 1150
            key.color = NSColor(calibratedWhite: 1, alpha: 1)
            key.areaType = .rectangle
            key.areaExtents = SIMD3<Float>(4, 4, 0)
            key.castsShadow = true
            key.shadowRadius = 8
            key.shadowColor = NSColor.black.withAlphaComponent(0.28)
            let keyNode = SCNNode()
            keyNode.light = key
            keyNode.position = SCNVector3(3.5, 6.2, 4.7)
            keyNode.constraints = [SCNLookAtConstraint(target: target)]
            scene.rootNode.addChildNode(keyNode)

            let fill = SCNLight()
            fill.type = .omni
            fill.intensity = 420
            fill.color = NSColor(calibratedWhite: 0.80, alpha: 1)
            let fillNode = SCNNode()
            fillNode.light = fill
            fillNode.position = SCNVector3(-4.5, 3.6, 2.5)
            scene.rootNode.addChildNode(fillNode)

            let ambient = SCNLight()
            ambient.type = .ambient
            ambient.intensity = 260
            ambient.color = NSColor(calibratedWhite: 0.72, alpha: 1)
            let ambientNode = SCNNode()
            ambientNode.light = ambient
            scene.rootNode.addChildNode(ambientNode)
        }

        private static func paperGeometry(task: HangingTask, index: Int, language: AppLanguage,
                                          colorHex: UInt32?) -> SCNGeometry {
            let columns = 16
            let rows = 8
            let width: Float = 2.82 - Float(index % 2) * 0.10
            let depth: Float = 1.34 + Float(index % 3) * 0.045
            let phase = Float(index) * 0.83
            var vertices: [SCNVector3] = []
            var normals: [SCNVector3] = []
            var textureCoordinates: [CGPoint] = []

            for row in 0...rows {
                let v = Float(row) / Float(rows)
                let z = (v - 0.5) * depth
                for column in 0...columns {
                    let u = Float(column) / Float(columns)
                    let x = (u - 0.5) * width
                    let edgeLift = pow(abs(x) / (width * 0.5), 2) * 0.045
                    let wave = sin(x * 2.45 + phase) * 0.038 + cos(z * 4.1 - phase) * 0.022 + edgeLift
                    vertices.append(SCNVector3(x, wave, z))
                    normals.append(SCNVector3(0, 1, 0))
                    textureCoordinates.append(CGPoint(x: CGFloat(u), y: CGFloat(v)))
                }
            }

            var indices: [UInt32] = []
            let stride = columns + 1
            for row in 0..<rows {
                for column in 0..<columns {
                    let a = UInt32(row * stride + column)
                    let b = a + 1
                    let c = UInt32((row + 1) * stride + column)
                    let d = c + 1
                    indices.append(contentsOf: [a, c, b, b, c, d])
                }
            }

            let indexData = indices.withUnsafeBufferPointer {
                Data(bytes: $0.baseAddress!, count: $0.count * MemoryLayout<UInt32>.size)
            }
            let element = SCNGeometryElement(data: indexData, primitiveType: .triangles,
                                             primitiveCount: indices.count / 3,
                                             bytesPerIndex: MemoryLayout<UInt32>.size)
            let geometry = SCNGeometry(sources: [
                SCNGeometrySource(vertices: vertices),
                SCNGeometrySource(normals: normals),
                SCNGeometrySource(textureCoordinates: textureCoordinates)
            ], elements: [element])
            let material = SCNMaterial()
            material.lightingModel = .constant
            material.diffuse.contents = receiptTexture(task: task, language: language,
                                                       colorHex: colorHex)
            material.roughness.contents = 0.82
            material.metalness.contents = 0.0
            material.isDoubleSided = false
            material.cullMode = .back
            geometry.materials = [material]
            return geometry
        }

        private static func receiptTexture(task: HangingTask, language: AppLanguage,
                                           colorHex: UInt32?) -> NSImage {
            let size = NSSize(width: 720, height: 340)
            let image = NSImage(size: size)
            image.lockFocus()
            let bounds = NSRect(origin: .zero, size: size)
            let paperColor = TaskTypePaperStyle.nsPaper(colorHex)
            let inkColor = TaskTypePaperStyle.nsInk(colorHex)
            let mutedInkColor = TaskTypePaperStyle.nsMutedInk(colorHex)
            paperColor.setFill()
            bounds.fill()
            PaperTextureAsset.image?.draw(in: bounds, from: .zero,
                                          operation: colorHex == nil ? .sourceOver : .multiply,
                                          fraction: colorHex == nil ? 0.55 : 0.28)

            inkColor.withAlphaComponent(0.82).setStroke()
            let border = NSBezierPath(rect: bounds.insetBy(dx: 18, dy: 18))
            border.lineWidth = 3
            border.stroke()

            let rule = NSBezierPath()
            rule.move(to: NSPoint(x: 18, y: 126))
            rule.line(to: NSPoint(x: 702, y: 126))
            rule.lineWidth = 3
            rule.stroke()

            let topRule = NSBezierPath()
            topRule.move(to: NSPoint(x: 18, y: 278))
            topRule.line(to: NSPoint(x: 702, y: 278))
            topRule.lineWidth = 2
            topRule.stroke()

            let headerDivider = NSBezierPath()
            headerDivider.move(to: NSPoint(x: 548, y: 278))
            headerDivider.line(to: NSPoint(x: 548, y: 322))
            headerDivider.lineWidth = 2
            headerDivider.stroke()

            let paragraph = NSMutableParagraphStyle()
            paragraph.lineBreakMode = .byTruncatingTail
            let title = String(task.title.prefix(55)).uppercased() as NSString
            title.draw(in: NSRect(x: 72, y: 151, width: 600, height: 94), withAttributes: [
                .font: LeLaboType.nsDisplay(38),
                .kern: 1.5,
                .foregroundColor: inkColor,
                .paragraphStyle: paragraph
            ])
            ("✓" as NSString).draw(at: NSPoint(x: 40, y: 170), withAttributes: [
                .font: LeLaboType.nsLabel(29, bold: true),
                .foregroundColor: inkColor
            ])

            let formatter = DateFormatter()
            formatter.locale = language.locale
            formatter.dateFormat = "yyyy.MM.dd  HH:mm"
            let published = "\(language.text("发布", "PUBLISHED"))  \(formatter.string(from: task.createdAt))" as NSString
            let completed = "\(language.text("完成", "COMPLETED"))  \(formatter.string(from: task.completedAt ?? Date()))" as NSString
            let metadata: [NSAttributedString.Key: Any] = [
                .font: LeLaboType.nsLabel(22),
                .foregroundColor: mutedInkColor
            ]
            published.draw(at: NSPoint(x: 44, y: 78), withAttributes: metadata)
            completed.draw(at: NSPoint(x: 44, y: 39), withAttributes: metadata)
            ("HANG LATER" as NSString).draw(at: NSPoint(x: 512, y: 294), withAttributes: [
                .font: LeLaboType.nsLabel(17, bold: true),
                .foregroundColor: inkColor.withAlphaComponent(0.82)
            ])
            image.unlockFocus()
            return image
        }
    }
}
