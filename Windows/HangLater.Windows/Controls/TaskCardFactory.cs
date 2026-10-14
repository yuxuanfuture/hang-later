using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HangLater.Windows.Models;
using HangLater.Windows.Services;
using HangLater.Windows.Views;

namespace HangLater.Windows.Controls;

public static class TaskCardFactory
{
    public static Border CreateListCard(App app, HangingTask task, bool completed = false)
    {
        var card = new Border { Background = (Brush)app.Resources["PanelBrush"], BorderBrush = (Brush)app.Resources["BorderBrush"], BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(20, 16, 20, 16) };
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var circle = new Button { Content = completed ? "✓" : "○", FontSize = 20, Padding = new Thickness(4), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        circle.Click += (_, _) => { if (completed) app.Repository.Restore(task.id); else app.Repository.Complete(task.id); app.RefreshAll(); };
        grid.Children.Add(circle);
        var preview = Preview(app, task, 84, 68); Grid.SetColumn(preview, 1); grid.Children.Add(preview);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        text.Children.Add(new TextBlock { Text = task.title, FontFamily = (FontFamily)app.Resources["DisplayFont"], FontWeight = FontWeights.SemiBold, FontSize = 17, TextTrimming = TextTrimming.CharacterEllipsis });
        var detail = task.SourceLabel + (completed ? $"  ·  {task.completedAt?.LocalDateTime:g}" : task.kind == HangingTaskKind.idea ? "  ·  AN IDEA" : task.dueAt is null ? "" : $"  ·  ⏰ {task.dueAt?.LocalDateTime:g}");
        text.Children.Add(new TextBlock { Text = detail, Foreground = (Brush)app.Resources["MutedBrush"], Margin = new Thickness(0, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(text, 2); grid.Children.Add(text);
        var action = new Button { Content = completed ? app.Text.T("重新挂起", "Hang again") : "⋯", Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        if (completed) action.Click += (_, _) => { app.Repository.Restore(task.id); app.RefreshAll(); };
        else { action.ContextMenu = TaskMenu(app, task); action.Click += (_, _) => action.ContextMenu.IsOpen = true; }
        Grid.SetColumn(action, 3); grid.Children.Add(action);
        card.Child = grid;
        card.MouseLeftButtonUp += (_, e) => { if (FindButton(e.OriginalSource as DependencyObject) is null) app.OpenTask(task); };
        return card;
    }

    public static Border CreateClotheslineCard(App app, HangingTask task, bool compact, Action<double>? moved = null)
    {
        var width = compact ? 220d : 248d;
        var card = new Border { Width = width, Background = (Brush)app.Resources["SlipBrush"], BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 3, Opacity = .34 }, Cursor = Cursors.Hand };
        var rows = new StackPanel();
        if (task.kind == HangingTaskKind.idea)
        {
            var idea = new TextBlock { Text = "IDEA", Foreground = (Brush)app.Resources["SlipInkBrush"], FontFamily = (FontFamily)app.Resources["DisplayFont"], FontWeight = FontWeights.Bold, FontSize = 13, Margin = new Thickness(15, 14, 15, 4) };
            rows.Children.Add(idea);
            rows.Children.Add(new TextBlock { Text = task.title, Foreground = (Brush)app.Resources["SlipInkBrush"], FontSize = 19, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(15, 8, 15, 22), MinHeight = compact ? 62 : 82 });
        }
        else
        {
            var preview = Preview(app, task, width - 2, compact ? 104 : 132); preview.Margin = new Thickness(0); rows.Children.Add(preview);
            rows.Children.Add(new TextBlock { Text = task.title, Foreground = (Brush)app.Resources["SlipInkBrush"], FontFamily = (FontFamily)app.Resources["DisplayFont"], FontWeight = FontWeights.Bold, FontSize = compact ? 15 : 17, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(14, 10, 14, 2) });
            rows.Children.Add(new TextBlock { Text = task.SourceLabel, Foreground = Brushes.DimGray, FontSize = 11, Margin = new Thickness(14, 0, 14, 10) });
        }
        var done = new Grid { Height = compact ? 42 : 48, Background = DoneBrush(app, task) };
        done.ColumnDefinitions.Add(new ColumnDefinition()); done.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var doneButton = new Button { Content = app.Text.T("○  完成", "○  Done"), Foreground = TaskTypeForeground(app, task), Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Left };
        doneButton.Click += (_, e) => { e.Handled = true; app.Repository.Complete(task.id); app.RefreshAll(); };
        done.Children.Add(doneButton);
        var more = new Button { Content = "•••", Foreground = TaskTypeForeground(app, task), Background = Brushes.Transparent, BorderThickness = new Thickness(0), ContextMenu = TaskMenu(app, task) };
        more.Click += (_, e) => { e.Handled = true; more.ContextMenu.IsOpen = true; }; Grid.SetColumn(more, 1); done.Children.Add(more);
        rows.Children.Add(new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 0), Child = done }); card.Child = rows;

        Point start = default; double original = 0; bool dragging = false;
        card.PreviewMouseLeftButtonDown += (_, e) => { if (FindButton(e.OriginalSource as DependencyObject) is not null) return; start = e.GetPosition(card.Parent as IInputElement); original = Canvas.GetLeft(card); if (double.IsNaN(original)) original = 0; dragging = false; card.CaptureMouse(); };
        card.PreviewMouseMove += (_, e) => { if (!card.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed || card.Parent is not Canvas canvas) return; var current = e.GetPosition(canvas); if (Math.Abs(current.X - start.X) > 3) dragging = true; Canvas.SetLeft(card, Math.Clamp(original + current.X - start.X, 0, Math.Max(0, canvas.ActualWidth - card.Width))); Panel.SetZIndex(card, 1000); };
        card.PreviewMouseLeftButtonUp += (_, e) => { if (!card.IsMouseCaptured) return; card.ReleaseMouseCapture(); if (dragging && card.Parent is Canvas canvas) moved?.Invoke(Canvas.GetLeft(card) / Math.Max(1, canvas.ActualWidth - card.Width)); else app.OpenTask(task); };
        return card;
    }

    public static ContextMenu TaskMenu(App app, HangingTask task)
    {
        var menu = new ContextMenu();
        var edit = new MenuItem { Header = app.Text.T("编辑", "Edit") }; edit.Click += (_, _) => { var dialog = new TaskEditorWindow(app, task) { Owner = app.MainWindowInstance }; if (dialog.ShowDialog() == true) app.RefreshAll(); }; menu.Items.Add(edit);
        var reminder = new MenuItem { Header = task.dueAt is null ? app.Text.T("添加提醒…", "Add reminder…") : app.Text.T("修改提醒…", "Change reminder…") };
        reminder.Click += (_, _) => { var dialog = new ReminderDialog(app, task) { Owner = app.MainWindowInstance }; if (dialog.ShowDialog() == true) app.RefreshAll(); }; menu.Items.Add(reminder);
        var types = new MenuItem { Header = app.Text.T("任务类型", "Task type") };
        var none = new MenuItem { Header = app.Text.T("默认", "Default"), IsCheckable = true, IsChecked = task.taskTypeID is null }; none.Click += (_, _) => { var copy = task.Clone(); copy.taskTypeID = null; app.Repository.Update(copy); app.RefreshAll(); }; types.Items.Add(none);
        foreach (var type in app.Repository.TaskTypes)
        {
            var item = new MenuItem { Header = type.name, IsCheckable = true, IsChecked = task.taskTypeID == type.id };
            item.Click += (_, _) => { var copy = task.Clone(); copy.taskTypeID = type.id; app.Repository.Update(copy); app.RefreshAll(); }; types.Items.Add(item);
        }
        menu.Items.Add(types); menu.Items.Add(new Separator());
        var delete = new MenuItem { Header = app.Text.T("删除", "Delete") }; delete.Click += (_, _) => { app.Repository.Delete(task.id); app.RefreshAll(); }; menu.Items.Add(delete);
        return menu;
    }

    private static FrameworkElement Preview(App app, HangingTask task, double width, double height)
    {
        var bitmap = ShellThumbnailService.LoadPreview(app.Repository, task, (int)Math.Max(width, height) * 2);
        if (bitmap is not null) return new Image { Source = bitmap, Width = width, Height = height, Stretch = Stretch.UniformToFill };
        return new Border { Width = width, Height = height, Background = new SolidColorBrush(Color.FromRgb(220, 213, 195)), Child = new TextBlock { Text = task.kind == HangingTaskKind.reminder ? "🔔" : task.kind == HangingTaskKind.meeting ? "↗" : "⌁", Foreground = Brushes.Black, FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
    }
    private static System.Windows.Media.Brush DoneBrush(App app, HangingTask task)
    {
        var type = app.Repository.TaskTypes.FirstOrDefault(x => x.id == task.taskTypeID);
        if (type is null) return (Brush)app.Resources["SlipBrush"];
        return new SolidColorBrush(Color.FromRgb((byte)(type.colorHex >> 16), (byte)(type.colorHex >> 8), (byte)type.colorHex));
    }
    private static System.Windows.Media.Brush TaskTypeForeground(App app, HangingTask task)
    {
        var type = app.Repository.TaskTypes.FirstOrDefault(x => x.id == task.taskTypeID);
        if (type is null) return Brushes.Black;
        var r = (type.colorHex >> 16) & 255; var g = (type.colorHex >> 8) & 255; var b = type.colorHex & 255;
        return (.299 * r + .587 * g + .114 * b) > 150 ? Brushes.Black : Brushes.White;
    }
    private static Button? FindButton(DependencyObject? current)
    {
        while (current is not null) { if (current is Button button) return button; current = VisualTreeHelper.GetParent(current); }
        return null;
    }
}
