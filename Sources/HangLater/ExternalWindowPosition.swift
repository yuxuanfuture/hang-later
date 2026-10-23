import AppKit
import ApplicationServices
import HangLaterCore

struct ExternalWindowSnapshot {
    let processIdentifier: pid_t
    let windowID: CGWindowID
    let originalFrame: CGRect
}

enum ExternalWindowPosition {
    enum RestoreResult {
        case restored
        case permissionRequired
        case unavailable
    }

    static func snapshot(at point: CGPoint, processIdentifier: pid_t) -> ExternalWindowSnapshot? {
        guard let windowInfo = CGWindowListCopyWindowInfo(
            [.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID
        ) as? [[String: Any]] else { return nil }
        let quartzPoint = CGPoint(x: point.x, y: CGDisplayBounds(CGMainDisplayID()).height - point.y)
        for info in windowInfo {
            guard (info[kCGWindowOwnerPID as String] as? NSNumber)?.int32Value == processIdentifier,
                  (info[kCGWindowLayer as String] as? NSNumber)?.intValue == 0,
                  let number = info[kCGWindowNumber as String] as? NSNumber,
                  let boundsDictionary = info[kCGWindowBounds as String] as? NSDictionary,
                  let bounds = CGRect(dictionaryRepresentation: boundsDictionary),
                  bounds.width >= 180, bounds.height >= 120 else { continue }
            let titleBar = CGRect(x: bounds.minX - 18, y: bounds.minY - 18,
                                  width: bounds.width + 36, height: min(82, bounds.height))
            if titleBar.contains(quartzPoint) {
                return ExternalWindowSnapshot(processIdentifier: processIdentifier,
                                              windowID: CGWindowID(number.uint32Value),
                                              originalFrame: bounds)
            }
        }
        return nil
    }

    static func restore(_ snapshot: ExternalWindowSnapshot,
                        promptForPermission: Bool = true) -> RestoreResult {
        guard AXIsProcessTrusted() else {
            if promptForPermission {
                let key = kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String
                _ = AXIsProcessTrustedWithOptions([key: true] as CFDictionary)
            }
            return .permissionRequired
        }
        guard let currentFrame = frame(for: snapshot.windowID),
              let windows = accessibilityWindows(processIdentifier: snapshot.processIdentifier) else {
            return .unavailable
        }
        let candidates = windows.compactMap { window in
            accessibilityFrame(window).map { (window, $0) }
        }
        guard let match = WindowDropIntent.restorationCandidateIndex(
                currentFrame: matchingFrame(currentFrame),
                candidates: candidates.map { matchingFrame($0.1) }
              ) else { return .unavailable }
        var originalPosition = snapshot.originalFrame.origin
        guard let value = AXValueCreate(.cgPoint, &originalPosition),
              AXUIElementSetAttributeValue(candidates[match].0,
                                           kAXPositionAttribute as CFString, value) == .success else {
            return .unavailable
        }
        return .restored
    }

    private static func frame(for windowID: CGWindowID) -> CGRect? {
        guard let info = (CGWindowListCopyWindowInfo([.optionIncludingWindow], windowID)
                          as? [[String: Any]])?.first,
              let boundsDictionary = info[kCGWindowBounds as String] as? NSDictionary else { return nil }
        return CGRect(dictionaryRepresentation: boundsDictionary)
    }

    private static func accessibilityWindows(processIdentifier: pid_t) -> [AXUIElement]? {
        let application = AXUIElementCreateApplication(processIdentifier)
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(application, kAXWindowsAttribute as CFString, &value) == .success else {
            return nil
        }
        return value as? [AXUIElement]
    }

    private static func accessibilityFrame(_ window: AXUIElement) -> CGRect? {
        var positionReference: CFTypeRef?
        var sizeReference: CFTypeRef?
        guard AXUIElementCopyAttributeValue(window, kAXPositionAttribute as CFString,
                                            &positionReference) == .success,
              AXUIElementCopyAttributeValue(window, kAXSizeAttribute as CFString,
                                            &sizeReference) == .success,
              let positionReference, let sizeReference,
              CFGetTypeID(positionReference) == AXValueGetTypeID(),
              CFGetTypeID(sizeReference) == AXValueGetTypeID() else { return nil }
        var position = CGPoint.zero
        var size = CGSize.zero
        guard AXValueGetValue(positionReference as! AXValue, .cgPoint, &position),
              AXValueGetValue(sizeReference as! AXValue, .cgSize, &size) else { return nil }
        return CGRect(origin: position, size: size)
    }

    private static func matchingFrame(_ frame: CGRect) -> WindowDropIntent.Frame {
        WindowDropIntent.Frame(x: Double(frame.origin.x), y: Double(frame.origin.y),
                               width: Double(frame.size.width), height: Double(frame.size.height))
    }
}
