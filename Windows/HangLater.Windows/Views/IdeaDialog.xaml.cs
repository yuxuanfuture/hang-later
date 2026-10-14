using System.Windows;
using HangLater.Windows.Models;

namespace HangLater.Windows.Views;

public partial class IdeaDialog : Window
{
    private readonly App app;
    public IdeaDialog(App app) { this.app = app; InitializeComponent(); Heading.Text = app.Text.T("挂一个想法", "HANG AN IDEA"); CancelButton.Content = app.Text.T("取消", "Cancel"); HangButton.Content = app.Text.T("挂起来", "Hang it"); IdeaText.Focus(); }
    private void Hang_Click(object sender, RoutedEventArgs e) { if (string.IsNullOrWhiteSpace(IdeaText.Text)) return; app.Repository.Add(IdeaText.Text, note: app.Text.T("一个想法", "An idea"), kind: HangingTaskKind.idea); DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
