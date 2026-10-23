using System;
using System.Drawing;
using System.Windows.Forms;
using HangLater.Windows.Models;

namespace HangLater.Windows.Services;

public sealed class EdgeHoverService : IDisposable
{
	private readonly Timer timer = new Timer
	{
		Interval = 50
	};

	private AppSettings settings = new AppSettings();

	private DateTimeOffset? enteredAt;

	private string? activeScreen;

	private bool fired;

	public event Action<Screen>? Activated;

	public EdgeHoverService()
	{
		timer.Tick += Tick;
		timer.Start();
	}

	public void Apply(AppSettings value)
	{
		settings = value;
		Reset();
	}

	private void Tick(object? sender, EventArgs e)
	{
		if (!settings.EdgeHoverEnabled)
		{
			Reset();
			return;
		}
		Point mousePosition = Control.MousePosition;
		Screen screen = Screen.FromPoint(mousePosition);
		if (!InsideZone(mousePosition, screen))
		{
			Reset();
			return;
		}
		if (activeScreen != screen.DeviceName)
		{
			enteredAt = DateTimeOffset.Now;
			activeScreen = screen.DeviceName;
			fired = false;
		}
		if (!fired)
		{
			DateTimeOffset? dateTimeOffset = enteredAt;
			if (dateTimeOffset.HasValue && (DateTimeOffset.Now - enteredAt.Value).TotalMilliseconds >= (double)settings.EdgeHoverMilliseconds)
			{
				fired = true;
				this.Activated?.Invoke(screen);
			}
		}
	}

	private bool InsideZone(Point point, Screen screen)
	{
		Rectangle bounds = screen.Bounds;
		return EdgeActivationPolicy.Contains(point.X, point.Y, bounds.Left, bounds.Top, bounds.Width, settings.EdgeZone);
	}

	private void Reset()
	{
		enteredAt = null;
		activeScreen = null;
		fired = false;
	}

	public void Dispose()
	{
		timer.Dispose();
	}
}
