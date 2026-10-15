using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Views;

public partial class SettingsWindow : Window
{
    private readonly App app; private readonly List<TaskTypeDefinition> types; private Guid? selectedType;
    private static readonly uint[] Palette = [
        0xAA2651,0xBF2B54,0xE8365F,0xEF678F,0xF491AF,0xC33026,0xD1382B,0xE1543E,0xE4736E,0xEE9A96,
        0xEC6D20,0xF37C23,0xFD9727,0xFCB550,0xFECA80,0xF6A532,0xF9BD3A,0xFCE748,0xFDEE79,0xFDF29E,
        0x20752E,0x2B8537,0x42A74D,0x7BC082,0xA0D2A4,0x006055,0x067265,0x098276,0x47B1A8,0x7CC8C1,
        0x107D8A,0x1591A1,0x1FB7D2,0x4BCCDD,0x7DDBE8,0x2663BB,0x2973CE,0x2F87E1,0x3394EF,0x65B3F4,
        0x27328E,0x2F3D9A,0x3F51B2,0x7684C6,0x9DA6D8,0x422A99,0x4F30A1,0x663DB3,0x9275C9,0xB19DD8,
        0x662694,0x792B9D,0x9934AA,0xB96AC4,0xCE95D5,0x2F4048,0x3E545D,0x597786,0x8CA0AA,0xADBCC1,
        0x462B24,0x56392F,0x745041,0x9C8479,0xB9A8A0,0x0A0A0A,0x5A5A5A,0x9B9B9B,0xDFDFDF,0xFFFFFF];
    public SettingsWindow(App app)
    {
        this.app = app; types = app.Repository.TaskTypes.Select(x => x.Clone()).ToList(); InitializeComponent(); Localize(); LoadValues(); BuildPalette(); BuildTypes();
    }
    private void Localize()
    {
        var t = app.Text; Heading.Text = t.T("设置", "SETTINGS"); Subtitle.Text = t.T("定义触发方式、工作日和任务类型。", "Define triggers, workday hours, and task types."); SectionsLabel.Text = t.T("设置栏目", "SECTIONS"); DesktopNav.Content = t.T("▣　桌面晾衣绳", "▣　Desktop clothesline"); TriggersNav.Content = t.T("⌁　呼出方式", "⌁　Triggers"); WorkdayNav.Content = t.T("◷　工作日时间", "◷　Workday hours"); TypesNav.Content = t.T("◇　任务类型", "◇　Task types");
        DesktopHeading.Text = t.T("桌面晾衣绳", "DESKTOP CLOTHESLINE"); DesktopCopy.Text = t.T("决定绳子是否一直显示，以及它在屏幕上的上下位置。", "Keep the clothesline visible and choose its vertical position."); KeepVisibleCheck.Content = t.T("始终显示在桌面", "Always show on desktop"); HeightLabel.Text = t.T("屏幕高度", "Screen height"); TopLabel.Text = t.T("顶部", "Top"); BottomLabel.Text = t.T("底部", "Bottom");
        TriggersHeading.Text = t.T("呼出方式", "TRIGGERS"); TriggersCopy.Text = t.T("系统托盘单击始终可以显示或收起绳子。你也可以使用全局快捷键，或开启屏幕顶边悬停。", "A tray click always shows or hides the clothesline. You can also use a global shortcut or enable top-edge hover."); HotKeyLabel.Text = t.T("全局快捷键", "Global shortcut"); HotKeyHint.Text = t.T("使用 Ctrl、Alt、Shift 或 Win 加一个按键，例如 Ctrl+Alt+L。", "Use Ctrl, Alt, Shift, or Win plus one key, for example Ctrl+Alt+L."); EdgeHoverCheck.Content = t.T("鼠标停在屏幕顶边时呼出", "Reveal when the pointer rests at the top edge"); ZoneLabel.Text = t.T("触发区域", "Activation region"); var items = EdgeZoneBox.Items.Cast<ComboBoxItem>().ToArray(); items[0].Content = t.T("整个顶边", "Full top edge"); items[1].Content = t.T("左侧三分之一", "Left third"); items[2].Content = t.T("中间三分之一", "Center third"); items[3].Content = t.T("右侧三分之一", "Right third"); EdgeHint.Text = t.T("触发带固定为 3 像素高；鼠标停留 0.5 秒后才呼出，并显示在鼠标所在的显示器上。", "The strip is fixed at 3 pixels high. It activates after a 0.5-second pause on the display under the pointer.");
        WorkdayHeading.Text = t.T("工作日", "WORKDAY"); WorkdayCopy.Text = t.T("开始时间必须属于当天；结束时间可以属于第二天。完成钉只统计发布时间在当前工作日内的任务。", "The start belongs to the current day; the end may be on the following day. Done Spike counts tasks published in the current workday."); StartLabel.Text = t.T("开始（当天）", "Start (same day)"); EndLabel.Text = t.T("结束", "End"); FollowingDayCheck.Content = t.T("结束时间属于第二天", "End time is on the following day");
        TypesHeading.Text = t.T("任务类型", "TASK TYPES"); TypesCopy.Text = t.T("添加并命名类型，再选择颜色。只有便签底部的“完成”一行和完成钉纸片会使用对应颜色。", "Name types and choose colors. Only the Done row and Done Spike receipt use the assigned color."); DefaultTypeLabel.Text = t.T("默认　保留现在的纸张颜色", "Default　Keep the original paper color"); AddTypeButton.Content = t.T("＋ 添加任务类型", "＋ Add task type"); PaletteLabel.Text = t.T("为选中的类型选择颜色", "Choose a color for the selected type"); CancelButton.Content = t.T("取消", "Cancel"); SaveButton.Content = t.T("保存", "Save");
    }
    private void LoadValues()
    {
        var s = app.Settings; KeepVisibleCheck.IsChecked = s.KeepClotheslineVisible; HeightSlider.Value = s.ClotheslineVerticalPosition * 100; HotKeyBox.Text = s.GlobalHotKey; EdgeHoverCheck.IsChecked = s.EdgeHoverEnabled; EdgeZoneBox.SelectedIndex = (int)s.EdgeZone; StartTime.Text = $"{s.WorkdayStartMinute / 60:00}:{s.WorkdayStartMinute % 60:00}"; EndTime.Text = $"{s.WorkdayEndMinute / 60:00}:{s.WorkdayEndMinute % 60:00}"; FollowingDayCheck.IsChecked = s.WorkdayEndsFollowingDay;
    }
    private void BuildTypes()
    {
        TaskTypesPanel.Children.Clear();
        foreach (var type in types)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8), Background = selectedType == type.id ? new SolidColorBrush(Color.FromRgb(55,55,55)) : Brushes.Transparent }; grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            var color = new Button { Background = Brush(type.colorHex), Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0), Tag = type.id }; color.Click += (_, _) => { selectedType = type.id; BuildTypes(); }; grid.Children.Add(color);
            var name = new TextBox { Text = type.name, Tag = type.id }; name.TextChanged += (_, _) => { var target = types.First(x => x.id == (Guid)name.Tag); target.name = name.Text; }; Grid.SetColumn(name, 1); grid.Children.Add(name);
            var remove = new Button { Content = "🗑", Tag = type.id, Margin = new Thickness(8, 0, 0, 0) }; remove.Click += (_, _) => { types.RemoveAll(x => x.id == (Guid)remove.Tag); if (selectedType == (Guid)remove.Tag) selectedType = null; BuildTypes(); }; Grid.SetColumn(remove, 2); grid.Children.Add(remove); TaskTypesPanel.Children.Add(grid);
        }
    }
    private void BuildPalette()
    {
        foreach (var hex in Palette) { var button = new Button { Width = 31, Height = 31, Margin = new Thickness(3), Padding = new Thickness(0), Background = Brush(hex), BorderBrush = Brushes.Transparent, Tag = hex }; button.Click += (_, _) => { if (selectedType is null) return; types.First(x => x.id == selectedType).colorHex = (uint)button.Tag; BuildTypes(); }; PalettePanel.Children.Add(button); }
    }
    private static System.Windows.Media.Brush Brush(uint hex) => new SolidColorBrush(Color.FromRgb((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex));
    private void AddType_Click(object sender, RoutedEventArgs e) { var type = new TaskTypeDefinition { name = app.Text.T("新类型", "New type"), colorHex = Palette[types.Count % Palette.Length] }; types.Add(type); selectedType = type.id; BuildTypes(); }
    private static bool Minute(string value, out int minute)
    {
        if (TimeSpan.TryParseExact(value.Trim(), ["h\\:mm", "hh\\:mm"], CultureInfo.InvariantCulture, out var time) && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1)) { minute = (int)time.TotalMinutes; return true; } minute = 0; return false;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!HotKeyService.TryValidate(HotKeyBox.Text, out var hotKey)) { MessageBox.Show(app.Text.T("快捷键格式不正确。", "The shortcut format is invalid.")); return; }
        if (!Minute(StartTime.Text, out var start) || !Minute(EndTime.Text, out var end) || (FollowingDayCheck.IsChecked != true && end <= start)) { MessageBox.Show(app.Text.T("工作日时间无效。同一天结束时，结束必须晚于开始。", "Invalid workday. For a same-day end, the end must be later than the start.")); return; }
        try { app.Repository.ReplaceTaskTypes(types); }
        catch (Exception ex) { MessageBox.Show(ex.Message); return; }
        var value = app.Settings.Clone(); value.KeepClotheslineVisible = KeepVisibleCheck.IsChecked == true; value.ClotheslineVerticalPosition = HeightSlider.Value / 100; value.GlobalHotKey = hotKey; value.EdgeHoverEnabled = EdgeHoverCheck.IsChecked == true; value.EdgeZone = (EdgeActivationZone)Math.Max(0, EdgeZoneBox.SelectedIndex); value.WorkdayStartMinute = start; value.WorkdayEndMinute = end; value.WorkdayEndsFollowingDay = FollowingDayCheck.IsChecked == true;
        try { app.ApplySettings(value); DialogResult = true; } catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    private void DesktopNav_Click(object sender, RoutedEventArgs e) => DesktopSection.BringIntoView();
    private void TriggersNav_Click(object sender, RoutedEventArgs e) => TriggersSection.BringIntoView();
    private void WorkdayNav_Click(object sender, RoutedEventArgs e) => WorkdaySection.BringIntoView();
    private void TypesNav_Click(object sender, RoutedEventArgs e) => TypesSection.BringIntoView();
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
