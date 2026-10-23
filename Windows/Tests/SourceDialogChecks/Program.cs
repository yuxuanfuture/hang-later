using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HangLater.Windows;
using HangLater.Windows.Models;
using HangLater.Windows.Services;
using HangLater.Windows.Views;
using Button=System.Windows.Controls.Button;
using TextBox=System.Windows.Controls.TextBox;
using CheckBox=System.Windows.Controls.CheckBox;
using ComboBox=System.Windows.Controls.ComboBox;
using Image=System.Windows.Controls.Image;
using Brushes=System.Windows.Media.Brushes;
using Size=System.Windows.Size;
using Point=System.Windows.Point;

internal static class Program
{
    private static App app=null!;
    private static TaskRepository repo=null!;
    private static SettingsRepository settings=null!;
    [STAThread]
    private static void Main(string[] args)
    {
        try{Run(args);}catch(Exception error){Console.Error.WriteLine(error);Environment.Exit(1);}
    }
    private static void Run(string[] args)
    {
        app=new App();app.InitializeComponent();
        typeof(App).GetProperty("UseIsolatedTestStartup",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,true);
        string folder=Path.Combine(Path.GetTempPath(),"HangLater-SourceDialogChecks-"+Guid.NewGuid().ToString("N"));
        repo=new TaskRepository(folder);settings=new SettingsRepository(folder);
        Set("Repository",repo);Set("SettingsRepository",settings);Set("Text",new Localizer(false));
        Set("MainWindowInstance",new MainWindow(app){Title="Hang Later — add flow (isolated)"});Set("DesktopLine",new DesktopLineWindow(app));
        Console.WriteLine("Isolated repository: "+folder);
        if(args.Contains("--interactive")){Interactive();return;}
        var type=new TaskTypeDefinition{name="Research",colorHex=0x303090};repo.ReplaceTaskTypes(new[]{type});
        foreach(bool light in new[]{true,false})foreach(bool chinese in new[]{false,true})
        {
            Theme(light,chinese);string state=(light?"light":"dark")+"-"+(chinese?"zh":"en");
            var picker=new AddSourceWindow(app);
            var options=(StackPanel)picker.FindName("SourceOptions");
            var mapping=new[]{"write-task","reminder","choose-image","capture-app-window","chrome-page-video"};
            Check(options.Children.Count==5,"Source picker has exactly the five macOS choices in "+state);
            for(int i=0;i<5;i++)
            {
                var choice=(Button)options.Children[i];var icon=(Image)((Border)((Grid)choice.Content).Children[0]).Child;
                var source=(BitmapImage)icon.Source;
                Check((int)(AddSourceChoice)choice.Tag==i&&source.UriSource.ToString().EndsWith(mapping[i]+".png"),"Choice and supplied icon map one-to-one: "+mapping[i]);
                var disk=new BitmapImage(new Uri(Path.GetFullPath("Windows/HangLater/Resources/HangSomethingIcons/"+(light?"light":"dark")+"/@2x/"+mapping[i]+".png")));
                Check(Pixels(source).SequenceEqual(Pixels(disk)),"The supplied "+state+" pixels are embedded unchanged: "+mapping[i]);
            }
            Check(repo.Tasks.Count==0,"Opening the picker writes no task");
            Render(picker,"qa-source-picker-"+state+".png");picker.Close();
            var form=new TaskComposerWindow(app);
            Check(((TextBox)form.FindName("TitleText")).Tag!=null&&((TextBox)form.FindName("NoteText")).Tag!=null,"Composer has the reference's real input placeholders");
            Check(((ComboBox)form.FindName("TypeBox")).Items.Count==2,"Task types include Default border and configured types");
            Check(((FrameworkElement)form.FindName("ReminderTimePanel")).Visibility==Visibility.Collapsed,"Optional reminder is collapsed until enabled");
            Render(form,"qa-source-composer-"+state+".png");
            Check(Descendants(form.Content as DependencyObject).OfType<TextBlock>().Any(x=>x.Text==(chinese?"默认边框":"Default border")),"The task-type selector displays the actual selected name, not a model class name");
            var footer=(FrameworkElement)form.FindName("Footer");var shell=(FrameworkElement)form.Content;
            Check(footer.TranslatePoint(new Point(0,0),shell).Y+footer.ActualHeight<=shell.ActualHeight,"Composer footer is fully visible in "+state);
            ((CheckBox)form.FindName("ReminderCheck")).IsChecked=true;
            Check(((FrameworkElement)form.FindName("ReminderTimePanel")).Visibility==Visibility.Visible,"Adding a reminder reveals date and time");
            Render(form,"qa-source-reminder-"+state+".png");form.Close();
            var capture=new WindowCapturePickerWindow(app,Array.Empty<CapturableWindow>());
            Check(!capture.CaptureButton.IsEnabled&&capture.WindowsList.Items.Count==0,"Empty window picker cannot capture a nonexistent selection");Render(capture,"qa-source-capture-empty-"+state+".png");capture.Close();
        }
        Theme(true,false);
        CheckCalendar();
        var compact=new TaskComposerWindow(app,true){MaxHeight=480};Render(compact,"qa-source-composer-compact.png");
        var compactFooter=(FrameworkElement)compact.FindName("Footer");var compactShell=(FrameworkElement)compact.Content;
        Check(compactFooter.TranslatePoint(new Point(0,0),compactShell).Y+compactFooter.ActualHeight<=480,"A short work area keeps Cancel and Hang visible while the form scrolls");compact.Close();
        var draft=new TaskComposerWindow(app);
        Check(!draft.SaveDraft()&&repo.Tasks.Count==0,"An empty task is rejected without writes");
        Box(draft,"TitleText").Text="  A course to continue  ";Box(draft,"LinkText").Text="ftp://example.invalid";
        Check(!draft.SaveDraft()&&repo.Tasks.Count==0,"Invalid web links cannot create a partial task");
        Box(draft,"LinkText").Text="https://example.invalid/course";Box(draft,"NoteText").Text="Resume at 12:30\nRemember the key steps";
        ((ComboBox)draft.FindName("TypeBox")).SelectedIndex=1;
        Check(draft.SaveDraft(),"A named task with link, note and task type saves");
        var saved=repo.Tasks.Single();Check(saved.title=="A course to continue"&&saved.url=="https://example.invalid/course"&&saved.resumeURL==saved.url&&saved.taskTypeID==type.id&&saved.note.Contains('\n'),"All composer fields persist together without discarding the resume link");draft.Close();
        var reminder=new TaskComposerWindow(app,true);Box(reminder,"TitleText").Text="Reminder";Box(reminder,"LinkText").Text="https://us02web.zoom.us/j/123";
        ((DatePicker)reminder.FindName("DueDate")).SelectedDate=DateTime.Today.AddDays(1);Box(reminder,"DueTime").Text="not-a-time";
        Check(!reminder.SaveDraft()&&repo.Tasks.Count==1,"Invalid reminder time creates no task");Box(reminder,"DueTime").Text="12:30";
        Check(reminder.SaveDraft()&&repo.Tasks[0].dueAt.HasValue&&repo.Tasks[0].kind==HangingTaskKind.meeting,"The reminder route saves a timed meeting link");reminder.Close();
        var ordinary=new TaskComposerWindow(app,true);Box(ordinary,"TitleText").Text="Everyday reminder";((DatePicker)ordinary.FindName("DueDate")).SelectedDate=DateTime.Today.AddDays(1);Box(ordinary,"DueTime").Text="12:30";
        Check(ordinary.SaveDraft()&&repo.Tasks[0].kind==HangingTaskKind.reminder&&repo.Tasks[0].url==null,"Everyday reminders do not require a meeting link");ordinary.Close();
        string before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(repo.TaskFile)));
        var cancelled=new TaskComposerWindow(app);Box(cancelled,"TitleText").Text="Unsaved draft";cancelled.Close();
        Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(repo.TaskFile)))==before,"Closing a draft does not save or change existing tasks");
        CheckCaptureFailureStaysModal();
        Console.WriteLine("All source-dialog checks passed.");app.Shutdown();
    }
    private static void CheckCaptureFailureStaysModal()
    {
        var missing=new CapturableWindow((nint)1,"Closed fixture","Fixture",null,new NativeMethods.RECT{Left=100,Top=100,Right=600,Bottom=500});
        var picker=new WindowCapturePickerWindow(app,new[]{missing});picker.WindowsList.SelectedIndex=0;
        bool returned=false,checkedFailure=false;
        picker.Loaded+=(_,_)=>picker.CaptureButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(700)};
        timer.Tick+=(_,_)=>
        {
            timer.Stop();
            Check(!returned&&picker.IsVisible&&picker.Opacity==1&&picker.IsHitTestVisible&&picker.CaptureButton.IsEnabled,"A failed asynchronous capture keeps the modal open and restores interactive controls");
            checkedFailure=true;picker.DialogResult=false;
        };
        int count=repo.Tasks.Count;timer.Start();picker.ShowDialog();returned=true;
        Check(checkedFailure&&repo.Tasks.Count==count,"A stale capture target creates no task and cannot return from ShowDialog early");
    }
    private static TextBox Box(Window window,string name)=>(TextBox)window.FindName(name);
    private static void CheckCalendar()
    {
        foreach(bool light in new[]{false,true})
        {
            Theme(light,false);var form=new TaskComposerWindow(app,true);form.Show();form.UpdateLayout();
            var date=(DatePicker)form.FindName("DueDate");date.IsDropDownOpen=true;date.UpdateLayout();
            var popup=(System.Windows.Controls.Primitives.Popup)date.Template.FindName("PART_Popup",date);
            var calendar=(System.Windows.Controls.Calendar)popup.Child;calendar.UpdateLayout();
            var day=Descendants(calendar).OfType<TextBlock>().First(x=>x.Text==date.SelectedDate!.Value.Day.ToString());
            var ink=((SolidColorBrush)day.Foreground).Color;
            Check(ink.R<80&&ink.G<80&&ink.B<80,"The native calendar uses readable dark date ink in "+(light?"Light":"Dark"));
            var bitmap=new RenderTargetBitmap((int)calendar.ActualWidth*2,(int)calendar.ActualHeight*2,192,192,PixelFormats.Pbgra32);bitmap.Render(calendar);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create("qa-source-calendar-"+(light?"light":"dark")+".png"))encoder.Save(output);
            date.IsDropDownOpen=false;form.Close();
        }
        Theme(true,false);
    }
    private static void Set(string property,object value)=>typeof(App).GetProperty(property)!.SetValue(app,value);
    private static void Theme(bool light,bool chinese){settings.Value.LightAppearance=light;Set("Text",new Localizer(chinese));typeof(App).GetMethod("ApplyTheme",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(app,null);}
    private static byte[] Pixels(BitmapSource source){var format=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);byte[] pixels=new byte[format.PixelWidth*format.PixelHeight*4];format.CopyPixels(pixels,format.PixelWidth*4,0);return pixels;}
    private static void Render(Window window,string file)
    {
        var shell=(FrameworkElement)window.Content;
        foreach(var element in Descendants(shell).OfType<UIElement>())element.InvalidateMeasure();shell.InvalidateMeasure();
        double height=window.SizeToContent==SizeToContent.Height?double.PositiveInfinity:window.Height;
        shell.Measure(new Size(window.Width,height));if(double.IsInfinity(height))height=Math.Min(shell.DesiredSize.Height,window.MaxHeight);
        // Model the finite final window constraint, rather than arranging an
        // infinity-measured star grid directly into a shorter screenshot.
        shell.Measure(new Size(window.Width,height));
        shell.Arrange(new Rect(0,0,window.Width,height));shell.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.Width*2),(int)Math.Ceiling(height*2),192,192,PixelFormats.Pbgra32);bitmap.Render(shell);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(file))encoder.Save(output);
        Console.WriteLine($"RENDER {file} {window.Width}x{height} DIP");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject? root)
    {
        if(root==null)yield break;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var descendant in Descendants(child))yield return descendant;}
    }
    private static void Interactive()
    {
        var controls=new StackPanel{Margin=new Thickness(16)};
        foreach(var state in new[]{(true,false,"Light / English"),(false,false,"Dark / English"),(true,true,"白天 / 中文"),(false,true,"黑夜 / 中文")})
        {
            var button=new Button{Content=state.Item3,Margin=new Thickness(0,0,0,8),Height=32};button.Click+=(_,_)=>{Theme(state.Item1,state.Item2);app.MainWindowInstance.LocalizeAndRefresh();app.ShowAddSource();};controls.Children.Add(button);
        }
        foreach(var state in new[]{(true,false,"Light form / English"),(false,true,"Dark form / 中文")})
        {
            var button=new Button{Content=state.Item3,Margin=new Thickness(0,0,0,8),Height=32};button.Click+=(_,_)=>{Theme(state.Item1,state.Item2);new TaskComposerWindow(app){WindowStartupLocation=WindowStartupLocation.CenterScreen,ShowInTaskbar=true}.ShowDialog();};controls.Children.Add(button);
        }
        var control=new Window{Title="Add flow checks — isolated data",Width=330,Height=340,Content=controls};
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};int count=-1;timer.Tick+=(_,_)=>{if(count!=repo.Tasks.Count){count=repo.Tasks.Count;Console.WriteLine("RECEIPTS "+count);}};timer.Start();
        app.Run(control);
    }
    private static void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);Console.WriteLine("PASS "+label);}
}
