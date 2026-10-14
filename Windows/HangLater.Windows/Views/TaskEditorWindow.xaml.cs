using System.Windows;
using System.Globalization;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Views;

public partial class TaskEditorWindow : Window
{
    private readonly App app; private readonly HangingTask task;
    public TaskEditorWindow(App app, HangingTask task)
    {
        this.app = app; this.task = task; InitializeComponent(); var t = app.Text;
        Heading.Text = t.T("编辑", "EDIT"); TitleLabel.Text = t.T("标题", "Title"); LinkLabel.Text = t.T("链接", "Link"); NoteLabel.Text = t.T("备注", "Note"); TypeLabel.Text = t.T("任务类型", "Task type"); ReminderCheck.Content = t.T("保留提醒时间", "Keep reminder time"); CancelButton.Content = t.T("取消", "Cancel"); SaveButton.Content = t.T("保存", "Save");
        TitleText.Text = task.title; LinkText.Text = task.url ?? ""; NoteText.Text = task.note; ReminderCheck.IsChecked = task.dueAt is not null;
        var due = task.dueAt?.LocalDateTime ?? DateTime.Now.AddHours(1); DueDate.SelectedDate = due.Date; DueTime.Text = due.ToString("HH:mm"); ReminderTimePanel.Visibility = task.dueAt is null ? Visibility.Collapsed : Visibility.Visible;
        var choices = new List<TaskTypeDefinition> { new() { id = Guid.Empty, name = t.T("默认", "Default") } }; choices.AddRange(app.Repository.TaskTypes.Select(x => x.Clone())); TypeBox.ItemsSource = choices; TypeBox.SelectedItem = choices.FirstOrDefault(x => x.id == task.taskTypeID) ?? choices[0];
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var link = string.IsNullOrWhiteSpace(LinkText.Text) ? null : LinkText.Text.Trim(); if (link is not null && !TaskRepository.IsWebUrl(link)) { MessageBox.Show(app.Text.T("请输入完整链接。", "Use a complete link.")); return; }
        var copy = task.Clone(); copy.title = TitleText.Text.Trim(); copy.url = link; if (link is not null) copy.resumeURL = link; copy.note = NoteText.Text; var selected = (TaskTypeDefinition?)TypeBox.SelectedItem; copy.taskTypeID = selected?.id == Guid.Empty ? null : selected?.id;
        if (ReminderCheck.IsChecked == true)
        {
            if (!TimeSpan.TryParseExact(DueTime.Text.Trim(), ["h\\:mm", "hh\\:mm"], CultureInfo.InvariantCulture, out var time)) { MessageBox.Show(app.Text.T("提醒时间需要使用 HH:mm。", "Use HH:mm for the reminder time.")); return; }
            copy.dueAt = new DateTimeOffset((DueDate.SelectedDate ?? DateTime.Today).Date + time); copy.kind = MeetingLink(link) ? HangingTaskKind.meeting : HangingTaskKind.reminder;
        }
        else { copy.dueAt = null; if (copy.kind is HangingTaskKind.reminder or HangingTaskKind.meeting) copy.kind = null; }
        try { app.Repository.Update(copy); DialogResult = true; } catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    private void ReminderCheck_Changed(object sender, RoutedEventArgs e) { if (ReminderTimePanel is not null) ReminderTimePanel.Visibility = ReminderCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed; }
    private static bool MeetingLink(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Host.Contains("zoom", StringComparison.OrdinalIgnoreCase) || uri.Host.Contains("meeting.tencent", StringComparison.OrdinalIgnoreCase) || uri.Host.Contains("feishu", StringComparison.OrdinalIgnoreCase) || uri.Host.Contains("larksuite", StringComparison.OrdinalIgnoreCase));
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
