using System.Windows;
using HangLater.Windows.Services;
using Microsoft.Win32;

namespace HangLater.Windows.Views;

public partial class AddSourceWindow : Window
{
    private readonly App app;
    private sealed record WindowChoice(CapturableWindow Window) { public string Display => $"{Window.ProcessName}  ·  {Window.Title}"; }
    public AddSourceWindow(App app)
    {
        this.app = app; InitializeComponent(); var t = app.Text; Heading.Text = t.T("晾一件事", "HANG SOMETHING"); IdeaButton.Content = t.T("想法", "Idea"); ReminderButton.Content = t.T("提醒", "Reminder"); FileButton.Content = t.T("文件", "File"); RefreshWindowsButton.Content = t.T("刷新窗口", "Refresh windows"); TaskTitle.Text = ""; TaskTitle.ToolTip = t.T("待办标题", "Task title"); TaskNote.ToolTip = t.T("备注（可选）", "Note (optional)"); CancelButton.Content = t.T("取消", "Cancel"); AddTaskButton.Content = t.T("添加待办", "Add task"); CaptureButton.Content = t.T("挂选中的窗口", "Hang selected window"); RefreshWindows();
    }
    private void RefreshWindows() { WindowsList.ItemsSource = WindowCaptureService.ListWindows().Select(x => new WindowChoice(x)).ToArray(); }
    private void Idea_Click(object sender, RoutedEventArgs e) { var dialog = new IdeaDialog(app) { Owner = this }; if (dialog.ShowDialog() == true) DialogResult = true; }
    private void Reminder_Click(object sender, RoutedEventArgs e) { var dialog = new ReminderDialog(app) { Owner = this }; if (dialog.ShowDialog() == true) DialogResult = true; }
    private void File_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = app.Text.T("选择要挂起来的文件", "Choose files to hang") };
        if (picker.ShowDialog(this) != true) return; foreach (var file in picker.FileNames) app.Repository.Add(Path.GetFileName(file), filePath: file); DialogResult = true;
    }
    private void AddTask_Click(object sender, RoutedEventArgs e) { if (string.IsNullOrWhiteSpace(TaskTitle.Text)) return; app.Repository.Add(TaskTitle.Text, note: TaskNote.Text); DialogResult = true; }
    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (WindowsList.SelectedItem is not WindowChoice choice) return;
        try { Hide(); Thread.Sleep(180); var preview = WindowCaptureService.Capture(choice.Window, app.Repository.PreviewDirectory); app.Repository.Add(choice.Window.Title, previewFile: preview, appName: choice.Window.ProcessName, resumeUrl: null); var task = app.Repository.Tasks.First(); task.appBundleIdentifier = choice.Window.ExecutablePath; app.Repository.Update(task); DialogResult = true; }
        catch (Exception ex) { Show(); MessageBox.Show(ex.Message); }
    }
    private void RefreshWindows_Click(object sender, RoutedEventArgs e) => RefreshWindows();
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
