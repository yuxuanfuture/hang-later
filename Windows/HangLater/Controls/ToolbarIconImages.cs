using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HangLater.Windows.Controls;

public static class ToolbarIconImages
{
    private static BitmapSource? lightIcon;
    private static BitmapSource? darkIcon;

    public static BitmapSource DesktopClothesline(bool light)
    {
        if(light && lightIcon!=null)return lightIcon;
        if(!light && darkIcon!=null)return darkIcon;
        var original=new BitmapImage();original.BeginInit();
        original.UriSource=new Uri("pack://application:,,,/HangLater;component/Resources/ToolbarIcons/"+(light?"light":"dark")+"/desktop-clothesline.jpg");
        original.CacheOption=BitmapCacheOption.OnLoad;original.EndInit();original.Freeze();

        // The supplied JPEG exports are antialiased against black. Interpret that
        // matte as coverage at display time, keeping the source files and glyph RGB
        // samples intact instead of painting an opaque black tile on a light header.
        var decoded=new FormatConvertedBitmap(original,PixelFormats.Bgra32,null,0);
        int stride=decoded.PixelWidth*4;byte[] pixels=new byte[stride*decoded.PixelHeight];decoded.CopyPixels(pixels,stride,0);
        int peak=0;
        for(int i=0;i<pixels.Length;i+=4)peak=Math.Max(peak,Math.Max(pixels[i],Math.Max(pixels[i+1],pixels[i+2])));
        for(int i=0;i<pixels.Length;i+=4)
        {
            int intensity=Math.Max(pixels[i],Math.Max(pixels[i+1],pixels[i+2]));
            if(intensity<=3){pixels[i]=pixels[i+1]=pixels[i+2]=pixels[i+3]=0;}
            else pixels[i+3]=(byte)Math.Round(255d*intensity/peak);
        }
        var icon=BitmapSource.Create(decoded.PixelWidth,decoded.PixelHeight,96,96,PixelFormats.Pbgra32,null,pixels,stride);icon.Freeze();
        if(light)lightIcon=icon;else darkIcon=icon;
        return icon;
    }
}
