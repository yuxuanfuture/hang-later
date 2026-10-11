import AppKit
import Foundation
import HangLaterCore

enum AppResumeTarget {
    static func resolve(bundleIdentifier: String, appName: String?, windowTitle: String) -> URL? {
        let bundle = bundleIdentifier.lowercased()
        let name = appName?.lowercased() ?? ""
        if bundle.hasPrefix("com.google.chrome") || name.contains("chrome") {
            return chromeHistoryURL(matching: windowTitle) ?? chromeOpenTab(matching: windowTitle)
        }
        if bundle.contains("obsidian") || name.contains("obsidian") {
            return obsidianURL(from: windowTitle)
        }
        return nil
    }

    private static func chromeOpenTab(matching title: String) -> URL? {
        let escaped = title
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\"", with: "\\\"")
        let source = """
        tell application id "com.google.Chrome"
            repeat with browserWindow in windows
                repeat with browserTab in tabs of browserWindow
                    if (title of browserTab as text) is "\(escaped)" then
                        return URL of browserTab as text
                    end if
                end repeat
            end repeat
        end tell
        return ""
        """
        var failure: NSDictionary?
        guard let value = NSAppleScript(source: source)?.executeAndReturnError(&failure).stringValue,
              let url = TaskInput.webURL(value) else { return nil }
        return url
    }

    /// Old capture cards did not persist a URL. On their first click, recover only the exact
    /// saved page title from Chrome's local history, then AppStore writes the URL back to the card.
    private static func chromeHistoryURL(matching title: String) -> URL? {
        let root = FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Application Support/Google/Chrome", isDirectory: true)
        guard let profiles = try? FileManager.default.contentsOfDirectory(
            at: root, includingPropertiesForKeys: [.contentModificationDateKey, .isDirectoryKey],
            options: [.skipsHiddenFiles]
        ) else { return nil }
        let histories = profiles
            .filter { $0.lastPathComponent == "Default" || $0.lastPathComponent.hasPrefix("Profile ") }
            .map { $0.appendingPathComponent("History") }
            .filter { FileManager.default.fileExists(atPath: $0.path) }
            .sorted {
                let left = (try? $0.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast
                let right = (try? $1.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast
                return left > right
            }
        let candidates = [title, title.replacingOccurrences(of: " - Google Chrome", with: "")]
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        for history in histories {
            guard let snapshot = historySnapshot(history) else { continue }
            defer { try? FileManager.default.removeItem(at: snapshot.deletingLastPathComponent()) }
            for candidate in candidates {
                let quotedTitle = candidate.replacingOccurrences(of: "'", with: "''")
                let query = "SELECT url FROM urls WHERE title = '\(quotedTitle)' ORDER BY last_visit_time DESC LIMIT 1;"
                guard let output = runSQLite(database: snapshot, query: query),
                      let url = TaskInput.webURL(output) else { continue }
                return url
            }
        }
        return nil
    }

    private static func historySnapshot(_ history: URL) -> URL? {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("HangLater-Chrome-History-\(UUID().uuidString)", isDirectory: true)
        do {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            for suffix in ["", "-wal", "-shm"] {
                let source = URL(fileURLWithPath: history.path + suffix)
                guard FileManager.default.fileExists(atPath: source.path) else { continue }
                try FileManager.default.copyItem(at: source,
                                                 to: directory.appendingPathComponent("History" + suffix))
            }
            return directory.appendingPathComponent("History")
        } catch {
            try? FileManager.default.removeItem(at: directory)
            return nil
        }
    }

    private static func runSQLite(database: URL, query: String) -> String? {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/sqlite3")
        process.arguments = ["-readonly", database.path, query]
        let output = Pipe()
        process.standardOutput = output
        process.standardError = FileHandle.nullDevice
        do {
            try process.run()
            process.waitUntilExit()
        } catch { return nil }
        guard process.terminationStatus == 0 else { return nil }
        return String(data: output.fileHandleForReading.readDataToEndOfFile(), encoding: .utf8)?
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private static func obsidianURL(from windowTitle: String) -> URL? {
        var parts = windowTitle.components(separatedBy: " - ")
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        if let last = parts.last?.lowercased() {
            let versionTail = last.dropFirst("obsidian ".count)
            if last == "obsidian" || (last.hasPrefix("obsidian ") && versionTail.first?.isNumber == true) {
                parts.removeLast()
            }
        }
        guard parts.count >= 2 else { return nil }
        let vault = parts.removeLast()
        let file = parts.joined(separator: " - ")
        guard !vault.isEmpty, !file.isEmpty else { return nil }
        var components = URLComponents()
        components.scheme = "obsidian"
        components.host = "open"
        components.queryItems = [URLQueryItem(name: "vault", value: vault),
                                 URLQueryItem(name: "file", value: file)]
        return components.url
    }
}
