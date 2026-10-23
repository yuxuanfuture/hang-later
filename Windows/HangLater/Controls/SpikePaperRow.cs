using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HangLater.Windows.Models;

namespace HangLater.Windows.Controls;

public partial class SpikePaperRow : UserControl
{
    public const string DragFormat = "HangLater.SpikeTask";
    public event Action? OpenRequested;
    public event Action? AttachmentRequested;
    public event Action<double>? ScaleRequested;
    private Point? dragOrigin;

    public SpikePaperRow(HangingTask task, bool on, bool chinese, uint? tint)
    {
        InitializeComponent();
        ReceiptPaperStyle style = ReceiptPaperStyle.For(tint);
        Paper.Background = style.Surface;
        Paper.BorderBrush = style.Outline;
        foreach (TextBlock text in new[] { TitleLabel, SmallLabel, LargeLabel }) text.Foreground = style.Ink;
        DocumentIcon.Background = style.Ink;
        TimeLabel.Foreground = style.MutedInk;
        DragGlyph.Background = style.MutedInk;
        OpenButton.Background = style.IconBackground;
        ActionButton.Foreground = ReceiptPaperStyle.For(null).Ink;
        ActionButton.BorderBrush = ReceiptPaperStyle.For(null).Outline;
        if (tint.HasValue) ActionButton.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        SizeSlider.Foreground = style.Ink;
        TitleLabel.Text = task.title;
        TitleLabel.ToolTip = task.title;
        CultureInfo culture = CultureInfo.GetCultureInfo(chinese ? "zh-CN" : "en-US");
        string format = chinese ? "M月d日 HH:mm" : "MMM d, HH:mm";
        string published = task.createdAt.ToLocalTime().ToString(format, culture);
        string completed = (task.completedAt ?? task.createdAt).ToLocalTime().ToString(format, culture);
        TimeLabel.Text = chinese ? $"发布 {published}  ·  完成 {completed}" : $"Published {published}  ·  Completed {completed}";
        TimeLabel.ToolTip = TimeLabel.Text;
        SmallLabel.Text = chinese ? "小" : "S";
        LargeLabel.Text = chinese ? "大" : "L";
        ActionButton.Content = new TextBlock
        {
            Text = on ? (chinese ? "取下" : "Take off") : (chinese ? "串回" : "Put back"),
            Foreground = ReceiptPaperStyle.For(null).Ink, FontSize = 11, FontWeight = FontWeights.Bold
        };
        SizeControls.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        SizeSlider.Value = task.EffectiveSpikeScale;
        AutomationProperties.SetName(SizeSlider, chinese ? "纸片大小" : "Slip size");
        OpenButton.ToolTip = chinese ? "打开这件事" : "Open this task";
        DragHandle.ToolTip = chinese ? "按住拖动纸片" : "Drag this slip";
        OpenButton.Click += (_, _) => OpenRequested?.Invoke();
        ActionButton.Click += (_, _) => AttachmentRequested?.Invoke();
        SizeSlider.ValueChanged += (_, _) =>
        {
            SizeSlider.ToolTip = (chinese ? "纸片大小 " : "Slip size ") + $"{SizeSlider.Value:P0}";
            ScaleRequested?.Invoke(SizeSlider.Value);
        };
        DragHandle.MouseLeftButtonDown += (_, e) =>
        {
            dragOrigin = e.GetPosition(DragHandle);
            DragHandle.CaptureMouse();
            e.Handled = true;
        };
        DragHandle.MouseLeftButtonUp += (_, _) => { dragOrigin = null; DragHandle.ReleaseMouseCapture(); };
        DragHandle.MouseLeave += (_, _) => { if (Mouse.LeftButton != MouseButtonState.Pressed) dragOrigin = null; };
        DragHandle.MouseMove += (_, e) =>
        {
            if (dragOrigin is not Point origin || e.LeftButton != MouseButtonState.Pressed) return;
            Vector distance = e.GetPosition(DragHandle) - origin;
            if (Math.Abs(distance.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(distance.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            dragOrigin = null;
            DragHandle.ReleaseMouseCapture();
            DragDrop.DoDragDrop(DragHandle, new DataObject(DragFormat, task.id), DragDropEffects.Move);
        };
    }
}
