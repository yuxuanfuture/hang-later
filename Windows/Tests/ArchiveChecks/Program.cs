using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HangLater.Windows;
using HangLater.Windows.Controls;
using HangLater.Windows.Models;
using HangLater.Windows.Services;
using HangLater.Windows.Views;

internal static class Program
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main()
    {
        CheckGrouping();
        App app = new(); app.InitializeComponent();
        string directory = Path.Combine(Path.GetTempPath(), "HangLater-ArchiveChecks-" + Guid.NewGuid().ToString("N"));
        TaskRepository repository = new(directory);
        SettingsRepository settings = new(directory);
        settings.Value.Language = "zh"; settings.Value.LightAppearance = true;
        Set(app, "Repository", repository); Set(app, "SettingsRepository", settings); Set(app, "Text", new Localizer(true));
        // Neutral thumbnails keep the fixture independent of personal screenshots.
        for (int index = 0; index < 2; index++)
        {
            DrawingVisual visual = new();
            using (var drawing = visual.RenderOpen()) drawing.DrawRectangle(index == 0 ? Brushes.SlateGray : Brushes.DarkSlateBlue, null, new Rect(0,0,57,45));
            RenderTargetBitmap bitmap = new(57,45,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
            PngBitmapEncoder png = new(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream stream = File.Create(Path.Combine(repository.PreviewDirectory, $"reference-thumbnail-{index}.png")); png.Save(stream);
        }
        int[] counts = { 2, 1, 9, 9, 2, 1, 9, 10 };
        for (int day = 0; day < counts.Length; day++)
        for (int item = 0; item < counts[day]; item++)
        {
            string title = day == 0 ? item == 0 ? "PolarSeeker/ABSeeker-4B-RL · Hugging Face" : "欢迎" : $"归档检查 {18 - day} 日 / {item + 1}";
            var task = repository.Add(title, appName: item == 0 ? "chrome" : "code", previewFile: $"reference-thumbnail-{item % 2}.png");
            DateTime date = new(2026, 10, 18 - day);
            task.createdAt = new DateTimeOffset(day == 0 && item == 1 ? new DateTime(2026,10,16,21,43,0) : date.AddHours(13).AddMinutes(34));
            repository.Complete(task.id, new DateTimeOffset(date.AddHours(19).AddMinutes(17).AddSeconds(30 - item)));
        }
        MainWindow window = new(app); Set(app, "MainWindowInstance", window); Set(app, "DesktopLine", new DesktopLineWindow(app));
        ((Button)window.FindName("CompletedButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        CompletedArchiveView view = (CompletedArchiveView)window.FindName("CompletedPage");
        StackPanel sections = (StackPanel)view.FindName("ArchiveSections");
        void Layout(double width = 906)
        {
            view.Measure(new Size(width,590)); view.Arrange(new Rect(0,0,width,590)); view.UpdateLayout();
        }
        void Refresh() { view.Refresh(app); Layout(); }
        void Tab(string name) { ((RadioButton)view.FindName(name)).IsChecked = true; Layout(); }
        Refresh();
        Check(repository.Completed.Count() == 43 && sections.Children.Count == 8, "Fixture has 43 completed records across eight days");
        Check(((Expander)sections.Children[0]).IsExpanded && !((Expander)sections.Children[1]).IsExpanded, "Only the latest group is expanded by default");
        ((Expander)sections.Children[1]).IsExpanded = true;
        Tab("WeekTab"); Check(view.Grouping == CompletionGrouping.Week && sections.Children.Count == 2, "Week tab groups by calendar week");
        Check(((Expander)sections.Children[0]).IsExpanded && !((Expander)sections.Children[1]).IsExpanded, "Week mode independently expands only its first group");
        Tab("DayTab"); Check(((Expander)sections.Children[1]).IsExpanded, "Returning to Day preserves its group expansion state");
        ((Expander)sections.Children[1]).IsExpanded = false;
        Tab("MonthTab"); Check(view.Grouping == CompletionGrouping.Month && sections.Children.Count == 1, "Month tab combines all October completions");
        Tab("DayTab");
        view.Refresh(app, "PolarSeeker"); Layout(); Check(sections.Children.Count == 1, "Archive search filters groups without changing the total record count");
        view.Refresh(app, "missing-fixture"); Layout(); Check(sections.Children.Count == 0 && ((TextBlock)view.FindName("ArchiveEmpty")).Visibility == Visibility.Visible, "Unmatched search has a readable empty state");
        Refresh();
        CompletedTaskRow first = ((StackPanel)((Expander)sections.Children[0]).Content).Children.OfType<CompletedTaskRow>().First();
        var original = repository.Tasks.First(task => task.title.StartsWith("PolarSeeker"));
        DateTimeOffset completed = original.completedAt!.Value;
        first.RestoreButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout();
        Check(repository.Pending.Count() == 1 && repository.Completed.Count() == 42, "Hang again returns the record to pending without deleting it");
        repository.Complete(original.id, completed); window.RefreshAll(); Refresh();
        Check(repository.Completed.Count() == 43, "Fixture restoration preserves the archive record");
        foreach (bool light in new[] { true, false })
        {
            settings.Value.LightAppearance = light;
            typeof(App).GetMethod("ApplyTheme", Private)!.Invoke(app, null);
            Refresh();
            Save(view, $"qa-archive-fixture-{(light ? "light" : "dark")}.png");
        }
        foreach (double width in new[] { 752.0, 1100.0 })
        {
            Layout(width);
            foreach (CompletedTaskRow row in ((StackPanel)((Expander)sections.Children[0]).Content).Children)
            {
                Point titleRight = row.TitleLabel.TranslatePoint(new Point(row.TitleLabel.ActualWidth,0), row);
                Point actionLeft = row.RestoreButton.TranslatePoint(new Point(0,0),row);
                Check(row.TitleLabel.ActualWidth > 80 && titleRight.X < actionLeft.X, $"Title/action remain separate at {width:F0} DIP");
            }
        }
        Check(repository.Tasks.Count == 43, "All visual and interaction checks use only isolated fixture records");
        Console.WriteLine("PASS: all completion archive checks. Fixture data: " + directory);
        app.Shutdown();
    }

    private static void CheckGrouping()
    {
        HangingTask Task(int year, int month, int day, int hour = 12) => new() { completedAt = new DateTimeOffset(new DateTime(year,month,day,hour,0,0)) };
        var tasks = new[] { Task(2027,1,1), Task(2026,12,31), Task(2026,12,28), new HangingTask() };
        Check(CompletionArchive.Sections(tasks, CompletionGrouping.Day).Count == 3, "Pending tasks never appear in the completion archive");
        var months = CompletionArchive.Sections(tasks, CompletionGrouping.Month);
        Check(months.Count == 2 && months[0].Start == new DateTime(2027,1,1), "Month grouping separates years and sorts newest first");
        var weeks = CompletionArchive.Sections(tasks, CompletionGrouping.Week, DayOfWeek.Monday);
        Check(weeks.Count == 1 && weeks[0].Start == new DateTime(2026,12,28), "Calendar weeks correctly span year boundaries");
        Check(CompletionArchive.Sections(new[] { Task(2026,10,18) }, CompletionGrouping.Week, DayOfWeek.Sunday)[0].Start == new DateTime(2026,10,18), "Week start follows the configured calendar, including Sunday");
        Check(CompletionArchive.Title(new DateTime(2026,10,1), CompletionGrouping.Month, true) == "2026年10月", "Chinese month title is localized, not English");
        Check(CompletionArchive.Title(new DateTime(2026,12,28), CompletionGrouping.Week, true) == "12月28日 – 1月3日", "Week title displays its full date range across years");
        Check(CompletionArchive.Sections(new[] { Task(2026,10,18,9), Task(2026,10,18,17) }, CompletionGrouping.Day)[0].Tasks[0].completedAt!.Value.Hour == 17, "Tasks within each group sort by completion time descending");
    }

    private static void Save(FrameworkElement view, string name)
    {
        RenderTargetBitmap bitmap = new(906,590,96,96,PixelFormats.Pbgra32); bitmap.Render(view);
        PngBitmapEncoder png = new(); png.Frames.Add(BitmapFrame.Create(bitmap));
        string path = Path.Combine(Environment.CurrentDirectory,name);
        using FileStream stream = File.Create(path); png.Save(stream); Console.WriteLine("Fixture render: " + path);
    }
    private static void Set(object instance, string name, object value) => instance.GetType().GetProperty(name)!.SetValue(instance,value);
    private static void Check(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); Console.WriteLine("PASS: " + message); }
}
