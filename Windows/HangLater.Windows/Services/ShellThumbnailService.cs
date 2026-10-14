using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace HangLater.Windows.Services;

public static class ShellThumbnailService
{
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(SIZE size, SIIGBF flags, out IntPtr bitmap);
    }
    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
    [Flags] private enum SIIGBF { ResizeToFit = 0, BiggerSizeOk = 1, MemoryOnly = 2, IconOnly = 4, ThumbnailOnly = 8, InCacheOnly = 16 }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory imageFactory);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr objectHandle);

    public static BitmapSource? Get(string path, int size = 320)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return null;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
            factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF.BiggerSizeOk, out var bitmap);
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze(); return source;
            }
            finally { DeleteObject(bitmap); }
        }
        catch { return null; }
    }

    public static BitmapSource? LoadPreview(TaskRepository repository, Models.HangingTask task, int size = 320)
    {
        if (!string.IsNullOrWhiteSpace(task.previewFile))
        {
            var path = Path.Combine(repository.PreviewDirectory, task.previewFile);
            if (File.Exists(path))
            {
                try
                {
                    var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(path); bitmap.DecodePixelWidth = size; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
                }
                catch { }
            }
        }
        return task.filePath is null ? null : Get(task.filePath, size);
    }
}
