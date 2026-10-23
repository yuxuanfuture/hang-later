using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Controls;

// Row structure and sizes follow MainView.swift's archived taskRow (MIT).
public sealed class CompletedTaskRow : Border
{
    public Button RestoreButton { get; }
    public Button PreviewButton { get; }
    public Button DeleteButton { get; }
    public TextBlock TitleLabel { get; }
    public TextBlock MetadataLabel { get; }

    public CompletedTaskRow(App app, HangingTask task, Style plainButton, Style quietButton)
    {
        Brush Surface(string key, double opacity) { Brush brush = ((Brush)app.Resources[key]).Clone(); brush.Opacity = opacity; return brush; }
        Background = Surface("RaisedBrush", 0.75);
        BorderBrush = Surface("BorderBrush", 0.7); BorderThickness = new Thickness(0.7);
        Padding = new Thickness(19,16,16,16); Margin = new Thickness(0, 0, 0, 10);
        Grid layout = new Grid();
        foreach (double width in new[] { 20.0, 15, 57, 15, -1, 15, -2, 15, 14 })
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = width == -1 ? new GridLength(1, GridUnitType.Star) : width == -2 ? GridLength.Auto : new GridLength(width) });
        void Add(UIElement element, int column) { Grid.SetColumn(element, column); layout.Children.Add(element); }
        TextBlock Icon(string glyph, double size, Brush ink) => new TextBlock { Text = glyph, FontFamily = (FontFamily)app.Resources["IconFont"], FontSize = size, Foreground = ink, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        void Restore() { app.Repository.Restore(task.id); app.RefreshAll(); }
        Button status = new Button { Style = plainButton, Content = Icon("\uEC61", 20, (Brush)app.Resources["AccentBrush"]), ToolTip = app.Text.T("重新挂回晾衣绳", "Hang on the clothesline again"), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(status, app.Text.T("重新挂回晾衣绳", "Hang on the clothesline again"));
        status.Click += (_, _) => Restore(); Add(status, 0);
        var preview = ShellThumbnailService.LoadPreview(app.Repository, task, 180);
        Border tile = new Border { Width = 57, Height = 45, ClipToBounds = true, Background = (Brush)app.Resources["PanelBrush"] };
        tile.Child = preview != null ? new Image { Source = preview, Stretch = Stretch.UniformToFill }
            : Icon("\uE8A5", 20, (Brush)app.Resources["MutedBrush"]);
        PreviewButton = new Button { Style = plainButton, Content = tile, ToolTip = preview == null ? app.Text.T("打开任务内容", "Open task content") : app.Text.T("放大预览图", "Enlarge preview") };
        AutomationProperties.SetName(PreviewButton, PreviewButton.ToolTip.ToString());
        PreviewButton.Click += (_, _) =>
        {
            if (preview == null) { app.OpenTask(task); return; }
            Window viewer = new Window
            {
                Title = task.title, Owner = Window.GetWindow(this), Width = 900, Height = 640,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = (Brush)app.Resources["PaperBrush"],
                Content = new Image { Source = preview, Stretch = Stretch.Uniform, Margin = new Thickness(16) }
            };
            viewer.Show();
        };
        Add(PreviewButton, 2);
        FontFamily display = new FontFamily("Arial, Microsoft YaHei UI");
        TitleLabel = new TextBlock
        {
            Text = task.title, ToolTip = task.title, FontFamily = display,
            FontStretch = FontStretches.Condensed,
            FontSize = 13, FontWeight = FontWeights.Bold, Foreground = (Brush)app.Resources["InkBrush"], TextTrimming = TextTrimming.CharacterEllipsis
        };
        CultureInfo culture = CultureInfo.GetCultureInfo(app.Text.Chinese ? "zh-CN" : "en-US");
        string Date(DateTimeOffset date) => date.ToLocalTime().ToString(app.Text.Chinese ? "M月d日 HH:mm" : "MMM d, HH:mm", culture);
        string source = task.SourceLabel switch
        {
            "chrome" => "Google Chrome", "code" => "Code", "explorer" => app.Text.T("文件资源管理器", "File Explorer"),
            "AN IDEA" => app.Text.T("一个想法", "An idea"), "A SMALL THING" => app.Text.T("一件小事", "A small thing"), _ => task.SourceLabel
        };
        MetadataLabel = new TextBlock
        {
            Text = source + app.Text.T(" · 发布 ", " · Published ") + Date(task.createdAt) + app.Text.T(" · 完成 ", " · Completed ") + Date(task.completedAt ?? task.createdAt),
            FontFamily = (FontFamily)app.Resources["MonoFont"], FontSize = 11, Foreground = (Brush)app.Resources["MutedBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 7, 0, 0)
        };
        MetadataLabel.ToolTip = MetadataLabel.Text;
        Button content = new Button { Style = plainButton, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { TitleLabel, MetadataLabel } } };
        AutomationProperties.SetName(content, task.title);
        content.Click += (_, _) => app.OpenTask(task); Add(content, 4);
        RestoreButton = new Button
        {
            Style = quietButton, VerticalAlignment = VerticalAlignment.Center,
            Content = new TextBlock { Text = app.Text.T("再晾一次", "Hang again"), FontFamily = app.Text.Chinese ? new FontFamily("Microsoft YaHei UI") : (FontFamily)app.Resources["MonoFont"], FontSize = 11, FontWeight = FontWeights.Bold }
        };
        AutomationProperties.SetName(RestoreButton, app.Text.T("再晾一次", "Hang again"));
        RestoreButton.Click += (_, _) => Restore(); Add(RestoreButton, 6);
        DeleteButton = new Button { Style = plainButton, Content = Icon("\uE74D", 14, (Brush)app.Resources["MutedBrush"]), ToolTip = app.Text.T("永久删除", "Delete permanently"), Height = 30, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(DeleteButton, app.Text.T("永久删除", "Delete permanently"));
        DeleteButton.Click += (_, _) =>
        {
            if (MessageBox.Show(Window.GetWindow(this), app.Text.T("永久删除这件任务？此操作无法撤销。", "Delete this task permanently? This cannot be undone."), "Hang Later", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            app.Repository.Delete(task.id); app.RefreshAll();
        };
        Add(DeleteButton, 8);
        Child = layout;
    }
}
