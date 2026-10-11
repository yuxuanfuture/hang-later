import Foundation
import HangLaterCore
import UniformTypeIdentifiers

// Dependency-free checks also run on Macs with only Command Line Tools,
// which do not ship XCTest. Each case uses a separate temporary directory.
struct CheckFailure: LocalizedError {
    let message: String
    var errorDescription: String? { message }
}
func expect(_ condition: @autoclosure () -> Bool, _ message: String) throws {
    if !condition() { throw CheckFailure(message: message) }
}
func expectThrows(_ action: () throws -> Void) throws {
    do { try action() }
    catch { return }
    throw CheckFailure(message: "Expected the operation to fail")
}
var passed = 0
var failed = 0
func check(_ name: String, _ body: (URL) throws -> Void) {
    let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    defer { try? FileManager.default.removeItem(at: directory) }
    do { try body(directory); passed += 1; print("PASS: \(name)") }
    catch { failed += 1; print("FAIL: \(name): \(error.localizedDescription)") }
}

check("An application-only drag provider is accepted") { _ in
    let provider = NSItemProvider(item: Data([0]) as NSData,
                                  typeIdentifier: UTType.applicationBundle.identifier)
    try expect(DropSupport.preferredKind(for: provider)
               == .application(typeIdentifier: UTType.applicationBundle.identifier),
               "App bundles that do not advertise a file URL must still be accepted")
    try expect(DropSupport.acceptedContentTypes.contains(.applicationBundle),
               "The SwiftUI drop destination must register the application-bundle type")
}

check("Web and plain tasks survive restart with notes, dates, previews and capture IDs") { directory in
    let repository = try TaskRepository(directory: directory)
    let due = Date(timeIntervalSince1970: 1800000000)
    let web = try repository.add(title: "看视频", url: "https://example.com/watch?v=123&t=750", note: "从 12:30 开始", dueAt: due, previewFile: "preview.png", captureID: "capture-123.json")
    let plain = try repository.add(title: "写笔记", note: "整理自己的理解")
    let reopened = try TaskRepository(directory: directory)
    try expect(reopened.tasks == [plain, web], "All task fields must survive a restart")
    try expect(reopened.tasks.last?.captureID == "capture-123.json", "Capture identity must be retained for deduplication")
}

check("Image files and app windows survive restart with their launch targets") { directory in
    let repository = try TaskRepository(directory: directory)
    let image = try repository.add(title: "灵感图", previewFile: "image.png",
                                   filePath: "/tmp/inspiration.png")
    let app = try repository.add(title: "继续写文稿", previewFile: "window.png",
                                 appBundleIdentifier: "md.obsidian", appName: "Obsidian",
                                 resumeURL: "obsidian://open?vault=Notes&file=Draft")
    let reopened = try TaskRepository(directory: directory)
    try expect(reopened.tasks == [app, image], "Image and app task sources must survive restart")
    try expect(reopened.tasks.first?.appBundleIdentifier == "md.obsidian", "App bundle ID must be retained")
    try expect(reopened.tasks.first?.resumeURL == "obsidian://open?vault=Notes&file=Draft",
               "App content restore URL must be retained")
    try expect(reopened.tasks.last?.isImage == true, "Image file tasks must remain identifiable")
}

check("Complete, restart and restore retain the original task and URL") { directory in
    let repository = try TaskRepository(directory: directory)
    let task = try repository.add(title: "学视频", url: "https://example.com/video", note: "备注")
    let date = Date(timeIntervalSince1970: 1800000000)
    try repository.complete(task.id, at: date)
    try expect(repository.pending.isEmpty, "Completed task must leave the line")
    try expect(repository.completed.first?.completedAt == date, "Archive must include the completion time")
    let reopened = try TaskRepository(directory: directory)
    try expect(reopened.completed.first?.url == task.url, "Archive must retain the original URL")
    try reopened.restore(task.id)
    try expect(reopened.completed.isEmpty && reopened.pending == [task], "Restore must retain task identity and contents")
}

check("A completed paper can leave and rejoin the spike without leaving the archive") { directory in
    let repository = try TaskRepository(directory: directory)
    let task = try repository.add(title: "可取下的纸片")
    try repository.complete(task.id, at: Date(timeIntervalSince1970: 200))
    try expect(repository.completed.first?.isOnSpike == true, "Newly completed tasks must start on the spike")
    try repository.setSpikeAttached(false, for: task.id)
    try expect(repository.completed.count == 1 && repository.completed.first?.isOnSpike == false,
               "Taking a paper off must preserve its completed archive entry")
    let reopened = try TaskRepository(directory: directory)
    try expect(reopened.completed.first?.isOnSpike == false, "Detached state must survive restart")
    try reopened.setSpikeAttached(true, for: task.id)
    try expect(reopened.completed.first?.isOnSpike == true, "A detached paper must be attachable again")
    try reopened.moveOnSpike(task.id, x: 0.62, y: 2.36, z: -0.21)
    let positioned = try TaskRepository(directory: directory).completed.first
    try expect(positioned?.spikeX == 0.62 && positioned?.spikeY == 2.36 && positioned?.spikeZ == -0.21,
               "A paper's manually selected spike position must survive restart")
    try reopened.moveOnSpike(task.id, x: 4, y: 9, z: -3)
    try expect(reopened.completed.first?.spikeX == 0.95 && reopened.completed.first?.spikeY == 3.05
               && reopened.completed.first?.spikeZ == -0.38,
               "Paper movement must stay on the shaft and within the pierced paper area")
    let legacy = HangingTask(title: "旧完成记录", completedAt: Date(timeIntervalSince1970: 100))
    try expect(legacy.isOnSpike, "Legacy completed tasks without an attachment flag must remain visible")
}

check("Archive sorts by completion time and deletion persists") { directory in
    let repository = try TaskRepository(directory: directory)
    let first = try repository.add(title: "第一件")
    let second = try repository.add(title: "第二件")
    try repository.complete(second.id, at: Date(timeIntervalSince1970: 100))
    try repository.complete(first.id, at: Date(timeIntervalSince1970: 200))
    try expect(repository.completed.map(\.id) == [first.id, second.id], "Most recently completed item must come first")
    try repository.delete(first.id)
    let reopened = try TaskRepository(directory: directory)
    try expect(reopened.tasks.map(\.id) == [second.id], "Deletion must survive restart")
}

check("Bulk clothesline completion and deletion persist as one operation") { directory in
    let repository = try TaskRepository(directory: directory)
    let first = try repository.add(title: "第一件")
    let second = try repository.add(title: "第二件")
    let untouched = try repository.add(title: "保留")
    let completionDate = Date(timeIntervalSince1970: 2_000)
    try repository.completeAll(Set([first.id, second.id]), at: completionDate)
    try expect(repository.completed.count == 2, "Every selected pending task must be completed")
    try expect(repository.completed.allSatisfy { $0.completedAt == completionDate && $0.isOnSpike },
               "Bulk-completed tasks must share the action time and join the Done Spike")
    try expect(repository.pending.map(\.id) == [untouched.id], "Unselected tasks must remain on the clothesline")
    try repository.deleteAll(Set([untouched.id]))
    let reopened = try TaskRepository(directory: directory)
    try expect(reopened.pending.isEmpty, "Bulk deletion must persist after restart")
    try expect(Set(reopened.completed.map(\.id)) == Set([first.id, second.id]),
               "Bulk deletion must not remove completed tasks outside the selection")
}

check("Drag order persists and cannot move a pending task into the archive") { directory in
    let repository = try TaskRepository(directory: directory)
    let first = try repository.add(title: "A")
    let second = try repository.add(title: "B")
    let third = try repository.add(title: "C")
    try repository.move(first.id, before: third.id)
    let reopened = try TaskRepository(directory: directory)
    try expect(reopened.pending.map(\.id) == [first.id, third.id, second.id], "Drag order must survive restart")
    try repository.complete(second.id)
    try repository.move(third.id, before: second.id)
    try expect(repository.pending.map(\.id) == [first.id, third.id], "Archive entries are not reorder targets")
}

check("Free clothesline positions persist, overlap, and bring the last moved card forward") { directory in
    let repository = try TaskRepository(directory: directory)
    let first = try repository.add(title: "A")
    let second = try repository.add(title: "B")
    try repository.place(first.id, lineX: 0.42)
    let firstLayer = repository.tasks.first(where: { $0.id == first.id })?.lineLayer ?? 0
    try repository.place(second.id, lineX: 0.42)
    let reopened = try TaskRepository(directory: directory)
    let placedFirst = reopened.tasks.first(where: { $0.id == first.id })
    let placedSecond = reopened.tasks.first(where: { $0.id == second.id })
    try expect(placedFirst?.lineX == 0.42 && placedSecond?.lineX == 0.42,
               "Cards must be allowed to occupy the same part of the line")
    try expect((placedSecond?.lineLayer ?? 0) > firstLayer,
               "The last moved overlapping card must remain in front")
    try reopened.complete(first.id)
    try reopened.place(first.id, lineX: 0.9)
    try expect(reopened.completed.first?.lineX == 0.42,
               "Archived cards must not be repositioned on the live clothesline")
}

check("Completed archive groups tasks by month, week and day in newest-first order") { _ in
    try expect(CompletionGrouping.allCases == [.day, .week, .month],
               "The archive selector must present Day, Week, Month in that order")
    var calendar = Calendar(identifier: .gregorian)
    calendar.timeZone = TimeZone(secondsFromGMT: 0)!
    calendar.firstWeekday = 2
    func date(_ year: Int, _ month: Int, _ day: Int, _ hour: Int) -> Date {
        calendar.date(from: DateComponents(year: year, month: month, day: day, hour: hour))!
    }
    let tasks = [
        HangingTask(title: "周一", completedAt: date(2026, 10, 5, 9)),
        HangingTask(title: "周二", completedAt: date(2026, 10, 6, 18)),
        HangingTask(title: "下周", completedAt: date(2026, 10, 12, 8)),
        HangingTask(title: "上个月", completedAt: date(2026, 9, 30, 22))
    ]
    let months = CompletionArchive.sections(from: tasks, grouping: .month, calendar: calendar)
    let weeks = CompletionArchive.sections(from: tasks, grouping: .week, calendar: calendar)
    let days = CompletionArchive.sections(from: tasks, grouping: .day, calendar: calendar)
    try expect(months.map(\.tasks.count) == [3, 1], "Month groups must be newest first")
    try expect(weeks.map(\.tasks.count) == [1, 2, 1], "Week groups must use calendar week boundaries")
    try expect(days.count == 4, "Day grouping must separate distinct completion dates")
    try expect(months.first?.tasks.first?.title == "下周", "Tasks inside a section must be newest first")
}

check("Custom workdays filter Published time and cross midnight") { _ in
    var calendar = Calendar(identifier: .gregorian)
    calendar.timeZone = TimeZone(secondsFromGMT: 8 * 3600)!
    func date(_ day: Int, _ hour: Int, _ minute: Int = 0) -> Date {
        calendar.date(from: DateComponents(year: 2026, month: 10, day: day,
                                           hour: hour, minute: minute))!
    }

    let overnight = WorkdaySchedule(startMinute: 8 * 60, endMinute: 2 * 60)
    let afterMidnight = overnight.interval(containing: date(13, 1), calendar: calendar)
    try expect(afterMidnight?.start == date(12, 8), "01:00 must belong to the workday that began the previous morning")
    try expect(afterMidnight?.end == date(13, 2), "An early end time must resolve to the following day")
    try expect(overnight.contains(date(12, 20), inWorkdayContaining: date(13, 1), calendar: calendar),
               "A task published during the active workday must be eligible for Done Spike")
    try expect(!overnight.contains(date(12, 7, 59), inWorkdayContaining: date(13, 1), calendar: calendar),
               "A task published before the active workday must be excluded")
    try expect(overnight.interval(containing: date(13, 3), calendar: calendar) == nil,
               "The rest period after a workday ends must not be counted")

    let fullDay = WorkdaySchedule(startMinute: 0, endMinute: 0)
    try expect(fullDay.contains(date(12, 23, 59), inWorkdayContaining: date(12, 12), calendar: calendar),
               "Equal start and end times must represent a 24-hour workday")
}

check("Invalid input cannot change persisted tasks") { directory in
    let repository = try TaskRepository(directory: directory)
    _ = try repository.add(title: "保留这件事")
    try expectThrows { _ = try repository.add(title: "  \n") }
    for url in ["javascript:alert(1)", "file:///etc/passwd", "data:text/html,test", "https://", "https://user:password@example.com/"] {
        try expectThrows { _ = try repository.add(title: "无效链接", url: url) }
    }
    try expectThrows { _ = try repository.add(title: "无效恢复链接", resumeURL: "file:///tmp/private") }
    let reopened = try TaskRepository(directory: directory)
    try expect(reopened.tasks.count == 1, "Invalid input must not add any tasks")
}

check("Corrupt task file is preserved") { directory in
    try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    let file = directory.appendingPathComponent("tasks.json")
    let corrupt = Data("{ broken json".utf8)
    try corrupt.write(to: file)
    try expectThrows { _ = try TaskRepository(directory: directory) }
    let retained = try Data(contentsOf: file)
    try expect(retained == corrupt, "A corrupt file must never be overwritten with an empty list")
}

check("Failed atomic write preserves in-memory state") { directory in
    let repository = try TaskRepository(directory: directory)
    let task = try repository.add(title: "不要丢掉")
    try FileManager.default.removeItem(at: repository.dataFile)
    try FileManager.default.createDirectory(at: repository.dataFile, withIntermediateDirectories: false)
    try expectThrows { try repository.complete(task.id) }
    try expect(repository.tasks == [task], "Failed disk write must not update in-memory tasks")
}

check("URL parsing preserves video timestamps and rejects embedded text") { _ in
    try expect(TaskInput.webURL("  https://example.com/watch?v=x&t=123#section\n")?.absoluteString == "https://example.com/watch?v=x&t=123#section", "URL parameters must be retained")
    try expect(TaskInput.webURL("www.example.com")?.scheme == "https", "www URLs should default to HTTPS")
    try expect(TaskInput.webURL("明天看 https://example.com") == nil, "Mixed text should remain a note")
    try expect(TaskInput.webURL("https://example.com\nhttps://other.com") == nil, "Multiple lines are not one URL")
    try expect(TaskInput.webURL("obsidian://open") == nil, "Only web URLs are supported in this version")
    try expect(TaskInput.resumeURL("obsidian://open?vault=Notes&file=Draft")?.scheme == "obsidian",
               "Obsidian document links must be accepted as restore targets")
    try expect(TaskInput.resumeURL("file:///tmp/private") == nil,
               "Restore targets must reject unrelated URL schemes")
}

check("Chrome bridge must point to this app data directory") { directory in
    let current = directory.appendingPathComponent("current/BrowserBridge/capture-host.py")
    let stale = directory.appendingPathComponent("demo/BrowserBridge/capture-host.py")
    try FileManager.default.createDirectory(at: stale.deletingLastPathComponent(), withIntermediateDirectories: true)
    try Data("#!/bin/sh\n".utf8).write(to: stale)
    try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: stale.path)
    let manifest = try JSONSerialization.data(withJSONObject: [
        "name": "app.hanglater.capture",
        "type": "stdio",
        "path": stale.path,
        "allowed_origins": ["chrome-extension://test-extension/"]
    ])
    try expect(!BrowserBridgeManifest.matches(manifest, expectedExecutable: current,
                                              extensionID: "test-extension"),
               "An executable from a stale demo directory must not count as connected")
    try FileManager.default.createDirectory(at: current.deletingLastPathComponent(), withIntermediateDirectories: true)
    try Data("#!/bin/sh\n".utf8).write(to: current)
    try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: current.path)
    let currentManifest = try JSONSerialization.data(withJSONObject: [
        "name": "app.hanglater.capture", "type": "stdio", "path": current.path,
        "allowed_origins": ["chrome-extension://test-extension/"]
    ])
    try expect(BrowserBridgeManifest.matches(currentManifest, expectedExecutable: current,
                                             extensionID: "test-extension"),
               "The exact executable and extension origin should count as connected")
}

print("\(passed) passed, \(failed) failed")
exit(failed == 0 ? 0 : 1)
