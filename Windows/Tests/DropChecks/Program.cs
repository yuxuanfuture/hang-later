using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HangLater.Windows;
using HangLater.Windows.Services;
using HangLater.Windows.Views;
using Point=System.Drawing.Point;
using WpfApplication=System.Windows.Application;
using Button=System.Windows.Controls.Button;
using Brushes=System.Windows.Media.Brushes;
using Cursors=System.Windows.Input.Cursors;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if(args.Contains("--source")) { Source();return; }
        if(args.Contains("--interactive")) { Interactive();return; }
        WindowReturnChecks.Run();
        var initial=new NativeMethods.RECT {Left=100,Top=300,Right=700,Bottom=700};
        var moved=new NativeMethods.RECT {Left=130,Top=330,Right=730,Bottom=730};
        var resized=new NativeMethods.RECT {Left=130,Top=330,Right=750,Bottom=730};
        var external=new CapturableWindow((nint)1,"Fixture","Fixture","C:\\Fixture.exe",initial,10,100);
        var session=new DesktopDropSession();
        session.Begin(external,new Point(120,320));session.Move(new Point(150,350),moved);session.ObserveDataDrag(true);
        Check(!session.HintVisible&&!session.ProbeVisible,"Dragging cannot reveal a hidden clothesline or hint");
        session.SetLineVisible(true);Check(!session.HintVisible&&!session.ProbeVisible,"Explicit reveal is quiet while browsing to-do cards");
        session.Begin(external,new Point(120,320));Check(!session.HintVisible,"Mouse-down alone does not show a hint");
        session.Move(new Point(122,322),initial);Check(!session.ProbeVisible,"Pointer jitter below the drag threshold is ignored");
        session.Move(new Point(150,350),initial);Check(session.ProbeVisible&&!session.HintVisible,"Pointer movement alone only enables the nonvisual OLE probe");
        Check(session.FinishPointer()==null&&!session.HintVisible&&!session.ProbeVisible,"Plain content selection cannot pin a whole application");
        session.Begin(external,new Point(120,320));session.Move(new Point(150,350),moved);
        Check(session.HintVisible&&session.ProbeVisible,"An actual same-size application-window move reveals the drop hint");
        Check(session.FinishPointer()==external&&!session.HintVisible,"A real window drag supplies one capture candidate and hides on release");
        session.Begin(external,new Point(120,320));session.Move(new Point(150,350),resized);
        Check(!session.HintVisible&&session.FinishPointer()==null,"Window resizing is not a document/application drop");
        session.ObserveDataDrag(true);Check(session.HintVisible,"A supported OLE drag reveals the hint on an already visible line");
        Check(session.FinishPointer()==null&&!session.HintVisible,"OLE drops cannot also capture a duplicate source-window receipt");
        session.ObserveDataDrag(true);session.ObserveDataDrag(false);Check(!session.HintVisible,"Unsupported data or leaving the line removes the OLE hint");
        session.Begin(external,new Point(120,320));session.Move(new Point(150,350),moved);session.Reset();
        Check(!session.HintVisible&&!session.ProbeVisible&&session.FinishPointer()==null,"Escape/cancel clears both hint and capture intent");
        session.ObserveDataDrag(true);session.SetLineVisible(false);session.SetLineVisible(true);
        Check(!session.HintVisible&&!session.ProbeVisible,"Hide/show does not revive a stale drag frame");
        session.Begin(null,new Point(120,320));session.Move(new Point(150,350),moved);
        Check(!session.HintVisible&&!session.ProbeVisible,"Dragging an existing Hang Later card does not offer an external drop frame");
        session.Begin(external,new Point(120,320));session.Move(new Point(150,350),null);
        Check(!session.ProbeVisible&&session.Candidate==null,"A vanished or reused source handle cancels safely");
        var app=Fixture(out var repo,out _);var line=new DesktopLineWindow(app);
        var box=(Border)line.FindName("DropZone");var probe=(System.Windows.Shapes.Rectangle)line.FindName("ExternalDragProbe");
        Check(box.Visibility==Visibility.Collapsed&&probe.Visibility==Visibility.Collapsed,"The real XAML starts with no visual or input-catching drop box");
        line.LocalizeAndRefresh();line.RefreshTasks();line.PrepareToShow();
        Check(box.Visibility==Visibility.Collapsed&&probe.Visibility==Visibility.Collapsed,"Refreshing/localizing/preparing the line does not display a drop target");
        var live=(DesktopDropSession)typeof(DesktopLineWindow).GetField("dropSession",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(line)!;
        live.SetLineVisible(true);live.ObserveDataDrag(true);
        typeof(DesktopLineWindow).GetMethod("ApplyDropSession",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(line,null);
        Check(box.Visibility==Visibility.Visible&&probe.Visibility==Visibility.Visible,"The real view follows confirmed drag state");
        live.Reset();typeof(DesktopLineWindow).GetMethod("ApplyDropSession",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(line,null);
        Check(box.Visibility==Visibility.Collapsed&&probe.Visibility==Visibility.Collapsed,"Ending a drag also removes the layered-window probe");
        Check(!File.Exists(repo.TaskFile),"All checks leave production and isolated task records untouched");
        app.Shutdown();
    }

    private static App Fixture(out TaskRepository repo,out SettingsRepository settings)
    {
        var app=new App();app.InitializeComponent();
        typeof(App).GetProperty("UseIsolatedTestStartup",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,true);
        string folder=Path.Combine(Path.GetTempPath(),"HangLater-DropChecks-"+Guid.NewGuid().ToString("N"));
        repo=new TaskRepository(folder);settings=new SettingsRepository(folder);settings.Value.KeepClotheslineVisible=true;
        Set(app,"Repository",repo);Set(app,"SettingsRepository",settings);Set(app,"Text",new Localizer(true));
        typeof(App).GetMethod("ApplyTheme",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(app,null);
        return app;
    }
    private static void Interactive()
    {
        var app=Fixture(out var repo,out _);var line=new DesktopLineWindow(app){Title="Hang Later — Drop target (isolated)",ShowInTaskbar=true};
        Set(app,"DesktopLine",line);Set(app,"MainWindowInstance",new MainWindow(app));
        var hint=(Border)line.FindName("DropZone");
        hint.IsVisibleChanged+=(_,_)=>Console.WriteLine("HINT "+hint.IsVisible);
        var live=(DesktopDropSession)typeof(DesktopLineWindow).GetField("dropSession",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(line)!;
        var diagnostics=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(45)};
        string previous="";
        diagnostics.Tick+=(_,_)=>{string state=$"visible={live.LineVisible} source={live.Candidate?.Title} pointer={live.PointerDrag} moving={live.WindowMoving} data={live.DataDragging}";if(state!=previous){Console.WriteLine(state);previous=state;}};
        diagnostics.Start();
        void Toggle(){if(line.IsVisible)line.Hide();else{line.PrepareToShow();line.PlaceOn(System.Windows.Forms.Screen.PrimaryScreen!,0.18);line.Show();}}
        var hotkey=new HotKeyService();hotkey.Register("Ctrl+Alt+Shift+L",Toggle);
        var reveal=new Button {Content="Show / hide test clothesline",Margin=new Thickness(4),Height=30};reveal.Click+=(_,_)=>Toggle();
        var remember=new Button {Content="Remember current source position",Margin=new Thickness(4),Height=30};
        var pin=new Button {Content="Pin moved source and return",Margin=new Thickness(4),Height=30};
        var status=new TextBlock {Text="Isolated test — no production tasks",Margin=new Thickness(4),TextWrapping=TextWrapping.Wrap};
        var controls=new StackPanel {Margin=new Thickness(8),Children={reveal,remember,pin,status}};
        var control=new Window {Title="Drop test controls — isolated data",Width=400,Height=220,Left=20,Top=650,Content=controls};
        Console.WriteLine("Isolated repository: "+repo.RootDirectory);
        var timer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(300)};
        int count=-1;timer.Tick+=(_,_)=>{if(count!=repo.Tasks.Count){count=repo.Tasks.Count;Console.WriteLine("RECEIPTS "+count);}};timer.Start();
        var source=new ProcessStartInfo(Environment.ProcessPath!) {UseShellExecute=false,CreateNoWindow=true};source.ArgumentList.Add("--source");
        string runtimeRoot=new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent!.Parent!.Parent!.FullName;
        source.Environment["DOTNET_ROOT"]=runtimeRoot;source.Environment["DOTNET_ROOT_X64"]=runtimeRoot;
        using var child=Process.Start(source);
        CapturableWindow? remembered=null;
        bool awaitingReturn=false;
        remember.Click+=(_,_)=>
        {
            remembered=WindowCaptureService.ListWindows().FirstOrDefault(w=>w.ProcessId==child!.Id);
            status.Text=remembered==null ? "Source not ready" : $"Remembered show={remembered.Placement?.ShowCmd}, position={remembered.Bounds.Left},{remembered.Bounds.Top}";
            Console.WriteLine(status.Text);
        };
        pin.Click+=(_,_)=>
        {
            if(remembered==null)return;
            var dropped=WindowCaptureService.Snapshot(remembered.Handle);
            if(dropped==null)return;
            Console.WriteLine($"PIN source={remembered.Handle}, release={dropped.Bounds.Left},{dropped.Bounds.Top}");
            typeof(DesktopLineWindow).GetMethod("CaptureExternalWindow",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(line,new object[]{remembered,dropped,0.5});
            awaitingReturn=true;status.Text="Capturing and returning test source...";
        };
        timer.Tick+=(_,_)=>
        {
            if(!awaitingReturn||remembered==null||repo.Tasks.Count==0)return;
            var current=WindowCaptureService.Snapshot(remembered.Handle);
            if(current==null||current.Placement?.ShowCmd!=remembered.Placement?.ShowCmd||!current.Bounds.Equals(remembered.Bounds))return;
            awaitingReturn=false;status.Text=$"Pinned and returned: show={current.Placement?.ShowCmd}, {current.Bounds.Left},{current.Bounds.Top}";
            Console.WriteLine("RETURNED "+status.Text);
        };
        control.Closed+=(_,_)=>{timer.Stop();diagnostics.Stop();hotkey.Dispose();app.Shutdown();};
        app.Run(control);
    }
    private static void Source()
    {
        var app=new WpfApplication();
        var page=new Border {Background=Brushes.LightSkyBlue,Margin=new Thickness(24),Padding=new Thickness(20),Child=new TextBlock {Text="Drag this example page to the revealed line.\n\nThis is isolated test content.",TextWrapping=TextWrapping.Wrap},Cursor=Cursors.Hand};
        bool started=false;
        page.MouseMove+=(_,e)=>{if(e.LeftButton!=MouseButtonState.Pressed||started)return;started=true;try{System.Windows.DragDrop.DoDragDrop(page,new System.Windows.DataObject(System.Windows.DataFormats.Text,"https://example.invalid/drag-check"),System.Windows.DragDropEffects.Copy);}finally{started=false;}};
        var source=new Window {Title="Drag source — page and application fixture",Width=1100,Height=800,Left=0,Top=0,Content=page};
        app.Run(source);
    }
    private static void Set(object obj,string property,object value)=>obj.GetType().GetProperty(property)!.SetValue(obj,value);
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Console.WriteLine("PASS "+message);}
}
