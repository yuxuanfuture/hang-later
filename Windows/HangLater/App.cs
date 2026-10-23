using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using HangLater.Windows.Models;
using HangLater.Windows.Services;
using HangLater.Windows.Views;

namespace HangLater.Windows;

public partial class App : System.Windows.Application
{
	private const int DwmUseImmersiveDarkMode = 20;

	private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

	private const int DwmBorderColor = 34;

	private const int DwmCaptionColor = 35;

	private const int DwmTextColor = 36;

	private TrayService tray;

	private HotKeyService hotKey;

	private EdgeHoverService edgeHover;

	private ReminderService reminders;

	private DispatcherTimer inboxTimer;

	private bool exiting;

	private Mutex? instanceMutex;

	private EventWaitHandle? instanceSignal;

	private RegisteredWaitHandle? instanceWait;


	public TaskRepository Repository { get; private set; }

	public SettingsRepository SettingsRepository { get; private set; }

	public AppSettings Settings => SettingsRepository.Value;

	public Localizer Text { get; private set; }

	public MainWindow MainWindowInstance { get; private set; }

	public DesktopLineWindow DesktopLine { get; private set; }

	public bool IsExiting => exiting;

	// Isolated native-UI tests provide their own repository and dispatcher;
	// they must never initialize the production singleton, tray or user data.
	internal bool UseIsolatedTestStartup { get; set; }

	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		if (UseIsolatedTestStartup) return;
		string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HangLater");
		if (e.Args.Length != 0 && e.Args[0].StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
		{
			await ChromeBridgeService.RunHostAsync(root, e.Args[0]);
			Shutdown();
			return;
		}
		instanceMutex = new Mutex(initiallyOwned: true, "Local\\HangLater.Windows.Instance", out var createdNew);
		if (!createdNew)
		{
			try
			{
				EventWaitHandle.OpenExisting("Local\\HangLater.Windows.Show").Set();
			}
			catch
			{
			}
			Shutdown();
			return;
		}
		instanceSignal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, "Local\\HangLater.Windows.Show");
		instanceWait = ThreadPool.RegisterWaitForSingleObject(instanceSignal, delegate
		{
			base.Dispatcher.BeginInvoke(new Action(ShowMainWindow));
		}, null, -1, executeOnlyOnce: false);
		try
		{
			Repository = new TaskRepository(root);
			SettingsRepository = new SettingsRepository(root);
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(ex.Message, "Hang Later", MessageBoxButton.OK, MessageBoxImage.Hand);
			Shutdown(1);
			return;
		}
		Text = new Localizer(Settings.Language == "zh");
		ApplyTheme();
		Activated += delegate
		{
			ApplyWindowThemes();
		};
		MainWindowInstance = new MainWindow(this);
		DesktopLine = new DesktopLineWindow(this);
		string text = Path.Combine(AppContext.BaseDirectory, "Resources", "HangLater.ico");
		if (!File.Exists(text))
		{
			text = Path.Combine(AppContext.BaseDirectory, "HangLater.ico");
		}
		tray = new TrayService(text);
		tray.Localize(Text.Chinese);
		tray.ToggleRequested += ToggleDesktopLine;
		tray.OpenRequested += ShowMainWindow;
		tray.IdeaRequested += ShowIdea;
		tray.ReminderRequested += ShowReminder;
		tray.SettingsRequested += ShowSettings;
		tray.ExitRequested += QuitApplication;
		hotKey = new HotKeyService();
		RegisterHotKey();
		edgeHover = new EdgeHoverService();
		edgeHover.Apply(Settings);
		edgeHover.Activated += ShowDesktopLineOn;
		reminders = new ReminderService(() => Repository.Pending, tray);
		reminders.ReminderClicked += OpenTask;
		inboxTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		DateTimeOffset workdayNow = DateTimeOffset.Now;
		DateTime lastCalendarDate = workdayNow.Date;
		var lastWorkdayInterval = CurrentWorkdayInterval(workdayNow);
		inboxTimer.Tick += async delegate
		{
			DateTimeOffset now = DateTimeOffset.Now;
			var interval = CurrentWorkdayInterval(now);
			if (now.Date != lastCalendarDate || interval != lastWorkdayInterval)
			{
				lastCalendarDate = now.Date;
				lastWorkdayInterval = interval;
				MainWindowInstance.LocalizeAndRefresh();
			}
			if (await Repository.ImportBrowserInboxAsync() > 0)
			{
				RefreshAll();
			}
		};
		inboxTimer.Start();
		await Repository.ImportBrowserInboxAsync();
		MainWindowInstance.Show();
		if (Settings.KeepClotheslineVisible)
		{
			ShowDesktopLineOn(Screen.PrimaryScreen);
		}
	}

	private (DateTimeOffset Start, DateTimeOffset End)? CurrentWorkdayInterval(DateTimeOffset now)
	{
		return new WorkdaySchedule(Settings.WorkdayStartMinute, Settings.WorkdayEndMinute,
			Settings.WorkdayEndsFollowingDay).IntervalContaining(now);
	}

	public void ApplySettings(AppSettings settings)
	{
		string globalHotKey = Settings.GlobalHotKey;
		try
		{
			hotKey.Register(settings.GlobalHotKey, ToggleDesktopLine);
		}
		catch
		{
			hotKey.Register(globalHotKey, ToggleDesktopLine);
			throw;
		}
		SettingsRepository.Save(settings);
		Text.Set(settings.Language == "zh");
		ApplyTheme();
		tray.Localize(Text.Chinese);
		edgeHover.Apply(settings);
		MainWindowInstance.LocalizeAndRefresh();
		DesktopLine.LocalizeAndRefresh();
		if (settings.KeepClotheslineVisible)
		{
			ShowDesktopLineOn(Screen.FromPoint(Control.MousePosition));
		}
		else if (!DesktopLine.IsMouseOver)
		{
			DesktopLine.Hide();
		}
	}

	private void RegisterHotKey()
	{
		try
		{
			hotKey.Register(Settings.GlobalHotKey, ToggleDesktopLine);
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(ex.Message, "Hang Later", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	public void ToggleDesktopLine()
	{
		if (DesktopLine.IsVisible)
		{
			DesktopLine.Hide();
		}
		else
		{
			ShowDesktopLineOn(Screen.FromPoint(Control.MousePosition));
		}
	}

	public void ShowDesktopLineOn(Screen screen)
	{
		ShowDesktopLineOn(screen, Settings.ClotheslineVerticalPosition);
	}

	private void ShowDesktopLineOn(Screen screen, double verticalPosition)
	{
		if (!DesktopLine.IsVisible)
		{
			DesktopLine.PrepareToShow();
		}
		DesktopLine.PlaceOn(screen, verticalPosition);
		if (!DesktopLine.IsVisible)
		{
			DesktopLine.Show();
		}
		DesktopLine.ApplyNativeBounds();
		DesktopLine.RefreshTasks();
	}

	public void PreviewDesktopLine(bool visible, double verticalPosition)
	{
		DesktopLine.SetPreviewPinned(visible);
		if (visible)
		{
			ShowDesktopLineOn(Screen.FromPoint(Control.MousePosition), verticalPosition);
		}
		else
		{
			DesktopLine.Hide();
		}
	}

	public void FinishDesktopLinePreview(bool restoreVisible, double verticalPosition)
	{
		DesktopLine.SetPreviewPinned(false);
		if (restoreVisible)
		{
			ShowDesktopLineOn(Screen.FromPoint(Control.MousePosition), verticalPosition);
		}
		else
		{
			DesktopLine.Hide();
		}
	}

	public void CommitDesktopLinePreview()
	{
		DesktopLine.SetPreviewPinned(false);
	}

	public void ShowMainWindow()
	{
		MainWindowInstance.Show();
		MainWindowInstance.WindowState = WindowState.Normal;
		MainWindowInstance.Activate();
		MainWindowInstance.RefreshAll();
	}

	public void ShowIdea()
	{
		if (new IdeaDialog(this)
		{
			Owner = (MainWindowInstance.IsVisible ? MainWindowInstance : null)
		}.ShowDialog() == true)
		{
			RefreshAll();
		}
	}

	public void ShowReminder()
	{
		if (new ReminderDialog(this)
		{
			Owner = (MainWindowInstance.IsVisible ? MainWindowInstance : null)
		}.ShowDialog() == true)
		{
			RefreshAll();
		}
	}

	public void ShowSettings()
	{
		ShowMainWindow();
		SettingsWindow settingsWindow = new SettingsWindow(this);
		settingsWindow.Owner = MainWindowInstance;
		settingsWindow.ShowDialog();
	}

	public void ShowAddSource()
	{
		ShowMainWindow();
		bool restoreLine = DesktopLine.IsVisible;
		DesktopLine.Hide();
		try
		{
			var picker = new AddSourceWindow(this) { Owner = MainWindowInstance };
			if (picker.ShowDialog() == true && picker.Selection is AddSourceChoice choice &&
				AddSourceWorkflow.Run(this, MainWindowInstance, choice)) RefreshAll();
		}
		finally
		{
			if (restoreLine && !IsExiting) { DesktopLine.Show(); DesktopLine.ApplyNativeBounds(); }
		}
	}

	public async void OpenTask(HangingTask task)
	{
		try
		{
			ProcessStartInfo? document = TyporaResumeService.StartInfo(task);
			if (document != null)
			{
				Process.Start(document);
				// Upgrade a uniquely resolved legacy receipt once; later history changes
				// must never redirect it to a different same-named document.
				if (string.IsNullOrWhiteSpace(task.capturedDocumentPath) && document.ArgumentList.Count==1)
				{
					HangingTask resolved = task.Clone();
					resolved.capturedDocumentPath = document.ArgumentList[0];
					Repository.Update(resolved);
				}
				return;
			}
			string text = task.resumeURL ?? task.url ?? task.filePath;
			if (string.IsNullOrWhiteSpace(text))
				text = ObsidianResumeService.TryResolve(task.capturedProcessName ?? task.appName, task.capturedWindowTitle ?? task.title);
			if (!string.IsNullOrWhiteSpace(text))
			{
				Process.Start(new ProcessStartInfo(text)
				{
					UseShellExecute = true
				});
			}
			else if (!string.IsNullOrWhiteSpace(task.appBundleIdentifier) || !string.IsNullOrWhiteSpace(task.capturedProcessName ?? task.appName))
			{
				if (!await WindowCaptureService.ActivateAsync(HangingTaskTarget.FromTask(task)))
				{
					string message = WindowCaptureService.LastActivationStatus switch
					{
						WindowActivationStatus.RestoreRejected => Text.T("Windows 拒绝了恢复这个最小化窗口的请求。可能是应用权限级别不同或应用自身限制；没有修改系统权限。", "Windows rejected restoring this minimized window. The app may have a different privilege level or restrict restoration. System permissions were not changed."),
						WindowActivationStatus.RestoreTimedOut => Text.T("应用窗口仍处于最小化状态，未响应恢复请求。请检查应用是否无响应或正在显示系统提示。", "The app remained minimized and did not respond to restoration. Check whether it is unresponsive or has a system prompt."),
						WindowActivationStatus.ForegroundDenied => Text.T("窗口已找到，但 Windows 或应用没有允许切换到前台。没有重新启动或关闭这个应用。", "The window was found, but Windows or the app did not allow foreground activation. The app was not restarted or closed."),
						_ => Text.T("没有找到可恢复的应用窗口，或应用启动后未出现窗口。请检查应用的安装位置或启动状态。", "No resumable window was found, or the app did not expose a window after startup. Check its installation location or startup state.")
					};
					System.Windows.MessageBox.Show(message, "Hang Later", MessageBoxButton.OK, MessageBoxImage.Information);
				}
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(ex.Message, "Hang Later", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	public void RefreshAll()
	{
		MainWindowInstance.RefreshAll();
		DesktopLine.RefreshTasks();
	}

	public void QuitApplication()
	{
		exiting = true;
		inboxTimer?.Stop();
		reminders = null;
		edgeHover?.Dispose();
		hotKey?.Dispose();
		tray?.Dispose();
		instanceWait?.Unregister(null);
		instanceSignal?.Dispose();
		instanceMutex?.ReleaseMutex();
		instanceMutex?.Dispose();
		DesktopLine.Close();
		MainWindowInstance.Close();
		Shutdown();
	}

	private void ApplyTheme()
	{
		bool lightAppearance = Settings.LightAppearance;
		SetBrush("InkBrush", lightAppearance ? "#252522" : "#EFEFE9");
		SetBrush("AccentBrush", lightAppearance ? "#171715" : "#EFEFE9");
		SetBrush("MutedBrush", lightAppearance ? "#6C6C66" : "#B5B5AE");
		SetBrush("PaperBrush", lightAppearance ? "#F7F8F7" : "#212121");
		SetBrush("SidebarBrush", lightAppearance ? "#ECEDE9" : "#171717");
		SetBrush("PanelBrush", lightAppearance ? "#F2F3EF" : "#262626");
		SetBrush("RaisedBrush", lightAppearance ? "#F4F5F1" : "#282828");
		SetBrush("SearchBrush", lightAppearance ? "#F4F5F2" : "#272727");
		SetBrush("StageBrush", lightAppearance ? "#F7F8F7" : "#212121");
		SetBrush("RopeBrush", "#8C8C8C");
		SetBrush("DropTargetBrush", lightAppearance ? "#F4F5F1" : "#282828");
		SetBrush("BorderBrush", lightAppearance ? "#BFC0BA" : "#464641");
		SetBrush("DividerBrush", lightAppearance ? "#D5D6D2" : "#373734");
		ApplyWindowThemes();
	}

	private void SetBrush(string key, string value)
	{
		Color color = (Color)ColorConverter.ConvertFromString(value);
		if (base.Resources[key] is SolidColorBrush { IsFrozen: false } solidColorBrush)
		{
			solidColorBrush.Color = color;
		}
		else
		{
			base.Resources[key] = new SolidColorBrush(color);
		}
	}

	private void ApplyWindowThemes()
	{
		foreach (Window window in Windows)
		{
			ApplyWindowTheme(window);
		}
	}

	private void ApplyWindowTheme(Window window)
	{
		try
		{
			nint handle = new WindowInteropHelper(window).Handle;
			if (handle == 0)
			{
				return;
			}

			int dark = Settings.LightAppearance ? 0 : 1;
			if (DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
			{
				DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeBefore20H1, ref dark, sizeof(int));
			}

			int caption = ToColorRef(Settings.LightAppearance ? "#F7F8F7" : "#212121");
			int text = ToColorRef(Settings.LightAppearance ? "#252522" : "#EFEFE9");
			int border = ToColorRef(Settings.LightAppearance ? "#D5D6D2" : "#373734");
			DwmSetWindowAttribute(handle, DwmCaptionColor, ref caption, sizeof(int));
			DwmSetWindowAttribute(handle, DwmTextColor, ref text, sizeof(int));
			DwmSetWindowAttribute(handle, DwmBorderColor, ref border, sizeof(int));
		}
		catch
		{
			// Older Windows builds can ignore unsupported DWM attributes.
		}
	}

	private static int ToColorRef(string value)
	{
		Color color = (Color)ColorConverter.ConvertFromString(value);
		return color.R | (color.G << 8) | (color.B << 16);
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int valueSize);
}
