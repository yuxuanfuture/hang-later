using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace HangLater.Windows.Controls;

// WPF has no native tracking property. Keep real, accessible label text while
// rendering the source design's widely spaced small section headings.
public sealed class TrackedTextBlock : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(TrackedTextBlock),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public static readonly DependencyProperty LetterSpacingProperty = DependencyProperty.Register(
        nameof(LetterSpacing), typeof(double), typeof(TrackedTextBlock),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public double LetterSpacing { get => (double)GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }

    public TrackedTextBlock()
    {
        SetResourceReference(FontFamilyProperty, "MonoFont");
        SetResourceReference(ForegroundProperty, "InkBrush");
        Focusable = false; IsTabStop = false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LabelPeer(this);
    private sealed class LabelPeer : FrameworkElementAutomationPeer
    {
        public LabelPeer(TrackedTextBlock owner) : base(owner) { }
        protected override string GetNameCore() => ((TrackedTextBlock)Owner).Text;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }

    private FormattedText Letter(char character) => new FormattedText(character.ToString(), CultureInfo.CurrentUICulture,
        FlowDirection, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Foreground,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        foreach (char character in Text ?? "")
        {
            FormattedText letter = Letter(character);
            width += letter.WidthIncludingTrailingWhitespace + LetterSpacing;
            height = Math.Max(height, letter.Height);
        }
        return new Size(Math.Max(0, width - LetterSpacing), height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        double offset = 0;
        foreach (char character in Text ?? "")
        {
            FormattedText letter = Letter(character);
            drawingContext.DrawText(letter, new Point(offset, 0));
            offset += letter.WidthIncludingTrailingWhitespace + LetterSpacing;
        }
    }
}
