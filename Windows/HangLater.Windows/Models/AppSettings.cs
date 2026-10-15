namespace HangLater.Windows.Models;

public enum EdgeActivationZone { FullWidth, LeftThird, CenterThird, RightThird }

public sealed class AppSettings
{
    public string Language { get; set; } = "zh";
    public bool LightAppearance { get; set; }
    public bool KeepClotheslineVisible { get; set; }
    public double ClotheslineVerticalPosition { get; set; }
    public int WorkdayStartMinute { get; set; }
    public int WorkdayEndMinute { get; set; }
    public bool WorkdayEndsFollowingDay { get; set; } = true;
    public string GlobalHotKey { get; set; } = "Ctrl+Alt+L";
    public bool EdgeHoverEnabled { get; set; }
    public EdgeActivationZone EdgeZone { get; set; } = EdgeActivationZone.CenterThird;
    public int EdgeHoverMilliseconds { get; set; } = 500;
    public int EdgeHeightPixels { get; set; } = 3;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

public readonly record struct WorkdaySchedule(int StartMinute, int EndMinute, bool EndsFollowingDay)
{
    public (DateTimeOffset Start, DateTimeOffset End)? IntervalContaining(DateTimeOffset moment)
    {
        var local = moment.DateTime;
        var today = local.Date;
        foreach (var day in new[] { today, today.AddDays(-1) })
        {
            var start = new DateTimeOffset(day.AddMinutes(Math.Clamp(StartMinute, 0, 1439)), moment.Offset);
            var endDay = EndsFollowingDay ? day.AddDays(1) : day;
            var end = new DateTimeOffset(endDay.AddMinutes(Math.Clamp(EndMinute, 0, 1439)), moment.Offset);
            if (end > start && moment >= start && moment < end) return (start, end);
        }
        return null;
    }
}
