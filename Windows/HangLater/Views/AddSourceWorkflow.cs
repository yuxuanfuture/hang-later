using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using HangLater.Windows.Controls;
using HangLater.Windows.Services;
using Microsoft.Win32;

namespace HangLater.Windows.Views;

public static class AddSourceWorkflow
{
    public static bool Run(App app,Window owner,AddSourceChoice choice)
    {
        switch(choice)
        {
            case AddSourceChoice.Task:return new TaskComposerWindow(app){Owner=owner}.ShowDialog()==true;
            case AddSourceChoice.Reminder:return new TaskComposerWindow(app,reminder:true){Owner=owner}.ShowDialog()==true;
            case AddSourceChoice.Window:return new WindowCapturePickerWindow(app){Owner=owner}.ShowDialog()==true;
            case AddSourceChoice.Image:
                var dialog=new OpenFileDialog{Multiselect=true,Title=app.Text.T("选择要晾起来的图片或文件","Choose an image or file to hang"),Filter=app.Text.T("图片|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.tif;*.tiff|PDF 文件|*.pdf|所有文件|*.*","Images|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.tif;*.tiff|PDF files|*.pdf|All files|*.*")};
                if(dialog.ShowDialog(owner)!=true)return false;
                foreach(string path in dialog.FileNames)app.Repository.Add(Path.GetFileName(path),filePath:path);
                return dialog.FileNames.Length>0;
            case AddSourceChoice.Chrome:new SourceChromeGuideWindow(app){Owner=owner}.ShowDialog();return false;
            default:return false;
        }
    }
}

public sealed class SourceChromeGuideWindow : Window
{
    public SourceChromeGuideWindow(App app)
    {
        Width=540;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;SourceDialogLayout.Initialize(this,app);
        Title=app.Text.T("连接 Chrome","Connect Chrome");
        var panel=new StackPanel{Margin=new Thickness(28)};
        var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});header.Children.Add(new TextBlock{Text=app.Text.T("从 Chrome 晾网页或视频。","HANG FROM CHROME."),Style=(Style)Resources["DialogHeading"]});
        var close=new Button{Style=(Style)Resources["DialogIconButton"],Content=new Image{Source=SourceDialogIcons.Load("close",app.Settings.LightAppearance)},IsCancel=true};Grid.SetColumn(close,1);close.Click+=(_,_)=>DialogResult=false;AutomationProperties.SetName(close,app.Text.T("关闭","Close"));header.Children.Add(close);header.MouseLeftButtonDown+=(_,e)=>SourceDialogLayout.DragHeader(this,e);panel.Children.Add(header);
        panel.Children.Add(new TextBlock{Style=(Style)Resources["DialogCopy"],Margin=new Thickness(0,14,0,18),Text=app.Text.T("先连接本地桥接程序，再在 Chrome 加载扩展。打开网页后，点击扩展即可晾起当前页面或视频。","Connect the local bridge, then load the Chrome extension. Click the extension on a page to hang the page or video.")});
        panel.Children.Add(new TextBlock{Style=(Style)Resources["DialogCopy"],Text=app.Text.T("在 chrome://extensions 开启开发者模式，点击“加载已解压的扩展程序”，选择下列文件夹。","At chrome://extensions, enable Developer mode, choose Load unpacked, and select this folder.")});
        string folder=ChromeBridgeService.EnsureExtensionFiles(app.Repository.RootDirectory);
        panel.Children.Add(new TextBox{Text=folder,IsReadOnly=true,Style=(Style)Resources["DialogInput"],TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)});
        var status=new TextBlock{Style=(Style)Resources["DialogCopy"],Margin=new Thickness(0,14,0,0),Visibility=Visibility.Collapsed};panel.Children.Add(status);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0)};
        var open=new Button{Content=app.Text.T("打开扩展文件夹","Open extension folder"),Style=(Style)Resources["DialogButton"],Margin=new Thickness(0,0,8,0)};
        open.Click+=(_,_)=>{if(Directory.Exists(folder))Process.Start(new ProcessStartInfo(folder){UseShellExecute=true});};buttons.Children.Add(open);
        var install=new Button{Content=app.Text.T("连接 Chrome","Connect Chrome"),Style=(Style)Resources["DialogPrimaryButton"]};
        install.Click+=(_,_)=>{try{ChromeBridgeService.Install(app.Repository.RootDirectory,Environment.ProcessPath!);status.Text=app.Text.T("本地桥接已连接。接下来加载扩展，并把 Hang Later 固定到工具栏。","Local bridge connected. Load the extension and pin Hang Later to the toolbar.");}catch(Exception error){status.Text=error.Message;}status.Visibility=Visibility.Visible;};buttons.Children.Add(install);panel.Children.Add(buttons);
        Content=new Border{Background=(Brush)app.Resources["PaperBrush"],BorderBrush=(Brush)app.Resources["BorderBrush"],BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Child=panel};
    }
}
