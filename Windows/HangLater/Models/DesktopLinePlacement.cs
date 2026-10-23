using System;

namespace HangLater.Windows.Models;

public static class DesktopLinePlacement
{
	public static DesktopLineBounds PhysicalBounds(int workingLeft, int workingTop, int workingWidth, int workingHeight, double dpiScaleY, double verticalPosition)
	{
		double scale = Math.Max(0.5, dpiScaleY);
		int panelHeight = Math.Min((int)Math.Round(420.0 * scale), workingHeight);
		panelHeight = Math.Max(1, Math.Min(workingHeight, panelHeight));
		double position = Math.Clamp(verticalPosition, 0.0, 1.0);
		int topLift = (int)Math.Round(40.0 * scale);
		int travel = Math.Max(0, workingHeight - panelHeight + topLift);
		int y = workingTop - topLift + (int)Math.Round(travel * position);
		return new DesktopLineBounds(workingLeft, y, workingWidth, panelHeight);
	}
}
