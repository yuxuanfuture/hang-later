using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Interop;

namespace HangLater.Windows.Services;

public sealed class HotKeyService : IDisposable
{
    private const int Id = 0x484C;
    private readonly HwndSource source;
    private Action? activated;
    public string CurrentGesture { get; private set; } = "Ctrl+Alt+L";

    public HotKeyService()
    {
        source = new HwndSource(new HwndSourceParameters("HangLaterHotKeySink") { Width = 0, Height = 0, WindowStyle = 0 });
        source.AddHook(WndProc);
    }

    public void Register(string gesture, Action callback)
    {
        NativeMethods.UnregisterHotKey(source.Handle, Id);
        var (modifiers, key) = Parse(gesture);
        if (!NativeMethods.RegisterHotKey(source.Handle, Id, modifiers | NativeMethods.MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(key)))
            throw new Win32Exception($"The shortcut {gesture} is already used by another app.");
        CurrentGesture = Normalize(gesture); activated = callback;
    }

    public static bool TryValidate(string gesture, out string normalized)
    {
        try { Parse(gesture); normalized = Normalize(gesture); return true; }
        catch { normalized = ""; return false; }
    }

    private static (int Modifiers, Key Key) Parse(string gesture)
    {
        var parts = gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var modifiers = 0; Key? key = null;
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_CONTROL;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_ALT;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_SHIFT;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase)) modifiers |= NativeMethods.MOD_WIN;
            else if (Enum.TryParse<Key>(part, true, out var parsed)) key = parsed;
            else if (part.Length == 1 && Enum.TryParse<Key>(part.ToUpperInvariant(), out parsed)) key = parsed;
            else throw new FormatException("Invalid shortcut");
        }
        if (modifiers == 0 || key is null || key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)
            throw new FormatException("Use at least one modifier and one key.");
        return (modifiers, key.Value);
    }

    private static string Normalize(string gesture) => string.Join('+', gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Equals("Control", StringComparison.OrdinalIgnoreCase) ? "Ctrl" : char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant()));
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == Id) { handled = true; activated?.Invoke(); }
        return IntPtr.Zero;
    }
    public void Dispose() { NativeMethods.UnregisterHotKey(source.Handle, Id); source.RemoveHook(WndProc); source.Dispose(); }
}
