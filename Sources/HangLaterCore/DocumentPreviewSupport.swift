import Foundation

/// File types whose contents macOS can render through Quick Look for a clothesline preview.
public enum DocumentPreviewSupport {
    private static let quickLookExtensions: Set<String> = [
        "doc", "docm", "docx", "dot", "dotx",
        "ppt", "pptm", "pptx", "pps", "ppsx",
        "xls", "xlsm", "xlsx", "csv"
    ]

    public static func usesQuickLook(_ url: URL) -> Bool {
        quickLookExtensions.contains(url.pathExtension.lowercased())
    }

    public static func preservesPageAspectRatio(_ url: URL) -> Bool {
        url.pathExtension.lowercased() == "pdf" || usesQuickLook(url)
    }
}
