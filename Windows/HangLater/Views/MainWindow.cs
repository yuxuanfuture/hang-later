using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HangLater.Windows.Controls;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Views;

public partial class MainWindow : Window
{
	private enum Page
	{
		Clothesline,
		Todo,
		Completed,
		Spike
	}

	private readonly App app;

	private Page page;

	public MainWindow(App app)
	{
		MainWindow mainWindow = this;
		this.app = app;
		InitializeComponent();
		SpikeScene.ReceiptMoved += (id, x, y, z) => app.Repository.MoveSpike(id, x, y, z);
		SpikeScene.ReceiptResized += (id, scale) =>
		{
			app.Repository.ResizeSpike(id, scale);
			RefreshSpike();
		};
		SpikeScene.ReceiptClicked += id =>
		{
			app.Repository.SetSpike(id, attached: false);
			app.RefreshAll();
		};
		SpikePage.SizeChanged += (_, _) => SpikeStage.Height = Math.Max(545, SpikePage.ActualHeight - 62);
		base.Closing += delegate(object? _, CancelEventArgs e)
		{
			if (!app.IsExiting)
			{
				e.Cancel = true;
				mainWindow.Hide();
			}
		};
		base.SizeChanged += delegate
		{
			if (mainWindow.page == Page.Clothesline)
			{
				mainWindow.RefreshClothesline();
			}
		};
		LocalizeAndRefresh();
	}

	public void LocalizeAndRefresh()
	{
		Localizer text = app.Text;
		SpaceLabel.Text = text.T("我的空间", "MY SPACE");
		ClotheslineNavLabel.Text = text.T("晾衣绳", "Clothesline");
		TodoNavLabel.Text = text.T("待办清单", "To-do list");
		CompletedNavLabel.Text = text.T("已完成", "Completed");
		SpikeNavLabel.Text = text.T("完成钉", "Done Spike");
		ThemeNavLabel.Text = (app.Settings.LightAppearance ? text.T("黑夜", "Dark") : text.T("白天", "Light"));
		ApplySidebarIcons();
		LanguageNavLabel.Text = (app.Text.Chinese ? "EN" : "中文");
		SettingsNavLabel.Text = text.T("设置", "Settings");
		ChromeNavLabel.Text = text.T("连接 Chrome", "Connect Chrome");
		AddButton.Content = IconLabel("\uE710", text.T("晾一件事", "Hang something"), 12.0, 12.0);
		SpikeAddButton.Content = IconLabel("\uE710", text.T("晾一件事", "Hang something"), 12.0, 12.0);
		SearchBox.ToolTip = text.T("找一件事…", "Find something…");
		SearchPlaceholder.Text = text.T("找一件事…", "Find something…");
		EmptyTitle.Text = text.T("绳子空着，心里也轻一点。", "An empty line, a lighter mind.");
		EmptyCopy.Text = text.T("拖入网页或图片，也可以截取任意 App 窗口。", "Drag in a page or image, or capture any app window.");
		FirstThingButton.Content = IconLabel("\uE710", text.T("晾第一件事", "Hang the first thing"), 11.0, 11.0);
		LighterDesktopCopy.Text = text.T("让桌面轻一点，让脑袋空一点。", "A lighter desktop, a clearer mind.");
		SavedOnThisComputerLabel.Text = text.T("保存在这台电脑上", "SAVED ON THIS PC");
		DesktopLineShortcutLabel.Text = text.T("⌃⌥L  呼出晾衣绳", "⌃⌥L  SHOW THE DESKTOP CLOTHESLINE");
		CultureInfo dateCulture = app.Text.Chinese ? CultureInfo.GetCultureInfo("zh-CN") : CultureInfo.GetCultureInfo("en-US");
		DateLabel.Text = DateTime.Now.ToString(app.Text.Chinese ? "M月d日 dddd" : "dddd, MMMM d", dateCulture);
		UpdateNavigationSelection();
		RefreshAll();
	}

	private void ApplySidebarIcons()
	{
		string theme = app.Settings.LightAppearance ? "light" : "dark";
		BrandIcon.Source = new BitmapImage(new Uri("pack://application:,,,/HangLater;component/Resources/" +
			(app.Settings.LightAppearance ? "hang-later-pin-icon.png" : "hang-later-pin-icon-dark.jpg"), UriKind.Absolute));
		ClotheslineNavIcon.Source = SidebarIcon(theme, "clothesline.png");
		TodoNavIcon.Source = SidebarIcon(theme, "to-do-list.png");
		CompletedNavIcon.Source = SidebarIcon(theme, "completed.png");
		SpikeNavIcon.Source = SidebarIcon(theme, "done-spike.png");
		ThemeIcon.Source = SidebarIcon(theme, app.Settings.LightAppearance ? "dark-toggle.png" : "light-toggle.png");
		LanguageNavIcon.Source = SidebarIcon(theme, "language-zh-en.png");
		SettingsNavIcon.Source = SidebarIcon(theme, "settings.png");
		ChromeNavIcon.Source = SidebarIcon(theme, "connect-chrome.png");
		DesktopLineIcon.Source = ToolbarIconImages.DesktopClothesline(app.Settings.LightAppearance);
		PendingSummaryIcon.Source = StatusCountIcon(theme, "pending.png");
		CompletedSummaryIcon.Source = StatusCountIcon(theme, "completed.png");
	}

	private static BitmapImage StatusCountIcon(string theme, string fileName)
	{
		return new BitmapImage(new Uri($"pack://application:,,,/HangLater;component/Resources/StatusCountIcons/{theme}/{fileName}", UriKind.Absolute));
	}

	private static BitmapImage SidebarIcon(string theme, string fileName)
	{
		return new BitmapImage(new Uri($"pack://application:,,,/HangLater;component/Resources/SidebarIcons/{theme}/{fileName}", UriKind.Absolute));
	}

	private static StackPanel IconLabel(string glyph, string label, double iconSize, double textSize)
	{
		StackPanel panel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock icon = new TextBlock
		{
			Text = glyph,
			FontFamily = new FontFamily("Segoe Fluent Icons"),
			FontSize = iconSize,
			FontWeight = FontWeights.Normal,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		TextBlock text = new TextBlock
		{
			Text = label,
			FontFamily = new FontFamily("Courier New"),
			FontSize = textSize,
			FontWeight = FontWeights.Bold,
			VerticalAlignment = VerticalAlignment.Center
		};
		icon.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1)
		});
		text.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground")
		{
			RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1)
		});
		panel.Children.Add(icon);
		panel.Children.Add(text);
		return panel;
	}

	public void RefreshAll()
	{
		TodoNavCount.Text = app.Repository.Pending.Count().ToString();
		SpikeNavCount.Text = EligibleSpikeTasks().Count().ToString();
		switch (page)
		{
		case Page.Clothesline:
			RefreshClothesline();
			break;
		case Page.Todo:
			RefreshTodo();
			break;
		case Page.Completed:
			RefreshCompleted();
			break;
		case Page.Spike:
			RefreshSpike();
			break;
		}
	}

	private IEnumerable<HangingTask> Filter(IEnumerable<HangingTask> tasks)
	{
		string query = SearchBox.Text.Trim();
		if (query.Length != 0)
		{
			return tasks.Where((HangingTask x) => x.title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || x.note.Contains(query, StringComparison.CurrentCultureIgnoreCase) || (x.url?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false));
		}
		return tasks;
	}

	private void Switch(Page next)
	{
		bool changed = page != next;
		page = next;
		StandardPageLayout.Visibility = next == Page.Spike || next == Page.Completed ? Visibility.Collapsed : Visibility.Visible;
		ClotheslinePage.Visibility = ((next != Page.Clothesline) ? Visibility.Collapsed : Visibility.Visible);
		TodoPage.Visibility = ((next != Page.Todo) ? Visibility.Collapsed : Visibility.Visible);
		CompletedPage.Visibility = ((next != Page.Completed) ? Visibility.Collapsed : Visibility.Visible);
		SpikePage.Visibility = ((next != Page.Spike) ? Visibility.Collapsed : Visibility.Visible);
		UpdateNavigationSelection();
		RefreshAll();
		if (changed && next == Page.Spike) SpikePage.ScrollToTop();
	}

	private void UpdateNavigationSelection()
	{
		(Button, Page)[] array = new(Button, Page)[4]
		{
			(ClotheslineButton, Page.Clothesline),
			(TodoButton, Page.Todo),
			(CompletedButton, Page.Completed),
			(SpikeButton, Page.Spike)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(Button, Page) tuple = array[i];
			Button item = tuple.Item1;
			Page item2 = tuple.Item2;
			bool flag = page == item2;
			item.Background = (flag ? ((Brush)app.Resources["PanelBrush"]) : Brushes.Transparent);
			item.BorderBrush = (flag ? ((Brush)app.Resources["AccentBrush"]) : Brushes.Transparent);
			item.FontWeight = (flag ? FontWeights.Bold : FontWeights.Normal);
			item.Foreground = (flag ? ((Brush)app.Resources["AccentBrush"]) : ((Brush)app.Resources["InkBrush"]));
			item.Opacity = flag ? 1.0 : 0.75;
		}
	}

	private void RefreshClothesline()
	{
		PageTitle.Text = app.Text.T("给想做的事，留一个位置。", "GIVE WHAT MATTERS A PLACE.");
		PageSubtitle.Text = app.Text.T("想看的网页、想学的视频，还有生活里的小事。", "Pages, videos, images, app windows, and everyday tasks.");
		HangingTask[] tasks = Filter(app.Repository.Pending).ToArray();
		HangingNowTitle.Text = app.Text.T("正在晾着", "HANGING NOW");
		HangingNowCount.Text = app.Text.T($"{tasks.Length} 件", $"{tasks.Length} ITEMS");
		ClotheslineActions.Children.Clear();
		Style style = (Style)FindResource("QuietActionButton");
		Button button = new Button
		{
			Content = IconLabel("\uE930", app.Text.T("全部完成", "Complete all"), 14.0, 11.0),
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			Style = style,
			IsEnabled = (tasks.Length != 0)
		};
		button.Click += delegate
		{
			app.Repository.CompleteAll(tasks.Select((HangingTask x) => x.id));
			app.RefreshAll();
		};
		ClotheslineActions.Children.Add(button);
		Button button2 = new Button
		{
			Content = IconLabel("\uE74D", app.Text.T("全部删除", "Delete all"), 14.0, 11.0),
			Style = style,
			IsEnabled = (tasks.Length != 0)
		};
		button2.Click += delegate
		{
			if (MessageBox.Show(app.Text.T("删除当前所有纸片？", "Delete all current slips?"), "Hang Later", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
			{
				app.Repository.DeleteAll(tasks.Select((HangingTask x) => x.id));
				app.RefreshAll();
			}
		};
		ClotheslineActions.Children.Add(button2);
		Border[] array = ClotheslineCanvas.Children.OfType<Border>().ToArray();
		foreach (Border element in array)
		{
			ClotheslineCanvas.Children.Remove(element);
		}
		double num2 = ClotheslineCanvas.ActualWidth > 1.0 ? ClotheslineCanvas.ActualWidth : 900.0;
		UpdateClotheslineRope(num2);
		ClotheslineEmptyState.Visibility = ((tasks.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
		PendingSummary.Text = app.Text.T($"{app.Repository.Pending.Count()} 件待办", $"{app.Repository.Pending.Count()} pending");
		CompletedSummary.Text = app.Text.T($"{app.Repository.Completed.Count()} 件已完成", $"{app.Repository.Completed.Count()} completed");
		foreach (HangingTask task in tasks.OrderBy((HangingTask x) => x.lineLayer.GetValueOrDefault()))
		{
			Border border = TaskCardFactory.CreateClotheslineCard(app, task, compact: true, delegate(double ratio)
			{
				app.Repository.Place(task.id, ratio);
			}, (double left) => ClotheslineCardTop(left, TaskCardFactory.ClotheslineCompactWidth, num2));
			double length = (task.lineX ?? 0.5) * Math.Max(1.0, num2 - border.Width);
			Canvas.SetLeft(border, length);
			Canvas.SetTop(border, ClotheslineCardTop(length, border.Width, num2));
			Panel.SetZIndex(border, task.lineLayer.GetValueOrDefault());
			ClotheslineCanvas.Children.Add(border);
		}
	}

	private void UpdateClotheslineRope(double width)
	{
		const double ropeY = 12.0;
		const double sag = 28.0;
		PathFigure figure = new PathFigure
		{
			StartPoint = new Point(-10.0, ropeY),
			IsClosed = false
		};
		figure.Segments.Add(new QuadraticBezierSegment(new Point(width / 2.0, ropeY + sag * 2.0), new Point(width + 10.0, ropeY), isStroked: true));
		PathGeometry geometry = new PathGeometry
		{
			Figures = { figure }
		};
		geometry.Freeze();
		ClotheslineRopeShadow.Data = geometry;
		ClotheslineRope.Data = geometry;
		ClotheslineRopeHighlight.Data = geometry;
	}

	private static double ClotheslineCardTop(double left, double cardWidth, double ropeWidth)
	{
		double position = Math.Clamp((left + cardWidth / 2.0) / Math.Max(1.0, ropeWidth), 0.0, 1.0);
		double ropeY = 12.0 + 4.0 * 28.0 * position * (1.0 - position);
		return ropeY - 16.0;
	}

	private void RefreshTodo()
	{
		PageTitle.Text = app.Text.T("一件一件，慢慢来。", "ONE THING AT A TIME.");
		PageSubtitle.Text = app.Text.T("想看的网页、要学的内容，还有生活里的小事。", "Pages, videos, files, app windows, and everyday tasks.");
		TodoPanel.Children.Clear();
		foreach (HangingTask item in Filter(app.Repository.Pending))
		{
			TodoPanel.Children.Add(TaskCardFactory.CreateListCard(app, item));
		}
	}

	private void RefreshCompleted()
	{
		CompletedPage.Refresh(app, SearchBox.Text);
	}

	private void RefreshSpike()
	{
		SpikePageTitle.Text = app.Text.T("把完成的纸片，串在这里。", "KEEP FINISHED SLIPS ON THE SPIKE.");
		SpikePageSubtitle.Text = app.Text.T("拖动纸片自由移动，拖空白处旋转视角。", "Drag slips to move; drag empty space to rotate.");
		SpikeList.Children.Clear();
		OffSpikeList.Children.Clear();
		HangingTask[] source = EligibleSpikeTasks().ToArray();
		HangingTask[] attached = source.Where(x => x.IsOnSpike).ToArray();
		HangingTask[] array = Filter(attached).ToArray();
		HangingTask[] array2 = Filter(source.Where(x => !x.IsOnSpike)).ToArray();
		OnSpikeLabel.Text = app.Text.T("钉上的纸片", "ON THE SPIKE");
		OffSpikeLabel.Text = app.Text.T("已取下", "OFF THE SPIKE");
		OnSpikeCount.Text = array.Length.ToString();
		OffSpikeCount.Text = array2.Length.ToString();
		SpikeScene.Update(array, app.Settings.LightAppearance, app.Text.Chinese,
			app.Repository.TaskTypes.ToDictionary(type => type.id, type => type.colorHex));
		SpikeHeading.Text = app.Text.T("完成钉", "DONE SPIKE");
		SpikeHeadingCount.Text = attached.Length.ToString();
		string start = TimeSpan.FromMinutes(app.Settings.WorkdayStartMinute).ToString(@"hh\:mm");
		string end = TimeSpan.FromMinutes(app.Settings.WorkdayEndMinute).ToString(@"hh\:mm");
		string nextDay = app.Settings.WorkdayEndsFollowingDay ? app.Text.T("次日 ", "NEXT DAY ") : "";
		SpikeWorkdayLabel.Text = app.Text.T("仅统计完成于 ", "COMPLETED ") + start + " → " + nextDay + end;
		SpikeInteractionHint.Text = app.Text.T("移动纸片 · Ctrl + 滚轮调整大小 · 拖空白处旋转",
			"Move slips · Ctrl + scroll to resize · Drag empty space to rotate");
		SpikeDetailHint.Text = app.Text.T("拖动纸片调整位置。在纸片上按 Ctrl + 滚轮，或使用右侧的小 / 大滑杆，调整纸片大小。",
			"Drag a slip to reposition it. Ctrl + scroll over a slip, or use S / L on the right, to change its size.");
		foreach (HangingTask item in array.OrderByDescending(x => x.completedAt))
		{
			SpikeList.Children.Add(SpikeRow(item, on: true));
		}
		HangingTask[] array3 = array2;
		foreach (HangingTask task in array3)
		{
			OffSpikeList.Children.Add(SpikeRow(task, on: false));
		}
		if (array.Length == 0) SpikeList.Children.Add(SpikeEmptySection());
		if (array2.Length == 0) OffSpikeList.Children.Add(SpikeEmptySection());
		foreach (StackPanel section in new[] { SpikeList, OffSpikeList })
			if (section.Children.Count > 0 && section.Children[section.Children.Count - 1] is SpikePaperRow lastRow)
				lastRow.Margin = new Thickness(0);
	}

	private SpikePaperRow SpikeRow(HangingTask task, bool on)
	{
		uint? tint = app.Repository.TaskTypes.FirstOrDefault(type => type.id == task.taskTypeID)?.colorHex;
		SpikePaperRow row = new SpikePaperRow(task, on, app.Text.Chinese, tint);
		row.OpenRequested += () => app.OpenTask(task);
		row.AttachmentRequested += () =>
		{
			app.Repository.SetSpike(task.id, !on);
			app.RefreshAll();
		};
		row.ScaleRequested += scale =>
		{
			app.Repository.ResizeSpike(task.id, scale);
			SpikeScene.ResizeReceipt(task.id, scale);
		};
		return row;
	}

	private Border SpikeEmptySection()
	{
		Brush background = ((Brush)app.Resources["RaisedBrush"]).Clone();
		background.Opacity = 0.3;
		return new Border
		{
			Background = background,
			Padding = new Thickness(14, 17, 14, 17),
			MinHeight = 48,
			Child = new TextBlock
			{
				Text = string.IsNullOrWhiteSpace(SearchBox.Text) ? app.Text.T("这里还没有纸片。", "No slips here yet.") : app.Text.T("没有匹配的纸片。", "No matching slips."),
				FontSize = 10,
				Foreground = (Brush)app.Resources["MutedBrush"]
			}
		};
	}

	private void Spike_DragOver(object sender, DragEventArgs e)
	{
		e.Effects = e.Data.GetDataPresent(SpikePaperRow.DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
		e.Handled = true;
	}

	private void Spike_Drop(object sender, DragEventArgs e)
	{
		if (e.Data.GetData(SpikePaperRow.DragFormat) is not Guid id || !EligibleSpikeTasks().Any(task => task.id == id)) return;
		app.Repository.SetSpike(id, sender != OffSpikeList);
		app.RefreshAll();
		if (sender == SpikeStage) SpikeScene.PlaceReceiptAt(id, e.GetPosition(SpikeScene));
		e.Handled = true;
	}

	private IEnumerable<HangingTask> EligibleSpikeTasks()
	{
		return DoneSpikeArchive.EligibleCompleted(app.Repository.Tasks,
			new WorkdaySchedule(app.Settings.WorkdayStartMinute, app.Settings.WorkdayEndMinute, app.Settings.WorkdayEndsFollowingDay),
			DateTimeOffset.Now);
	}

	private void AddFile(string path)
	{
		if (File.Exists(path))
		{
			app.Repository.Add(System.IO.Path.GetFileName(path), null, "", null, null, path);
			app.RefreshAll();
		}
	}

	private void Clothesline_Click(object s, RoutedEventArgs e)
	{
		Switch(Page.Clothesline);
	}

	private void Todo_Click(object s, RoutedEventArgs e)
	{
		Switch(Page.Todo);
	}

	private void Completed_Click(object s, RoutedEventArgs e)
	{
		Switch(Page.Completed);
	}

	private void Spike_Click(object s, RoutedEventArgs e)
	{
		Switch(Page.Spike);
	}

	private void Settings_Click(object s, RoutedEventArgs e)
	{
		app.ShowSettings();
	}

	private void Add_Click(object s, RoutedEventArgs e)
	{
		app.ShowAddSource();
	}

	private void DesktopLine_Click(object s, RoutedEventArgs e)
	{
		app.ToggleDesktopLine();
	}

	private void Theme_Click(object s, RoutedEventArgs e)
	{
		AppSettings appSettings = app.Settings.Clone();
		appSettings.LightAppearance = !appSettings.LightAppearance;
		app.ApplySettings(appSettings);
	}

	private void Language_Click(object s, RoutedEventArgs e)
	{
		AppSettings appSettings = app.Settings.Clone();
		appSettings.Language = ((appSettings.Language == "zh") ? "en" : "zh");
		app.ApplySettings(appSettings);
	}

	private void Chrome_Click(object s, RoutedEventArgs e)
	{
		ChromeBridgeService.Install(app.Repository.RootDirectory, Environment.ProcessPath);
		string text = ChromeBridgeService.EnsureExtensionFiles(app.Repository.RootDirectory);
		MessageBox.Show(app.Text.T("Chrome 已连接。\n\n在 chrome://extensions 开启开发者模式并加载：\n" + text, "Chrome is connected.\n\nEnable Developer mode at chrome://extensions and load:\n" + text), "Hang Later");
	}

	private void Search_Changed(object s, TextChangedEventArgs e)
	{
		if (SearchPlaceholder != null)
		{
			SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
		}
		if (base.IsLoaded)
		{
			RefreshAll();
		}
	}

	private void Drop_DragOver(object s, DragEventArgs e)
	{
		e.Effects = ((e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text)) ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private void Drop_Drop(object s, DragEventArgs e)
	{
		if (e.Data.GetData(DataFormats.FileDrop) is string[] array)
		{
			string[] array2 = array;
			foreach (string path in array2)
			{
				AddFile(path);
			}
		}
		else if (e.Data.GetData(DataFormats.Text) is string text && TaskRepository.IsWebUrl(text))
		{
			TaskRepository repository = app.Repository;
			string host = new Uri(text).Host;
			string resumeUrl = text;
			repository.Add(host, text, "", null, null, null, null, resumeUrl);
		}
		app.RefreshAll();
	}
}
