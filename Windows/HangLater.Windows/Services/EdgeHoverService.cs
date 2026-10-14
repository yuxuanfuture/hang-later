using HangLater.Windows.Models;
using DrawingPoint = System.Drawing.Point;
using FormsScreen = System.Windows.Forms.Screen;
using FormsTimer = System.Windows.Forms.Timer;

namespace HangLater.Windows.Services;

public sealed class EdgeHoverService : IDisposable
{
    private readonly FormsTimer timer = new() { Interval = 50 };
    private AppSettings settings = new();
    private DateTimeOffset? enteredAt;
    private string? activeScreen;
    private bool fired;
    public event Action<FormsScreen>? Activated;

    public EdgeHoverService() { timer.Tick += Tick; timer.Start(); }
    public void Apply(AppSettings value) { settings = value; Reset(); }
    private void Tick(object? sender, EventArgs e)
    {
        if (!settings.EdgeHoverEnabled) { Reset(); return; }
        var cursor = System.Windows.Forms.Control.MousePosition;
        var screen = FormsScreen.FromPoint(cursor);
        if (!InsideZone(cursor, screen)) { Reset(); return; }
        if (activeScreen != screen.DeviceName) { enteredAt = DateTimeOffset.Now; activeScreen = screen.DeviceName; fired = false; }
        if (!fired && enteredAt is not null && (DateTimeOffset.Now - enteredAt.Value).TotalMilliseconds >= settings.EdgeHoverMilliseconds)
        {
            fired = true; Activated?.Invoke(screen);
        }
    }
    private bool InsideZone(DrawingPoint point, FormsScreen screen)
    {
        var bounds = screen.Bounds;
        return EdgeActivationPolicy.Contains(point.X, point.Y, bounds.Left, bounds.Top, bounds.Width, settings.EdgeZone);
    }
    private void Reset() { enteredAt = null; activeScreen = null; fired = false; }
    public void Dispose() => timer.Dispose();
}
