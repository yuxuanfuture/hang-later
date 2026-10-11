import Foundation
import UniformTypeIdentifiers

public enum DropImportKind: Equatable, Sendable {
    case fileURL
    case application(typeIdentifier: String)
    case image
    case url
    case plainText
}

public enum DropSupport {
    public static let acceptedContentTypes: [UTType] = [
        .fileURL, .applicationBundle, .application, .image, .url, .plainText
    ]

    public static func preferredKind(for provider: NSItemProvider) -> DropImportKind? {
        if provider.hasItemConformingToTypeIdentifier(UTType.fileURL.identifier) { return .fileURL }
        if provider.hasItemConformingToTypeIdentifier(UTType.applicationBundle.identifier) {
            return .application(typeIdentifier: UTType.applicationBundle.identifier)
        }
        if provider.hasItemConformingToTypeIdentifier(UTType.application.identifier) {
            return .application(typeIdentifier: UTType.application.identifier)
        }
        if provider.hasItemConformingToTypeIdentifier(UTType.image.identifier) { return .image }
        if provider.hasItemConformingToTypeIdentifier(UTType.url.identifier) { return .url }
        if provider.hasItemConformingToTypeIdentifier(UTType.plainText.identifier) { return .plainText }
        return nil
    }
}
