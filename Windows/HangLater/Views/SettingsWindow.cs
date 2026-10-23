using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using HangLater.Windows.Controls;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Views;

public partial class SettingsWindow : Window
{
    private readonly App app;
    private readonly List<TaskTypeDefinition> types;
    private readonly bool originalLineVisible;
    private readonly double originalLinePosition;
    private bool previewActive, settingsCommitted;
    private string selectedSection = "desktop";
    private Guid? editingColorType;
    private readonly Popup palettePopup = new() { AllowsTransparency = true, StaysOpen = false, Placement = PlacementMode.Right };
    private readonly Border paletteSurface = new();
    public IReadOnlyList<TaskTypeDefinition> DraftTaskTypes => types.AsReadOnly();
    public FrameworkElement PaletteSurface => paletteSurface;

    private static readonly uint[] Palette = {
        0xAA2651,0xBF2B54,0xE8365F,0xEF678F,0xF491AF,0xC33026,0xD1382B,0xE1543E,0xE4736E,0xEE9A96,
        0xEC6D20,0xF37C23,0xFD9727,0xFCB550,0xFECA80,0xF6A532,0xF9BD3A,0xFCE748,0xFDEE79,0xFDF29E,
        0x20752E,0x2B8537,0x42A74D,0x7BC082,0xA0D2A4,0x006055,0x067265,0x098276,0x47B1A8,0x7CC8C1,
        0x107D8A,0x1591A1,0x1FB7D2,0x4BCCDD,0x7DDBE8,0x2663BB,0x2973CE,0x2F87E1,0x3394EF,0x65B3F4,
        0x27328E,0x2F3D9A,0x3F51B2,0x7684C6,0x9DA6D8,0x422A99,0x4F30A1,0x663DB3,0x9275C9,0xB19DD8,
        0x662694,0x792B9D,0x9934AA,0xB96AC4,0xCE95D5,0x2F4048,0x3E545D,0x597786,0x8CA0AA,0xADBCC1,
        0x462B24,0x56392F,0x745041,0x9C8479,0xB9A8A0,0x0A0A0A,0x5A5A5A,0x9B9B9B,0xDFDFDF,0xFFFFFF
    };
    private static readonly uint[] NewTypeColors = { 0xE8365F,0xFD9727,0x42A74D,0x1FB7D2,0x2F87E1,0x663DB3,0x597786,0x745041 };

    public SettingsWindow(App app)
    {
        this.app = app;
        originalLineVisible = app.DesktopLine.IsVisible;
        originalLinePosition = app.Settings.ClotheslineVerticalPosition;
        types = app.Repository.TaskTypes.Select(type => type.Clone()).ToList();
        InitializeComponent();
        ConfigureAppearance();
        Localize();
        LoadValues();
        BuildTypes();
        SelectSection("desktop");
        KeepVisibleCheck.Checked += DesktopPreview_Changed;
        KeepVisibleCheck.Unchecked += DesktopPreview_Changed;
        HeightSlider.ValueChanged += DesktopPreview_Changed;
        StartTime.TextChanged += (_, _) => UpdateWorkdaySummary();
        EndTime.TextChanged += (_, _) => UpdateWorkdaySummary();
        EndDayBox.SelectionChanged += (_, _) => UpdateWorkdaySummary();
        SaveButton.IsDefault = true;
        CancelButton.IsCancel = true;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { if (palettePopup.IsOpen) palettePopup.IsOpen = false; else Close(); e.Handled = true; } };
        Closed += (_, _) => {
            palettePopup.IsOpen = false;
            if (previewActive && !settingsCommitted) app.FinishDesktopLinePreview(originalLineVisible, originalLinePosition);
        };
    }

    private void ConfigureAppearance()
    {
        bool light = app.Settings.LightAppearance;
        Resources["SettingsCopyFont"] = new FontFamily(app.Text.Chinese ? "Microsoft YaHei UI" : "Courier New, Microsoft YaHei UI");
        Resources["SettingsCardBrush"] = Brush(light ? 0xF5F6F3u : 0x262626u);
        Resources["SettingsCardBorderBrush"] = Brush(light ? 0xE2E3DFu : 0x32322Fu);
        Resources["SettingsThumbBrush"] = Brush(light ? 0xFFFFFFu : 0xABABA7u);
        Resources["SettingsRailBrush"] = Brush(light ? 0xE4E6E3u : 0x444442u);
        Resources["SettingsSliderFillBrush"] = Brush(light ? 0x252522u : 0x8C8C88u);
        SaveLabel.Foreground = Brush(light ? 0xF7F8F7u : 0x171717u);
        string theme = light ? "light" : "dark";
        BitmapImage Icon(string name) => new(new Uri($"pack://application:,,,/HangLater;component/Resources/SettingsSectionIcons/{theme}/{name}.png", UriKind.Absolute));
        DesktopNavIcon.Source = Icon("desktop-clothesline");
        WorkdayNavIcon.Source = Icon("workday-hours");
        TypesNavIcon.Source = Icon("task-types");
        WorkdaySummaryIcon.Source = Icon("workday-hours");
        SectionsLabel.FontFamily = (FontFamily)Resources["SettingsCopyFont"];
        Heading.FontFamily = new FontFamily(app.Text.Chinese ? "Microsoft YaHei UI" : "Arial");
        Heading.FontStretch = app.Text.Chinese ? FontStretches.Normal : FontStretches.Condensed;
        Heading.FontSize = app.Text.Chinese ? 28 : 30;
        HotKeyLabel.TextWrapping = TextWrapping.Wrap;
        Heading.LineHeight = app.Text.Chinese ? 30 : 35;
        HeaderRow.Height = new GridLength(app.Text.Chinese ? 74 : 80);
        if (app.Text.Chinese) SettingsRoot.Margin = new Thickness(14,22,9,12);
        DesktopCopy.Margin = new Thickness(0,5,0,app.Text.Chinese ? 12 : 15);
        if (app.Text.Chinese) foreach (Button nav in new[] { DesktopNav, WorkdayNav, TypesNav }) nav.Padding = new Thickness(12,11,8,11);
        foreach (TrackedTextBlock heading in new[] { DesktopHeading, WorkdayHeading, TypesHeading }) heading.LetterSpacing = app.Text.Chinese ? 0 : 1.2;
    }

    private void Localize()
    {
        Localizer t = app.Text;
        Title = t.T("设置", "Settings");
        Heading.Text = t.T("设置", "SETTINGS");
        Subtitle.Text = t.T("定义你的工作日，也为不同类型的任务做上标记。", "Define your workday and mark different kinds of work.");
        SectionsLabel.Text = t.T("设置栏目", "SECTIONS");
        DesktopNavLabel.Text = t.T("桌面晾衣绳", "Desktop\nclothesline");
        WorkdayNavLabel.Text = t.T("工作日时间", "Workday hours");
        TypesNavLabel.Text = t.T("任务类型", "Task types");
        DesktopHeading.Text = t.T("桌面晾衣绳", "DESKTOP CLOTHESLINE");
        DesktopCopy.Text = t.T("决定晾衣绳是否一直显示，以及它在屏幕上的上下位置。", "Keep the clothesline visible and choose its vertical position on screen.");
        KeepVisibleLabel.Text = t.T("始终显示在桌面", "Always show on desktop");
        KeepVisibleHint.Text = t.T("打开后，晾衣绳不会自动收起。", "The clothesline stays open instead of hiding automatically.");
        AutomationProperties.SetName(KeepVisibleCheck, KeepVisibleLabel.Text);
        HotKeyLabel.Text = t.T("显示晾衣绳快捷键", "Show clothesline shortcut");
        HotKeyHint.Text = t.T("快捷键需包含 Ctrl、Alt、Shift 或 Win。", "Include Ctrl, Alt, Shift, or Win.");
        HotKeyBox.ToolTip = t.T("点击并按下快捷键，或输入 Ctrl+Alt+L。", "Press a shortcut, or enter Ctrl+Alt+L.");
        AutomationProperties.SetName(HotKeyBox, HotKeyLabel.Text);
        ResetHotKeyButton.ToolTip = t.T("恢复 Ctrl+Alt+L", "Reset to Ctrl+Alt+L");
        AutomationProperties.SetName(ResetHotKeyButton, ResetHotKeyButton.ToolTip.ToString());
        HeightLabel.Text = t.T("屏幕高度", "SCREEN HEIGHT");
        AutomationProperties.SetName(HeightSlider, HeightLabel.Text);
        TopLabel.Text = t.T("顶部", "TOP"); BottomLabel.Text = t.T("底部", "BOTTOM");
        WorkdayHeading.Text = t.T("工作日", "WORKDAY");
        WorkdayCopy.Text = t.T("完成钉只统计在这个时间段内完成的任务，不限制创建时间。结束日期可以选择当天或第二天。", "Done Spike counts tasks completed inside this window, regardless of creation time. Choose a same-day or next-day end.");
        StartLabel.Text = t.T("开始", "START"); SameDayLabel.Text = t.T("当天", "SAME DAY"); EndLabel.Text = t.T("结束", "END");
        SameDayItem.Content = t.T("当天", "Same day"); NextDayItem.Content = t.T("第二天", "Next day");
        AutomationProperties.SetName(StartTime, t.T("工作日开始时间", "Workday start time"));
        AutomationProperties.SetName(EndTime, t.T("工作日结束时间", "Workday end time"));
        AutomationProperties.SetName(EndDayBox, t.T("结束日期", "End day"));
        TypesHeading.Text = t.T("任务类型", "TASK TYPES");
        TypesCopy.Text = t.T("添加并命名类型，再从色板中选择颜色。“完成”一行和完成钉纸片会使用对应颜色。", "Add and name types, then choose from the palette.\nOnly the Done row and Done Spike receipts use it.");
        DefaultTypeLabel.Text = t.T("默认", "Default"); DefaultTypeHint.Text = t.T("保持现在的纸张颜色", "Keep the current paper row");
        AddTypeLabel.Text = t.T("添加任务类型", "Add task type");
        WindowsOptions.Header = t.T("Windows 呼出选项", "Windows reveal options");
        TriggersCopy.Text = t.T("单击系统托盘图标也可以显示或收起晾衣绳。还可以启用屏幕顶边悬停。", "A tray click shows or hides the clothesline. You can also enable top-edge hover.");
        EdgeHoverCheck.Content = t.T("鼠标停在屏幕顶边时呼出", "Reveal when the pointer rests at the top edge");
        ZoneLabel.Text = t.T("触发区域", "Activation region");
        string[] zones = t.Chinese ? new[] { "整个顶边", "左侧三分之一", "中间三分之一", "右侧三分之一" } : new[] { "Full top edge", "Left third", "Center third", "Right third" };
        for (int i = 0; i < zones.Length; i++) ((ComboBoxItem)EdgeZoneBox.Items[i]).Content = zones[i];
        EdgeHint.Text = t.T("触发带为 3 像素高，停留 0.5 秒后在鼠标所在的显示器呼出。", "The strip is 3 pixels high and activates after 0.5 seconds on the pointer's display.");
        CancelLabel.Text = t.T("取消", "Cancel"); SaveLabel.Text = t.T("保存", "Save");
        CloseButton.ToolTip = t.T("关闭，不保存", "Close without saving");
        AutomationProperties.SetName(CloseButton, CloseButton.ToolTip.ToString());
    }

    private void LoadValues()
    {
        AppSettings settings = app.Settings;
        KeepVisibleCheck.IsChecked = settings.KeepClotheslineVisible;
        HeightSlider.Value = settings.ClotheslineVerticalPosition * 100;
        HeightValue.Text = $"{Math.Round(HeightSlider.Value):0}%";
        HotKeyBox.Text = settings.GlobalHotKey;
        EdgeHoverCheck.IsChecked = settings.EdgeHoverEnabled;
        EdgeZoneBox.SelectedIndex = (int)settings.EdgeZone;
        StartTime.Text = TimeField(settings.WorkdayStartMinute);
        EndTime.Text = TimeField(settings.WorkdayEndMinute);
        EndDayBox.SelectedIndex = settings.WorkdayEndsFollowingDay ? 1 : 0;
        UpdateWorkdaySummary();
    }

    private string TimeField(int minute) => DateTime.Today.AddMinutes(minute).ToString(app.Text.Chinese ? "HH:mm" : "h:mm tt", CultureInfo.InvariantCulture);
    private static bool Minute(string value, out int minute)
    {
        if (TimeSpan.TryParseExact(value.Trim(), new[] { @"h\:mm", @"hh\:mm" }, CultureInfo.InvariantCulture, out var time) && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1)) { minute = (int)time.TotalMinutes; return true; }
        if (DateTime.TryParseExact(value.Trim(), new[] { "h:mm tt", "hh:mm tt" }, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var clock)) { minute = clock.Hour * 60 + clock.Minute; return true; }
        minute = 0; return false;
    }
    private void UpdateWorkdaySummary()
    {
        if (!Minute(StartTime.Text, out int start) || !Minute(EndTime.Text, out int end) || (EndDayBox.SelectedIndex != 1 && end <= start)) {
            WorkdayRange.Text = app.Text.T("请填写有效时间", "Enter a valid time range"); ActiveWorkday.Text = app.Text.T("时间格式为 00:00。当天结束时，结束须晚于开始。", "Use 00:00 or 12:00 AM. A same-day end must follow the start."); return;
        }
        string nextDay = EndDayBox.SelectedIndex == 1 ? app.Text.T("次日 ", "NEXT DAY ") : "";
        WorkdayRange.Text = $"{start / 60:00}:{start % 60:00}  →  {nextDay}{end / 60:00}:{end % 60:00}";
        var interval = new WorkdaySchedule(start, end, EndDayBox.SelectedIndex == 1).IntervalContaining(DateTimeOffset.Now);
        var culture = CultureInfo.GetCultureInfo(app.Text.Chinese ? "zh-CN" : "en-US");
        string Date(DateTimeOffset value) => value.ToString(app.Text.Chinese ? "M月d日 HH:mm" : "MMM d, HH:mm", culture);
        ActiveWorkday.Text = interval.HasValue ? app.Text.T("当前工作日：", "Current workday: ") + Date(interval.Value.Start) + "  –  " + Date(interval.Value.End)
            : app.Text.T("当前不在这个工作日时间段内。", "The current time is outside this workday window.");
    }

    private void DesktopPreview_Changed(object sender, RoutedEventArgs e)
    {
        HeightValue.Text = $"{Math.Round(HeightSlider.Value):0}%";
        previewActive = true;
        app.PreviewDesktopLine(KeepVisibleCheck.IsChecked == true, HeightSlider.Value / 100);
    }
    private void HotKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab or Key.Escape or Key.Back or Key.Delete || (Keyboard.Modifiers == ModifierKeys.Control && key == Key.A)) return;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (Keyboard.Modifiers == ModifierKeys.None) return;
        var keys = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) keys.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) keys.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) keys.Add("Shift");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) keys.Add("Win");
        keys.Add(key.ToString());
        HotKeyBox.Text = string.Join("+", keys); e.Handled = true;
    }
    private void ResetHotKey_Click(object sender, RoutedEventArgs e) => HotKeyBox.Text = "Ctrl+Alt+L";

    private void BuildTypes()
    {
        TaskTypesPanel.Children.Clear();
        foreach (TaskTypeDefinition type in types)
        {
            Grid row = new() { Margin = new Thickness(0, 0, 0, 9), Tag = type.id };
            foreach (GridLength width in new[] { new GridLength(42), new GridLength(11), new GridLength(1, GridUnitType.Star), new GridLength(9), new GridLength(50), new GridLength(7), new GridLength(26) }) row.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            Grid preview = new() { Width = 40, Height = 27, Background = Brush(0xE9E3D3), VerticalAlignment = VerticalAlignment.Center };
            preview.Children.Add(new Border { Height = 11, VerticalAlignment = VerticalAlignment.Bottom, Background = Brush(type.colorHex), Opacity = 0.82 });
            row.Children.Add(new Border { BorderBrush = Brush(0x93938A), BorderThickness = new Thickness(1), Width = 42, Height = 29, Child = preview });
            TextBox name = new() { Text = type.name, Tag = type.id, Style = (Style)Resources["SettingsInput"], Height = 32 };
            AutomationProperties.SetName(name, app.Text.T("类型名称", "Type name"));
            name.TextChanged += (_, _) => type.name = name.Text;
            Grid.SetColumn(name, 2); row.Children.Add(name);
            Button color = new() { Width = 50, Height = 32, Padding = new Thickness(5, 0, 5, 0), Style = (Style)Resources["QuietButton"], Tag = type.id };
            StackPanel colorLabel = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            colorLabel.Children.Add(new System.Windows.Shapes.Ellipse { Width = 18, Height = 18, Fill = Brush(type.colorHex), Stroke = (Brush)Resources["SettingsCardBorderBrush"], StrokeThickness = 0.8 });
            colorLabel.Children.Add(Glyph("\uE70D", 9, new Thickness(5, 0, 0, 0)));
            color.Content = colorLabel; color.Click += (_, _) => OpenPalette(type.id, color);
            AutomationProperties.SetName(color, app.Text.T("选择颜色：", "Choose color: ") + type.name);
            Grid.SetColumn(color, 4); row.Children.Add(color);
            Button remove = new() { Style = (Style)Resources["IconButton"], Width = 26, Height = 32, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Content = Glyph("\uE74D", 13), ToolTip = app.Text.T("删除类型，任务恢复默认颜色", "Delete type; tasks return to the default color") };
            AutomationProperties.SetName(remove, app.Text.T("删除类型：", "Delete type: ") + type.name);
            remove.Click += (_, _) => { types.Remove(type); palettePopup.IsOpen = false; BuildTypes(); };
            Grid.SetColumn(remove, 6); row.Children.Add(remove);
            TaskTypesPanel.Children.Add(row);
        }
    }

    public void OpenPalette(Guid typeId, FrameworkElement target)
    {
        editingColorType = typeId;
        BuildPalette();
        palettePopup.PlacementTarget = target;
        palettePopup.HorizontalOffset = 6; palettePopup.VerticalOffset = -110;
        palettePopup.IsOpen = true;
    }
    private void BuildPalette()
    {
        var type = types.FirstOrDefault(item => item.id == editingColorType);
        StackPanel content = new();
        content.Children.Add(new TrackedTextBlock { Text = app.Text.T("选择颜色", "CHOOSE A COLOR"), FontFamily = (FontFamily)Resources["SettingsCopyFont"], FontWeight = FontWeights.Bold, FontSize = 12.5, LetterSpacing = app.Text.Chinese ? 0 : 1.2, Foreground = (Brush)app.Resources["InkBrush"], Margin = new Thickness(0, 0, 0, 11) });
        UniformGrid swatches = new() { Columns = 10, Rows = 7, Width = 302, Height = 210 };
        foreach (uint hex in Palette)
        {
            Button swatch = new() { Width = 23, Height = 23, Padding = new Thickness(0), Background = Brush(hex), Tag = hex, ToolTip = $"#{hex:X6}", BorderThickness = new Thickness(hex == 0xFFFFFF ? 1 : 0), BorderBrush = (Brush)app.Resources["BorderBrush"] };
            FrameworkElementFactory circle = new(typeof(Border)); circle.SetValue(Border.CornerRadiusProperty, new CornerRadius(12)); circle.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) }); circle.SetValue(Border.BorderBrushProperty, (Brush)app.Resources["BorderBrush"]); circle.SetValue(Border.BorderThicknessProperty, swatch.BorderThickness);
            FrameworkElementFactory presenter = new(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); circle.AppendChild(presenter);
            swatch.Template = new ControlTemplate(typeof(Button)) { VisualTree = circle };
            if (type?.colorHex == hex) { var check = Glyph("\uE73E", 12); check.Foreground = Contrast(hex); swatch.Content = check; }
            AutomationProperties.SetName(swatch, $"#{hex:X6}");
            swatch.Click += (_, _) => { if (type != null) type.colorHex = hex; palettePopup.IsOpen = false; BuildTypes(); };
            swatches.Children.Add(swatch);
        }
        content.Children.Add(swatches);
        paletteSurface.Background = (Brush)app.Resources["PaperBrush"]; paletteSurface.BorderBrush = (Brush)Resources["SettingsCardBorderBrush"];
        paletteSurface.BorderThickness = new Thickness(1); paletteSurface.CornerRadius = new CornerRadius(12); paletteSurface.Padding = new Thickness(16,10,16,10); paletteSurface.Child = content;
        paletteSurface.Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0.25 };
        if (palettePopup.Child == null) palettePopup.Child = new Border { Padding = new Thickness(10), Child = paletteSurface };
    }

    private TextBlock Glyph(string value, double size, Thickness? margin = null) => new() { Text = value, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = size, Foreground = (Brush)app.Resources["MutedBrush"], VerticalAlignment = VerticalAlignment.Center, Margin = margin ?? new Thickness(0) };
    private static SolidColorBrush Brush(uint hex) => new(Color.FromRgb((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex));
    private static Brush Contrast(uint hex) => (((hex >> 16) & 255) * 0.299 + ((hex >> 8) & 255) * 0.587 + (hex & 255) * 0.114) < 145 ? Brushes.White : Brushes.Black;
    private void AddType_Click(object sender, RoutedEventArgs e)
    {
        int number = types.Count + 1;
        string Name() => app.Text.T($"新类型 {number}", $"New type {number}");
        while (types.Any(type => type.name.Equals(Name(), StringComparison.CurrentCultureIgnoreCase))) number++;
        types.Add(new TaskTypeDefinition { name = Name(), colorHex = NewTypeColors[types.Count % NewTypeColors.Length] });
        BuildTypes();
    }

    public bool TryCreateDraft(out AppSettings settings, out string error)
    {
        settings = app.Settings.Clone(); error = "";
        if (!HotKeyService.TryValidate(HotKeyBox.Text, out string hotkey)) { error = app.Text.T("快捷键格式不正确。", "The shortcut format is invalid."); return false; }
        if (!Minute(StartTime.Text, out int start) || !Minute(EndTime.Text, out int end) || (EndDayBox.SelectedIndex != 1 && end <= start)) { error = app.Text.T("工作日时间无效。同一天结束时，结束必须晚于开始。", "Invalid workday. A same-day end must be later than the start."); return false; }
        if (types.Any(type => string.IsNullOrWhiteSpace(type.name)) || types.Select(type => type.name.Trim()).Distinct(StringComparer.CurrentCultureIgnoreCase).Count() != types.Count) { error = app.Text.T("任务类型需要名称，且不能重名。", "Task types need distinct, non-empty names."); return false; }
        settings.KeepClotheslineVisible = KeepVisibleCheck.IsChecked == true; settings.ClotheslineVerticalPosition = HeightSlider.Value / 100;
        settings.GlobalHotKey = hotkey; settings.EdgeHoverEnabled = EdgeHoverCheck.IsChecked == true; settings.EdgeZone = (EdgeActivationZone)Math.Max(0, EdgeZoneBox.SelectedIndex);
        settings.WorkdayStartMinute = start; settings.WorkdayEndMinute = end; settings.WorkdayEndsFollowingDay = EndDayBox.SelectedIndex == 1;
        return true;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCreateDraft(out var settings, out string error)) { MessageBox.Show(this, error, Title); return; }
        try { app.Repository.ReplaceTaskTypes(types); app.CommitDesktopLinePreview(); app.ApplySettings(settings); settingsCommitted = true; DialogResult = true; }
        catch (Exception ex) { DesktopPreview_Changed(this, new RoutedEventArgs()); MessageBox.Show(this, ex.Message, Title); }
    }
    private void SelectSection(string section)
    {
        selectedSection = section;
        foreach (var (name, button, label, icon) in new[] { ("desktop", DesktopNav, DesktopNavLabel, DesktopNavIcon), ("workday", WorkdayNav, WorkdayNavLabel, WorkdayNavIcon), ("types", TypesNav, TypesNavLabel, TypesNavIcon) }) {
            bool selected = name == selectedSection;
            button.Background = selected ? (Brush)Resources["SettingsCardBrush"] : Brushes.Transparent;
            button.BorderBrush = selected ? (Brush)app.Resources["InkBrush"] : Brushes.Transparent;
            label.Foreground = (Brush)app.Resources[selected ? "InkBrush" : "MutedBrush"]; label.FontWeight = selected ? FontWeights.Bold : FontWeights.Normal; icon.Opacity = selected ? 1 : 0.65;
        }
    }
    private void Navigate(string name, FrameworkElement section)
    {
        SelectSection(name); SettingsScroll.UpdateLayout();
        SettingsScroll.ScrollToVerticalOffset(name == "desktop" ? 0 : section.TranslatePoint(new Point(0, 0), SectionsContent).Y + 28);
    }
    private void DesktopNav_Click(object sender, RoutedEventArgs e) => Navigate("desktop", DesktopSection);
    private void WorkdayNav_Click(object sender, RoutedEventArgs e) => Navigate("workday", WorkdaySection);
    private void TypesNav_Click(object sender, RoutedEventArgs e) => Navigate("types", TypesSection);
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void Header_Drag(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
