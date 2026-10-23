using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Controls;

public partial class CompletedArchiveView : UserControl
{
    private readonly Dictionary<(CompletionGrouping, DateTime), bool> expansion = new();
    private App? app;
    private string query = "";
    private bool refreshing;
    public CompletionGrouping Grouping { get; private set; } = CompletionGrouping.Day;

    public CompletedArchiveView() { InitializeComponent(); }

    public void Refresh(App application, string search = "")
    {
        app = application; query = search.Trim(); refreshing = true;
        try
        {
            bool chinese = app.Text.Chinese;
            FontFamily display = chinese ? new FontFamily("Microsoft YaHei UI") : (FontFamily)app.Resources["DisplayFont"];
            HeadingTitle.FontFamily = display;
            HeadingTitle.FontStretch = chinese ? FontStretches.Normal : FontStretches.Condensed;
            HeadingSubtitle.FontFamily = chinese ? new FontFamily("Microsoft YaHei UI") : (FontFamily)app.Resources["MonoFont"];
            HeadingSubtitle.Margin = new Thickness(0, chinese ? 6 : 11, 0, 0);
            HeadingTitle.Text = app.Text.T("那些做完的小事。", "THE THINGS YOU FINISHED.");
            HeadingSubtitle.Text = app.Text.T("每一次收起，都是一点小小的前进。", "Every finished task is a little progress.");
            ArchiveAddButton.Content = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = "\uE710", FontFamily = (FontFamily)app.Resources["IconFont"], FontSize = 12, Foreground = (Brush)app.Resources["PaperBrush"], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) },
                new TextBlock { Text = app.Text.T("晾一件事", "Hang something"), FontFamily = chinese ? new FontFamily("Microsoft YaHei UI") : (FontFamily)app.Resources["MonoFont"], FontWeight = FontWeights.Bold, FontSize = 12, LineHeight = 14, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Foreground = (Brush)app.Resources["PaperBrush"] }
            }};
            ArchivedCount.Text = app.Text.T($"已收好 · {app.Repository.Completed.Count()} 件", $"Archived · {app.Repository.Completed.Count()}");
            Resources["ArchiveSegmentSelectedBrush"] = app.Settings.LightAppearance ? Brushes.White : (Brush)app.Resources["RaisedBrush"];
            DayTab.Content = app.Text.T("日", "Day"); WeekTab.Content = app.Text.T("周", "Week"); MonthTab.Content = app.Text.T("月", "Month");
            DayTab.IsChecked = Grouping == CompletionGrouping.Day;
            WeekTab.IsChecked = Grouping == CompletionGrouping.Week;
            MonthTab.IsChecked = Grouping == CompletionGrouping.Month;
            foreach (RadioButton tab in new[] { DayTab, WeekTab, MonthTab }) tab.ToolTip = app.Text.T("完成记录分组：", "Group completed tasks: ") + tab.Content;
            IEnumerable<HangingTask> tasks = app.Repository.Completed;
            if (query.Length > 0) tasks = tasks.Where(task => task.title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || task.note.Contains(query, StringComparison.CurrentCultureIgnoreCase) || (task.url?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false));
            var sections = CompletionArchive.Sections(tasks, Grouping);
            ArchiveSections.Children.Clear();
            for (int index = 0; index < sections.Count; index++)
            {
                CompletionSection section = sections[index];
                var key = (Grouping, section.Start);
                Expander group = new Expander
                {
                    Style = (Style)FindResource("ArchiveSection"), Margin = new Thickness(0,0,0,11),
                    IsExpanded = expansion.TryGetValue(key, out bool expanded) ? expanded : index == 0
                };
                Grid header = new Grid();
                foreach (GridLength width in new[] { new GridLength(12), new GridLength(10), GridLength.Auto, new GridLength(10), new GridLength(1,GridUnitType.Star), new GridLength(10), GridLength.Auto })
                    header.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
                TextBlock arrow = new TextBlock { FontFamily = (FontFamily)app.Resources["IconFont"], FontSize = 10, FontWeight = FontWeights.Bold, Foreground = (Brush)app.Resources["MutedBrush"], VerticalAlignment = VerticalAlignment.Center };
                Style arrowStyle = new Style(typeof(TextBlock));
                arrowStyle.Setters.Add(new Setter(TextBlock.TextProperty, "\uE76C"));
                DataTrigger opened = new DataTrigger { Binding = new Binding("IsExpanded") { Source = group }, Value = true };
                opened.Setters.Add(new Setter(TextBlock.TextProperty, "\uE70D")); arrowStyle.Triggers.Add(opened); arrow.Style = arrowStyle;
                header.Children.Add(arrow);
                Brush ink = ((Brush)app.Resources["InkBrush"]).Clone(); ink.Opacity = 0.82;
                TextBlock title = new TextBlock { Text = CompletionArchive.Title(section.Start, Grouping, chinese), FontFamily = new FontFamily("Arial, Microsoft YaHei UI"),
                    FontStretch = FontStretches.Condensed, FontSize = 14, LineHeight = 17, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, FontWeight = FontWeights.Bold, Foreground = ink, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(title, 2); header.Children.Add(title);
                Brush rule = ((Brush)app.Resources["BorderBrush"]).Clone(); rule.Opacity = 0.7;
                Border divider = new Border { Height = 1, Background = rule, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(divider, 4); header.Children.Add(divider);
                TextBlock count = new TextBlock { Text = chinese ? $"{section.Tasks.Count} 件" : section.Tasks.Count == 1 ? "1 item" : $"{section.Tasks.Count} items", FontSize = 10, Foreground = (Brush)app.Resources["MutedBrush"], VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(count, 6); header.Children.Add(count);
                group.Header = header;
                StackPanel rows = new StackPanel();
                foreach (HangingTask task in section.Tasks) rows.Children.Add(new CompletedTaskRow(app, task, (Style)FindResource("ArchiveButton"), (Style)FindResource("ArchiveQuietButton")));
                if (rows.Children.Count > 0) ((FrameworkElement)rows.Children[rows.Children.Count - 1]).Margin = new Thickness(0);
                group.Content = rows;
                void SetExpansion(bool value) { expansion[key] = value; group.ToolTip = value ? app.Text.T("收起这一组", "Collapse this group") : app.Text.T("展开这一组", "Expand this group"); }
                group.Expanded += (_, _) => SetExpansion(true);
                group.Collapsed += (_, _) => SetExpansion(false);
                group.ToolTip = group.IsExpanded ? app.Text.T("收起这一组", "Collapse this group") : app.Text.T("展开这一组", "Expand this group");
                ArchiveSections.Children.Add(group);
            }
            ArchiveEmpty.Visibility = sections.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ArchiveEmpty.Text = query.Length == 0 ? app.Text.T("这里还没有完成记录。", "No finished tasks here yet.") : app.Text.T("没有找到匹配的完成记录。", "No matching finished tasks.");
        }
        finally { refreshing = false; }
    }

    private void Grouping_Checked(object sender, RoutedEventArgs e)
    {
        if (refreshing || app == null || sender is not RadioButton tab || !Enum.TryParse(tab.Tag?.ToString(), out CompletionGrouping next)) return;
        Grouping = next;
        Refresh(app, query);
        ArchiveScroll.ScrollToTop();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => app?.ShowAddSource();
}
