using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HangLater.Windows;
using HangLater.Windows.Models;
using HangLater.Windows.Services;
using HangLater.Windows.Views;

internal static class Program
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    private static void Main()
    {
        App app = new(); app.InitializeComponent();
        string directory = Path.Combine(Path.GetTempPath(), "HangLater-SettingsChecks-" + Guid.NewGuid().ToString("N"));
        TaskRepository repo = new(directory); SettingsRepository settings = new(directory);
        settings.Value.KeepClotheslineVisible = false; settings.Value.ClotheslineVerticalPosition = 0.07;
        settings.Value.WorkdayStartMinute = 0; settings.Value.WorkdayEndMinute = 120; settings.Value.WorkdayEndsFollowingDay = true;
        repo.ReplaceTaskTypes(new[] { new TaskTypeDefinition { name="论文", colorHex=0x27328E }, new TaskTypeDefinition { name="OpenReview", colorHex=0xAA2651 } });
        settings.Save(settings.Value);
        Set(app,"Repository",repo); Set(app,"SettingsRepository",settings); Set(app,"Text",new Localizer(false));
        Set(app,"MainWindowInstance",new MainWindow(app)); Set(app,"DesktopLine",new DesktopLineWindow(app));
        string originalTasks = File.ReadAllText(repo.TaskFile), originalTypes = File.ReadAllText(repo.TypeFile), originalSettings = File.ReadAllText(settings.FilePath);
        foreach (var (light, chinese) in new[] { (true,false), (false,true) })
        {
            settings.Value.LightAppearance=light; settings.Value.Language=chinese?"zh":"en"; app.Text.Set(chinese);
            typeof(App).GetMethod("ApplyTheme",Private)!.Invoke(app,null);
            SettingsWindow window = new(app); FrameworkElement root=(FrameworkElement)window.Content;
            void Layout(double width=798,double height=672) { root.Measure(new Size(width,height)); root.Arrange(new Rect(0,0,width,height)); root.UpdateLayout(); root.UpdateLayout(); }
            T Find<T>(string name) => (T)window.FindName(name);
            void Click(string name) { Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(); }
            Layout(light?798:783,672);
            Check(Find<StackPanel>("SectionMenu").Children.OfType<Button>().Count()==3,"Exactly three source-mapped sidebar sections");
            foreach(string iconName in new[]{"DesktopNavIcon","WorkdayNavIcon","TypesNavIcon"})
                Check(((BitmapImage)Find<Image>(iconName).Source).UriSource.ToString().Contains(light?"/light/":"/dark/"),iconName+" selects the correct theme asset");
            Check(Find<TextBlock>("HeightValue").Text=="7%","Height label reflects the source-like seven-percent fixture");
            Check(Find<TextBlock>("SaveLabel").Foreground is SolidColorBrush ink && (light?ink.Color.R>230:ink.Color.R<40),"Primary action has contrasting themed ink");
            Save(root,$"qa-settings-fixture-{(light?"light":"dark")}.png");
            ScrollViewer scroll=Find<ScrollViewer>("SettingsScroll");
            Click("WorkdayNav"); Check(scroll.VerticalOffset>100,"Workday sidebar navigation scrolls to the section");
            Check(Find<TextBlock>("WorkdayNavLabel").FontWeight==FontWeights.Bold,"The active sidebar item gets the selected weight");
            Check(window.TryCreateDraft(out var draft,out _) && draft.WorkdayEndMinute==120 && draft.WorkdayEndsFollowingDay,"Initial draft keeps the workday configuration");
            Find<TextBox>("StartTime").Text="3:00 PM"; Find<TextBox>("EndTime").Text="6:00 AM";
            Check(window.TryCreateDraft(out draft,out _) && draft.WorkdayStartMinute==900 && draft.WorkdayEndMinute==360,"AM/PM time editing creates the correct overnight schedule");
            Find<ComboBox>("EndDayBox").SelectedIndex=0;
            Check(!window.TryCreateDraft(out _,out _),"Invalid same-day end is rejected without saving");
            Find<TextBox>("StartTime").Text="00:00"; Find<TextBox>("EndTime").Text="02:00"; Find<ComboBox>("EndDayBox").SelectedIndex=1;
            Check(Find<TextBlock>("WorkdayRange").Text.Contains("02:00"),"Live range summary follows edited controls");
            Find<TextBox>("HotKeyBox").Text="bad-key"; Check(!window.TryCreateDraft(out _,out _),"Invalid hotkey is rejected"); Click("ResetHotKeyButton");
            Check(Find<TextBox>("HotKeyBox").Text=="Ctrl+Alt+L","Reset restores the Windows shortcut, not a macOS key chord");
            Find<Slider>("HeightSlider").Value=42; Check(Find<TextBlock>("HeightValue").Text=="42%","Height percent updates immediately");
            Find<Slider>("HeightSlider").Value=7;
            Click("TypesNav"); Check(scroll.VerticalOffset>200,"Task-type navigation keeps the form reachable");
            StackPanel rows=Find<StackPanel>("TaskTypesPanel");
            typeof(SettingsWindow).GetField("editingColorType",Private)!.SetValue(window,window.DraftTaskTypes[0].id);
            typeof(SettingsWindow).GetMethod("BuildPalette",Private)!.Invoke(window,null);
            var paletteContent=(StackPanel)((Border)window.PaletteSurface).Child;
            UniformGrid swatches=(UniformGrid)paletteContent.Children[1];
            Check(swatches.Children.Count==70 && swatches.Columns==10 && swatches.Rows==7,"Popover contains the complete 10-by-7 circular palette");
            Button selected=swatches.Children.OfType<Button>().Single(button=>(uint)button.Tag==0x27328E);
            Check(selected.Content is TextBlock,"Current palette color carries its checkmark");
            var popupEffect=window.PaletteSurface.Effect; window.PaletteSurface.Effect=null;
            Save(window.PaletteSurface,$"qa-settings-palette-{(light?"light":"dark")}.png",336,258);
            window.PaletteSurface.Effect=popupEffect;
            swatches.Children.OfType<Button>().Single(button=>(uint)button.Tag==0x42A74D).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(window.DraftTaskTypes[0].colorHex==0x42A74D && repo.TaskTypes[0].colorHex==0x27328E,"Color changes update the draft only");
            typeof(SettingsWindow).GetMethod("BuildPalette",Private)!.Invoke(window,null);
            Check(window.PaletteSurface.Parent!=null,"Rebuilding the palette reuses its host safely");
            ((Grid)rows.Children[0]).Children.OfType<TextBox>().Single().Text="OpenReview";
            Check(!window.TryCreateDraft(out _,out _),"Duplicate type names cannot be saved");
            ((Grid)rows.Children[0]).Children.OfType<TextBox>().Single().Text="论文";
            Click("AddTypeButton"); Click("AddTypeButton");
            Check(window.DraftTaskTypes.Count==4 && window.DraftTaskTypes.Select(type=>type.name).Distinct().Count()==4,"Repeated Add creates unique task-type names");
            ((Grid)rows.Children[3]).Children.OfType<Button>().Last().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(window.DraftTaskTypes.Count==3,"Deleting a draft type removes only that draft row");
            // Restore fixture names/colors before comparing the source-like type section.
            ((Grid)rows.Children[2]).Children.OfType<Button>().Last().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            typeof(SettingsWindow).GetField("editingColorType",Private)!.SetValue(window,window.DraftTaskTypes[0].id);
            typeof(SettingsWindow).GetMethod("BuildPalette",Private)!.Invoke(window,null);
            ((UniformGrid)((StackPanel)((Border)window.PaletteSurface).Child).Children[1]).Children.OfType<Button>().Single(button=>(uint)button.Tag==0x27328E).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Find<TextBox>("StartTime").Text=chinese?"00:00":"12:00 AM"; Find<TextBox>("EndTime").Text=chinese?"02:00":"2:00 AM";
            Click("DesktopNav"); Layout();
            double timeTop=Find<TextBox>("StartTime").TranslatePoint(new Point(0,0),Find<StackPanel>("SectionsContent")).Y-34;
            scroll.ScrollToVerticalOffset(timeTop+28); Layout();
            Save(root,$"qa-settings-types-{(light?"light":"dark")}.png");
            foreach(double width in new[]{720.0,980.0}) { Layout(width,560); Check(Find<Button>("SaveButton").TranslatePoint(new Point(0,0),root).Y+44<=560,"Footer stays inside the minimum-height window"); Check(Find<TextBox>("HotKeyBox").ActualWidth>=110,"Shortcut control remains usable at width "+width); }
            window.Close();
        }
        Check(File.ReadAllText(repo.TaskFile)==originalTasks && File.ReadAllText(repo.TypeFile)==originalTypes && File.ReadAllText(settings.FilePath)==originalSettings,"Cancelled settings drafts never write task, type, or settings files");
        Console.WriteLine("PASS: all settings checks. Isolated fixture: "+directory);
        app.Shutdown();
    }
    private static void Set(object obj,string name,object value)=>obj.GetType().GetProperty(name)!.SetValue(obj,value);
    private static void Check(bool success,string label) { if(!success) throw new InvalidOperationException(label); Console.WriteLine("PASS: "+label); }
    private static void Save(FrameworkElement view,string file,int? width=null,int? height=null)
    {
        int w=width??(int)Math.Round(view.ActualWidth),h=height??(int)Math.Round(view.ActualHeight);
        if(width.HasValue) {view.Measure(new Size(w,h));view.Arrange(new Rect(0,0,w,h));view.UpdateLayout();}
        RenderTargetBitmap bitmap=new(w,h,96,96,PixelFormats.Pbgra32);
        if(view is Border) { DrawingVisual visual=new(); using(var dc=visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(view){Stretch=Stretch.Fill},null,new Rect(0,0,w,h)); bitmap.Render(visual); }
        else bitmap.Render(view);
        PngBitmapEncoder png=new();png.Frames.Add(BitmapFrame.Create(bitmap));using FileStream stream=File.Create(Path.Combine(Environment.CurrentDirectory,file));png.Save(stream);
        Console.WriteLine("Fixture render: "+file);
    }
}
