using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HangLater.Windows.Controls;

namespace HangLater.Windows.Views;

public static class SourceDialogLayout
{
    public static void Initialize(Window window,App app)
    {
        window.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/HangLater;component/Views/SourceDialogStyles.xaml")});
        window.Resources["DialogCopyFont"]=new FontFamily(app.Text.Chinese?"Microsoft YaHei UI":"Courier New");
        window.Resources["DialogChevron"]=SourceDialogIcons.Load("chevron-right",app.Settings.LightAppearance);
        window.Foreground=(Brush)app.Resources["InkBrush"];
        window.MaxHeight=Math.Max(340,SystemParameters.WorkArea.Height-24);
        window.MaxWidth=Math.Max(420,SystemParameters.WorkArea.Width-24);
    }
    public static void DragHeader(Window window,MouseButtonEventArgs e)
    {
        DependencyObject? target=e.OriginalSource as DependencyObject;
        while(target!=null){if(target is Button)return;target=VisualTreeHelper.GetParent(target);}
        if(e.LeftButton==MouseButtonState.Pressed)window.DragMove();
    }
}
