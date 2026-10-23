using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using HangLater.Windows.Controls;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Views;

public partial class DesktopLineWindow : Window
{
	private readonly App app;

	private readonly DispatcherTimer leaveTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(420.0)
	};

	private readonly DispatcherTimer externalWindowDragTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(45.0)
	};

	private (int X, int Y, int Width, int Height)? nativeBounds;

	private bool pointerEnteredSinceShow;

	private bool previewPinned;

	private bool leftButtonWasDown;

	private bool windowCapturePending;

	private readonly DesktopDropSession dropSession = new DesktopDropSession();

	private DateTime suppressExternalWindowCaptureUntil;

	private HwndSource? nativeSource;





	public DesktopLineWindow(App app)
	{
		DesktopLineWindow desktopLineWindow = this;
		this.app = app;
		InitializeComponent();
		base.IsVisibleChanged += delegate
		{
			dropSession.SetLineVisible(IsVisible);
			leftButtonWasDown = (System.Windows.Forms.Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;
			ApplyDropSession();
		};
		base.MouseEnter += delegate
		{
			desktopLineWindow.pointerEnteredSinceShow = true;
			desktopLineWindow.leaveTimer.Stop();
		};
		base.MouseLeave += delegate
		{
			if (desktopLineWindow.pointerEnteredSinceShow && !desktopLineWindow.ShouldStayVisible)
			{
				desktopLineWindow.leaveTimer.Start();
			}
		};
		leaveTimer.Tick += delegate
		{
			desktopLineWindow.leaveTimer.Stop();
			if (!desktopLineWindow.IsMouseOver && !desktopLineWindow.ShouldStayVisible)
			{
				desktopLineWindow.Hide();
			}
		};
		externalWindowDragTimer.Tick += delegate
		{
			desktopLineWindow.TrackExternalWindowDrag();
		};
		base.Loaded += delegate
		{
			desktopLineWindow.leftButtonWasDown = (System.Windows.Forms.Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;
			desktopLineWindow.externalWindowDragTimer.Start();
		};
		base.Closing += delegate(object? _, CancelEventArgs e)
		{
			if (!app.IsExiting)
			{
				e.Cancel = true;
				desktopLineWindow.Hide();
			}
			else
			{
				desktopLineWindow.nativeSource?.RemoveHook(desktopLineWindow.WindowProc);
				desktopLineWindow.externalWindowDragTimer.Stop();
			}
		};
		base.SourceInitialized += delegate
		{
			desktopLineWindow.nativeSource = HwndSource.FromHwnd(new WindowInteropHelper(desktopLineWindow).Handle);
			desktopLineWindow.nativeSource?.AddHook(desktopLineWindow.WindowProc);
			desktopLineWindow.ApplyNativeBounds();
		};
		base.SizeChanged += delegate
		{
			Canvas.SetLeft(desktopLineWindow.DropZone, Math.Max(12.0, (desktopLineWindow.ActualWidth - desktopLineWindow.DropZone.Width) / 2.0));
			desktopLineWindow.RefreshTasks();
		};
		LocalizeAndRefresh();
	}

	private bool ShouldStayVisible => previewPinned || app.Settings.KeepClotheslineVisible || dropSession.ProbeVisible;

	private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
	{
		const int WmNcHitTest = 132;
		const int HtTransparent = -1;
		const int HtClient = 1;
		if (message != WmNcHitTest)
		{
			return IntPtr.Zero;
		}
		long packed = lParam.ToInt64();
		int screenX = unchecked((short)(packed & 65535));
		int screenY = unchecked((short)((packed >> 16) & 65535));
		handled = true;
		return IsInteractivePoint(new System.Windows.Point(screenX, screenY)) ? HtClient : HtTransparent;
	}

	private bool IsInteractivePoint(System.Windows.Point screenPoint)
	{
		if (!IsVisible || !IsLoaded)
		{
			return false;
		}
		System.Windows.Point windowPoint = PointFromScreen(screenPoint);
		if (dropSession.ProbeVisible && new System.Windows.Rect(0,0,ActualWidth,ActualHeight).Contains(windowPoint)) return true;
		foreach (Border border in LineCanvas.Children.OfType<Border>())
		{
			if (!border.IsVisible || border.ActualWidth <= 0.0 || border.ActualHeight <= 0.0)
			{
				continue;
			}
			try
			{
				System.Windows.Rect bounds = border.TransformToAncestor(this).TransformBounds(
					new System.Windows.Rect(0.0, 0.0, border.ActualWidth, border.ActualHeight));
				if (border != DropZone)
				{
					bounds.Inflate(22.0, 22.0);
				}
				if (bounds.Contains(windowPoint))
				{
					return true;
				}
			}
			catch (InvalidOperationException)
			{
			}
		}
		return false;
	}

	public void PrepareToShow()
	{
		pointerEnteredSinceShow = false;
		leaveTimer.Stop();
		dropSession.Reset();
		ApplyDropSession();
	}

	public void SetPreviewPinned(bool pinned)
	{
		previewPinned = pinned;
		if (pinned)
		{
			leaveTimer.Stop();
		}
	}

	public void PlaceOn(Screen screen, double verticalPosition)
	{
		(double X, double Y) tuple = NativeMethods.ScaleAt(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
		double item = tuple.X;
		double item2 = tuple.Y;
		Rectangle workingArea = screen.WorkingArea;
		DesktopLineBounds desktopLineBounds = DesktopLinePlacement.PhysicalBounds(workingArea.Left, workingArea.Top, workingArea.Width, workingArea.Height, item2, verticalPosition);
		nativeBounds = (desktopLineBounds.X, desktopLineBounds.Y, desktopLineBounds.Width, desktopLineBounds.Height);
		base.Width = (double)desktopLineBounds.Width / item;
		base.Height = (double)desktopLineBounds.Height / item2;
		base.Left = (double)desktopLineBounds.X / item;
		base.Top = (double)desktopLineBounds.Y / item2;
		ApplyNativeBounds();
	}

	public void ApplyNativeBounds()
	{
		(int, int, int, int)? tuple = nativeBounds;
		if (tuple.HasValue)
		{
			(int, int, int, int) valueOrDefault = tuple.GetValueOrDefault();
			nint handle = new WindowInteropHelper(this).Handle;
			if (handle != IntPtr.Zero)
			{
				NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST, valueOrDefault.Item1, valueOrDefault.Item2, valueOrDefault.Item3, valueOrDefault.Item4, 80u);
			}
		}
	}

	public void LocalizeAndRefresh()
	{
		DropText.Text = app.Text.T("\ud83d\udccc  拖一件事到这里", "\ud83d\udccc  DRAG SOMETHING HERE");
		RefreshTasks();
	}

	public void RefreshTasks()
	{
		Border[] array = (from x in LineCanvas.Children.OfType<Border>()
			where x != DropZone
			select x).ToArray();
		foreach (Border element in array)
		{
			LineCanvas.Children.Remove(element);
		}
		double num2 = Math.Max(900.0, base.ActualWidth);
		UpdateDesktopRope(num2);
		HangingTask[] array2 = app.Repository.Pending.OrderBy((HangingTask x) => x.lineLayer.GetValueOrDefault()).ToArray();
		foreach (HangingTask task in array2)
		{
			Border border = TaskCardFactory.CreateClotheslineCard(app, task, compact: true, delegate(double ratio)
			{
				app.Repository.Place(task.id, ratio);
			}, (double left) => DesktopCardTop(left, TaskCardFactory.ClotheslineCompactWidth, num2));
			double left = (task.lineX ?? 0.5) * Math.Max(1.0, num2 - border.Width);
			Canvas.SetLeft(border, left);
			Canvas.SetTop(border, DesktopCardTop(left, border.Width, num2));
			System.Windows.Controls.Panel.SetZIndex(border, task.lineLayer.GetValueOrDefault());
			LineCanvas.Children.Add(border);
		}
		Canvas.SetLeft(DropZone, Math.Max(12.0, (num2 - DropZone.Width) / 2.0));
		System.Windows.Controls.Panel.SetZIndex(DropZone, 2000);
	}

	private void UpdateDesktopRope(double width)
	{
		const double ropeY = 105.0;
		const double sag = 13.0;
		PathFigure figure = new PathFigure
		{
			StartPoint = new System.Windows.Point(-10.0, ropeY),
			IsClosed = false
		};
		figure.Segments.Add(new QuadraticBezierSegment(new System.Windows.Point(width / 2.0, ropeY + sag * 2.0), new System.Windows.Point(width + 10.0, ropeY), isStroked: true));
		PathGeometry geometry = new PathGeometry
		{
			Figures = { figure }
		};
		geometry.Freeze();
		DesktopRopeHit.Data = geometry;
		DesktopRopeShadow.Data = geometry;
		DesktopRope.Data = geometry;
		DesktopRopeHighlight.Data = geometry;
	}

	private static double DesktopCardTop(double left, double cardWidth, double ropeWidth)
	{
		double position = Math.Clamp((left + cardWidth / 2.0) / Math.Max(1.0, ropeWidth), 0.0, 1.0);
		return 105.0 + 4.0 * 13.0 * position * (1.0 - position) - 16.0;
	}

	private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
	{
		bool supported = IsVisible && HasSupportedDrop(e.Data);
		dropSession.ObserveDataDrag(supported);
		ApplyDropSession();
		e.Effects = (supported ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None);
		e.Handled = true;
	}

	private void Window_DragLeave(object sender, System.Windows.DragEventArgs e)
	{
		// Ignore child-to-child DragLeave bubbling while still inside the clothesline.
		System.Windows.Point point = e.GetPosition(LineCanvas);
		if (!new System.Windows.Rect(0,0,ActualWidth,ActualHeight).Contains(point))
		{
			dropSession.ObserveDataDrag(false);
			ApplyDropSession();
		}
	}

	private void Window_Drop(object sender, System.Windows.DragEventArgs e)
	{
		if (!IsVisible || !HasSupportedDrop(e.Data))
		{
			dropSession.Reset();
			ApplyDropSession();
			e.Effects=System.Windows.DragDropEffects.None;
			e.Handled=true;
			return;
		}
		suppressExternalWindowCaptureUntil = DateTime.UtcNow.AddSeconds(1.0);
		dropSession.Reset();
		ApplyDropSession();
		System.Windows.Point position = e.GetPosition(LineCanvas);
		double dropRatio = Math.Clamp(position.X / Math.Max(1.0, LineCanvas.ActualWidth), 0.03, 0.97);
		HangingTask[] created;
		if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] source)
		{
			created = source.Where((string item) => File.Exists(item) || Directory.Exists(item)).Select(delegate(string item)
			{
				string name = Path.GetFileName(item.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
				return app.Repository.Add(name, null, "", null, null, item);
			}).ToArray();
		}
		else if (e.Data.GetData(System.Windows.DataFormats.Text) is string text && !string.IsNullOrWhiteSpace(text))
		{
			text = text.Trim();
			if (TaskRepository.IsWebUrl(text))
			{
				created = new HangingTask[1] { app.Repository.Add(new Uri(text).Host, text, "", null, null, null, null, text) };
			}
			else
			{
				string title = text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? text;
				if (title.Length > 120)
				{
					title = title.Substring(0, 117) + "...";
				}
				created = new HangingTask[1] { app.Repository.Add(title, null, text == title ? "" : text) };
			}
		}
		else
		{
			created = Array.Empty<HangingTask>();
		}
		for (int i = 0; i < created.Length; i++)
		{
			double offset = ((double)i - (double)(created.Length - 1) / 2.0) * 0.045;
			app.Repository.Place(created[i].id, Math.Clamp(dropRatio + offset, 0.03, 0.97));
		}
		app.RefreshAll();
		e.Handled = true;
	}

	private static bool HasSupportedDrop(System.Windows.IDataObject data)
	{
		if (data.GetDataPresent(System.Windows.DataFormats.FileDrop))
		{
			return true;
		}
		return data.GetDataPresent(System.Windows.DataFormats.Text) && data.GetData(System.Windows.DataFormats.Text) is string text && !string.IsNullOrWhiteSpace(text);
	}

	private void TrackExternalWindowDrag()
	{
		bool leftButtonDown = (System.Windows.Forms.Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;
		System.Drawing.Point mousePosition = System.Windows.Forms.Control.MousePosition;
		if (!IsVisible || (NativeMethods.GetAsyncKeyState(0x1B) & 0x8000) != 0)
		{
			dropSession.Reset();
			ApplyDropSession();
			leftButtonWasDown = leftButtonDown;
			return;
		}
		if (leftButtonDown && !leftButtonWasDown)
		{
			dropSession.Begin(WindowCaptureService.WindowAt(mousePosition),mousePosition);
		}
		if (leftButtonDown && dropSession.Candidate is CapturableWindow candidate)
		{
			NativeMethods.GetWindowThreadProcessId(candidate.Handle,out uint processId);
			dropSession.Move(mousePosition,processId==candidate.ProcessId && NativeMethods.GetWindowRect(candidate.Handle,out var bounds) ? bounds : null);
			ApplyDropSession();
		}
		if (!leftButtonDown && leftButtonWasDown)
		{
			bool overDropZone = IsOverDropZone(mousePosition);
			CapturableWindow? released = dropSession.FinishPointer();
			ApplyDropSession();
			if (released != null && overDropZone && !windowCapturePending)
			{
				System.Windows.Point localPoint = PointFromScreen(new System.Windows.Point(mousePosition.X, mousePosition.Y));
				double ratio = Math.Clamp(localPoint.X / Math.Max(1.0, ActualWidth), 0.03, 0.97);
				var dropped = WindowCaptureService.Snapshot(released.Handle);
				if (WindowDropReturnService.SameWindow(released, dropped)) CaptureExternalWindow(released, dropped!, ratio);
			}
		}
		leftButtonWasDown = leftButtonDown;
	}

	private void ApplyDropSession()
	{
		bool wasProbing = ExternalDragProbe.Visibility == Visibility.Visible;
		DropZone.Visibility = dropSession.HintVisible ? Visibility.Visible : Visibility.Collapsed;
		ExternalDragProbe.Visibility = dropSession.ProbeVisible ? Visibility.Visible : Visibility.Collapsed;
		DropZone.Opacity = 1.0;
		if (dropSession.ProbeVisible) leaveTimer.Stop();
		else if (wasProbing && IsVisible && pointerEnteredSinceShow && !IsMouseOver && !ShouldStayVisible) leaveTimer.Start();
	}

	private bool IsOverDropZone(System.Drawing.Point screenPoint)
	{
		if (!IsVisible || !dropSession.HintVisible)
		{
			return false;
		}
		System.Windows.Point localPoint = PointFromScreen(new System.Windows.Point(screenPoint.X, screenPoint.Y));
		return new System.Windows.Rect(Canvas.GetLeft(DropZone),Canvas.GetTop(DropZone),DropZone.Width,DropZone.Height).Contains(localPoint);
	}

	private async void CaptureExternalWindow(CapturableWindow candidate, CapturableWindow dropped, double ratio)
	{
		windowCapturePending = true;
		try
		{
			await Task.Delay(220);
			if (DateTime.UtcNow < suppressExternalWindowCaptureUntil)
			{
				return;
			}
			CapturableWindow? window = WindowCaptureService.Snapshot(candidate.Handle);
			if (!WindowDropReturnService.SameWindow(candidate, window))
			{
				return;
			}
			string previewFile = WindowCaptureService.Capture(window!, app.Repository.PreviewDirectory);
			HangingTask task = app.Repository.Add(window.Title, null, "", null, previewFile, null, window.ProcessName);
			WindowCaptureService.SetCapturedTarget(task, window);
			app.Repository.Update(task);
			app.Repository.Place(task.id, ratio);
			WindowDropReturnService.RestoreAfterPin(candidate, dropped);
			app.RefreshAll();
		}
		finally
		{
			windowCapturePending = false;
		}
	}
}
