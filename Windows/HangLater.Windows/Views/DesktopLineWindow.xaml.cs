using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HangLater.Windows.Controls;
using HangLater.Windows.Services;
using FormsScreen = System.Windows.Forms.Screen;

namespace HangLater.Windows.Views;

public partial class DesktopLineWindow : Window
{
    private readonly App app;
    private readonly DispatcherTimer leaveTimer = new() { Interval = TimeSpan.FromMilliseconds(420) };
    public DesktopLineWindow(App app)
    {
        this.app = app; InitializeComponent();
        MouseEnter += (_, _) => leaveTimer.Stop();
        MouseLeave += (_, _) => { if (!app.Settings.KeepClotheslineVisible) leaveTimer.Start(); };
        leaveTimer.Tick += (_, _) => { leaveTimer.Stop(); if (!IsMouseOver && !app.Settings.KeepClotheslineVisible) Hide(); };
        Closing += (_, e) => { if (!app.IsExiting) { e.Cancel = true; Hide(); } };
        SizeChanged += (_, _) => { Canvas.SetLeft(DropZone, Math.Max(12, (ActualWidth - DropZone.Width) / 2)); RefreshTasks(); };
        LocalizeAndRefresh();
    }
    public void PlaceOn(FormsScreen screen, double verticalPosition)
    {
        var scale = NativeMethods.ScaleAt(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
        var scaleX = scale.X; var scaleY = scale.Y;
        var area = screen.WorkingArea; Width = area.Width / scaleX; Height = Math.Min(330, area.Height / scaleY * .38);
        Left = area.Left / scaleX; var available = area.Height / scaleY - Height; Top = area.Top / scaleY + available * Math.Clamp(verticalPosition, 0, 1);
    }
    public void LocalizeAndRefresh() { DropText.Text = app.Text.T("📌  拖一件事到这里", "📌  DRAG SOMETHING HERE"); RefreshTasks(); }
    public void RefreshTasks()
    {
        foreach (var card in LineCanvas.Children.OfType<Border>().Where(x => x != DropZone).ToArray()) LineCanvas.Children.Remove(card);
        var width = Math.Max(900, ActualWidth); var cards = app.Repository.Pending.OrderBy(x => x.lineLayer ?? 0).ToArray();
        foreach (var task in cards)
        {
            var card = TaskCardFactory.CreateClotheslineCard(app, task, true, ratio => app.Repository.Place(task.id, ratio));
            Canvas.SetLeft(card, (task.lineX ?? .5) * Math.Max(1, width - card.Width)); Canvas.SetTop(card, 112 + (task.id.GetHashCode() & 23)); Panel.SetZIndex(card, task.lineLayer ?? 0); LineCanvas.Children.Add(card);
        }
        Canvas.SetLeft(DropZone, Math.Max(12, (width - DropZone.Width) / 2)); Panel.SetZIndex(DropZone, 2000);
    }
    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        var over = e.GetPosition(DropZone); var accepted = over.X >= 0 && over.Y >= 0 && over.X <= DropZone.ActualWidth && over.Y <= DropZone.ActualHeight;
        e.Effects = accepted && (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text)) ? DragDropEffects.Copy : DragDropEffects.None; DropZone.Opacity = accepted ? 1 : .76; e.Handled = true;
    }
    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        DropZone.Opacity = 1; var over = e.GetPosition(DropZone); if (over.X < 0 || over.Y < 0 || over.X > DropZone.ActualWidth || over.Y > DropZone.ActualHeight) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            foreach (var file in files.Where(File.Exists)) app.Repository.Add(Path.GetFileName(file), filePath: file);
        else if (e.Data.GetData(DataFormats.Text) is string text && TaskRepository.IsWebUrl(text)) app.Repository.Add(new Uri(text).Host, url: text, resumeUrl: text);
        app.RefreshAll();
    }
}
