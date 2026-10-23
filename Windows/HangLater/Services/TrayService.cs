using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HangLater.Windows.Services;

public sealed class TrayService : IDisposable
{
	private readonly NotifyIcon icon;

	public NotifyIcon NativeIcon => icon;

	public event Action? ToggleRequested;

	public event Action? OpenRequested;

	public event Action? IdeaRequested;

	public event Action? ReminderRequested;

	public event Action? SettingsRequested;

	public event Action? ExitRequested;

	public TrayService(string iconPath)
	{
		icon = new NotifyIcon
		{
			Text = "Hang Later",
			Icon = (File.Exists(iconPath) ? new Icon(iconPath) : (Icon.ExtractAssociatedIcon(Environment.ProcessPath) ?? SystemIcons.Application)),
			Visible = true,
			ContextMenuStrip = BuildMenu()
		};
		icon.MouseClick += delegate(object? _, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				this.ToggleRequested?.Invoke();
			}
		};
	}

	public void Localize(bool chinese)
	{
		icon.ContextMenuStrip = BuildMenu(chinese);
	}

	private ContextMenuStrip BuildMenu(bool chinese = false)
	{
		return new ContextMenuStrip
		{
			Items =
			{
				{
					chinese ? "打开 Hang Later" : "Open Hang Later",
					(Image?)null,
					(EventHandler?)delegate
					{
						this.OpenRequested?.Invoke();
					}
				},
				{
					chinese ? "显示 / 收起晾衣绳" : "Show / hide clothesline",
					(Image?)null,
					(EventHandler?)delegate
					{
						this.ToggleRequested?.Invoke();
					}
				},
				(ToolStripItem)new ToolStripSeparator(),
				{
					chinese ? "挂一个想法…" : "Hang an Idea…",
					(Image?)null,
					(EventHandler?)delegate
					{
						this.IdeaRequested?.Invoke();
					}
				},
				{
					chinese ? "挂一个提醒…" : "Hang a Reminder…",
					(Image?)null,
					(EventHandler?)delegate
					{
						this.ReminderRequested?.Invoke();
					}
				},
				{
					chinese ? "晾一件事…" : "Hang Something…",
					(Image?)null,
					(EventHandler?)delegate
					{
						this.OpenRequested?.Invoke();
					}
				},
				{
					chinese ? "设置…" : "Settings…",
					(Image?)null,
					(EventHandler?)delegate
					{
						this.SettingsRequested?.Invoke();
					}
				},
				(ToolStripItem)new ToolStripSeparator(),
				{
					chinese ? "退出 Hang Later" : "Quit Hang Later",
					(Image?)null,
					(EventHandler?)delegate
					{
						this.ExitRequested?.Invoke();
					}
				}
			}
		};
	}

	public void ShowReminder(string title, string message)
	{
		icon.BalloonTipTitle = title;
		icon.BalloonTipText = message;
		icon.BalloonTipIcon = ToolTipIcon.Info;
		icon.ShowBalloonTip(10000);
	}

	public void Dispose()
	{
		icon.Visible = false;
		icon.Dispose();
	}
}
