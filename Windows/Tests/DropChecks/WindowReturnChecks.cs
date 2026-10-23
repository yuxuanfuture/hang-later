using System.Runtime.InteropServices;
using HangLater.Windows.Services;

internal static class WindowReturnChecks
{
    public static void Run()
    {
        var bounds = Rect(-900, 180, 800, 600);
        var placement = new NativeMethods.WINDOWPLACEMENT
        {
            Length = (uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>(), ShowCmd = 1,
            NormalPosition = Rect(-900, 140, 800, 600)
        };
        var original = new CapturableWindow((nint)10, "Original document", "Fixture", "C:\\Fixture.exe", bounds, 100, 1234, true, placement);
        var dropped = original with { Bounds = Rect(400, 80, 800, 600), Placement = placement };
        var platform = new Fake { Window = dropped };
        Check(Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>() == 44, "WINDOWPLACEMENT matches the Windows SDK ABI, without the Mac-only rcDevice field");
        Check(WindowDropReturnService.RestoreAfterPin(original, dropped, platform), "An accepted application pin requests return to the original window");
        Check(platform.AppliedHandle == original.Handle && platform.ScreenBounds!.Value.Equals(bounds), "Normal windows return to their exact original screen bounds, including a negative monitor origin");
        Check(platform.Placement.NormalPosition.Equals(placement.NormalPosition), "Workspace placement coordinates are not incorrectly substituted for screen coordinates");
        Check(platform.Placement.Flags == 4 && original.Placement!.Value.Flags == 0, "Restoration is asynchronous and does not mutate the saved origin");
        var maximized = original with { Placement = placement with { ShowCmd = 3 } };
        var drag = new DesktopDropSession();
        drag.SetLineVisible(true);drag.Begin(maximized,new System.Drawing.Point(0,0));
        drag.Move(new System.Drawing.Point(400,80),Rect(400,80,600,400));
        Check(drag.HintVisible && drag.FinishPointer()==maximized, "A maximized title-bar drag remains a pin gesture after Windows restores its smaller normal size");
        platform = new Fake { Window = dropped };
        Check(WindowDropReturnService.RestoreAfterPin(maximized, dropped, platform) && platform.ScreenBounds == null && platform.Placement.ShowCmd == 3, "A previously maximized window becomes maximized again instead of staying near the drop target");
        Check(platform.Placement.NormalPosition.Equals(placement.NormalPosition), "Maximized windows also retain their original unmaximized restore rectangle");
        platform = new Fake { Window = dropped with { Placement = placement with { ShowCmd = 3 } } };
        Check(WindowDropReturnService.RestoreAfterPin(original, platform.Window, platform) && platform.CurrentlyMaximized && platform.ScreenBounds!.Value.Equals(bounds), "A normal window that Windows maximized at drop time still restores its normal origin");
        var snapped = original with { Placement = placement with { NormalPosition = Rect(-700, 400, 500, 350) } };
        platform = new Fake { Window = dropped };
        Check(WindowDropReturnService.RestoreAfterPin(snapped, dropped, platform) && platform.ScreenBounds!.Value.Equals(bounds), "A snapped-sized window returns to its displayed rectangle, not its older pre-snap restore rectangle");
        Reject(original, dropped, dropped with { Handle = (nint)11 }, "A reused or different window handle is never moved");
        Reject(original, dropped, dropped with { ProcessId = 101 }, "A different process cannot receive the return operation");
        Reject(original, dropped, dropped with { ProcessStartUtcTicks = 4321 }, "A restarted process with a reused PID is never moved");
        Reject(original, dropped, null, "A closed source window is left alone");
        Reject(original, dropped, dropped with { IsVisible = false }, "A source hidden after pinning is not forcibly shown");
        Reject(original, dropped, dropped with { Bounds = Rect(420, 80, 800, 600) }, "A subsequent intentional user move is not undone");
        Reject(original, dropped, dropped with { Placement = placement with { ShowCmd = 2 } }, "A source minimized after pinning is not forcibly restored");
        Reject(original with { Placement = null }, dropped, dropped, "Missing original placement does not cause a guessed restore");
        Reject(original with { ProcessStartUtcTicks = null }, dropped, dropped, "An unverified process session cannot move a window");
        Reject(original, dropped with { Handle = (nint)20 }, dropped, "A different release-time window is rejected");
        Reject(original with { Bounds = Rect(0, 0, 0, 0) }, dropped, dropped, "Invalid original screen bounds are rejected");
        platform = new Fake { Window = dropped, PointerDown = true };
        Check(!WindowDropReturnService.RestoreAfterPin(original, dropped, platform) && platform.AppliedHandle == 0, "A new mouse drag takes precedence over an older pin's return");
        platform = new Fake { Window = dropped, ApplyResult = false };
        Check(!WindowDropReturnService.RestoreAfterPin(original, dropped, platform), "An OS-rejected return is reported as failure, not a false success");
        platform = new Fake { Window = dropped with { Title = "Renamed document", Bounds = Rect(401, 81, 800, 600) } };
        Check(WindowDropReturnService.RestoreAfterPin(original, dropped, platform), "A title change or one-pixel frame adjustment does not change source identity");
    }

    private static NativeMethods.RECT Rect(int x, int y, int width, int height) => new() { Left=x, Top=y, Right=x+width, Bottom=y+height };
    private static void Reject(CapturableWindow origin, CapturableWindow dropped, CapturableWindow? current, string label)
    {
        var fake = new Fake { Window = current };
        Check(!WindowDropReturnService.RestoreAfterPin(origin, dropped, fake) && fake.AppliedHandle == 0, label);
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
        Console.WriteLine("PASS " + label);
    }
    private sealed class Fake : WindowDropReturnService.IPlatform
    {
        public CapturableWindow? Window;
        public bool PointerDown { get; set; }
        public bool ApplyResult = true;
        public nint AppliedHandle;
        public NativeMethods.WINDOWPLACEMENT Placement;
        public NativeMethods.RECT? ScreenBounds;
        public bool CurrentlyMaximized;
        public CapturableWindow? Current(nint handle) => Window;
        public bool Apply(nint handle, NativeMethods.WINDOWPLACEMENT placement, NativeMethods.RECT? screenBounds, bool currentlyMaximized)
        {
            AppliedHandle=handle; Placement=placement; ScreenBounds=screenBounds; CurrentlyMaximized=currentlyMaximized;
            return ApplyResult;
        }
    }
}
