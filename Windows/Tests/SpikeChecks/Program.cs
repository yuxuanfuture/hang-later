using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
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
        CheckCompletionEligibility();
        HangLater.Windows.App app = new HangLater.Windows.App();
        app.InitializeComponent();
        app.Resources["DisplayFont"] = new FontFamily("Arial");
        app.Resources["MonoFont"] = new FontFamily("Courier New");
        string directory = Path.Combine(Path.GetTempPath(), "HangLater-SpikeChecks-" + Guid.NewGuid().ToString("N"));
        TaskRepository repository = new TaskRepository(directory);
        var task = repository.Add("3D SPIKE POSITION CHECK");
        repository.Complete(task.id);
        ReceiptSpike3DView view = new ReceiptSpike3DView();
        view.Measure(new Size(400, 400)); view.Arrange(new Rect(0, 0, 400, 400)); view.UpdateLayout();
        view.Update(new[] { task }, true, false, new Dictionary<Guid, uint>());
        var papers = (IList)Field(view, "papers");
        Check(papers.Count == 1, "One completed task creates one 3D paper");
        object paper = papers[0]!;
        GeometryModel3D model = (GeometryModel3D)Property(paper, "Model");
        MeshGeometry3D mesh = (MeshGeometry3D)model.Geometry;
        Check(mesh.Positions.Count == 153 && mesh.TriangleIndices.Count == 768, "Upstream 16 x 8 curved mesh topology");
        Model3DGroup assembly = (Model3DGroup)Field(view, "assembly");
        Viewport3D viewport = (Viewport3D)Field(view, "viewport");
        PerspectiveCamera camera = (PerspectiveCamera)viewport.Camera;
        Point3D local = model.Transform.Transform(mesh.Positions[76]);
        Point3D world = assembly.Transform.Transform(local);
        Vector3D forward = camera.LookDirection; forward.Normalize();
        Vector3D right = Vector3D.CrossProduct(forward, camera.UpDirection); right.Normalize();
        Vector3D up = Vector3D.CrossProduct(right, forward); up.Normalize();
        Vector3D offset = world - camera.Position;
        double depth = Vector3D.DotProduct(offset, forward);
        double scale = viewport.ActualWidth / (2 * Math.Tan(camera.FieldOfView * Math.PI / 360));
        Point projected = new Point(viewport.ActualWidth / 2 + Vector3D.DotProduct(offset, right) * scale / depth,
            viewport.ActualHeight / 2 - Vector3D.DotProduct(offset, up) * scale / depth);
        Check(view.GetType().GetMethod("HitPaper", Private)!.Invoke(view, new object[] { projected }) != null,
            "Perspective-projected paper is selectable by the 3D ray hit test");
        TranslateTransform3D translation = (TranslateTransform3D)Property(paper, "Translation");
        view.ReceiptMoved += repository.MoveSpike;
        foreach (double height in new[] { 0.72, 1.34, 2.2, 3.05 })
        {
            translation.OffsetX = 0.2; translation.OffsetY = height; translation.OffsetZ = -0.1;
            view.GetType().GetMethod("SavePosition", Private)!.Invoke(view, new[] { paper });
            var restored = new TaskRepository(directory).Tasks.Single();
            Check(restored.spikePositionVersion == 1 && Math.Abs(restored.spikeY!.Value - height) < 0.000001
                && restored.spikeX == 0.2 && restored.spikeZ == -0.1, $"3D position persists at height {height:F2}");
        }
        Point3D before = assembly.Transform.Transform(new Point3D(1, 2, 0));
        view.GetType().GetField("yaw", Private)!.SetValue(view, 1.7);
        view.GetType().GetField("pitch", Private)!.SetValue(view, 0.18);
        view.GetType().GetMethod("UpdateRotation", Private)!.Invoke(view, null);
        Point3D after = assembly.Transform.Transform(new Point3D(1, 2, 0));
        Check((after - before).Length > 0.3, "Yaw and pitch update the shared live 3D assembly");
        view.Update(Array.Empty<HangLater.Windows.Models.HangingTask>(), false, false, new Dictionary<Guid, uint>());
        Check(((IList)Field(view, "papers")).Count == 0 && assembly.Children.Count >= 6, "Empty dark scene retains the model and has no paper");
        CheckPage(app, directory);
        Console.WriteLine("PASS: all spike geometry, hit-test, height, persistence, rotation and empty-state checks.");
        Console.WriteLine("Isolated check data: " + directory);
        app.Shutdown();
    }

    private static void CheckCompletionEligibility()
    {
        DateTimeOffset start = new(2026, 10, 21, 0, 0, 0, TimeSpan.FromHours(8));
        DateTimeOffset now = start.AddHours(12);
        WorkdaySchedule daily = new(0, 0, true);
        HangingTask oldCreated = new() { title = "Created months ago, completed today", createdAt = start.AddDays(-90), completedAt = start.AddHours(10) };
        HangingTask yesterday = new() { title = "Completed yesterday", createdAt = start.AddDays(-2), completedAt = start.AddTicks(-1) };
        HangingTask pending = new() { title = "Pending today", createdAt = start.AddHours(9) };
        HangingTask atStart = new() { title = "Completed exactly at start", createdAt = start.AddDays(-1), completedAt = start };
        HangingTask atEnd = new() { title = "Completed exactly next midnight", createdAt = start.AddDays(-1), completedAt = start.AddDays(1) };
        HangingTask offSpike = new() { title = "Taken off but still completed today", createdAt = start.AddDays(-20), completedAt = start.AddHours(11), spikeAttached = false };
        HangingTask otherOffset = new() { title = "Same instant with another offset", createdAt = start.AddDays(-1), completedAt = now.ToOffset(TimeSpan.FromHours(-5)) };
        HangingTask[] tasks = { yesterday, pending, atEnd, oldCreated, atStart, offSpike, otherOffset };
        var eligible = DoneSpikeArchive.EligibleCompleted(tasks, daily, now);
        Check(eligible.Count == 4 && eligible.Contains(oldCreated), "Old creation dates do not exclude tasks completed today");
        Check(!eligible.Contains(yesterday) && !eligible.Contains(pending) && !eligible.Contains(atEnd),
            "Yesterday, pending tasks and the next-day exclusive boundary are excluded");
        Check(eligible.Contains(atStart) && eligible.First() == otherOffset, "Start boundary is inclusive and offset-equivalent completion times sort newest first");
        Check(eligible.Contains(offSpike) && !offSpike.IsOnSpike && oldCreated.IsOnSpike,
            "Completion filtering preserves independent ON/OFF attachment state");
        Check(DoneSpikeArchive.EligibleCompleted(tasks, daily, start.AddDays(1).AddHours(12)).SequenceEqual(new[] { atEnd }),
            "The next workday drops yesterday's receipts and admits today's completion");

        WorkdaySchedule overnight = new(15 * 60, 6 * 60, true);
        HangingTask late = new() { createdAt = start.AddDays(-30), completedAt = start.AddHours(23) };
        HangingTask early = new() { createdAt = start.AddDays(-30), completedAt = start.AddDays(1).AddHours(5) };
        HangingTask cutoff = new() { createdAt = start.AddDays(-30), completedAt = start.AddDays(1).AddHours(6) };
        var overnightTasks = new[] { late, early, cutoff };
        Check(DoneSpikeArchive.EligibleCompleted(overnightTasks, overnight, start.AddDays(1).AddHours(2))
                .SequenceEqual(new[] { early, late }),
            "A configured overnight workday groups late-night completions regardless of creation date");
        Check(DoneSpikeArchive.EligibleCompleted(overnightTasks, overnight, start.AddDays(1).AddHours(10)).Count == 0,
            "Outside the configured workday no receipt is eligible");
        Check(DoneSpikeArchive.EligibleCompleted(tasks, new WorkdaySchedule(10 * 60, 9 * 60, false), now).Count == 0,
            "An invalid same-day workday cannot admit receipts");
    }

    private static void CheckPage(HangLater.Windows.App app, string directory)
    {
        string pageDirectory = Path.Combine(directory, "page-fixture");
        TaskRepository repository = new TaskRepository(pageDirectory);
        SettingsRepository settings = new SettingsRepository(pageDirectory);
        settings.Value.Language = "en";
        settings.Value.WorkdayStartMinute = settings.Value.WorkdayEndMinute = 0;
        settings.Value.WorkdayEndsFollowingDay = true;
        TaskTypeDefinition blue = new() { name = "Fixture blue", colorHex = 0x333699 };
        repository.ReplaceTaskTypes(new[] { blue });
        string[] titles = { "组会", "Gmail - Review of manuscript TFS-2026-09…", "如何通过“随航”将 iPad 用作 Mac …副屏" };
        double[] publishedMinutes = { 18 * 60 + 17, 17 * 60 + 39, 45 };
        double[] completedMinutes = { 18 * 60 + 29, 17 * 60 + 47, 2 * 60 + 21 };
        for (int i = 0; i < titles.Length; i++)
        {
            var task = repository.Add(titles[i]);
            if (i == 1) task.taskTypeID = blue.id;
            repository.Complete(task.id);
            task.createdAt = new DateTimeOffset(DateTime.Today.AddMinutes(publishedMinutes[i]));
            task.completedAt = new DateTimeOffset(DateTime.Today.AddMinutes(completedMinutes[i]));
            repository.MoveSpike(task.id, 0, 2.4 - i * 0.8, 0);
        }
        SetProperty(app, "Repository", repository);
        SetProperty(app, "SettingsRepository", settings);
        SetProperty(app, "Text", new Localizer(false));
        MainWindow window = new MainWindow(app);
        SetProperty(app, "MainWindowInstance", window);
        SetProperty(app, "DesktopLine", new DesktopLineWindow(app));
        ((Button)window.FindName("SpikeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        FrameworkElement content = (FrameworkElement)window.Content;
        const double width = 1140, height = 700;
        void Layout()
        {
            content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
            content.UpdateLayout();
        }
        Layout();
        ScrollViewer scroll = (ScrollViewer)window.FindName("SpikePage");
        StackPanel list = (StackPanel)window.FindName("SpikeList");
        StackPanel off = (StackPanel)window.FindName("OffSpikeList");
        Check(scroll.ScrollableHeight > 80, "Done Spike has whole-page overflow, including the hero");
        Check(list.Children.Count == 3 && list.Children.OfType<SpikePaperRow>().Count() == 3, "All three eligible completed fixture tasks appear as paper rows");
        Check(off.Children.Count == 1 && off.Children[0] is Border, "Empty OFF THE SPIKE has its placeholder");
        ReceiptSpike3DView pageScene = (ReceiptSpike3DView)window.FindName("SpikeScene");
        HangingTask historical = repository.Tasks.First();
        DateTimeOffset originalCreated = historical.createdAt;
        DateTimeOffset? originalCompleted = historical.completedAt;
        historical.createdAt = DateTimeOffset.Now.AddDays(-60);
        window.RefreshAll(); Layout();
        Check(list.Children.OfType<SpikePaperRow>().Count() == 3 && ((IList)Field(pageScene, "papers")).Count == 3,
            "The live page and 3D model admit an old-created receipt completed today");
        historical.completedAt = new DateTimeOffset(DateTime.Today.AddTicks(-1));
        window.RefreshAll(); Layout();
        Check(list.Children.OfType<SpikePaperRow>().Count() == 2 && ((IList)Field(pageScene, "papers")).Count == 2,
            "The live page and 3D model exclude a receipt completed yesterday");
        historical.createdAt = originalCreated;
        historical.completedAt = originalCompleted;
        window.RefreshAll(); Layout();
        Check(((TextBlock)window.FindName("SpikeWorkdayLabel")).Text.StartsWith("COMPLETED "),
            "The page accurately labels completion-time filtering");
        MouseWheelEventArgs wheel = new(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.MouseWheelEvent };
        pageScene.GetType().GetMethod("OnZoom", Private)!.Invoke(pageScene, new object[] { pageScene, wheel });
        Check(!wheel.Handled, "Unmodified wheel over the 3D view bubbles to whole-page scrolling");
        TextBox search = (TextBox)window.FindName("SearchBox");
        search.Text = "Gmail"; window.RefreshAll(); Layout();
        Check(list.Children.OfType<SpikePaperRow>().Count() == 1 && ((IList)Field(pageScene, "papers")).Count == 1, "Search consistently filters the paper rows and 3D scene");
        search.Text = "no-matching-fixture"; window.RefreshAll(); Layout();
        Check(list.Children[0] is Border && ((IList)Field(pageScene, "papers")).Count == 0, "Unmatched search shows its empty state, without deleting records");
        search.Text = ""; window.RefreshAll(); Layout();
        SpikePaperRow row = list.Children.OfType<SpikePaperRow>().First();
        ((Button)row.FindName("ActionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Layout();
        Check(list.Children.OfType<SpikePaperRow>().Count() == 2 && off.Children.OfType<SpikePaperRow>().Count() == 1,
            "Take off moves the record to OFF THE SPIKE without removing the completed task");
        ((Button)((SpikePaperRow)off.Children[0]).FindName("ActionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Layout();
        Check(list.Children.OfType<SpikePaperRow>().Count() == 3 && off.Children.OfType<SpikePaperRow>().Count() == 0, "Put back restores the ON THE SPIKE row");
        Slider slider = (Slider)((SpikePaperRow)list.Children[0]).FindName("SizeSlider");
        slider.Value = 1.2;
        Check(repository.Tasks.First(task => task.title == titles[0]).EffectiveSpikeScale == 1.2, "Compact size slider persists its scale immediately");
        foreach (var task in repository.Tasks) repository.ResizeSpike(task.id, 0.9);
        window.RefreshAll(); Layout();
        ((System.Windows.Controls.Primitives.IScrollInfo)scroll.Template.FindName("PART_ScrollContentPresenter", scroll)).SetVerticalOffset(double.PositiveInfinity); Layout();
        Check(scroll.VerticalOffset > 80, "Scrolling moves the hero away while keeping the fixed header and sidebar");
        foreach (bool light in new[] { false, true })
        {
            settings.Value.LightAppearance = light;
            typeof(HangLater.Windows.App).GetMethod("ApplyTheme", Private)!.Invoke(app, null);
            window.RefreshAll(); Layout();
            ((System.Windows.Controls.Primitives.IScrollInfo)scroll.Template.FindName("PART_ScrollContentPresenter", scroll)).SetVerticalOffset(double.PositiveInfinity); Layout();
            ReceiptPaperStyle paper = ReceiptPaperStyle.For(blue.colorHex);
            Check(paper.Ink.Color.R > 240, "Blue paper has readable light ink in " + (light ? "Warm" : "Dark"));
            TextBlock buttonText = (TextBlock)((Button)((SpikePaperRow)list.Children[0]).FindName("ActionButton")).Content;
            Check(((SolidColorBrush)buttonText.Foreground).Color.R < 40, "Beige action label keeps dark ink in " + (light ? "Warm" : "Dark"));
            RenderTargetBitmap bitmap = new((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string output = Path.Combine(Environment.CurrentDirectory, "qa-spike-page-fixture-" + (light ? "warm" : "dark") + ".png");
            using (FileStream stream = File.Create(output)) encoder.Save(stream);
            Console.WriteLine("Fixture render: " + output);
        }
        foreach (double testWidth in new[] { 970.0, 1440.0 })
        {
            content.Measure(new Size(testWidth, height)); content.Arrange(new Rect(0, 0, testWidth, height)); content.UpdateLayout();
            foreach (SpikePaperRow paperRow in list.Children)
            {
                FrameworkElement title = (FrameworkElement)paperRow.FindName("TitleLabel");
                FrameworkElement action = (FrameworkElement)paperRow.FindName("ActionButton");
                Point titleRight = title.TranslatePoint(new Point(title.ActualWidth, 0), paperRow);
                Point actionLeft = action.TranslatePoint(new Point(0, 0), paperRow);
                Check(title.ActualWidth > 50 && titleRight.X <= actionLeft.X - 10, $"Paper title/action do not overlap at {testWidth:F0} DIP");
            }
        }
        Check(new TaskRepository(pageDirectory).Completed.Count() == 3, "Fixture tests preserve all completed task records");
    }

    private static void SetProperty(object instance, string name, object value) => instance.GetType().GetProperty(name)!.SetValue(instance, value);

    private static object Field(object instance, string name) => instance.GetType().GetField(name, Private)!.GetValue(instance)!;
    private static object Property(object instance, string name) => instance.GetType().GetProperty(name)!.GetValue(instance)!;
    private static void Check(bool passed, string description)
    {
        if (!passed) throw new InvalidOperationException(description);
        Console.WriteLine("PASS: " + description);
    }
}
