using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using HangLater.Windows.Models;
using HangLater.Windows.Services;

namespace HangLater.Windows.Views;

public partial class IdeaDialog : Window
{
	private readonly App app;






	public IdeaDialog(App app)
	{
		this.app = app;
		InitializeComponent();
		Heading.Text = app.Text.T("挂一个想法", "HANG AN IDEA");
		CancelButton.Content = app.Text.T("取消", "Cancel");
		HangButton.Content = app.Text.T("挂起来", "Hang it");
		IdeaText.Focus();
	}

	private void Hang_Click(object sender, RoutedEventArgs e)
	{
		if (!string.IsNullOrWhiteSpace(IdeaText.Text))
		{
			TaskRepository repository = app.Repository;
			string text = IdeaText.Text;
			string note = app.Text.T("一个想法", "An idea");
			HangingTaskKind? kind = HangingTaskKind.idea;
			repository.Add(text, null, note, null, null, null, null, null, kind);
			base.DialogResult = true;
		}
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}
}
