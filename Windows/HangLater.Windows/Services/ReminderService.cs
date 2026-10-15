using HangLater.Windows.Models;
using Timer = System.Windows.Threading.DispatcherTimer;

namespace HangLater.Windows.Services;

public sealed class ReminderService
{
    private readonly Timer timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly HashSet<Guid> delivered = [];
    private readonly Func<IEnumerable<HangingTask>> tasks;
    private readonly TrayService tray;
    public event Action<HangingTask>? ReminderClicked;
    private HangingTask? latest;
    public ReminderService(Func<IEnumerable<HangingTask>> tasks, TrayService tray)
    {
        this.tasks = tasks; this.tray = tray;
        timer.Tick += (_, _) => Check();
        tray.NativeIcon.BalloonTipClicked += (_, _) => { if (latest is not null) ReminderClicked?.Invoke(latest); };
        timer.Start();
    }
    private void Check()
    {
        var now = DateTimeOffset.Now;
        var due = tasks().Where(x => !x.IsCompleted && x.dueAt is not null && x.dueAt <= now
            && x.dueAt > now.AddMinutes(-5) && !delivered.Contains(x.id)).OrderBy(x => x.dueAt).ToArray();
        foreach (var task in due)
        {
            delivered.Add(task.id); latest = task;
            tray.ShowReminder("Hang Later", task.title + (task.url is null ? "" : "\nClick to open"));
        }
    }
}
