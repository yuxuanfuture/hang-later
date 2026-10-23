using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using HangLater.Windows.Models;
using HangLater.Windows.Services;
using HangLater.Windows.Views;

namespace HangLater.Windows.Controls;

public static class TaskCardFactory
{
	public const double ClotheslineCompactWidth = 220.0;

	public const double ClotheslineCardTopInset = 44.0;

	private static readonly ImageSource blackClotheslineClipSource = LoadClotheslineClip("clothesline-clip-black.png");

	private static readonly ImageSource whiteClotheslineClipSource = LoadClotheslineClip("clothesline-clip-white.png");

	public static Border CreateListCard(App app, HangingTask task, bool completed = false)
	{
		Border obj = new Border
		{
			Background = (Brush)app.Resources["PanelBrush"],
			BorderBrush = (Brush)app.Resources["BorderBrush"],
			BorderThickness = new Thickness(1.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
			Padding = new Thickness(20.0, 16.0, 20.0, 16.0)
		};
		Grid grid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition
				{
					Width = new GridLength(42.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(96.0)
				},
				new ColumnDefinition(),
				new ColumnDefinition
				{
					Width = GridLength.Auto
				}
			}
		};
		Button button = new Button
		{
			Content = completed ? (object)"✓" : new Ellipse
			{
				Width = 24.0,
				Height = 24.0,
				Stroke = (Brush)app.Resources["MutedBrush"],
				StrokeThickness = 1.6,
				IsHitTestVisible = false
			},
			FontSize = 20.0,
			Width = 42.0,
			Height = 42.0,
			Padding = new Thickness(0.0),
			HorizontalContentAlignment = HorizontalAlignment.Center,
			VerticalContentAlignment = VerticalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			BorderThickness = new Thickness(0.0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			ToolTip = completed ? app.Text.T("重新挂起", "Hang again") : app.Text.T("标记完成", "Mark complete")
		};
		System.Windows.Automation.AutomationProperties.SetName(button,
			completed ? app.Text.T("重新挂起", "Hang again") : app.Text.T("标记完成", "Mark complete"));
		button.Click += delegate
		{
			if (completed)
			{
				app.Repository.Restore(task.id);
			}
			else
			{
				app.Repository.Complete(task.id);
			}
			app.RefreshAll();
		};
		grid.Children.Add(button);
		FrameworkElement element = Preview(app, task, 84.0, 68.0);
		Grid.SetColumn(element, 1);
		grid.Children.Add(element);
		StackPanel stackPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(14.0, 0.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = task.title,
			FontFamily = (FontFamily)app.Resources["DisplayFont"],
			FontWeight = FontWeights.SemiBold,
			FontSize = 17.0,
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		string text = task.SourceLabel + (completed ? $"  ·  {task.completedAt?.LocalDateTime:g}" : ((task.kind == HangingTaskKind.idea) ? "  ·  AN IDEA" : ((!task.dueAt.HasValue) ? "" : $"  ·  ⏰ {task.dueAt?.LocalDateTime:g}")));
		stackPanel.Children.Add(new TextBlock
		{
			Text = text,
			Foreground = (Brush)app.Resources["MutedBrush"],
			Margin = new Thickness(0.0, 6.0, 0.0, 0.0),
			TextTrimming = TextTrimming.CharacterEllipsis
		});
		Grid.SetColumn(stackPanel, 2);
		grid.Children.Add(stackPanel);
		Button action = new Button
		{
			Content = (completed ? app.Text.T("重新挂起", "Hang again") : "⋯"),
			Margin = new Thickness(16.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		if (completed)
		{
			action.Click += delegate
			{
				app.Repository.Restore(task.id);
				app.RefreshAll();
			};
		}
		else
		{
			action.ContextMenu = TaskMenu(app, task);
			action.Click += delegate
			{
				action.ContextMenu.IsOpen = true;
			};
		}
		Grid.SetColumn(action, 3);
		grid.Children.Add(action);
		obj.Child = grid;
		obj.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			if (FindButton(e.OriginalSource as DependencyObject) == null)
			{
				app.OpenTask(task);
			}
		};
		return obj;
	}

	public static Border CreateClotheslineCard(App app, HangingTask task, bool compact, Action<double>? moved = null, Func<double, double>? topForLeft = null)
	{
		double num = (compact ? ClotheslineCompactWidth : 248.0);
		double[] macRestingTilts = { -2.0, 1.6, -1.2, 2.1 };
		int tintIndex = (task.tint % macRestingTilts.Length + macRestingTilts.Length) % macRestingTilts.Length;
		double restingTilt = macRestingTilts[tintIndex];
		Border card = new Border
		{
			Width = num,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			Cursor = Cursors.Hand
		};
		DropShadowEffect paperShadow = new DropShadowEffect
		{
			BlurRadius = 9.0,
			ShadowDepth = 6.0,
			Opacity = 0.09
		};
		Border paper = new Border
		{
			Width = num,
			Background = (Brush)app.Resources["SlipBrush"],
			BorderBrush = Brushes.Black,
			BorderThickness = new Thickness(1.0),
			Effect = paperShadow,
			Margin = new Thickness(0.0, ClotheslineCardTopInset, 0.0, 0.0)
		};
		SkewTransform paperRipple = new SkewTransform();
		ScaleTransform paperScale = new ScaleTransform();
		RotateTransform paperSway = new RotateTransform(restingTilt);
		TransformGroup paperMotion = new TransformGroup();
		paperMotion.Children.Add(paperRipple);
		paperMotion.Children.Add(paperScale);
		paperMotion.Children.Add(paperSway);
		paper.RenderTransformOrigin = new Point(0.5, 0.0);
		paper.RenderTransform = paperMotion;
		StackPanel stackPanel = new StackPanel();
		if (task.kind == HangingTaskKind.idea)
		{
			TextBlock element = new TextBlock
			{
				Text = "IDEA",
				Foreground = (Brush)app.Resources["SlipInkBrush"],
				FontFamily = (FontFamily)app.Resources["DisplayFont"],
				FontWeight = FontWeights.Bold,
				FontSize = 13.0,
				Margin = new Thickness(15.0, 14.0, 15.0, 4.0)
			};
			stackPanel.Children.Add(element);
			stackPanel.Children.Add(new TextBlock
			{
				Text = task.title,
				Foreground = (Brush)app.Resources["SlipInkBrush"],
				FontSize = 19.0,
				FontWeight = FontWeights.SemiBold,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(15.0, 8.0, 15.0, 22.0),
				MinHeight = (compact ? 62 : 82)
			});
		}
		else
		{
			FrameworkElement frameworkElement = Preview(app, task, num - 2.0, compact ? 104 : 132);
			frameworkElement.Margin = new Thickness(0.0);
			stackPanel.Children.Add(frameworkElement);
			stackPanel.Children.Add(new TextBlock
			{
				Text = task.title,
				Foreground = (Brush)app.Resources["SlipInkBrush"],
				FontFamily = (FontFamily)app.Resources["DisplayFont"],
				FontWeight = FontWeights.Bold,
				FontSize = (compact ? 15 : 17),
				TextTrimming = TextTrimming.CharacterEllipsis,
				Margin = new Thickness(14.0, 10.0, 14.0, 2.0)
			});
			stackPanel.Children.Add(new TextBlock
			{
				Text = task.SourceLabel,
				Foreground = Brushes.DimGray,
				FontSize = 11.0,
				Margin = new Thickness(14.0, 0.0, 14.0, 10.0)
			});
		}
		Grid grid = new Grid
		{
			Height = (compact ? 42 : 48),
			Background = DoneBrush(app, task)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		Button button = new Button
		{
			Content = CompleteButtonContent(app, compact),
			Foreground = Brushes.Black,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			HorizontalAlignment = HorizontalAlignment.Left
		};
		button.Click += delegate(object _, RoutedEventArgs e)
		{
			e.Handled = true;
			app.Repository.Complete(task.id);
			app.RefreshAll();
		};
		grid.Children.Add(button);
		Button more = new Button
		{
			Content = new TextBlock
			{
				Text = "•••",
				Foreground = Brushes.Black,
				FontFamily = (FontFamily)app.Resources["MonoFont"],
				FontSize = 14.0,
				FontWeight = FontWeights.Bold
			},
			Foreground = Brushes.Black,
			Background = Brushes.Transparent,
			BorderThickness = new Thickness(0.0),
			ContextMenu = TaskMenu(app, task)
		};
		more.Click += delegate(object _, RoutedEventArgs e)
		{
			e.Handled = true;
			more.ContextMenu.IsOpen = true;
		};
		Grid.SetColumn(more, 1);
		grid.Children.Add(more);
		stackPanel.Children.Add(new Border
		{
			BorderBrush = Brushes.Black,
			BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
			Child = grid
		});
		paper.Child = stackPanel;
		Grid hangingCard = new Grid
		{
			ClipToBounds = false
		};
		hangingCard.Children.Add(paper);
		Image clip = new Image
		{
			Source = app.Settings.LightAppearance ? whiteClotheslineClipSource : blackClotheslineClipSource,
			Width = 26.0,
			Height = 72.0,
			Stretch = Stretch.Fill,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			IsHitTestVisible = false,
			Effect = new DropShadowEffect
			{
				BlurRadius = 3.0,
				ShadowDepth = 1.0,
				Opacity = 0.28
			}
		};
		hangingCard.Children.Add(clip);
		card.Child = hangingCard;
		Point start = default(Point);
		double original = 0.0;
		bool dragging = false;
		bool settling = false;
		double latestWind = 0.0;
		double latestRipple = 0.0;
		double releasedWind = 0.0;
		double releasedRipple = 0.0;
		DateTime settleStartedAt = DateTime.UtcNow;
		DispatcherTimer macWindTimer = new DispatcherTimer(DispatcherPriority.Render)
		{
			Interval = TimeSpan.FromSeconds(1.0 / 30.0)
		};

		void ApplyMacMotion(double wind, double ripple, double scale)
		{
			paperSway.Angle = restingTilt + wind * 2.8;
			paperRipple.AngleY = ripple * 3.2;
			paperScale.ScaleX = scale;
			paperScale.ScaleY = scale;
			paper.CornerRadius = new CornerRadius(
				1.0 + Math.Max(0.0, wind) * 5.0,
				1.0 + Math.Max(0.0, -wind) * 6.0,
				1.0 + Math.Max(0.0, ripple) * 9.0,
				1.0 + Math.Max(0.0, -ripple) * 10.0);
		}

		double MacSpring(double elapsed)
		{
			const double response = 0.24;
			const double dampingFraction = 0.78;
			double undampedFrequency = 2.0 * Math.PI / response;
			double dampedRatio = Math.Sqrt(1.0 - dampingFraction * dampingFraction);
			double dampedFrequency = undampedFrequency * dampedRatio;
			return Math.Exp(-dampingFraction * undampedFrequency * elapsed) *
				(Math.Cos(dampedFrequency * elapsed) +
				 dampingFraction / dampedRatio * Math.Sin(dampedFrequency * elapsed));
		}

		macWindTimer.Tick += delegate
		{
			if (dragging)
			{
				double time = (DateTime.UtcNow - new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
				latestWind = Math.Sin(time * 7.1);
				latestRipple = Math.Cos(time * 5.3 + task.tint);
				ApplyMacMotion(latestWind, latestRipple, 1.025);
				return;
			}
			if (settling)
			{
				double elapsed = (DateTime.UtcNow - settleStartedAt).TotalSeconds;
				double spring = MacSpring(elapsed);
				ApplyMacMotion(releasedWind * spring, releasedRipple * spring, 1.0 + 0.025 * spring);
				if (elapsed >= 0.55)
				{
					settling = false;
					macWindTimer.Stop();
					ApplyMacMotion(0.0, 0.0, 1.0);
					paperShadow.BlurRadius = 9.0;
					paperShadow.ShadowDepth = 6.0;
					paperShadow.Opacity = 0.09;
				}
			}
		};

		ApplyMacMotion(0.0, 0.0, 1.0);
		card.PreviewMouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
		{
			if (FindButton(e.OriginalSource as DependencyObject) == null)
			{
				start = e.GetPosition(card.Parent as IInputElement);
				original = Canvas.GetLeft(card);
				if (double.IsNaN(original))
				{
					original = 0.0;
				}
				settling = false;
				macWindTimer.Stop();
				ApplyMacMotion(0.0, 0.0, 1.0);
				paperShadow.BlurRadius = 9.0;
				paperShadow.ShadowDepth = 6.0;
				paperShadow.Opacity = 0.09;
				dragging = false;
				card.CaptureMouse();
			}
		};
		card.PreviewMouseMove += delegate(object _, MouseEventArgs e)
		{
			if (card.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed && card.Parent is Canvas canvas)
			{
				Point position = e.GetPosition(canvas);
				if (!dragging && Math.Abs(position.X - start.X) > 4.0)
				{
					dragging = true;
					paperShadow.BlurRadius = 20.0;
					paperShadow.ShadowDepth = 15.0;
					paperShadow.Opacity = 0.24;
					macWindTimer.Start();
				}
				double nextLeft = Math.Clamp(original + position.X - start.X, 0.0, Math.Max(0.0, canvas.ActualWidth - card.Width));
				Canvas.SetLeft(card, nextLeft);
				if (topForLeft != null)
				{
					Canvas.SetTop(card, topForLeft(nextLeft));
				}
				Panel.SetZIndex(card, 1000);
			}
		};
		card.PreviewMouseLeftButtonUp += delegate
		{
			if (card.IsMouseCaptured)
			{
				card.ReleaseMouseCapture();
				bool wasDragging = dragging;
				if (wasDragging && card.Parent is Canvas canvas)
				{
					moved?.Invoke(Canvas.GetLeft(card) / Math.Max(1.0, canvas.ActualWidth - card.Width));
					Panel.SetZIndex(card, task.lineLayer.GetValueOrDefault());
				}
				else
				{
					app.OpenTask(task);
				}
				dragging = false;
				if (wasDragging)
				{
					releasedWind = latestWind;
					releasedRipple = latestRipple;
					settleStartedAt = DateTime.UtcNow;
					settling = true;
					macWindTimer.Start();
				}
			}
		};
		return card;
	}

	private static ImageSource LoadClotheslineClip(string fileName)
	{
		BitmapImage bitmap = new BitmapImage();
		bitmap.BeginInit();
		bitmap.UriSource = new Uri($"pack://application:,,,/HangLater;component/Resources/{fileName}", UriKind.Absolute);
		bitmap.CacheOption = BitmapCacheOption.OnLoad;
		bitmap.EndInit();
		bitmap.Freeze();
		return bitmap;
	}

	public static ContextMenu TaskMenu(App app, HangingTask task)
	{
		ContextMenu contextMenu = new ContextMenu
		{
			Background = new SolidColorBrush(Color.FromRgb(248, 248, 246)),
			Foreground = Brushes.Black,
			BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 61)),
			BorderThickness = new Thickness(1.0),
			FontFamily = (FontFamily)app.Resources["MonoFont"],
			FontSize = 12.0
		};
		MenuItem menuItem = new MenuItem
		{
			Header = MenuHeader(app, app.Text.T("编辑", "Edit")),
			Foreground = Brushes.Black
		};
		menuItem.Click += delegate
		{
			if (new TaskEditorWindow(app, task)
			{
				Owner = app.MainWindowInstance
			}.ShowDialog() == true)
			{
				app.RefreshAll();
			}
		};
		contextMenu.Items.Add(menuItem);
		MenuItem menuItem2 = new MenuItem
		{
			Header = MenuHeader(app, (!task.dueAt.HasValue) ? app.Text.T("添加提醒…", "Add reminder…") : app.Text.T("修改提醒…", "Change reminder…")),
			Foreground = Brushes.Black
		};
		menuItem2.Click += delegate
		{
			if (new ReminderDialog(app, task)
			{
				Owner = app.MainWindowInstance
			}.ShowDialog() == true)
			{
				app.RefreshAll();
			}
		};
		contextMenu.Items.Add(menuItem2);
		MenuItem menuItem3 = new MenuItem
		{
			Header = MenuHeader(app, app.Text.T("任务类型", "Task type")),
			Foreground = Brushes.Black
		};
		MenuItem menuItem4 = new MenuItem
		{
			Header = MenuHeader(app, app.Text.T("默认", "Default")),
			IsCheckable = true,
			IsChecked = !task.taskTypeID.HasValue,
			Foreground = Brushes.Black
		};
		menuItem4.Click += delegate
		{
			HangingTask hangingTask = task.Clone();
			hangingTask.taskTypeID = null;
			app.Repository.Update(hangingTask);
			app.RefreshAll();
		};
		menuItem3.Items.Add(menuItem4);
		foreach (TaskTypeDefinition type in app.Repository.TaskTypes)
		{
			MenuItem menuItem5 = new MenuItem
			{
				Header = MenuHeader(app, type.name),
				IsCheckable = true,
				IsChecked = (task.taskTypeID == type.id),
				Foreground = Brushes.Black
			};
			menuItem5.Click += delegate
			{
				HangingTask hangingTask = task.Clone();
				hangingTask.taskTypeID = type.id;
				app.Repository.Update(hangingTask);
				app.RefreshAll();
			};
			menuItem3.Items.Add(menuItem5);
		}
		contextMenu.Items.Add(menuItem3);
		contextMenu.Items.Add(new Separator());
		MenuItem menuItem6 = new MenuItem
		{
			Header = MenuHeader(app, app.Text.T("删除", "Delete")),
			Foreground = Brushes.Black
		};
		menuItem6.Click += delegate
		{
			app.Repository.Delete(task.id);
			app.RefreshAll();
		};
		contextMenu.Items.Add(menuItem6);
		return contextMenu;
	}

	private static TextBlock MenuHeader(App app, string text)
	{
		return new TextBlock
		{
			Text = text,
			Foreground = Brushes.Black,
			FontFamily = (FontFamily)app.Resources["MonoFont"],
			FontSize = 12.0
		};
	}

	private static FrameworkElement Preview(App app, HangingTask task, double width, double height)
	{
		BitmapSource bitmapSource = ShellThumbnailService.LoadPreview(app.Repository, task, (int)Math.Max(width, height) * 2);
		if (bitmapSource != null)
		{
			return new Image
			{
				Source = bitmapSource,
				Width = width,
				Height = height,
				Stretch = Stretch.UniformToFill
			};
		}
		return new Border
		{
			Width = width,
			Height = height,
			Background = new SolidColorBrush(Color.FromRgb(220, 213, 195)),
			Child = new TextBlock
			{
				Text = ((task.kind == HangingTaskKind.reminder) ? "\ud83d\udd14" : ((task.kind == HangingTaskKind.meeting) ? "↗" : "⌁")),
				Foreground = Brushes.Black,
				FontSize = 28.0,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			}
		};
	}

	private static StackPanel CompleteButtonContent(App app, bool compact)
	{
		StackPanel content = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		content.Children.Add(new Ellipse
		{
			Width = compact ? 16.0 : 18.0,
			Height = compact ? 16.0 : 18.0,
			Stroke = Brushes.Black,
			StrokeThickness = 1.6,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		});
		content.Children.Add(new TextBlock
		{
			Text = app.Text.T("完成", "Done"),
			Foreground = Brushes.Black,
			FontFamily = (FontFamily)app.Resources["MonoFont"],
			FontSize = compact ? 12.0 : 13.0,
			FontWeight = FontWeights.SemiBold,
			VerticalAlignment = VerticalAlignment.Center
		});
		return content;
	}

	private static Brush DoneBrush(App app, HangingTask task)
	{
		TaskTypeDefinition taskTypeDefinition = app.Repository.TaskTypes.FirstOrDefault(delegate(TaskTypeDefinition x)
		{
			Guid id = x.id;
			Guid? taskTypeID = task.taskTypeID;
			return id == taskTypeID;
		});
		if (taskTypeDefinition == null)
		{
			return (Brush)app.Resources["SlipBrush"];
		}
		return new SolidColorBrush(Color.FromRgb((byte)(taskTypeDefinition.colorHex >> 16), (byte)(taskTypeDefinition.colorHex >> 8), (byte)taskTypeDefinition.colorHex));
	}

	private static Brush TaskTypeForeground(App app, HangingTask task)
	{
		TaskTypeDefinition taskTypeDefinition = app.Repository.TaskTypes.FirstOrDefault(delegate(TaskTypeDefinition x)
		{
			Guid id = x.id;
			Guid? taskTypeID = task.taskTypeID;
			return id == taskTypeID;
		});
		if (taskTypeDefinition == null)
		{
			return Brushes.Black;
		}
		uint num = (taskTypeDefinition.colorHex >> 16) & 0xFF;
		uint num2 = (taskTypeDefinition.colorHex >> 8) & 0xFF;
		uint num3 = taskTypeDefinition.colorHex & 0xFF;
		if (!(0.299 * (double)num + 0.587 * (double)num2 + 0.114 * (double)num3 > 150.0))
		{
			return Brushes.White;
		}
		return Brushes.Black;
	}

	private static Button? FindButton(DependencyObject? current)
	{
		while (current != null)
		{
			if (current is Button result)
			{
				return result;
			}
			current = VisualTreeHelper.GetParent(current);
		}
		return null;
	}
}
