namespace HangLater.Windows.Models;

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

	public AppSettings Clone()
	{
		return (AppSettings)MemberwiseClone();
	}
}
