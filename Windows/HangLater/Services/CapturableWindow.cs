namespace HangLater.Windows.Services;

public sealed record CapturableWindow(nint Handle, string Title, string ProcessName, string? ExecutablePath, NativeMethods.RECT Bounds, int ProcessId = 0, long? ProcessStartUtcTicks = null, bool IsVisible = true, NativeMethods.WINDOWPLACEMENT? Placement = null);
