using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HangLater.Windows;
using HangLater.Windows.Controls;
using HangLater.Windows.Services;
using HangLater.Windows.Views;
using Path=System.IO.Path;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app=new App();app.InitializeComponent();
        typeof(App).GetProperty("UseIsolatedTestStartup",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,true);
        string folder=Path.Combine(Path.GetTempPath(),"HangLater-TodoChecks-"+Guid.NewGuid().ToString("N"));
        var repo=new TaskRepository(folder);var settings=new SettingsRepository(folder);
        void Set(string property,object value)=>typeof(App).GetProperty(property)!.SetValue(app,value);
        Set("Repository",repo);Set("SettingsRepository",settings);Set("Text",new Localizer(true));
        var task=repo.Add("Sample page / 示例任务","https://example.invalid","Test only");
        Set("MainWindowInstance",new MainWindow(app));Set("DesktopLine",new DesktopLineWindow(app));
        foreach(bool light in new[]{true,false})
        {
            settings.Value.LightAppearance=light;
            typeof(App).GetMethod("ApplyTheme",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(app,null);
            var card=TaskCardFactory.CreateListCard(app,task);
            var grid=(Grid)card.Child;var button=(Button)grid.Children[0];
            Check(button.Content is Ellipse,"Pending completion control is vector geometry, not a font-dependent tiny circle");
            var circle=(Ellipse)button.Content;
            Check(circle.Width==24&&circle.Height==24&&circle.StrokeThickness==1.6,"Circle has the reference-like 24 DIP diameter and readable stroke");
            Check(circle.Stroke==app.Resources["MutedBrush"],"Circle uses readable themed ink in "+(light?"Light":"Dark"));
            Check(button.Width==42&&button.Height==42&&!circle.IsHitTestVisible,"The 42 DIP button remains the click target, not only the thin circle stroke");
            Check(AutomationProperties.GetName(button)=="标记完成","The vector button retains an accessible completion label");
            var panel=new Border {Width=850,Height=140,Padding=new Thickness(12),Background=(Brush)app.Resources["PaperBrush"],Child=card};
            panel.Measure(new Size(850,140));panel.Arrange(new Rect(0,0,850,140));panel.UpdateLayout();
            Check(button.ActualWidth==42&&button.ActualHeight==42&&circle.ActualWidth==24,"Layout does not shrink the completion circle");
            var preview=(FrameworkElement)grid.Children[1];
            var circlePoint=circle.TranslatePoint(new Point(0,0),grid);
            var previewPoint=preview.TranslatePoint(new Point(0,0),grid);
            Check(circlePoint.X+24<previewPoint.X,"The enlarged circle does not overlap the preview image");
            var bitmap=new RenderTargetBitmap(1275,210,144,144,PixelFormats.Pbgra32);bitmap.Render(panel);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var output=File.Create("qa-todo-circle-"+(light?"light":"dark")+".png"))encoder.Save(output);
            if(light)
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(repo.Pending.Count()==0&&repo.Tasks.Single().completedAt.HasValue,"Clicking the enlarged control still completes only its task");
                repo.Restore(task.id);
            }
        }
        Check(repo.Tasks.Count==1&&repo.Pending.Count()==1,"Tests retain their fixture task and never use the production repository");
        Console.WriteLine("All to-do circle checks passed. Isolated data: "+folder);
        app.Shutdown();
    }
    private static void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);Console.WriteLine("PASS "+label);}
}
