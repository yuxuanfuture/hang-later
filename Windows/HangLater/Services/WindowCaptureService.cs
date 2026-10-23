using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HangLater.Windows.Models;

namespace HangLater.Windows.Services;

public static class WindowCaptureService
{
	public static event Action<string>? ActivationTrace;
	public static WindowActivationStatus? LastActivationStatus { get; private set; }
	public static IReadOnlyList<CapturableWindow> ListWindows()
	{
		return EnumerateWindows(false).OrderBy(x => x.ProcessName).ThenBy(x => x.Title).ToArray();
	}

	// Minimized windows have tiny/off-screen rectangles. They are still valid activation targets.
	private static IReadOnlyList<CapturableWindow> EnumerateWindows(bool forActivation, bool includeHidden = false)
	{
		List<CapturableWindow> result = new List<CapturableWindow>();
		NativeMethods.EnumWindows(delegate(nint handle, nint _)
		{
			CapturableWindow? window = DescribeWindow(handle, forActivation, includeHidden);
			if (window != null)
			{
				result.Add(window);
			}
			return true;
		}, IntPtr.Zero);
		return result;
	}

	public static CapturableWindow? WindowAt(Point screenPoint)
	{
		nint handle = NativeMethods.WindowFromPoint(new NativeMethods.POINT(screenPoint.X, screenPoint.Y));
		if (handle == IntPtr.Zero)
		{
			return null;
		}
		handle = NativeMethods.GetAncestor(handle, NativeMethods.GA_ROOT);
		return DescribeWindow(handle);
	}

	public static CapturableWindow? Snapshot(nint handle) => DescribeWindow(handle);

	public static CapturableWindow? Resolve(CapturableWindow candidate)
	{
		CapturableWindow? current = DescribeWindow(candidate.Handle);
		if (current != null && SameApplication(current, new HangingTaskTarget(candidate.ExecutablePath, candidate.ProcessName))
			&& current.ProcessId == candidate.ProcessId && current.ProcessStartUtcTicks == candidate.ProcessStartUtcTicks)
		{
			return current;
		}
		IReadOnlyList<CapturableWindow> windows = ListWindows();
		return windows.FirstOrDefault((CapturableWindow x) =>
			string.Equals(x.Title, candidate.Title, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(x.ExecutablePath, candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase))
			?? windows.FirstOrDefault((CapturableWindow x) =>
				string.Equals(x.Title, candidate.Title, StringComparison.OrdinalIgnoreCase) &&
				string.Equals(x.ProcessName, candidate.ProcessName, StringComparison.OrdinalIgnoreCase));
	}

	public static string Capture(CapturableWindow window, string previewDirectory)
	{
		int width = Math.Min(window.Bounds.Right - window.Bounds.Left, 2400);
		int height = Math.Min(window.Bounds.Bottom - window.Bounds.Top, 1800);
		using Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			nint hdc = graphics.GetHdc();
			bool flag;
			try
			{
				flag = NativeMethods.PrintWindow(window.Handle, hdc, 2u);
			}
			finally
			{
				graphics.ReleaseHdc(hdc);
			}
			if (!flag)
			{
				graphics.CopyFromScreen(window.Bounds.Left, window.Bounds.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
			}
		}
		string text = $"window-{Guid.NewGuid():N}.png";
		bitmap.Save(Path.Combine(previewDirectory, text), ImageFormat.Png);
		return text;
	}

	public static void SetCapturedTarget(HangingTask task, CapturableWindow window)
	{
		task.appBundleIdentifier = window.ExecutablePath;
		task.capturedWindowTitle = window.Title;
		task.capturedProcessName = window.ProcessName;
		task.capturedLaunchExecutablePath = ApplicationLaunchService.Resolve(window.ExecutablePath);
		task.capturedDocumentPath = TyporaResumeService.TryResolve(window.ProcessName,window.Title);
		task.capturedWindowHandle = window.Handle.ToInt64();
		task.capturedProcessId = window.ProcessId;
		task.capturedProcessStartUtcTicks = window.ProcessStartUtcTicks;
		task.resumeURL = ObsidianResumeService.TryResolve(window.ProcessName, window.Title, capturingCurrentWindow: true) ?? task.resumeURL;
	}

	public static bool SameApplication(CapturableWindow window, HangingTaskTarget target) =>
		!string.IsNullOrWhiteSpace(target.ExecutablePath)
			? string.Equals(window.ExecutablePath, target.ExecutablePath, StringComparison.OrdinalIgnoreCase)
			: !string.IsNullOrWhiteSpace(target.ProcessName) && string.Equals(window.ProcessName, target.ProcessName, StringComparison.OrdinalIgnoreCase);

	public static CapturableWindow? SelectTarget(IEnumerable<CapturableWindow> windows, HangingTaskTarget target) =>
		windows.Where(x => x.IsVisible && SameApplication(x, target)).OrderByDescending(x =>
			(target.WindowHandle == x.Handle.ToInt64() && target.ProcessId == x.ProcessId &&
			 target.ProcessStartUtcTicks.HasValue && target.ProcessStartUtcTicks == x.ProcessStartUtcTicks ? 1000 : 0) +
			(!string.IsNullOrWhiteSpace(target.WindowTitle) && string.Equals(x.Title, target.WindowTitle, StringComparison.OrdinalIgnoreCase) ? 100 : 0) +
			(target.ProcessId == x.ProcessId && target.ProcessStartUtcTicks.HasValue && target.ProcessStartUtcTicks == x.ProcessStartUtcTicks ? 10 : 0)).FirstOrDefault();

	public static CapturableWindow? SelectHiddenTarget(IEnumerable<CapturableWindow> windows, HangingTaskTarget target) =>
		windows.FirstOrDefault(x => !x.IsVisible && SameApplication(x,target) &&
			((target.WindowHandle == x.Handle.ToInt64() && target.ProcessId == x.ProcessId &&
			  target.ProcessStartUtcTicks.HasValue && target.ProcessStartUtcTicks == x.ProcessStartUtcTicks) ||
			 (!string.IsNullOrWhiteSpace(target.WindowTitle) && string.Equals(x.Title,target.WindowTitle,StringComparison.OrdinalIgnoreCase))));

	public static async Task<bool> ActivateAsync(HangingTaskTarget target)
	{
		LastActivationStatus = null;
		var available = EnumerateWindows(true);
		CapturableWindow? window = SelectTarget(available, target);
		ActivationTrace?.Invoke($"select visible={available.Count} match={window?.Handle.ToInt64()} pid={window?.ProcessId}");
		if (window != null) return await BringToForegroundAsync(window);
		CapturableWindow? hidden = SelectHiddenTarget(EnumerateWindows(true,true),target);
		HangingTaskTarget activationTarget = hidden == null ? target : target with
		{
			WindowHandle=hidden.Handle.ToInt64(), ProcessId=hidden.ProcessId, ProcessStartUtcTicks=hidden.ProcessStartUtcTicks
		};
		// Closing to tray may destroy the app's child UI. Only its official entry
		// point can rebuild it; ShowWindow on that hidden shell can produce a blank window.
		ProcessStartInfo? startInfo = ApplicationLaunchService.StartInfo(target);
		if (startInfo == null) return false;
		using Process? started = Process.Start(startInfo);
		// Single-instance/tray apps may return a short-lived launcher, so re-enumerate by identity.
		Stopwatch startup = Stopwatch.StartNew();
		while (startup.Elapsed < TimeSpan.FromSeconds(10))
		{
			await Task.Delay(200);
			window = SelectTarget(EnumerateWindows(true), activationTarget);
			if (window != null && await BringToForegroundAsync(window)) return true;
		}
		return false;
	}

	private static async Task<bool> BringToForegroundAsync(CapturableWindow window)
	{
		LastActivationStatus = await WindowActivationService.ActivateAsync(window.Handle, trace: message => ActivationTrace?.Invoke(message));
		ActivationTrace?.Invoke($"result={LastActivationStatus}");
		return LastActivationStatus == WindowActivationStatus.Activated;
	}

	private static CapturableWindow? DescribeWindow(nint handle, bool forActivation = false, bool includeHidden = false)
	{
		bool visible = NativeMethods.IsWindowVisible(handle);
		if (handle == IntPtr.Zero || (!visible && !includeHidden))
		{
			return null;
		}
		int windowTextLength = NativeMethods.GetWindowTextLength(handle);
		if (windowTextLength < 1 || !NativeMethods.GetWindowRect(handle, out var rect) ||
			((rect.Right - rect.Left < 120 || rect.Bottom - rect.Top < 80) && (!forActivation || !NativeMethods.IsIconic(handle))))
		{
			return null;
		}
		StringBuilder title = new StringBuilder(windowTextLength + 1);
		NativeMethods.GetWindowText(handle, title, title.Capacity);
		NativeMethods.GetWindowThreadProcessId(handle, out var processId);
		try
		{
			using Process process = Process.GetProcessById((int)processId);
			if (process.Id == Environment.ProcessId)
			{
				return null;
			}
			long? started = null;
			try { started = process.StartTime.ToUniversalTime().Ticks; } catch { }
			return new CapturableWindow(handle, title.ToString(), process.ProcessName, QueryExecutablePath(processId), rect, (int)processId, started, visible, WindowDropReturnService.ReadPlacement(handle));
		}
		catch
		{
			return null;
		}
	}

	private static string? QueryExecutablePath(uint pid)
	{
		nint num = NativeMethods.OpenProcess(4096u, inherit: false, pid);
		if (num == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder(32768);
			int size = stringBuilder.Capacity;
			return NativeMethods.QueryFullProcessImageName(num, 0, stringBuilder, ref size) ? stringBuilder.ToString() : null;
		}
		finally
		{
			NativeMethods.CloseHandle(num);
		}
	}
}
