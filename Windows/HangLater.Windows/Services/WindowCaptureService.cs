using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace HangLater.Windows.Services;

public sealed record CapturableWindow(IntPtr Handle, string Title, string ProcessName, string? ExecutablePath,
    NativeMethods.RECT Bounds);

public static class WindowCaptureService
{
    public static IReadOnlyList<CapturableWindow> ListWindows()
    {
        var result = new List<CapturableWindow>();
        NativeMethods.EnumWindows((handle, _) =>
        {
            if (!NativeMethods.IsWindowVisible(handle)) return true;
            var length = NativeMethods.GetWindowTextLength(handle);
            if (length < 1 || !NativeMethods.GetWindowRect(handle, out var bounds) || bounds.Right - bounds.Left < 120 || bounds.Bottom - bounds.Top < 80) return true;
            var title = new StringBuilder(length + 1); NativeMethods.GetWindowText(handle, title, title.Capacity);
            NativeMethods.GetWindowThreadProcessId(handle, out var pid);
            try
            {
                var process = Process.GetProcessById((int)pid);
                if (process.Id == Environment.ProcessId) return true;
                result.Add(new CapturableWindow(handle, title.ToString(), process.ProcessName, QueryExecutablePath(pid), bounds));
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return result.OrderBy(x => x.ProcessName).ThenBy(x => x.Title).ToArray();
    }

    public static string Capture(CapturableWindow window, string previewDirectory)
    {
        var width = Math.Min(window.Bounds.Right - window.Bounds.Left, 2400);
        var height = Math.Min(window.Bounds.Bottom - window.Bounds.Top, 1800);
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var context = graphics.GetHdc(); bool captured;
            try { captured = NativeMethods.PrintWindow(window.Handle, context, 2); }
            finally { graphics.ReleaseHdc(context); }
            if (!captured) graphics.CopyFromScreen(window.Bounds.Left, window.Bounds.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        }
        var name = $"window-{Guid.NewGuid():N}.png";
        bitmap.Save(Path.Combine(previewDirectory, name), ImageFormat.Png);
        return name;
    }
    public static void Activate(HangingTaskTarget target)
    {
        var existing = ListWindows().FirstOrDefault(x => string.Equals(x.ExecutablePath, target.ExecutablePath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { NativeMethods.ShowWindow(existing.Handle, NativeMethods.SW_RESTORE); NativeMethods.SetForegroundWindow(existing.Handle); return; }
        if (!string.IsNullOrWhiteSpace(target.ExecutablePath)) Process.Start(new ProcessStartInfo(target.ExecutablePath) { UseShellExecute = true });
    }
    private static string? QueryExecutablePath(uint pid)
    {
        var process = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return null;
        try { var builder = new StringBuilder(32768); var size = builder.Capacity; return NativeMethods.QueryFullProcessImageName(process, 0, builder, ref size) ? builder.ToString() : null; }
        finally { NativeMethods.CloseHandle(process); }
    }
}

public sealed record HangingTaskTarget(string? ExecutablePath);
