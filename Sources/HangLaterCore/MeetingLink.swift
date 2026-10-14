import Foundation

public enum MeetingPlatform: String, Codable, Equatable, Sendable {
    case zoom
    case tencentMeeting
    case feishu
    case lark
}

public enum MeetingLink {
    public static func platform(for rawValue: String?) -> MeetingPlatform? {
        guard let rawValue, let url = TaskInput.webURL(rawValue) else { return nil }
        return platform(for: url)
    }

    public static func platform(for url: URL) -> MeetingPlatform? {
        guard let host = url.host?.lowercased(), url.path != "/", !url.path.isEmpty else { return nil }
        if matches(host, domain: "zoom.us")
            || matches(host, domain: "zoom.com")
            || matches(host, domain: "zoomgov.com") {
            let meetingPaths = ["/j/", "/s/", "/w/", "/my/", "/wc/join/"]
            return meetingPaths.contains(where: { url.path.lowercased().contains($0) }) ? .zoom : nil
        }
        if matches(host, domain: "meeting.tencent.com")
            || matches(host, domain: "voovmeeting.com") {
            return .tencentMeeting
        }
        if matches(host, domain: "vc.feishu.cn")
            || matches(host, domain: "meetings.feishu.cn") {
            return .feishu
        }
        if matches(host, domain: "vc.larksuite.com")
            || matches(host, domain: "meetings.larksuite.com") {
            return .lark
        }
        return nil
    }

    private static func matches(_ host: String, domain: String) -> Bool {
        host == domain || host.hasSuffix("." + domain)
    }
}
