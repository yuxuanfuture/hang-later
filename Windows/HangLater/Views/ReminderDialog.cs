using System;
using System.CodeDom.Compiler;
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

public partial class ReminderDialog : Window
{
	private readonly App app;

	private readonly HangingTask? editing;














	public ReminderDialog(App app, HangingTask? task = null)
	{
		this.app = app;
		editing = task;
		InitializeComponent();
		Localizer text = app.Text;
		Heading.Text = text.T("挂一个提醒", "HANG A REMINDER");
		TitleLabel.Text = text.T("需要记住什么？", "What do you need to remember?");
		LinkLabel.Text = text.T("链接（可选）", "Link (optional)");
		LinkHint.Text = text.T("支持在线会议链接，点击即达；无链接时适合生活提醒。", "Online meeting links open in one click; leave it empty for everyday reminders.");
		Footnote.Text = text.T("Windows 会在时间到时提醒你", "Windows will remind you when it’s time");
		CancelButton.Content = text.T("取消", "Cancel");
		HangButton.Content = text.T("保存提醒", "Save reminder");
		RemoveButton.Content = text.T("移除提醒", "Remove reminder");
		DateTime dateTime = task?.dueAt?.LocalDateTime ?? DateTime.Now.AddHours(1.0);
		DueDate.SelectedDate = dateTime.Date;
		DueTime.Text = dateTime.ToString("HH:mm");
		if (task != null)
		{
			TitleText.Text = task.title;
			LinkText.Text = task.url ?? "";
			RemoveButton.Visibility = Visibility.Visible;
		}
		TitleText.Focus();
	}

	private void Hang_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(TitleText.Text))
		{
			return;
		}
		if (!TimeSpan.TryParseExact(DueTime.Text.Trim(), new string[2] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out var result))
		{
			MessageBox.Show("Use HH:mm");
			return;
		}
		DateTimeOffset dateTimeOffset = new DateTimeOffset((DueDate.SelectedDate ?? DateTime.Today).Date + result);
		if (dateTimeOffset < DateTimeOffset.Now)
		{
			MessageBox.Show(app.Text.T("请选择未来的时间。", "Choose a future time."));
			return;
		}
		string text = (string.IsNullOrWhiteSpace(LinkText.Text) ? null : LinkText.Text.Trim());
		if (text != null && !TaskRepository.IsWebUrl(text))
		{
			MessageBox.Show(app.Text.T("请输入完整的 http 或 https 链接。", "Use a complete http or https link."));
			return;
		}
		HangingTaskKind value = ((!MeetingPlatform(text)) ? HangingTaskKind.reminder : HangingTaskKind.meeting);
		if (editing == null)
		{
			app.Repository.Add(TitleText.Text, text, "", dateTimeOffset, null, null, null, text, value);
		}
		else
		{
			HangingTask hangingTask = editing.Clone();
			hangingTask.title = TitleText.Text.Trim();
			hangingTask.url = text;
			hangingTask.resumeURL = text;
			hangingTask.dueAt = dateTimeOffset;
			hangingTask.kind = value;
			app.Repository.Update(hangingTask);
		}
		base.DialogResult = true;
	}

	private void Remove_Click(object sender, RoutedEventArgs e)
	{
		if (editing == null)
		{
			return;
		}
		HangingTask hangingTask = editing.Clone();
		hangingTask.dueAt = null;
		HangingTaskKind? kind = hangingTask.kind;
		bool flag;
		if (kind.HasValue)
		{
			HangingTaskKind valueOrDefault = kind.GetValueOrDefault();
			if ((uint)(valueOrDefault - 1) <= 1u)
			{
				flag = true;
				goto IL_004a;
			}
		}
		flag = false;
		goto IL_004a;
		IL_004a:
		if (flag)
		{
			hangingTask.kind = null;
		}
		app.Repository.Update(hangingTask);
		base.DialogResult = true;
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}

	private static bool MeetingPlatform(string? raw)
	{
		if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri result))
		{
			return false;
		}
		string text = result.Host.ToLowerInvariant();
		string text2 = result.AbsolutePath.ToLowerInvariant();
		if (((!text.EndsWith("zoom.us") && !text.EndsWith("zoom.com") && !text.EndsWith("zoomgov.com")) || !new string[5] { "/j/", "/s/", "/w/", "/my/", "/wc/join/" }.Any(text2.Contains)) && !text.EndsWith("meeting.tencent.com") && !text.EndsWith("voovmeeting.com") && !text.EndsWith("vc.feishu.cn") && !text.EndsWith("meetings.feishu.cn") && !text.EndsWith("vc.larksuite.com"))
		{
			return text.EndsWith("meetings.larksuite.com");
		}
		return true;
	}
}
