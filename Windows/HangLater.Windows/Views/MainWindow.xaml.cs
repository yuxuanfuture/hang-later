using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ellipse = System.Windows.Shapes.Ellipse;
using Polygon = System.Windows.Shapes.Polygon;
using HangLater.Windows.Controls;
using HangLater.Windows.Models;
using HangLater.Windows.Services;
using Microsoft.Win32;

namespace HangLater.Windows.Views;

public partial class MainWindow : Window
{
    private readonly App app;
    private enum Page { Clothesline, Todo, Completed, Spike }
    private Page page = Page.Clothesline;
    private readonly HashSet<DateTime> collapsedSections = [];
    private CompletionGrouping grouping = CompletionGrouping.Day;
    private double spikeRotation;
    private Point? spikeDrag;

    public MainWindow(App app)
    {
        this.app = app; InitializeComponent(); CompletionGroupingBox.SelectedIndex = 0;
        Closing += (_, e) => { if (!app.IsExiting) { e.Cancel = true; Hide(); } };
        SizeChanged += (_, _) => { if (page == Page.Clothesline) RefreshClothesline(); };
        SpikeCanvas.MouseLeftButtonDown += (_, e) => { spikeDrag = e.GetPosition(SpikeCanvas); SpikeCanvas.CaptureMouse(); };
        SpikeCanvas.MouseMove += (_, e) =>
        {
            if (spikeDrag is null || e.LeftButton != MouseButtonState.Pressed) return;
            var point = e.GetPosition(SpikeCanvas); spikeRotation += (point.X - spikeDrag.Value.X) / 15; spikeDrag = point; RotateSpikeReceipts();
        };
        SpikeCanvas.MouseLeftButtonUp += (_, _) => { spikeDrag = null; SpikeCanvas.ReleaseMouseCapture(); };
        LocalizeAndRefresh();
    }
    public void LocalizeAndRefresh()
    {
        var t = app.Text; SpaceLabel.Text = t.T("我的空间", "MY SPACE");
        ClotheslineButton.Content = t.T("☰　晾衣绳", "☰　Clothesline");
        TodoButton.Content = $"☷　{t.T("待办清单", "To-do list")}　 {app.Repository.Pending.Count()}";
        CompletedButton.Content = t.T("✓　已完成", "✓　Completed");
        SpikeButton.Content = $"📌　{t.T("完成钉", "Done Spike")}　 {EligibleSpikeTasks().Count()}";
        ThemeButton.Content = app.Settings.LightAppearance ? t.T("☾　黑夜", "☾　Dark") : t.T("☀　白天", "☀　Light");
        LanguageButton.Content = app.Text.Chinese ? "文　EN" : "文　中文";
        SettingsButton.Content = t.T("⚙　设置", "⚙　Settings"); ChromeButton.Content = t.T("↪　连接 Chrome", "↪　Connect Chrome");
        AddButton.Content = t.T("＋ 晾一件事", "＋ Hang something"); SearchBox.ToolTip = t.T("找一件事…", "Find something…"); DropLabel.Text = t.T("📌  拖一件事到这里", "📌  DRAG SOMETHING HERE");
        DateLabel.Text = DateTime.Now.ToString(app.Text.Chinese ? "M月d日 dddd" : "dddd, MMMM d", CultureInfo.CurrentCulture);
        RefreshAll();
    }
    public void RefreshAll()
    {
        TodoButton.Content = $"☷　{app.Text.T("待办清单", "To-do list")}　 {app.Repository.Pending.Count()}";
        SpikeButton.Content = $"📌　{app.Text.T("完成钉", "Done Spike")}　 {EligibleSpikeTasks().Count()}";
        switch (page) { case Page.Clothesline: RefreshClothesline(); break; case Page.Todo: RefreshTodo(); break; case Page.Completed: RefreshCompleted(); break; case Page.Spike: RefreshSpike(); break; }
    }
    private IEnumerable<HangingTask> Filter(IEnumerable<HangingTask> tasks)
    {
        var query = SearchBox.Text.Trim(); return query.Length == 0 ? tasks : tasks.Where(x => x.title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || x.note.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }
    private void Switch(Page next)
    {
        page = next; ClotheslinePage.Visibility = next == Page.Clothesline ? Visibility.Visible : Visibility.Collapsed; TodoPage.Visibility = next == Page.Todo ? Visibility.Visible : Visibility.Collapsed; CompletedPage.Visibility = next == Page.Completed ? Visibility.Visible : Visibility.Collapsed; SpikePage.Visibility = next == Page.Spike ? Visibility.Visible : Visibility.Collapsed; PageActions.Children.Clear(); RefreshAll();
    }
    private void RefreshClothesline()
    {
        PageTitle.Text = app.Text.T("给重要的事，留一个位置。", "GIVE WHAT MATTERS A PLACE."); PageSubtitle.Text = app.Text.T("网页、视频、文件、App 窗口和生活里的小事。", "Pages, videos, files, app windows, and everyday tasks.");
        var tasks = Filter(app.Repository.Pending).ToArray(); HangingNowLabel.Text = app.Text.T($"●  现在挂着　{tasks.Length} 件", $"●  HANGING NOW　{tasks.Length} ITEMS");
        PageActions.Children.Clear(); var complete = new Button { Content = app.Text.T("全部完成", "Complete all"), Margin = new Thickness(0, 0, 8, 0) }; complete.Click += (_, _) => { app.Repository.CompleteAll(tasks.Select(x => x.id)); app.RefreshAll(); }; PageActions.Children.Add(complete);
        var delete = new Button { Content = app.Text.T("全部删除", "Delete all") }; delete.Click += (_, _) => { if (MessageBox.Show(app.Text.T("删除当前所有纸片？", "Delete all current slips?"), "Hang Later", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { app.Repository.DeleteAll(tasks.Select(x => x.id)); app.RefreshAll(); } }; PageActions.Children.Add(delete);
        foreach (var child in ClotheslineCanvas.Children.OfType<Border>().Where(x => x != DropTarget).ToArray()) ClotheslineCanvas.Children.Remove(child);
        var canvasWidth = Math.Max(900, ClotheslineCanvas.ActualWidth); var yBase = 100d;
        foreach (var task in tasks.OrderBy(x => x.lineLayer ?? 0))
        {
            var card = TaskCardFactory.CreateClotheslineCard(app, task, true, ratio => app.Repository.Place(task.id, ratio));
            var x = (task.lineX ?? .5) * Math.Max(1, canvasWidth - card.Width); Canvas.SetLeft(card, x); Canvas.SetTop(card, yBase + (task.id.GetHashCode() & 31)); Panel.SetZIndex(card, task.lineLayer ?? 0); ClotheslineCanvas.Children.Add(card);
        }
    }
    private void RefreshTodo()
    {
        PageTitle.Text = app.Text.T("一件一件，慢慢来。", "ONE THING AT A TIME."); PageSubtitle.Text = app.Text.T("想看的网页、要学的内容，还有生活里的小事。", "Pages, videos, files, app windows, and everyday tasks."); TodoPanel.Children.Clear();
        foreach (var task in Filter(app.Repository.Pending)) TodoPanel.Children.Add(TaskCardFactory.CreateListCard(app, task));
    }
    private void RefreshCompleted()
    {
        PageTitle.Text = app.Text.T("这些事情，你已经完成了。", "THE THINGS YOU FINISHED."); PageSubtitle.Text = app.Text.T("每一个完成，都是一点进展。", "Every finished task is a little progress."); CompletedPanel.Children.Clear();
        foreach (var section in GroupCompleted(Filter(app.Repository.Completed)))
        {
            var expander = new Expander { IsExpanded = !collapsedSections.Contains(section.Start), Margin = new Thickness(0, 0, 0, 16) };
            expander.Header = new TextBlock { Text = SectionTitle(section.Start) + $"　　　　　　　　 {section.Tasks.Count} {app.Text.T("件", "items")}", FontFamily = (FontFamily)app.Resources["DisplayFont"], FontSize = 19, FontWeight = FontWeights.Bold };
            var list = new StackPanel(); foreach (var task in section.Tasks) list.Children.Add(TaskCardFactory.CreateListCard(app, task, true)); expander.Content = list;
            expander.Collapsed += (_, _) => collapsedSections.Add(section.Start); expander.Expanded += (_, _) => collapsedSections.Remove(section.Start); CompletedPanel.Children.Add(expander);
        }
    }
    private void RefreshSpike()
    {
        PageTitle.Text = app.Text.T("把完成的纸片，串在这里。", "KEEP FINISHED SLIPS ON THE SPIKE."); PageSubtitle.Text = app.Text.T("拖纸片改变穿钉位置，也可以取下再串回。", "Move slips on the spike, or take one off and put it back.");
        SpikeCanvas.Children.Clear(); SpikeList.Children.Clear(); OffSpikeList.Children.Clear(); var all = EligibleSpikeTasks().ToArray(); var on = all.Where(x => x.IsOnSpike).ToArray(); var off = all.Where(x => !x.IsOnSpike).ToArray();
        OnSpikeLabel.Text = app.Text.T($"钉上的纸片　{on.Length}", $"ON THE SPIKE　{on.Length}"); OffSpikeLabel.Text = app.Text.T($"已取下　{off.Length}", $"TAKEN OFF　{off.Length}");
        var baseEllipse = new Ellipse { Width = 250, Height = 80, Fill = new LinearGradientBrush(Colors.LightGray, Colors.DimGray, 90), Stroke = Brushes.White, StrokeThickness = 1 }; Canvas.SetLeft(baseEllipse, 165); Canvas.SetBottom(baseEllipse, 35); SpikeCanvas.Children.Add(baseEllipse);
        var spike = new Polygon { Fill = Brushes.Silver, Points = new PointCollection([new Point(286, 80), new Point(298, 410), new Point(310, 80)]) }; Canvas.SetBottom(spike, 85); SpikeCanvas.Children.Add(spike);
        var index = 0;
        foreach (var task in on.OrderBy(x => x.spikeY ?? 0))
        {
            var scale = task.EffectiveSpikeScale; var receipt = Receipt(task, scale); receipt.Tag = (index % 2 == 0) ? -4d : 5d; var x = 80 + (task.spikeX ?? ((index % 3) - 1) * .18) * 90; var y = 470 - index * 34 - (task.spikeY ?? 0) * 14; Canvas.SetLeft(receipt, x); Canvas.SetTop(receipt, Math.Max(60, y)); receipt.RenderTransform = new RotateTransform(spikeRotation + (double)receipt.Tag, 190 * scale, 45 * scale); SpikeCanvas.Children.Add(receipt); SpikeList.Children.Add(SpikeRow(task, true)); index++;
        }
        foreach (var task in off) OffSpikeList.Children.Add(SpikeRow(task, false));
    }
    private void RotateSpikeReceipts()
    {
        foreach (var receipt in SpikeCanvas.Children.OfType<Border>().Where(x => x.Tag is double))
            receipt.RenderTransform = new RotateTransform(spikeRotation + (double)receipt.Tag, receipt.Width / 2, receipt.Height / 2);
    }
    private Border SpikeRow(HangingTask task, bool on)
    {
        var border = new Border { BorderBrush = (Brush)app.Resources["BorderBrush"], BorderThickness = new Thickness(1), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 10) };
        var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = task.title, FontWeight = FontWeights.Bold });
        if (on)
        {
            var slider = new Slider { Minimum = .6, Maximum = 1.5, Value = task.EffectiveSpikeScale, TickFrequency = .1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 10, 0, 6) };
            slider.PreviewMouseLeftButtonUp += (_, _) => { app.Repository.ResizeSpike(task.id, slider.Value); RefreshSpike(); }; stack.Children.Add(slider);
            var button = new Button { Content = app.Text.T("取下", "Take off") }; button.Click += (_, _) => { app.Repository.SetSpike(task.id, false); app.RefreshAll(); }; stack.Children.Add(button);
        }
        else { var button = new Button { Content = app.Text.T("串回", "Put back") }; button.Click += (_, _) => { app.Repository.SetSpike(task.id, true); app.RefreshAll(); }; stack.Children.Add(button); }
        border.Child = stack; return border;
    }
    private Border Receipt(HangingTask task, double scale)
    {
        var type = app.Repository.TaskTypes.FirstOrDefault(x => x.id == task.taskTypeID); var color = type is null ? Color.FromRgb(234, 228, 211) : Color.FromRgb((byte)(type.colorHex >> 16), (byte)(type.colorHex >> 8), (byte)type.colorHex);
        var border = new Border { Width = 380 * scale, Height = 90 * scale, Background = new SolidColorBrush(color), BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Padding = new Thickness(14 * scale) };
        border.Child = new TextBlock { Text = task.title + "\n" + task.createdAt.LocalDateTime.ToString("g") + "  →  " + task.completedAt?.LocalDateTime.ToString("g"), Foreground = (color.R + color.G + color.B) > 430 ? Brushes.Black : Brushes.White, FontSize = 12 * scale, FontWeight = FontWeights.Bold }; return border;
    }
    private IEnumerable<HangingTask> EligibleSpikeTasks()
    {
        var schedule = new WorkdaySchedule(app.Settings.WorkdayStartMinute, app.Settings.WorkdayEndMinute, app.Settings.WorkdayEndsFollowingDay); var interval = schedule.IntervalContaining(DateTimeOffset.Now);
        return interval is null ? [] : app.Repository.Completed.Where(x => x.createdAt >= interval.Value.Start && x.createdAt < interval.Value.End);
    }
    private sealed record CompletedSection(DateTime Start, List<HangingTask> Tasks);
    private IEnumerable<CompletedSection> GroupCompleted(IEnumerable<HangingTask> source)
    {
        DateTime Key(HangingTask task)
        {
            var date = task.completedAt!.Value.LocalDateTime;
            if (grouping == CompletionGrouping.Month) return new(date.Year, date.Month, 1);
            if (grouping == CompletionGrouping.Week) { var diff = ((int)date.DayOfWeek + 6) % 7; return date.Date.AddDays(-diff); }
            return date.Date;
        }
        return source.GroupBy(Key).OrderByDescending(x => x.Key).Select(x => new CompletedSection(x.Key, x.OrderByDescending(y => y.completedAt).ToList()));
    }
    private string SectionTitle(DateTime date) => grouping switch { CompletionGrouping.Month => date.ToString("MMMM yyyy"), CompletionGrouping.Week => app.Text.T($"{date:M} 起的一周", $"Week of {date:MMM d}"), _ => date.ToString(app.Text.Chinese ? "M月d日 dddd" : "dddd, MMMM d") };
    private void AddFile(string path)
    {
        if (!File.Exists(path)) return; app.Repository.Add(Path.GetFileName(path), filePath: path); app.RefreshAll();
    }
    private void Clothesline_Click(object s, RoutedEventArgs e) => Switch(Page.Clothesline);
    private void Todo_Click(object s, RoutedEventArgs e) => Switch(Page.Todo);
    private void Completed_Click(object s, RoutedEventArgs e) => Switch(Page.Completed);
    private void Spike_Click(object s, RoutedEventArgs e) => Switch(Page.Spike);
    private void Settings_Click(object s, RoutedEventArgs e) => app.ShowSettings();
    private void Add_Click(object s, RoutedEventArgs e) => app.ShowAddSource();
    private void Theme_Click(object s, RoutedEventArgs e) { var v = app.Settings.Clone(); v.LightAppearance = !v.LightAppearance; app.ApplySettings(v); }
    private void Language_Click(object s, RoutedEventArgs e) { var v = app.Settings.Clone(); v.Language = v.Language == "zh" ? "en" : "zh"; app.ApplySettings(v); }
    private void Chrome_Click(object s, RoutedEventArgs e)
    {
        var path = ChromeBridgeService.Install(app.Repository.RootDirectory, Environment.ProcessPath!);
        var extension = Path.Combine(AppContext.BaseDirectory, "ChromeExtension");
        MessageBox.Show(app.Text.T($"Chrome 已连接。\n\n在 chrome://extensions 开启开发者模式并加载：\n{extension}", $"Chrome is connected.\n\nEnable Developer mode at chrome://extensions and load:\n{extension}"), "Hang Later");
    }
    private void Search_Changed(object s, TextChangedEventArgs e) { if (IsLoaded) RefreshAll(); }
    private void Grouping_Changed(object s, SelectionChangedEventArgs e) { grouping = (CompletionGrouping)Math.Max(0, CompletionGroupingBox.SelectedIndex); if (IsLoaded && page == Page.Completed) RefreshCompleted(); }
    private void Drop_DragOver(object s, System.Windows.DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private void Drop_Drop(object s, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files) foreach (var file in files) AddFile(file);
        else if (e.Data.GetData(DataFormats.Text) is string text && TaskRepository.IsWebUrl(text)) app.Repository.Add(new Uri(text).Host, url: text, resumeUrl: text);
        app.RefreshAll();
    }
}
