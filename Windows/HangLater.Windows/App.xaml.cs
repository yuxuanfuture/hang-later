using System.Diagnostics;
using System.Windows;
using HangLater.Windows.Models;
using HangLater.Windows.Services;
using HangLater.Windows.Views;
using FormsScreen = System.Windows.Forms.Screen;

namespace HangLater.Windows;

public partial class App : System.Windows.Application
{
    public TaskRepository Repository { get; private set; } = null!;
    public SettingsRepository SettingsRepository { get; private set; } = null!;
    public AppSettings Settings => SettingsRepository.Value;
    public Localizer Text { get; private set; } = null!;
    public MainWindow MainWindowInstance { get; private set; } = null!;
    public DesktopLineWindow DesktopLine { get; private set; } = null!;
    private TrayService tray = null!;
    private HotKeyService hotKey = null!;
    private EdgeHoverService edgeHover = null!;
    private ReminderService reminders = null!;
    private System.Windows.Threading.DispatcherTimer inboxTimer = null!;
    private bool exiting;
    private Mutex? instanceMutex;
    private EventWaitHandle? instanceSignal;
    private RegisteredWaitHandle? instanceWait;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HangLater");
        if (e.Args.Length > 0 && e.Args[0].StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
        {
            await ChromeBridgeService.RunHostAsync(root, e.Args[0]); Shutdown(); return;
        }
        instanceMutex = new Mutex(true, @"Local\HangLater.Windows.Instance", out var firstInstance);
        if (!firstInstance)
        {
            try { EventWaitHandle.OpenExisting(@"Local\HangLater.Windows.Show").Set(); } catch { }
            Shutdown(); return;
        }
        instanceSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\HangLater.Windows.Show");
        instanceWait = ThreadPool.RegisterWaitForSingleObject(instanceSignal, (_, _) => Dispatcher.BeginInvoke(ShowMainWindow), null, Timeout.Infinite, false);
        try
        {
            Repository = new TaskRepository(root);
            SettingsRepository = new SettingsRepository(root);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Hang Later", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); return;
        }
        Text = new Localizer(Settings.Language == "zh"); ApplyTheme();
        MainWindowInstance = new MainWindow(this);
        DesktopLine = new DesktopLineWindow(this);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "HangLater.ico");
        if (!File.Exists(iconPath)) iconPath = Path.Combine(AppContext.BaseDirectory, "HangLater.ico");
        tray = new TrayService(iconPath); tray.Localize(Text.Chinese);
        tray.ToggleRequested += ToggleDesktopLine;
        tray.OpenRequested += ShowMainWindow;
        tray.IdeaRequested += ShowIdea;
        tray.ReminderRequested += ShowReminder;
        tray.SettingsRequested += ShowSettings;
        tray.ExitRequested += QuitApplication;
        hotKey = new HotKeyService(); RegisterHotKey();
        edgeHover = new EdgeHoverService(); edgeHover.Apply(Settings); edgeHover.Activated += ShowDesktopLineOn;
        reminders = new ReminderService(() => Repository.Pending, tray); reminders.ReminderClicked += OpenTask;
        inboxTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        inboxTimer.Tick += async (_, _) => { if (await Repository.ImportBrowserInboxAsync() > 0) RefreshAll(); };
        inboxTimer.Start();
        await Repository.ImportBrowserInboxAsync();
        MainWindowInstance.Show();
        if (Settings.KeepClotheslineVisible) ShowDesktopLineOn(FormsScreen.PrimaryScreen!);
    }

    public void ApplySettings(AppSettings settings)
    {
        var previousHotKey = Settings.GlobalHotKey;
        try { hotKey.Register(settings.GlobalHotKey, ToggleDesktopLine); }
        catch { hotKey.Register(previousHotKey, ToggleDesktopLine); throw; }
        SettingsRepository.Save(settings); Text.Set(settings.Language == "zh"); ApplyTheme(); tray.Localize(Text.Chinese);
        edgeHover.Apply(settings);
        MainWindowInstance.LocalizeAndRefresh(); DesktopLine.LocalizeAndRefresh();
        if (settings.KeepClotheslineVisible) ShowDesktopLineOn(FormsScreen.FromPoint(System.Windows.Forms.Control.MousePosition));
        else if (!DesktopLine.IsMouseOver) DesktopLine.Hide();
    }

    private void RegisterHotKey()
    {
        try { hotKey.Register(Settings.GlobalHotKey, ToggleDesktopLine); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Hang Later", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    public void ToggleDesktopLine()
    {
        if (DesktopLine.IsVisible) DesktopLine.Hide();
        else ShowDesktopLineOn(FormsScreen.FromPoint(System.Windows.Forms.Control.MousePosition));
    }
    public void ShowDesktopLineOn(FormsScreen screen)
    {
        DesktopLine.PlaceOn(screen, Settings.ClotheslineVerticalPosition); DesktopLine.Show(); DesktopLine.Activate(); DesktopLine.RefreshTasks();
    }
    public void ShowMainWindow()
    {
        MainWindowInstance.Show(); MainWindowInstance.WindowState = WindowState.Normal; MainWindowInstance.Activate(); MainWindowInstance.RefreshAll();
    }
    public void ShowIdea() { var dialog = new IdeaDialog(this) { Owner = MainWindowInstance.IsVisible ? MainWindowInstance : null }; if (dialog.ShowDialog() == true) RefreshAll(); }
    public void ShowReminder() { var dialog = new ReminderDialog(this) { Owner = MainWindowInstance.IsVisible ? MainWindowInstance : null }; if (dialog.ShowDialog() == true) RefreshAll(); }
    public void ShowSettings() { ShowMainWindow(); var dialog = new SettingsWindow(this) { Owner = MainWindowInstance }; dialog.ShowDialog(); }
    public void ShowAddSource() { ShowMainWindow(); var dialog = new AddSourceWindow(this) { Owner = MainWindowInstance }; if (dialog.ShowDialog() == true) RefreshAll(); }
    public void OpenTask(HangingTask task)
    {
        try
        {
            var target = task.resumeURL ?? task.url ?? task.filePath;
            if (!string.IsNullOrWhiteSpace(target)) Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            else if (!string.IsNullOrWhiteSpace(task.appBundleIdentifier)) WindowCaptureService.Activate(new(task.appBundleIdentifier));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Hang Later", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    public void RefreshAll() { MainWindowInstance.RefreshAll(); DesktopLine.RefreshTasks(); }
    public void QuitApplication()
    {
        exiting = true; inboxTimer?.Stop(); reminders = null!; edgeHover?.Dispose(); hotKey?.Dispose(); tray?.Dispose(); instanceWait?.Unregister(null); instanceSignal?.Dispose(); instanceMutex?.ReleaseMutex(); instanceMutex?.Dispose(); DesktopLine.Close(); MainWindowInstance.Close(); Shutdown();
    }
    public bool IsExiting => exiting;

    private void ApplyTheme()
    {
        var light = Settings.LightAppearance;
        SetBrush("InkBrush", light ? "#171715" : "#F1F0EA");
        SetBrush("MutedBrush", light ? "#65645F" : "#A9A8A2");
        SetBrush("PaperBrush", light ? "#F3F2EC" : "#1F1F1F");
        SetBrush("PanelBrush", light ? "#ECEAE2" : "#262626");
        SetBrush("RaisedBrush", light ? "#E2E0D7" : "#2D2D2D");
        SetBrush("BorderBrush", light ? "#C7C4B9" : "#44433F");
    }
    private void SetBrush(string key, string value)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
        if (Resources[key] is System.Windows.Media.SolidColorBrush brush && !brush.IsFrozen) brush.Color = color;
        else Resources[key] = new System.Windows.Media.SolidColorBrush(color);
    }
}
