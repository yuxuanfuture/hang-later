using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using HangLater.Windows.Models;

namespace HangLater.Windows.Services;

public static class ShellThumbnailService
{
	[ComImport]
	[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IShellItemImageFactory
	{
		void GetImage(SIZE size, SIIGBF flags, out nint bitmap);
	}

	private struct SIZE
	{
		public int cx;

		public int cy;
	}

	[Flags]
	private enum SIIGBF
	{
		ResizeToFit = 0,
		BiggerSizeOk = 1,
		MemoryOnly = 2,
		IconOnly = 4,
		ThumbnailOnly = 8,
		InCacheOnly = 0x10
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
	private static extern void SHCreateItemFromParsingName(string path, nint bindContext, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory imageFactory);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(nint objectHandle);

	public static BitmapSource? Get(string path, int size = 320)
	{
		if (!File.Exists(path) && !Directory.Exists(path))
		{
			return null;
		}
		try
		{
			Guid riid = typeof(IShellItemImageFactory).GUID;
			SHCreateItemFromParsingName(path, IntPtr.Zero, ref riid, out IShellItemImageFactory imageFactory);
			imageFactory.GetImage(new SIZE
			{
				cx = size,
				cy = size
			}, SIIGBF.BiggerSizeOk, out var bitmap);
			try
			{
				BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
				bitmapSource.Freeze();
				return bitmapSource;
			}
			finally
			{
				DeleteObject(bitmap);
			}
		}
		catch
		{
			return null;
		}
	}

	public static BitmapSource? LoadPreview(TaskRepository repository, HangingTask task, int size = 320)
	{
		if (!string.IsNullOrWhiteSpace(task.previewFile))
		{
			string text = Path.Combine(repository.PreviewDirectory, task.previewFile);
			if (File.Exists(text))
			{
				try
				{
					BitmapImage bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.UriSource = new Uri(text);
					bitmapImage.DecodePixelWidth = size;
					bitmapImage.EndInit();
					bitmapImage.Freeze();
					return bitmapImage;
				}
				catch
				{
				}
			}
		}
		if (task.filePath != null)
		{
			return Get(task.filePath, size);
		}
		return null;
	}
}
