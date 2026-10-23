using HangLater.Windows.Models;

namespace HangLater.Windows.Services;

public sealed record HangingTaskTarget(string? ExecutablePath, string? ProcessName = null,
    string? WindowTitle = null, long? WindowHandle = null, int? ProcessId = null, long? ProcessStartUtcTicks = null,
    string? LaunchExecutablePath = null)
{
    public static HangingTaskTarget FromTask(HangingTask task) => new(task.appBundleIdentifier,
        task.capturedProcessName ?? task.appName, task.capturedWindowTitle ?? task.title,
        task.capturedWindowHandle, task.capturedProcessId, task.capturedProcessStartUtcTicks, task.capturedLaunchExecutablePath);
}
