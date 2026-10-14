import Foundation

/// Filters the global mouse signal used for dragging app windows onto the desktop clothesline.
/// A deliberate window drop has movement, time, and a short dwell inside the visible target.
public enum WindowDropIntent {
    public static let minimumDistance = 44.0
    public static let minimumDuration: TimeInterval = 0.28
    public static let minimumTargetDwell: TimeInterval = 0.22

    public static func qualifies(maximumDistance: Double, duration: TimeInterval,
                                 targetDwell: TimeInterval?, isInsideTarget: Bool) -> Bool {
        isInsideTarget
            && maximumDistance >= minimumDistance
            && duration >= minimumDuration
            && (targetDwell ?? 0) >= minimumTargetDwell
    }
}
