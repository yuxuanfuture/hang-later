import Foundation

/// Filters the global mouse signal used for dragging app windows onto the desktop clothesline.
/// A deliberate window drop has movement, time, and a short dwell inside the visible target.
public enum WindowDropIntent {
    public struct Frame: Equatable, Sendable {
        public let x: Double
        public let y: Double
        public let width: Double
        public let height: Double

        public init(x: Double, y: Double, width: Double, height: Double) {
            self.x = x
            self.y = y
            self.width = width
            self.height = height
        }
    }

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

    public static func restorationCandidateIndex(currentFrame: Frame,
                                                  candidates: [Frame]) -> Int? {
        let scores = candidates.enumerated().map { index, frame in
            let originDistance = hypot(frame.x - currentFrame.x, frame.y - currentFrame.y)
            let sizeDifference = abs(frame.width - currentFrame.width)
                + abs(frame.height - currentFrame.height)
            return (index, originDistance + sizeDifference)
        }
        guard let closest = scores.min(by: { $0.1 < $1.1 }), closest.1 <= 80 else { return nil }
        return closest.0
    }
}
