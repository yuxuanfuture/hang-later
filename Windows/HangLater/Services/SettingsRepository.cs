using System;
using System.IO;
using System.Text.Json;
using HangLater.Windows.Models;

namespace HangLater.Windows.Services;

public sealed class SettingsRepository
{
	public string FilePath { get; }

	public AppSettings Value { get; private set; }

	public SettingsRepository(string root)
	{
		FilePath = Path.Combine(root, "settings.windows.json");
		try
		{
			Value = (File.Exists(FilePath) ? (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonSupport.Options) ?? new AppSettings()) : new AppSettings());
		}
		catch
		{
			Value = new AppSettings();
		}
		Normalize();
	}

	public void Save(AppSettings value)
	{
		Value = value;
		Normalize();
		string text = FilePath + ".tmp";
		File.WriteAllText(text, JsonSerializer.Serialize(Value, JsonSupport.Options));
		File.Move(text, FilePath, overwrite: true);
	}

	private void Normalize()
	{
		Value.ClotheslineVerticalPosition = Math.Clamp(Value.ClotheslineVerticalPosition, 0.0, 1.0);
		Value.EdgeHoverMilliseconds = 500;
		Value.EdgeHeightPixels = 3;
		Value.WorkdayStartMinute = Math.Clamp(Value.WorkdayStartMinute, 0, 1439);
		Value.WorkdayEndMinute = Math.Clamp(Value.WorkdayEndMinute, 0, 1439);
		if (string.IsNullOrWhiteSpace(Value.GlobalHotKey))
		{
			Value.GlobalHotKey = "Ctrl+Alt+L";
		}
	}
}
