import Foundation

public enum DesktopLineLayout {
    public static func clampedPosition(_ position: Double) -> Double {
        min(max(position, 0), 1)
    }

    /// Returns the panel's lower screen coordinate. Position 0 keeps it at the top;
    /// position 1 keeps the complete panel at the bottom of the visible desktop.
    public static func verticalOrigin(visibleMinY: Double, visibleHeight: Double,
                                      panelHeight: Double, position: Double) -> Double {
        let travel = max(visibleHeight - panelHeight, 0)
        return visibleMinY + travel * (1 - clampedPosition(position))
    }
}
