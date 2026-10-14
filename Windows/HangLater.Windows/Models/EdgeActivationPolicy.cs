namespace HangLater.Windows.Models;

public static class EdgeActivationPolicy
{
    public const int FixedHeightPixels = 3;
    public const int FixedHoverMilliseconds = 500;

    public static bool Contains(int x, int y, int left, int top, int width, EdgeActivationZone zone)
    {
        if (y < top || y >= top + FixedHeightPixels || x < left || x >= left + width) return false;
        var relative = x - left; var third = width / 3.0;
        return zone switch
        {
            EdgeActivationZone.LeftThird => relative < third,
            EdgeActivationZone.CenterThird => relative >= third && relative < third * 2,
            EdgeActivationZone.RightThird => relative >= third * 2,
            _ => true
        };
    }
}
