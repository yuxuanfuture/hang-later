using System;
using System.Threading.Tasks;

namespace HangLater.Windows.Services;

public enum WindowActivationStatus { Activated, NotVisible, TargetGone, RestoreRejected, RestoreTimedOut, ForegroundDenied }

/// <summary>The same restore/foreground state machine for every captured application.</summary>
public static class WindowActivationService
{
    public interface IPlatform
    {
        bool Exists(nint window);
        bool Visible(nint window);
        bool Minimized(nint window);
        bool Enabled(nint window);
        nint RootOwner(nint window);
        nint LastPopup(nint window);
        bool Restore(nint window);
        bool RestoreSystemCommand(nint window);
        bool Foreground(nint window);
        nint CurrentForeground();
        Task Delay();
    }

    public static async Task<WindowActivationStatus> ActivateAsync(nint window, IPlatform? platform = null, Action<string>? trace = null)
    {
        platform ??= new WindowsPlatform();
        if (!platform.Exists(window)) return WindowActivationStatus.TargetGone;
        if (!platform.Visible(window)) return WindowActivationStatus.NotVisible;
        bool restoreQueued = true, systemRestoreQueued = true, systemRestoreAttempted = false;
        if (platform.Minimized(window)) restoreQueued = platform.Restore(window);
        // Request foreground while handling the user's click, before yielding the UI thread.
        nint target = ActivePopup(window, platform);
        bool accepted = platform.Foreground(target);
        trace?.Invoke($"begin target={window} popup={target} restore={restoreQueued} foreground={accepted}");
        for (int attempt = 0; attempt < 40; attempt++)
        {
            await platform.Delay();
            if (!platform.Exists(window)) return WindowActivationStatus.TargetGone;
            if (!platform.Visible(window)) return WindowActivationStatus.NotVisible;
            if (platform.Minimized(window))
            {
                // Some custom window procedures handle the system-menu restore command
                // instead of the asynchronous show notification. Never force-minimize,
                // attach input queues, synthesize Alt, or change foreground/security policy.
                if (attempt >= 3 && !systemRestoreAttempted)
                {
                    systemRestoreAttempted = true;
                    systemRestoreQueued = platform.RestoreSystemCommand(window);
                    trace?.Invoke($"system restore={systemRestoreQueued}");
                    if (!restoreQueued && !systemRestoreQueued) return WindowActivationStatus.RestoreRejected;
                }
                continue;
            }
            target = ActivePopup(window, platform);
            nint foreground = platform.CurrentForeground();
            if (foreground == target || foreground == window || (foreground != 0 && platform.RootOwner(foreground) == window))
                return WindowActivationStatus.Activated;
            platform.Foreground(target);
        }
        trace?.Invoke($"end minimized={platform.Minimized(window)} foreground={platform.CurrentForeground()}");
        return platform.Minimized(window) ? WindowActivationStatus.RestoreTimedOut : WindowActivationStatus.ForegroundDenied;
    }

    private static nint ActivePopup(nint window, IPlatform platform)
    {
        nint popup = platform.LastPopup(window);
        return popup != 0 && popup != window && platform.Exists(popup) && platform.Visible(popup) &&
            platform.Enabled(popup) && platform.RootOwner(popup) == window ? popup : window;
    }

    private sealed class WindowsPlatform : IPlatform
    {
        public bool Exists(nint window) => NativeMethods.IsWindow(window);
        public bool Visible(nint window) => NativeMethods.IsWindowVisible(window);
        public bool Minimized(nint window) => NativeMethods.IsIconic(window);
        public bool Enabled(nint window) => NativeMethods.IsWindowEnabled(window);
        public nint RootOwner(nint window) => NativeMethods.GetAncestor(window, NativeMethods.GA_ROOTOWNER);
        public nint LastPopup(nint window) => NativeMethods.GetLastActivePopup(window);
        public bool Restore(nint window) => NativeMethods.ShowWindowAsync(window, NativeMethods.SW_RESTORE);
        public bool RestoreSystemCommand(nint window) => NativeMethods.PostMessage(window, 0x0112, 0xF120, 0);
        public bool Foreground(nint window) => NativeMethods.SetForegroundWindow(window);
        public nint CurrentForeground() => NativeMethods.GetForegroundWindow();
        public Task Delay() => Task.Delay(75);
    }
}
