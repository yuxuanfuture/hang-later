using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;
using HangLater.Windows;
using HangLater.Windows.Models;
using HangLater.Windows.Services;
using HangLater.Windows.Views;

if (args.Length == 2 && args[0] == "--inspect-launch")
{
    var launch = ApplicationLaunchService.StartInfo(new(args[1]));
    Console.WriteLine(JsonSerializer.Serialize(new { launch?.FileName,launch?.WorkingDirectory }));
    return;
}

if(args.Contains("--inspect-typora"))
{
    var receipt = new TaskRepository().Tasks.First(t=>TyporaResumeService.IsTypora(t.appName));
    var start = TyporaResumeService.StartInfo(receipt);
    Console.WriteLine(JsonSerializer.Serialize(new { start?.FileName,Document=start?.ArgumentList.SingleOrDefault() }));
    return;
}

if (args.Contains("--interactive"))
{
    Thread ui = new(() =>
    {
        App app = new(); app.InitializeComponent();
        typeof(App).GetProperty("UseIsolatedTestStartup",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,true);
        string data = Path.Combine(Path.GetTempPath(), "HangLater-ResumeUI-" + Guid.NewGuid().ToString("N"));
        TaskRepository repo = new(data); SettingsRepository settings = new(data);
        settings.Value.Language = "zh"; settings.Value.KeepClotheslineVisible = true;
        void Set(string name,object value) => typeof(App).GetProperty(name)!.SetValue(app,value);
        Set("Repository",repo);Set("SettingsRepository",settings);Set("Text",new Localizer(true));
        typeof(App).GetMethod("ApplyTheme",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(app,null);
        var available = WindowCaptureService.ListWindows();
        var explorerWindow = available.FirstOrDefault(w=>w.ProcessName.Equals("explorer",StringComparison.OrdinalIgnoreCase) && (w.Title.Contains("下载")||w.Title.Contains("Downloads")));
        if(explorerWindow != null) { var explorer=repo.Add(explorerWindow.Title,appName:explorerWindow.ProcessName); WindowCaptureService.SetCapturedTarget(explorer,explorerWindow);repo.Update(explorer); }
        WindowCaptureService.ActivationTrace += message => Console.WriteLine("Activation: " + message);
        if (args.Contains("--uu") || args.Contains("--apps") || args.Contains("--typora"))
        {
            // Copy only the user-selected application's receipt into temporary data; never modify its source record.
            var source = new TaskRepository();
            foreach (var receipt in source.Tasks.Where(t=>args.Contains("--typora") ? TyporaResumeService.IsTypora(t.appName) : args.Contains("--apps") ? !string.IsNullOrWhiteSpace(t.appBundleIdentifier) : string.Equals(t.appName,"GameViewer",StringComparison.OrdinalIgnoreCase)).GroupBy(t=>t.appName).Select(g=>g.First()))
            {
            var copy = repo.Add(receipt.title,appName:receipt.appName);
            copy.appBundleIdentifier=receipt.appBundleIdentifier; copy.capturedWindowTitle=receipt.capturedWindowTitle;
            copy.capturedProcessId=receipt.capturedProcessId; copy.capturedWindowHandle=receipt.capturedWindowHandle;
            copy.capturedProcessStartUtcTicks=receipt.capturedProcessStartUtcTicks;
            copy.capturedLaunchExecutablePath=receipt.capturedLaunchExecutablePath; repo.Update(copy);
            copy.capturedDocumentPath=receipt.capturedDocumentPath;copy.resumeURL=receipt.resumeURL;copy.filePath=receipt.filePath;repo.Update(copy);
            if(args.Contains("--typora"))
            {
                var current = available.FirstOrDefault(w=>TyporaResumeService.IsTypora(w.ProcessName) && w.Title==receipt.capturedWindowTitle);
                if(current!=null && args.Contains("--capture-current")) { WindowCaptureService.SetCapturedTarget(copy,current);repo.Update(copy); }
                var other = repo.Add("Typora QA — README-Windows.md",appName:"Typora");
                other.appBundleIdentifier=receipt.appBundleIdentifier;other.capturedDocumentPath=Path.GetFullPath("README-Windows.md");repo.Update(other);
            }
            }
        }
        else
        {
            var obsidianWindow = available.First(w=>w.ProcessName.Equals("Obsidian",StringComparison.OrdinalIgnoreCase));
            var note = repo.Add("Pinned note: "+obsidianWindow.Title,appName:obsidianWindow.ProcessName);
            WindowCaptureService.SetCapturedTarget(note,obsidianWindow);repo.Update(note);
            var obsidian = repo.Add("Obsidian — application activation check",appName:"Obsidian");obsidian.appBundleIdentifier=obsidianWindow.ExecutablePath;repo.Update(obsidian);
        }
        MainWindow main = new(app) { Title="Hang Later — Resume verification (isolated data)" };
        DesktopLineWindow line = new(app) { Title="Hang Later — Resume test desktop", ShowInTaskbar=true };
        Set("MainWindowInstance",main);Set("DesktopLine",line);
        if(args.Contains("--apps") || args.Contains("--typora")) main.WindowState=System.Windows.WindowState.Maximized;
        main.Show();
        if(args.Contains("--apps") || args.Contains("--typora")) ((System.Windows.Controls.Button)main.FindName("TodoButton")).RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        else { line.PlaceOn(System.Windows.Forms.Screen.PrimaryScreen!,0.45);line.Show(); }
        Console.WriteLine("Isolated native UI repository: "+data);
        Dispatcher.Run();
    });
    ui.SetApartmentState(ApartmentState.STA);ui.Start();ui.Join();return;
}

static void Check(bool value, string name)
{
    if (!value) throw new Exception(name);
    Console.WriteLine("PASS " + name);
}

await ActivationChecks.Run(Check);
TyporaChecks.Run(Check);

CapturableWindow Window(int handle, string title, string? exe, string process = "Editor", int pid = 40, long start = 100) =>
    new((nint)handle, title, process, exe, new NativeMethods.RECT(), pid, start);
var first = Window(1, "Alpha", @"C:\Editor\Editor.exe");
var wanted = Window(2, "Report", @"C:\Editor\Editor.exe");
var otherApp = Window(3, "Report", @"D:\Other\Editor.exe");
var windows = new[] { first, otherApp, wanted };
Check(WindowCaptureService.SelectTarget(windows, new(@"c:\editor\EDITOR.EXE", WindowTitle: "Report")) == wanted, "Legacy title selects the correct same-app window, case-insensitive path");
Check(WindowCaptureService.SelectTarget(windows, new(null)) == null, "Missing executable never matches a null-path random window");
Check(WindowCaptureService.SelectTarget(new[] { Window(4,"Unknown",null) }, new(null)) == null, "Null executable identity is rejected");
Check(WindowCaptureService.SelectTarget(windows, new(@"X:\Missing.exe", "Editor")) == null, "A conflicting known executable does not fall back to the process name");
Check(WindowCaptureService.SelectTarget(windows, new(null,"Editor", "Report")) == otherApp, "Process-name fallback is available only for old missing-path captures");
Check(WindowCaptureService.SelectTarget(windows, new(first.ExecutablePath,"Editor","Report",1,40,100)) == first, "Valid same-session captured window wins after its title changes");
Check(WindowCaptureService.SelectTarget(windows, new(first.ExecutablePath,"Editor","Report",1,999,100)) == wanted, "Reused handle from another process is not preferred");
Check(WindowCaptureService.SelectTarget(windows, new(first.ExecutablePath,"Editor","Report",1,40,999)) == wanted, "Reused PID after a restart is not preferred");
Check(WindowCaptureService.SelectTarget(new[] { wanted, first }, new(first.ExecutablePath)) == wanted, "Unidentified same-app fallback preserves z-order rather than sorting titles");
var hidden = wanted with { IsVisible=false };
Check(WindowCaptureService.SelectTarget(new[]{hidden},new(wanted.ExecutablePath,WindowTitle:wanted.Title))==null,"Hidden windows do not enter the ordinary visible selection");
Check(WindowCaptureService.SelectHiddenTarget(new[]{hidden},new(wanted.ExecutablePath,WindowTitle:wanted.Title))==hidden,"A matching captured main-window title can restore from the tray");
Check(WindowCaptureService.SelectHiddenTarget(new[]{hidden},new(wanted.ExecutablePath))==null,"Hidden utility windows are never guessed without title or identity");
Check(WindowCaptureService.SelectHiddenTarget(new[]{hidden},new(wanted.ExecutablePath,WindowHandle:2,ProcessId:40,ProcessStartUtcTicks:100))==hidden,"Validated session identity can restore a renamed hidden window");
Check(WindowCaptureService.SelectHiddenTarget(new[]{hidden},new(wanted.ExecutablePath,WindowHandle:2,ProcessId:40,ProcessStartUtcTicks:999))==null,"Stale hidden-window handles cannot bypass the process-start check");
Check(WindowCaptureService.SelectHiddenTarget(new[]{hidden},new(otherApp.ExecutablePath,WindowTitle:wanted.Title))==null,"A hidden title alone cannot match another application's executable");

string root = Path.Combine(Path.GetTempPath(), "HangLater-ResumeChecks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string launchRoot=Path.Combine(root,"BootstrapApp");Directory.CreateDirectory(Path.Combine(launchRoot,"bin"));
string launchChild=Path.Combine(launchRoot,"bin","UniqueFixtureApp.exe");string launchParent=Path.Combine(launchRoot,"UniqueFixtureApp.exe");
File.WriteAllBytes(launchChild,new byte[]{0});File.WriteAllBytes(launchParent,new byte[]{0});
Check(ApplicationLaunchService.Resolve(launchChild,inspect:_=>new("Vendor","Product"))==launchParent,"bin window owner resolves to a matching user-facing bootstrapper");
Check(ApplicationLaunchService.Resolve(launchChild,inspect:p=>p==launchParent?new("Other vendor","Other product"):new("Vendor","Product"))==launchChild,"Unrelated parent executable is not treated as a launcher");
Check(ApplicationLaunchService.Resolve(launchChild,inspect:_=>null)==launchChild,"Missing product metadata never authorizes a guessed parent executable");
Check(!ApplicationLaunchService.SameProduct(new("Vendor",""),new("Vendor","")),"Empty product identities do not match");
var launchInfo=ApplicationLaunchService.StartInfo(new(launchChild));
Check(launchInfo != null && launchInfo.WorkingDirectory==Path.GetDirectoryName(launchChild)&&launchInfo.UseShellExecute,"Application startup uses its own executable directory, not Hang Later's directory");
Check(ApplicationLaunchService.Resolve("relative.exe")==null,"Relative launch paths are not searched in the current workspace");
string vault = Path.Combine(root,"Vault"); Directory.CreateDirectory(vault);
string config = Path.Combine(root,"obsidian.json");
File.WriteAllText(config,JsonSerializer.Serialize(new { vaults = new Dictionary<string,object> { ["vault-id"] = new { path=vault, open=true } } }));
string note=Path.Combine(vault,"中文 # 100%.md"); File.WriteAllText(note,"fixture");
string? uri=ObsidianResumeService.TryResolve("Obsidian","中文 # 100% - Vault - Obsidian 1.12.7",config);
Check(uri == "obsidian://open?path="+Uri.EscapeDataString(note),"Unique note uses an encoded absolute Obsidian open URI");
Check(ObsidianResumeService.TryResolve("Other","中文 # 100% - Vault - Obsidian",config)==null,"Other applications never use the Obsidian resolver");
Check(ObsidianResumeService.TryResolve("Obsidian","新标签页 - Vault - Obsidian 1.12.7",config)=="obsidian://open?vault=vault-id","An empty tab opens only its vault, not a guessed last-open note");
Check(ObsidianResumeService.TryResolve("Obsidian","Deleted - Vault - Obsidian",config)==null,"Missing documents fall back without inventing a path");
Directory.CreateDirectory(Path.Combine(vault,"A"));Directory.CreateDirectory(Path.Combine(vault,"B"));
File.WriteAllText(Path.Combine(vault,"A","Duplicate.md"),"fixture");File.WriteAllText(Path.Combine(vault,"B","Duplicate.md"),"fixture");
Check(ObsidianResumeService.TryResolve("Obsidian","Duplicate - Vault - Obsidian",config)==null,"Ambiguous legacy note titles do not jump to an arbitrary document");
Directory.CreateDirectory(Path.Combine(vault,".obsidian"));
File.WriteAllText(Path.Combine(vault,".obsidian","workspace.json"),"""{"active":"leaf1","main":{"children":[{"id":"leaf1","state":{"state":{"file":"A/Duplicate.md"}}}]}}""");
Check(ObsidianResumeService.TryResolve("Obsidian","Duplicate - Vault - Obsidian",config,true)=="obsidian://open?path="+Uri.EscapeDataString(Path.Combine(vault,"A","Duplicate.md")),"Current capture can disambiguate using the matching active workspace path");
Check(ObsidianResumeService.TryResolve("Obsidian","Duplicate - Vault - Obsidian",config)==null,"Later legacy clicks cannot substitute the current workspace for historical note identity");
Check(ObsidianResumeService.IsOpenUri(uri)&&!ObsidianResumeService.IsOpenUri("obsidian://new?file=Bad"),"Only non-mutating Obsidian open links are accepted by the editor");
File.WriteAllText(config,"broken json");
Check(ObsidianResumeService.TryResolve("Obsidian","Report - Vault - Obsidian",config)==null,"Corrupt or transient app configuration does not break activation");
File.WriteAllText(config,"""{"vaults":5}""");
Check(ObsidianResumeService.TryResolve("Obsidian","Report - Vault - Obsidian",config)==null,"An unexpected configuration shape falls back without crashing");

TaskRepository repo=new(Path.Combine(root,"Repository"));
var task=repo.Add("Report",appName:"Editor"); WindowCaptureService.SetCapturedTarget(task,wanted);repo.Update(task);
var reloaded=new TaskRepository(repo.RootDirectory).Tasks.Single();
Check(reloaded.capturedWindowHandle==2&&reloaded.capturedProcessId==40&&reloaded.capturedProcessStartUtcTicks==100&&reloaded.capturedWindowTitle=="Report","Capture identity survives repository serialization");
Check(HangingTaskTarget.FromTask(reloaded).WindowTitle=="Report","Reopen uses the original capture title independently of the edited task name");
Check(repo.Tasks.Count==1&&!repo.Tasks[0].IsCompleted,"Capture metadata does not change task state");
task.capturedLaunchExecutablePath=launchParent;repo.Update(task);
Check(HangingTaskTarget.FromTask(new TaskRepository(repo.RootDirectory).Tasks.Single()).LaunchExecutablePath==launchParent,"Launcher identity persists separately from the window-owner executable");
Console.WriteLine("All resume checks passed. Isolated fixtures: "+root);
