using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HangLater.Windows.Controls;

// A receipt keeps its own paper/ink palette in both application appearances.
public sealed class ReceiptPaperStyle
{
    private static readonly Dictionary<long, ReceiptPaperStyle> Cache = new();
    public ImageBrush Surface { get; }
    public SolidColorBrush Ink { get; }
    public SolidColorBrush MutedInk { get; }
    public SolidColorBrush Outline { get; }
    public SolidColorBrush IconBackground { get; }

    private ReceiptPaperStyle(uint? tint)
    {
        Color color = tint.HasValue
            ? Color.FromRgb((byte)(tint.Value >> 16), (byte)(tint.Value >> 8), (byte)tint.Value)
            : Color.FromRgb(235, 232, 214);
        bool lightInk = tint.HasValue && 0.299 * color.R + 0.587 * color.G + 0.114 * color.B <= 150;
        Ink = new SolidColorBrush(lightInk ? Color.FromRgb(247, 247, 240) : Color.FromRgb(30, 30, 26));
        MutedInk = new SolidColorBrush(lightInk ? Color.FromRgb(198, 198, 219) : Color.FromRgb(114, 112, 101));
        Outline = new SolidColorBrush(Ink.Color) { Opacity = 0.55 };
        IconBackground = new SolidColorBrush(Ink.Color) { Opacity = 0.10 };
        BitmapImage paper = new BitmapImage(new Uri("pack://application:,,,/HangLater;component/Resources/le-labo-paper.png"));
        FormatConvertedBitmap source = new FormatConvertedBitmap(paper, PixelFormats.Bgra32, null, 0);
        int stride = source.PixelWidth * 4;
        byte[] pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            // Multiply the supplied paper grain into colored labels, without
            // washing dark task colors out with an opaque beige overlay.
            double grain = 0.6 + 0.4 * (0.114 * pixels[i] + 0.587 * pixels[i + 1] + 0.299 * pixels[i + 2]) / 255;
            pixels[i] = (byte)(tint.HasValue ? grain * color.B : pixels[i] * 0.55 + color.B * 0.45);
            pixels[i + 1] = (byte)(tint.HasValue ? grain * color.G : pixels[i + 1] * 0.55 + color.G * 0.45);
            pixels[i + 2] = (byte)(tint.HasValue ? grain * color.R : pixels[i + 2] * 0.55 + color.R * 0.45);
            pixels[i + 3] = 255;
        }
        BitmapSource texture = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        texture.Freeze();
        Surface = new ImageBrush(texture) { Stretch = Stretch.Fill };
        Surface.Freeze(); Ink.Freeze(); MutedInk.Freeze(); Outline.Freeze(); IconBackground.Freeze();
    }

    public static ReceiptPaperStyle For(uint? tint)
    {
        long key = tint.HasValue ? tint.Value : -1L;
        if (!Cache.TryGetValue(key, out ReceiptPaperStyle? style)) Cache[key] = style = new ReceiptPaperStyle(tint);
        return style;
    }
}
