import AppKit
import QuickLookThumbnailing

@MainActor
enum DocumentThumbnail {
    /// Uses the same Quick Look thumbnail service that Finder and the macOS desktop use.
    static func image(for url: URL,
                      size: CGSize = CGSize(width: 960, height: 960),
                      scale: CGFloat = 2) async -> NSImage? {
        let request = QLThumbnailGenerator.Request(
            fileAt: url,
            size: size,
            scale: scale,
            representationTypes: [.thumbnail, .lowQualityThumbnail]
        )
        request.iconMode = false
        return await withCheckedContinuation { continuation in
            QLThumbnailGenerator.shared.generateBestRepresentation(for: request) { representation, _ in
                continuation.resume(returning: representation?.nsImage)
            }
        }
    }
}
