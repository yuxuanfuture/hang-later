using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using HangLater.Windows.Models;

namespace HangLater.Windows.Services;

public sealed class ReminderService
{
	private readonly DispatcherTimer timer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(15.0)
	};

	private readonly HashSet<Guid> delivered = new HashSet<Guid>();

	private readonly Func<IEnumerable<HangingTask>> tasks;

	private readonly TrayService tray;

	private HangingTask? latest;

	public event Action<HangingTask>? ReminderClicked;

	public ReminderService(Func<IEnumerable<HangingTask>> tasks, TrayService tray)
	{
		this.tasks = tasks;
		this.tray = tray;
		timer.Tick += delegate
		{
			Check();
		};
		tray.NativeIcon.BalloonTipClicked += delegate
		{
			if (latest != null)
			{
				this.ReminderClicked?.Invoke(latest);
			}
		};
		timer.Start();
	}

	private void Check()
	{
		DateTimeOffset now = DateTimeOffset.Now;
		HangingTask[] array = (from x in tasks().Where(delegate(HangingTask x)
			{
				if (!x.IsCompleted && x.dueAt.HasValue)
				{
					DateTimeOffset? dueAt = x.dueAt;
					DateTimeOffset dateTimeOffset = now;
					if (dueAt.HasValue && dueAt.GetValueOrDefault() <= dateTimeOffset && x.dueAt > now.AddMinutes(-5.0))
					{
						return !delivered.Contains(x.id);
					}
				}
				return false;
			})
			orderby x.dueAt
			select x).ToArray();
		foreach (HangingTask hangingTask in array)
		{
			delivered.Add(hangingTask.id);
			latest = hangingTask;
			tray.ShowReminder("Hang Later", hangingTask.title + ((hangingTask.url == null) ? "" : "\nClick to open"));
		}
	}
}
