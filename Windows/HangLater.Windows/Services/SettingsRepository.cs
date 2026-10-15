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
        try { Value = File.Exists(FilePath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonSupport.Options) ?? new() : new(); }
        catch { Value = new(); }
        Normalize();
    }
    public void Save(AppSettings value)
    {
        Value = value; Normalize();
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Value, JsonSupport.Options));
        File.Move(temp, FilePath, true);
    }
    private void Normalize()
    {
        Value.ClotheslineVerticalPosition = Math.Clamp(Value.ClotheslineVerticalPosition, 0, 1);
        Value.EdgeHoverMilliseconds = EdgeActivationPolicy.FixedHoverMilliseconds;
        Value.EdgeHeightPixels = EdgeActivationPolicy.FixedHeightPixels;
        Value.WorkdayStartMinute = Math.Clamp(Value.WorkdayStartMinute, 0, 1439);
        Value.WorkdayEndMinute = Math.Clamp(Value.WorkdayEndMinute, 0, 1439);
        if (string.IsNullOrWhiteSpace(Value.GlobalHotKey)) Value.GlobalHotKey = "Ctrl+Alt+L";
    }
}
