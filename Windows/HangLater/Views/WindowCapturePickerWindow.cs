using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using HangLater.Windows.Controls;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Views;

public sealed class WindowCapturePickerWindow : Window
{
    private readonly App app;
    public ListBox WindowsList { get; } = new ListBox { Name="CaptureWindowList",Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch };
    public Button CaptureButton { get; } = new Button { Name="CaptureButton",IsEnabled=false };
    private readonly TextBlock message=new TextBlock { TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed,Margin=new Thickness(0,10,0,0) };
    private bool busy;

    public WindowCapturePickerWindow(App app,IEnumerable<CapturableWindow>? fixtureWindows=null)
    {
        this.app=app;Width=560;Height=560;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        SourceDialogLayout.Initialize(this,app);Title=app.Text.T("截取 App 窗口","Capture an app window");
        var root=new Grid{Margin=new Thickness(28)};
        foreach(var height in new[]{GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto})root.RowDefinitions.Add(new RowDefinition{Height=height});
        var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});
        var text=new StackPanel{Margin=new Thickness(0,0,14,0)};
        text.Children.Add(new TextBlock{Text=app.Text.T("晾起一个 App 窗口。","HANG AN APP WINDOW."),Style=(Style)Resources["DialogHeading"]});
        text.Children.Add(new TextBlock{Text=app.Text.T("选择窗口后，会保存当前画面；点纸片会重新唤起这个 App。","Choose a window to save its current view. Clicking the card brings the app back."),Style=(Style)Resources["DialogCopy"],Margin=new Thickness(0,7,0,0)});header.Children.Add(text);
        var close=new Button{Style=(Style)Resources["DialogIconButton"],Content=new Image{Source=SourceDialogIcons.Load("close",app.Settings.LightAppearance)},VerticalAlignment=VerticalAlignment.Top,IsCancel=true};Grid.SetColumn(close,1);close.Click+=(_,_)=>DialogResult=false;AutomationProperties.SetName(close,app.Text.T("关闭","Close"));header.Children.Add(close);header.MouseLeftButtonDown+=(_,e)=>SourceDialogLayout.DragHeader(this,e);root.Children.Add(header);
        message.Style=(Style)Resources["DialogCopy"];Grid.SetRow(message,1);root.Children.Add(message);
        WindowsList.Margin=new Thickness(0,18,0,0);WindowsList.Foreground=(Brush)app.Resources["InkBrush"];Grid.SetRow(WindowsList,2);root.Children.Add(WindowsList);
        WindowsList.SelectionChanged+=(_,_)=>CaptureButton.IsEnabled=!busy&&WindowsList.SelectedItem!=null;
        WindowsList.MouseDoubleClick+=async(_,_)=>await CaptureSelected();
        var footer=new Grid{Margin=new Thickness(0,18,0,0)};footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var refresh=new Button{Content=app.Text.T("刷新窗口","Refresh windows"),Style=(Style)Resources["DialogButton"],HorizontalAlignment=HorizontalAlignment.Left};refresh.Click+=(_,_)=>{if(!busy)LoadWindows(WindowCaptureService.ListWindows());};footer.Children.Add(refresh);
        var actions=new StackPanel{Orientation=Orientation.Horizontal};Grid.SetColumn(actions,1);footer.Children.Add(actions);
        var cancel=new Button{Content=app.Text.T("取消","Cancel"),Style=(Style)Resources["DialogButton"],Margin=new Thickness(0,0,8,0)};cancel.Click+=(_,_)=>DialogResult=false;actions.Children.Add(cancel);
        CaptureButton.Content=app.Text.T("截取并挂上","Capture and hang");CaptureButton.Style=(Style)Resources["DialogPrimaryButton"];CaptureButton.Click+=async(_,_)=>await CaptureSelected();actions.Children.Add(CaptureButton);
        Grid.SetRow(footer,3);root.Children.Add(footer);
        Content=new Border{CornerRadius=new CornerRadius(12),Background=(Brush)app.Resources["PaperBrush"],BorderBrush=(Brush)app.Resources["BorderBrush"],BorderThickness=new Thickness(1),Child=root};
        LoadWindows(fixtureWindows??WindowCaptureService.ListWindows());
    }

    private void LoadWindows(IEnumerable<CapturableWindow> windows)
    {
        WindowsList.Items.Clear();CaptureButton.IsEnabled=false;
        foreach(var window in windows.Where(x=>!(x.ProcessName=="explorer"&&x.Title=="Program Manager")))
        {
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(34)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(13)});row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new Image{Source=SourceDialogIcons.Load("capture-app-window",app.Settings.LightAppearance),Width=26,Height=26});
            var text=new StackPanel();text.Children.Add(new TextBlock{Text=window.Title,FontFamily=new FontFamily("Arial"),FontStretch=FontStretches.Condensed,FontWeight=FontWeights.Bold,FontSize=13,Foreground=(Brush)app.Resources["InkBrush"],TextTrimming=TextTrimming.CharacterEllipsis});
            text.Children.Add(new TextBlock{Text=$"{window.ProcessName} · {window.Bounds.Right-window.Bounds.Left} × {window.Bounds.Bottom-window.Bounds.Top}",Style=(Style)Resources["DialogCopy"],FontSize=9,Margin=new Thickness(0,5,0,0)});Grid.SetColumn(text,2);row.Children.Add(text);
            var item=new ListBoxItem{Tag=window,Content=new Border{Background=(Brush)app.Resources["RaisedBrush"],BorderBrush=(Brush)app.Resources["BorderBrush"],BorderThickness=new Thickness(1),Padding=new Thickness(13),Child=row},Padding=new Thickness(0),Margin=new Thickness(0,0,0,9),HorizontalContentAlignment=HorizontalAlignment.Stretch,Background=Brushes.Transparent,Foreground=(Brush)app.Resources["InkBrush"]};
            AutomationProperties.SetName(item,window.Title+" · "+window.ProcessName);WindowsList.Items.Add(item);
        }
        message.Text=app.Text.T("没有找到可晾起的窗口。先打开一个 App 再试。","No available windows were found. Open an app and try again.");message.Visibility=WindowsList.Items.Count==0?Visibility.Visible:Visibility.Collapsed;
    }
    private async Task CaptureSelected()
    {
        if(busy||WindowsList.SelectedItem is not ListBoxItem item||item.Tag is not CapturableWindow original)return;
        busy=true;CaptureButton.IsEnabled=false;
        try
        {
            // Keep the modal loop alive while clearing the picker from the capture.
            // Hide() would end ShowDialog before the asynchronous capture finishes.
            Opacity=0;IsHitTestVisible=false;((UIElement)Content).IsEnabled=false;await Task.Delay(180);
            var window=WindowCaptureService.Snapshot(original.Handle);
            if(!WindowDropReturnService.SameWindow(original,window))throw new InvalidOperationException(app.Text.T("这个窗口已关闭或发生变化，请刷新后重试。","This window changed or closed. Refresh and try again."));
            string preview=await Task.Run(()=>WindowCaptureService.Capture(window!,app.Repository.PreviewDirectory));
            var task=app.Repository.Add(window!.Title,previewFile:preview,appName:window.ProcessName);
            WindowCaptureService.SetCapturedTarget(task,window);app.Repository.Update(task);DialogResult=true;
        }
        catch(Exception error){message.Text=error.Message;message.Visibility=Visibility.Visible;}
        finally
        {
            busy=false;CaptureButton.IsEnabled=WindowsList.SelectedItem!=null;
            if(IsVisible){Opacity=1;IsHitTestVisible=true;((UIElement)Content).IsEnabled=true;}
        }
    }
}
