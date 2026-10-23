using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HangLater.Windows.Services;

/// <summary>Return only a successfully pinned source window, never an ordinary window move.</summary>
public static class WindowDropReturnService
{
    public interface IPlatform
    {
        CapturableWindow? Current(nint handle);
        bool PointerDown { get; }
        bool Apply(nint handle, NativeMethods.WINDOWPLACEMENT placement, NativeMethods.RECT? screenBounds, bool currentlyMaximized);
    }

    public static NativeMethods.WINDOWPLACEMENT? ReadPlacement(nint handle)
    {
        var placement = new NativeMethods.WINDOWPLACEMENT { Length = (uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>() };
        return NativeMethods.GetWindowPlacement(handle, ref placement) ? placement : null;
    }

    public static bool SameWindow(CapturableWindow original, CapturableWindow? current) =>
        current != null && original.Handle != 0 && original.Handle == current.Handle &&
        original.ProcessId > 0 && original.ProcessId == current.ProcessId &&
        original.ProcessStartUtcTicks.HasValue && original.ProcessStartUtcTicks == current.ProcessStartUtcTicks &&
        string.Equals(original.ExecutablePath, current.ExecutablePath, StringComparison.OrdinalIgnoreCase);

    // Called only AFTER capture, receipt persistence and placement have succeeded.
    public static bool RestoreAfterPin(CapturableWindow original, CapturableWindow dropped, IPlatform? platform = null)
    {
        platform ??= new WindowsPlatform();
        if (!SameWindow(original, dropped) || !original.IsVisible || !dropped.IsVisible || platform.PointerDown ||
            original.Placement is not NativeMethods.WINDOWPLACEMENT placement ||
            (placement.ShowCmd != 1 && placement.ShowCmd != 3) || !Valid(placement.NormalPosition) || !Valid(original.Bounds)) return false;
        var current = platform.Current(original.Handle);
        // A closed/restarted window, minimization, or a subsequent user move must not be undone.
        if (!SameWindow(original, current) || !current!.IsVisible || !Near(current.Bounds, dropped.Bounds) ||
            current.Placement is not NativeMethods.WINDOWPLACEMENT currentPlacement ||
            (currentPlacement.ShowCmd != 1 && currentPlacement.ShowCmd != 3)) return false;
        placement.Length = (uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>();
        placement.Flags |= 4; // WPF_ASYNCWINDOWPLACEMENT: never block on another application's input queue.
        // Placement coordinates are WORKSPACE coordinates. Preserve maximized restore bounds via
        // SetWindowPlacement; normal/snapped windows use the original SCREEN rectangle via SetWindowPos.
        NativeMethods.RECT? screenBounds = placement.ShowCmd == 3 ? null : original.Bounds;
        return platform.Apply(original.Handle, placement, screenBounds, currentPlacement.ShowCmd == 3);
    }

    private static bool Valid(NativeMethods.RECT rect) => rect.Right > rect.Left && rect.Bottom > rect.Top;
    private static bool Near(NativeMethods.RECT a, NativeMethods.RECT b) =>
        Math.Abs((long)a.Left-b.Left)<=2 && Math.Abs((long)a.Top-b.Top)<=2 &&
        Math.Abs((long)a.Right-b.Right)<=2 && Math.Abs((long)a.Bottom-b.Bottom)<=2;

    private sealed class WindowsPlatform : IPlatform
    {
        public CapturableWindow? Current(nint handle) => WindowCaptureService.Snapshot(handle);
        public bool PointerDown => (Control.MouseButtons & MouseButtons.Left) != 0;
        public bool Apply(nint handle, NativeMethods.WINDOWPLACEMENT placement, NativeMethods.RECT? screenBounds, bool currentlyMaximized)
        {
            if (!screenBounds.HasValue) return NativeMethods.SetWindowPlacement(handle, in placement);
            if (currentlyMaximized && !NativeMethods.SetWindowPlacement(handle, in placement)) return false;
            var bounds = screenBounds.Value;
            const uint NoZOrder = 0x0004, NoActivate = 0x0010, AsyncWindowPos = 0x4000;
            return NativeMethods.SetWindowPos(handle, 0, bounds.Left, bounds.Top,
                bounds.Right-bounds.Left, bounds.Bottom-bounds.Top, NoZOrder | NoActivate | AsyncWindowPos);
        }
    }
}
