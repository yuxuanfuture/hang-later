using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Views;

public partial class TaskEditorWindow : Window
{
	private readonly App app;

	private readonly HangingTask task;

















	public TaskEditorWindow(App app, HangingTask task)
	{
		this.app = app;
		this.task = task;
		InitializeComponent();
		Localizer text = app.Text;
		Heading.Text = text.T("编辑", "EDIT");
		TitleLabel.Text = text.T("标题", "Title");
		LinkLabel.Text = text.T("链接", "Link");
		NoteLabel.Text = text.T("备注", "Note");
		TypeLabel.Text = text.T("任务类型", "Task type");
		ReminderCheck.Content = text.T("保留提醒时间", "Keep reminder time");
		CancelButton.Content = text.T("取消", "Cancel");
		SaveButton.Content = text.T("保存", "Save");
		TitleText.Text = task.title;
		LinkText.Text = task.url ?? (ObsidianResumeService.IsOpenUri(task.resumeURL) ? task.resumeURL : "");
		if (TyporaResumeService.IsTypora(task.capturedProcessName ?? task.appName))
		{
			LinkLabel.Text = text.T("Markdown 文档路径", "Markdown document path");
			LinkText.Text = task.capturedDocumentPath ?? task.filePath ?? TyporaResumeService.TryResolve(task.capturedProcessName ?? task.appName,task.capturedWindowTitle ?? task.title) ?? "";
			LinkText.ToolTip = text.T("文档的完整本地路径；同名文档不会自动猜测。", "Full local path; documents with duplicate names are never guessed.");
		}
		NoteText.Text = task.note;
		ReminderCheck.IsChecked = task.dueAt.HasValue;
		DateTime dateTime = task.dueAt?.LocalDateTime ?? DateTime.Now.AddHours(1.0);
		DueDate.SelectedDate = dateTime.Date;
		DueTime.Text = dateTime.ToString("HH:mm");
		ReminderTimePanel.Visibility = ((!task.dueAt.HasValue) ? Visibility.Collapsed : Visibility.Visible);
		List<TaskTypeDefinition> list = new List<TaskTypeDefinition>
		{
			new TaskTypeDefinition
			{
				id = Guid.Empty,
				name = text.T("默认", "Default")
			}
		};
		list.AddRange(app.Repository.TaskTypes.Select((TaskTypeDefinition x) => x.Clone()));
		TypeBox.ItemsSource = list;
		TypeBox.SelectedItem = list.FirstOrDefault(delegate(TaskTypeDefinition x)
		{
			Guid id = x.id;
			Guid? taskTypeID = task.taskTypeID;
			return id == taskTypeID;
		}) ?? list[0];
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		string text = (string.IsNullOrWhiteSpace(LinkText.Text) ? null : LinkText.Text.Trim());
		bool typora = TyporaResumeService.IsTypora(task.capturedProcessName ?? task.appName);
		if (typora && text != null && TyporaResumeService.ExistingDocument(text)==null)
		{
			MessageBox.Show(app.Text.T("请输入已存在的 Markdown 文档完整本地路径。", "Use the full local path of an existing Markdown document."));
			return;
		}
		if (!typora && text != null && !TaskRepository.IsWebUrl(text) && !ObsidianResumeService.IsOpenUri(text))
		{
			MessageBox.Show(app.Text.T("请输入完整网页链接或 obsidian://open 文档链接。", "Use a complete web link or an obsidian://open note link."));
			return;
		}
		HangingTask hangingTask = task.Clone();
		hangingTask.title = TitleText.Text.Trim();
		if (typora)
		{
			hangingTask.capturedDocumentPath = text == null ? null : TyporaResumeService.ExistingDocument(text);
			hangingTask.resumeURL = null;
			text = null;
		}
		hangingTask.url = ObsidianResumeService.IsOpenUri(text) ? null : text;
		if (text != null)
		{
			hangingTask.resumeURL = text;
		}
		else if (ObsidianResumeService.IsOpenUri(task.resumeURL))
		{
			hangingTask.resumeURL = null;
		}
		hangingTask.note = NoteText.Text;
		TaskTypeDefinition taskTypeDefinition = (TaskTypeDefinition)TypeBox.SelectedItem;
		hangingTask.taskTypeID = ((taskTypeDefinition?.id == Guid.Empty) ? ((Guid?)null) : taskTypeDefinition?.id);
		if (ReminderCheck.IsChecked == true)
		{
			if (!TimeSpan.TryParseExact(DueTime.Text.Trim(), new string[2] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out var result))
			{
				MessageBox.Show(app.Text.T("提醒时间需要使用 HH:mm。", "Use HH:mm for the reminder time."));
				return;
			}
			hangingTask.dueAt = new DateTimeOffset((DueDate.SelectedDate ?? DateTime.Today).Date + result);
			hangingTask.kind = ((!MeetingLink(text)) ? HangingTaskKind.reminder : HangingTaskKind.meeting);
			goto IL_0228;
		}
		hangingTask.dueAt = null;
		HangingTaskKind? kind = hangingTask.kind;
		bool flag;
		if (kind.HasValue)
		{
			HangingTaskKind valueOrDefault = kind.GetValueOrDefault();
			if ((uint)(valueOrDefault - 1) <= 1u)
			{
				flag = true;
				goto IL_0213;
			}
		}
		flag = false;
		goto IL_0213;
		IL_0228:
		try
		{
			app.Repository.Update(hangingTask);
			base.DialogResult = true;
			return;
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message);
			return;
		}
		IL_0213:
		if (flag)
		{
			hangingTask.kind = null;
		}
		goto IL_0228;
	}

	private void ReminderCheck_Changed(object sender, RoutedEventArgs e)
	{
		if (ReminderTimePanel != null)
		{
			ReminderTimePanel.Visibility = ((ReminderCheck.IsChecked != true) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private static bool MeetingLink(string? value)
	{
		if (Uri.TryCreate(value, UriKind.Absolute, out Uri result))
		{
			if (!result.Host.Contains("zoom", StringComparison.OrdinalIgnoreCase) && !result.Host.Contains("meeting.tencent", StringComparison.OrdinalIgnoreCase) && !result.Host.Contains("feishu", StringComparison.OrdinalIgnoreCase))
			{
				return result.Host.Contains("larksuite", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}
}
