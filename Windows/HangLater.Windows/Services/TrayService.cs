using System.Drawing;
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
            Icon = File.Exists(iconPath) ? new Icon(iconPath) : Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ToggleRequested?.Invoke(); };
    }
    public void Localize(bool chinese) => icon.ContextMenuStrip = BuildMenu(chinese);
    private ContextMenuStrip BuildMenu(bool chinese = false)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(chinese ? "打开 Hang Later" : "Open Hang Later", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(chinese ? "显示 / 收起晾衣绳" : "Show / hide clothesline", null, (_, _) => ToggleRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(chinese ? "挂一个想法…" : "Hang an Idea…", null, (_, _) => IdeaRequested?.Invoke());
        menu.Items.Add(chinese ? "挂一个提醒…" : "Hang a Reminder…", null, (_, _) => ReminderRequested?.Invoke());
        menu.Items.Add(chinese ? "晾一件事…" : "Hang Something…", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(chinese ? "设置…" : "Settings…", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(chinese ? "退出 Hang Later" : "Quit Hang Later", null, (_, _) => ExitRequested?.Invoke());
        return menu;
    }
    public void ShowReminder(string title, string message)
    {
        icon.BalloonTipTitle = title; icon.BalloonTipText = message; icon.BalloonTipIcon = ToolTipIcon.Info; icon.ShowBalloonTip(10000);
    }
    public void Dispose() { icon.Visible = false; icon.Dispose(); }
}
