using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HangLater.Windows.Services;

public static class NativeMethods
{
	public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	public struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	public struct POINT
	{
		public int X;

		public int Y;

		public POINT(int x, int y)
		{
			X = x;
			Y = y;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct WINDOWPLACEMENT
	{
		public uint Length;
		public uint Flags;
		public uint ShowCmd;
		public POINT MinPosition;
		public POINT MaxPosition;
		public RECT NormalPosition;
	}

	public const int WM_HOTKEY = 786;

	public const int MOD_ALT = 1;

	public const int MOD_CONTROL = 2;

	public const int MOD_SHIFT = 4;

	public const int MOD_WIN = 8;

	public const int MOD_NOREPEAT = 16384;

	public const int SW_RESTORE = 9;

	public const uint PROCESS_QUERY_LIMITED_INFORMATION = 4096u;

	public const uint GA_ROOT = 2u;

	public const uint GA_ROOTOWNER = 3u;

	public static readonly nint HWND_TOPMOST = new IntPtr(-1);

	public const uint SWP_NOACTIVATE = 16u;

	public const uint SWP_SHOWWINDOW = 64u;

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool RegisterHotKey(nint hWnd, int id, int modifiers, uint key);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool UnregisterHotKey(nint hWnd, int id);

	[DllImport("user32.dll")]
	public static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

	[DllImport("user32.dll")]
	public static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetWindowText(nint hWnd, StringBuilder text, int maxCount);

	[DllImport("user32.dll")]
	public static extern int GetWindowTextLength(nint hWnd);

	[DllImport("user32.dll")]
	public static extern bool GetWindowRect(nint hWnd, out RECT rect);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool GetWindowPlacement(nint hWnd, ref WINDOWPLACEMENT placement);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool SetWindowPlacement(nint hWnd, in WINDOWPLACEMENT placement);

	[DllImport("user32.dll")]
	public static extern nint WindowFromPoint(POINT point);

	[DllImport("user32.dll")]
	public static extern nint GetAncestor(nint hWnd, uint flags);

	[DllImport("user32.dll")]
	public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

	[DllImport("user32.dll")]
	public static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	public static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	public static extern bool IsIconic(nint hWnd);

	[DllImport("user32.dll")]
	public static extern bool IsWindow(nint hWnd);

	[DllImport("user32.dll")]
	public static extern short GetAsyncKeyState(int key);

	[DllImport("user32.dll")]
	public static extern bool IsWindowEnabled(nint hWnd);

	[DllImport("user32.dll")]
	public static extern nint GetLastActivePopup(nint hWnd);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool PostMessage(nint hWnd, uint message, nint wParam, nint lParam);

	[DllImport("user32.dll")]
	public static extern bool ShowWindowAsync(nint hWnd, int command);

	[DllImport("user32.dll")]
	public static extern bool ShowWindow(nint hWnd, int command);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool SetWindowPos(nint hWnd, nint insertAfter, int x, int y, int width, int height, uint flags);

	[DllImport("user32.dll")]
	public static extern bool PrintWindow(nint hWnd, nint deviceContext, uint flags);

	[DllImport("kernel32.dll", SetLastError = true)]
	public static extern nint OpenProcess(uint access, bool inherit, uint processId);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern bool QueryFullProcessImageName(nint process, int flags, StringBuilder name, ref int size);

	[DllImport("kernel32.dll")]
	public static extern bool CloseHandle(nint handle);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromPoint(POINT point, uint flags);

	[DllImport("shcore.dll")]
	private static extern int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

	public static (double X, double Y) ScaleAt(int x, int y)
	{
		try
		{
			nint num = MonitorFromPoint(new POINT(x, y), 2u);
			if (num != IntPtr.Zero && GetDpiForMonitor(num, 0, out var dpiX, out var dpiY) == 0)
			{
				return (X: (double)dpiX / 96.0, Y: (double)dpiY / 96.0);
			}
		}
		catch
		{
		}
		return (X: 1.0, Y: 1.0);
	}
}
