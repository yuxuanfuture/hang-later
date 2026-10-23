using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Interop;

namespace HangLater.Windows.Services;

public sealed class HotKeyService : IDisposable
{
	private const int Id = 18508;

	private readonly HwndSource source;

	private Action? activated;

	public string CurrentGesture { get; private set; } = "Ctrl+Alt+L";

	public HotKeyService()
	{
		source = new HwndSource(new HwndSourceParameters("HangLaterHotKeySink")
		{
			Width = 0,
			Height = 0,
			WindowStyle = 0
		});
		source.AddHook(WndProc);
	}

	public void Register(string gesture, Action callback)
	{
		NativeMethods.UnregisterHotKey(source.Handle, 18508);
		var (num, key) = Parse(gesture);
		if (!NativeMethods.RegisterHotKey(source.Handle, 18508, num | 0x4000, (uint)KeyInterop.VirtualKeyFromKey(key)))
		{
			throw new Win32Exception("The shortcut " + gesture + " is already used by another app.");
		}
		CurrentGesture = Normalize(gesture);
		activated = callback;
	}

	public static bool TryValidate(string gesture, out string normalized)
	{
		try
		{
			Parse(gesture);
			normalized = Normalize(gesture);
			return true;
		}
		catch
		{
			normalized = "";
			return false;
		}
	}

	private static (int Modifiers, Key Key) Parse(string gesture)
	{
		string[] array = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		int num = 0;
		Key? key = null;
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (text.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || text.Equals("Control", StringComparison.OrdinalIgnoreCase))
			{
				num |= 2;
				continue;
			}
			if (text.Equals("Alt", StringComparison.OrdinalIgnoreCase))
			{
				num |= 1;
				continue;
			}
			if (text.Equals("Shift", StringComparison.OrdinalIgnoreCase))
			{
				num |= 4;
				continue;
			}
			if (text.Equals("Win", StringComparison.OrdinalIgnoreCase) || text.Equals("Windows", StringComparison.OrdinalIgnoreCase))
			{
				num |= 8;
				continue;
			}
			if (Enum.TryParse<Key>(text, ignoreCase: true, out var result))
			{
				key = result;
				continue;
			}
			if (text.Length == 1 && Enum.TryParse<Key>(text.ToUpperInvariant(), out result))
			{
				key = result;
				continue;
			}
			throw new FormatException("Invalid shortcut");
		}
		bool flag = num == 0 || !key.HasValue;
		bool flag2;
		if (!flag)
		{
			if (key.HasValue)
			{
				Key valueOrDefault = key.GetValueOrDefault();
				if ((uint)(valueOrDefault - 116) <= 5u)
				{
					flag2 = true;
					goto IL_0124;
				}
			}
			flag2 = false;
			goto IL_0124;
		}
		goto IL_0128;
		IL_0128:
		if (flag)
		{
			throw new FormatException("Use at least one modifier and one key.");
		}
		return (Modifiers: num, Key: key.Value);
		IL_0124:
		flag = flag2;
		goto IL_0128;
	}

	private static string Normalize(string gesture)
	{
		return string.Join('+', from x in gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			select (!x.Equals("Control", StringComparison.OrdinalIgnoreCase)) ? (char.ToUpperInvariant(x[0]) + x.Substring(1, x.Length - 1).ToLowerInvariant()) : "Ctrl");
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == 786 && ((IntPtr)wParam).ToInt32() == 18508)
		{
			handled = true;
			activated?.Invoke();
		}
		return IntPtr.Zero;
	}

	public void Dispose()
	{
		NativeMethods.UnregisterHotKey(source.Handle, 18508);
		source.RemoveHook(WndProc);
		source.Dispose();
	}
}
