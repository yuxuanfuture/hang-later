import Foundation

/// Validation shared by the UI and dependency-free regression checks.
/// A native host that merely exists may still deliver pages to another app data directory.
public enum BrowserBridgeManifest {
    public static func executableURL(in data: Data) -> URL? {
        guard let manifest = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let path = manifest["path"] as? String else { return nil }
        return URL(fileURLWithPath: path)
    }

    public static func matches(_ data: Data, expectedExecutable: URL, extensionID: String) -> Bool {
        guard let manifest = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              manifest["name"] as? String == "app.hanglater.capture",
              manifest["type"] as? String == "stdio",
              let path = manifest["path"] as? String,
              let origins = manifest["allowed_origins"] as? [String],
              origins.contains("chrome-extension://\(extensionID)/") else { return false }
        let actual = URL(fileURLWithPath: path).standardizedFileURL.resolvingSymlinksInPath()
        let expected = expectedExecutable.standardizedFileURL.resolvingSymlinksInPath()
        return actual == expected && FileManager.default.isExecutableFile(atPath: expected.path)
    }
}
