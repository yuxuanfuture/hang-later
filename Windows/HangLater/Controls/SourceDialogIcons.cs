using System;
using System.Windows.Media.Imaging;

namespace HangLater.Windows.Controls;

/// <summary>Exact user-supplied macOS icon exports, with separate Light/Dark variants.</summary>
public static class SourceDialogIcons
{
    public static BitmapImage Load(string name,bool light)
    {
        var source=new BitmapImage();source.BeginInit();
        source.UriSource=new Uri("pack://application:,,,/HangLater;component/Resources/HangSomethingIcons/"+(light?"light":"dark")+"/@2x/"+name+".png");
        source.CacheOption=BitmapCacheOption.OnLoad;source.EndInit();source.Freeze();return source;
    }
}
