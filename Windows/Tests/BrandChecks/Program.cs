using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HangLater.Windows;
using HangLater.Windows.Controls;
using HangLater.Windows.Services;
using HangLater.Windows.Views;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        App app=new();app.InitializeComponent();
        typeof(App).GetProperty("UseIsolatedTestStartup",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(app,true);
        string folder=Path.Combine(Path.GetTempPath(),"HangLater-BrandChecks-"+Guid.NewGuid().ToString("N"));
        var repo=new TaskRepository(folder);var settings=new SettingsRepository(folder);
        string extension = ChromeBridgeService.EnsureExtensionFiles(folder);
        Check(Directory.GetFiles(extension).Length == Directory.GetFiles("ChromeExtension").Length, "Portable EXE embeds every browser extension asset");
        foreach (string file in Directory.GetFiles("ChromeExtension"))
            Check(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(Path.Combine(extension,Path.GetFileName(file)))), "Extracted extension matches source: " + Path.GetFileName(file));
        Check(ChromeBridgeService.EnsureExtensionFiles(folder) == extension, "Repeated extraction keeps the stable per-user extension location");
        void Set(string property,object value)=>typeof(App).GetProperty(property)!.SetValue(app,value);
        Set("Repository",repo);Set("SettingsRepository",settings);Set("Text",new Localizer(false));
        MainWindow main=new(app);
        Set("MainWindowInstance",main);Set("DesktopLine",new DesktopLineWindow(app));
        foreach(bool light in new[]{false,true,false})
        {
            settings.Value.LightAppearance=light;
            typeof(App).GetMethod("ApplyTheme",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(app,null);
            main.LocalizeAndRefresh();
            var logo=(Image)main.FindName("BrandIcon");
            var source=(BitmapImage)logo.Source;
            Check(source.UriSource.ToString().EndsWith(light?"hang-later-pin-icon.png":"hang-later-pin-icon-dark.jpg"),"Theme chooses the exact "+(light?"unchanged light":"uploaded dark")+" logo resource");
            Check(logo.Width==37 && logo.Height==37 && logo.Stretch==Stretch.Uniform,"Logo size and alignment remain unchanged");
            var disk=new BitmapImage(new Uri(Path.GetFullPath("Windows/HangLater/Resources/"+(light?"hang-later-pin-icon.png":"hang-later-pin-icon-dark.jpg"))));
            Check(Pixels(source).SequenceEqual(Pixels(disk)),"Embedded logo pixels match the original resource without recoloring or conversion");
            CheckToolbar(main,app,light);
            CheckStatusCounts(main,app,light);
            CheckFooterLayout(main,light);
            var header=(FrameworkElement)main.FindName("BrandHeader");
            header.Measure(new Size(230,48));header.Arrange(new Rect(0,0,230,48));header.UpdateLayout();
            var visual=new DrawingVisual();
            using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle((Brush)app.Resources["SidebarBrush"],null,new Rect(0,0,250,68));
                dc.PushTransform(new TranslateTransform(10,10));
                dc.DrawRectangle(new VisualBrush(header){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top},null,new Rect(0,0,230,48));
            }
            var bitmap=new RenderTargetBitmap(250,68,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var output=File.Create("qa-brand-logo-"+(light?"light":"dark")+".png"))encoder.Save(output);
        }
        var toggle=(Button)main.FindName("DesktopLineButton");toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(app.DesktopLine.IsVisible,"The replacement image button still reveals the desktop clothesline");
        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!app.DesktopLine.IsVisible,"Clicking the same image button again still hides the clothesline");
        Check(!File.Exists(repo.TaskFile)&&!File.Exists(settings.FilePath),"Theme verification writes no task or settings data");
        app.Shutdown();
    }

    private static void CheckToolbar(MainWindow main,App app,bool light)
    {
        var button=(Button)main.FindName("DesktopLineButton");var icon=(Image)main.FindName("DesktopLineIcon");
        Check(ReferenceEquals(button.Content,icon)&&ReferenceEquals(icon.Source,ToolbarIconImages.DesktopClothesline(light)),"Toolbar chooses the uploaded "+(light?"Light":"Dark")+" image instead of a font glyph");
        Check(icon.Width==18&&icon.Height==16&&button.ToolTip.ToString()=="Show the desktop clothesline","Toolbar keeps its compact size and existing action label");
        string theme=light?"light":"dark";
        var raw=new BitmapImage(new Uri(Path.GetFullPath("Windows/HangLater/Resources/ToolbarIcons/"+theme+"/desktop-clothesline.jpg")));
        var original=Pixels(raw);var display=(BitmapSource)icon.Source;byte[] output=new byte[display.PixelWidth*display.PixelHeight*4];display.CopyPixels(output,display.PixelWidth*4,0);
        Check(raw.PixelWidth==display.PixelWidth&&raw.PixelHeight==display.PixelHeight,"The display texture keeps the source pixel geometry");
        bool rgbKept=true;int transparent=0,opaque=0;
        for(int i=0;i<output.Length;i+=4)
        {
            int brightness=Math.Max(original[i],Math.Max(original[i+1],original[i+2]));
            if(brightness<=3){if(output[i+3]!=0)rgbKept=false;transparent++;}
            else if(output[i]!=original[i]||output[i+1]!=original[i+1]||output[i+2]!=original[i+2])rgbKept=false;
            if(output[i+3]==255)opaque++;
        }
        Check(rgbKept&&transparent>output.Length/16&&opaque>0,"Source glyph RGB is preserved while the black matte is transparent");
        var root=(FrameworkElement)main.Content;root.Measure(new Size(1100,720));root.Arrange(new Rect(0,0,1100,720));root.UpdateLayout();
        Save(root,"qa-toolbar-page-"+theme+".png",1100,720,2);
        var comparison=new DrawingVisual();using(var dc=comparison.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White,null,new Rect(0,0,220,85));
            dc.DrawText(new FormattedText("Reference ("+theme+")",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,Brushes.Black,1),new Point(8,5));
            dc.DrawText(new FormattedText("Windows",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,Brushes.Black,1),new Point(126,5));
            dc.DrawRectangle(Brushes.Black,null,new Rect(8,25,button.ActualWidth,button.ActualHeight));
            var position=icon.TranslatePoint(new Point(0,0),button);double scale=Math.Min(icon.Width/raw.PixelWidth,icon.Height/raw.PixelHeight);
            double width=raw.PixelWidth*scale,height=raw.PixelHeight*scale;
            dc.DrawImage(raw,new Rect(8+position.X+(icon.Width-width)/2,25+position.Y+(icon.Height-height)/2,width,height));
            dc.DrawRectangle((Brush)app.Resources["PaperBrush"],null,new Rect(126,25,button.ActualWidth,button.ActualHeight));
            dc.PushTransform(new TranslateTransform(126,25));dc.DrawRectangle(new VisualBrush(button){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top},null,new Rect(0,0,button.ActualWidth,button.ActualHeight));dc.Pop();
        }
        Save(comparison,"qa-toolbar-compare-"+theme+".png",220,85,4);
    }

    private static void Save(Visual visual,string path,double width,double height,int scale)
    {
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width*scale),(int)Math.Ceiling(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
    }

    private static void CheckStatusCounts(MainWindow main,App app,bool light)
    {
        string theme=light?"light":"dark";
        foreach(var mapping in new[]{("PendingSummaryIcon","pending.png"),("CompletedSummaryIcon","completed.png")})
        {
            var icon=(Image)main.FindName(mapping.Item1);var source=(BitmapImage)icon.Source;
            var disk=new BitmapImage(new Uri(Path.GetFullPath("Windows/HangLater/Resources/StatusCountIcons/"+theme+"/"+mapping.Item2)));
            Check(source.UriSource.ToString().EndsWith(theme+"/"+mapping.Item2)&&Pixels(source).SequenceEqual(Pixels(disk)),"Status count uses the exact "+theme+"/"+mapping.Item2+" PNG pixels");
            Check(icon.Width==13&&icon.Height==13&&source.PixelWidth==26&&source.PixelHeight==26,"Retina status icon renders at its intended half-size logical dimensions");
        }
        Check(((TextBlock)main.FindName("PendingSummary")).Text=="0 pending"&&((TextBlock)main.FindName("CompletedSummary")).Text=="0 completed","Replacing status icons does not alter the count labels");
        var summary=(FrameworkElement)main.FindName("StatusCountSummary");
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle((Brush)app.Resources["PaperBrush"],null,new Rect(0,0,250,40));
            dc.PushTransform(new TranslateTransform(10,10));dc.DrawRectangle(new VisualBrush(summary){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top},null,new Rect(0,0,summary.ActualWidth,summary.ActualHeight));dc.Pop();
        }
        Save(visual,"qa-status-count-"+theme+".png",250,40,4);
    }

    private static byte[] Pixels(BitmapSource source)
    {
        var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
        byte[] pixels=new byte[converted.PixelWidth*converted.PixelHeight*4];converted.CopyPixels(pixels,converted.PixelWidth*4,0);return pixels;
    }

    private static void CheckFooterLayout(MainWindow main,bool light)
    {
        var root=(FrameworkElement)main.Content;
        var summary=(FrameworkElement)main.FindName("StatusCountSummary");
        var copy=(FrameworkElement)main.FindName("LighterDesktopCopy");
        var saved=(FrameworkElement)main.FindName("SavedOnThisComputerLabel");
        var footer=(FrameworkElement)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(saved)));
        var first=(FrameworkElement)main.FindName("FirstThingButton");
        double? firstY=null;
        foreach(double height in new[]{692d,720d,900d})
        {
            root.Measure(new Size(1100,height));root.Arrange(new Rect(0,0,1100,height));root.UpdateLayout();
            double footerY=footer.TranslatePoint(new Point(0,0),root).Y;
            double summaryY=summary.TranslatePoint(new Point(0,summary.ActualHeight/2),root).Y;
            double copyY=copy.TranslatePoint(new Point(0,copy.ActualHeight/2),root).Y;
            Check(Math.Abs((footerY-summaryY)-38.5)<2,"Summary keeps the reference-like bottom gap at viewport height "+height);
            Check(Math.Abs(summaryY-copyY)<1,"Counts and closing copy share one baseline at viewport height "+height);
            Check(first.TranslatePoint(new Point(0,first.ActualHeight),root).Y<summaryY-25,"Bottom status row does not overlap the empty-state action");
            double currentFirstY=first.TranslatePoint(new Point(0,0),root).Y;
            if(firstY.HasValue)Check(Math.Abs(firstY.Value-currentFirstY)<1,"Moving the bottom row does not move the empty-state action");else firstY=currentFirstY;
            if(height==720)Save(root,"qa-clothesline-footer-"+(light?"light":"dark")+".png",1100,height,2);
            Console.WriteLine($"FOOTER {height}: center-to-rule {footerY-summaryY:F2} DIP");
        }
        root.Measure(new Size(1100,720));root.Arrange(new Rect(0,0,1100,720));root.UpdateLayout();
    }
    private static void Check(bool success,string label){if(!success)throw new InvalidOperationException(label);Console.WriteLine("PASS "+label);}
}
