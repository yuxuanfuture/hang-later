namespace HangLater.Windows.Models;

public static class EdgeActivationPolicy
{
	public const int FixedHeightPixels = 3;

	public const int FixedHoverMilliseconds = 500;

	public static bool Contains(int x, int y, int left, int top, int width, EdgeActivationZone zone)
	{
		if (y < top || y >= top + 3 || x < left || x >= left + width)
		{
			return false;
		}
		int num = x - left;
		double num2 = (double)width / 3.0;
		return zone switch
		{
			EdgeActivationZone.LeftThird => (double)num < num2,
			EdgeActivationZone.CenterThird => (double)num >= num2 && (double)num < num2 * 2.0,
			EdgeActivationZone.RightThird => (double)num >= num2 * 2.0,
			_ => true,
		};
	}
}
