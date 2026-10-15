using System.Globalization;
using System.Windows;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Views;

public partial class ReminderDialog : Window
{
    private readonly App app; private readonly HangingTask? editing;
    public ReminderDialog(App app, HangingTask? task = null)
    {
        this.app = app; editing = task; InitializeComponent(); var t = app.Text;
        Heading.Text = t.T("挂一个提醒", "HANG A REMINDER"); TitleLabel.Text = t.T("需要记住什么？", "What do you need to remember?"); LinkLabel.Text = t.T("链接（可选）", "Link (optional)");
        LinkHint.Text = t.T("支持在线会议链接，点击即达；无链接时适合生活提醒。", "Online meeting links open in one click; leave it empty for everyday reminders.");
        Footnote.Text = t.T("Windows 会在时间到时提醒你", "Windows will remind you when it’s time"); CancelButton.Content = t.T("取消", "Cancel"); HangButton.Content = t.T("保存提醒", "Save reminder"); RemoveButton.Content = t.T("移除提醒", "Remove reminder");
        var due = task?.dueAt?.LocalDateTime ?? DateTime.Now.AddHours(1); DueDate.SelectedDate = due.Date; DueTime.Text = due.ToString("HH:mm");
        if (task is not null) { TitleText.Text = task.title; LinkText.Text = task.url ?? ""; RemoveButton.Visibility = Visibility.Visible; }
        TitleText.Focus();
    }
    private void Hang_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleText.Text)) return;
        if (!TimeSpan.TryParseExact(DueTime.Text.Trim(), ["h\\:mm", "hh\\:mm"], CultureInfo.InvariantCulture, out var time)) { MessageBox.Show("Use HH:mm"); return; }
        var date = DueDate.SelectedDate ?? DateTime.Today; var due = new DateTimeOffset(date.Date + time); if (due < DateTimeOffset.Now) { MessageBox.Show(app.Text.T("请选择未来的时间。", "Choose a future time.")); return; }
        var link = string.IsNullOrWhiteSpace(LinkText.Text) ? null : LinkText.Text.Trim(); if (link is not null && !TaskRepository.IsWebUrl(link)) { MessageBox.Show(app.Text.T("请输入完整的 http 或 https 链接。", "Use a complete http or https link.")); return; }
        var kind = MeetingPlatform(link) ? HangingTaskKind.meeting : HangingTaskKind.reminder;
        if (editing is null) app.Repository.Add(TitleText.Text, url: link, dueAt: due, resumeUrl: link, kind: kind);
        else { var copy = editing.Clone(); copy.title = TitleText.Text.Trim(); copy.url = link; copy.resumeURL = link; copy.dueAt = due; copy.kind = kind; app.Repository.Update(copy); }
        DialogResult = true;
    }
    private void Remove_Click(object sender, RoutedEventArgs e) { if (editing is null) return; var copy = editing.Clone(); copy.dueAt = null; if (copy.kind is HangingTaskKind.reminder or HangingTaskKind.meeting) copy.kind = null; app.Repository.Update(copy); DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private static bool MeetingPlatform(string? raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return false; var host = uri.Host.ToLowerInvariant(); var path = uri.AbsolutePath.ToLowerInvariant();
        return ((host.EndsWith("zoom.us") || host.EndsWith("zoom.com") || host.EndsWith("zoomgov.com")) && new[] { "/j/", "/s/", "/w/", "/my/", "/wc/join/" }.Any(path.Contains))
            || host.EndsWith("meeting.tencent.com") || host.EndsWith("voovmeeting.com") || host.EndsWith("vc.feishu.cn") || host.EndsWith("meetings.feishu.cn") || host.EndsWith("vc.larksuite.com") || host.EndsWith("meetings.larksuite.com");
    }
}
