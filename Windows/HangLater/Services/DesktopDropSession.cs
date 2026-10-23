using System;
using System.Drawing;

namespace HangLater.Windows.Services;

/// <summary>Only an explicitly visible clothesline may offer a drop target.</summary>
public sealed class DesktopDropSession
{
    private readonly int threshold;
    private Point origin;
    public bool LineVisible { get; private set; }
    public CapturableWindow? Candidate { get; private set; }
    public bool PointerDrag { get; private set; }
    public bool WindowMoving { get; private set; }
    public bool DataDragging { get; private set; }
    public bool HintVisible => LineVisible && (WindowMoving || DataDragging);
    public bool ProbeVisible => LineVisible && (PointerDrag || DataDragging);

    public DesktopDropSession(int threshold = 4) => this.threshold = Math.Max(1,threshold);

    public void SetLineVisible(bool visible)
    {
        if (LineVisible != visible) Reset();
        LineVisible = visible;
    }

    public void Begin(CapturableWindow? externalWindow, Point screenPoint)
    {
        Reset();
        if (!LineVisible) return;
        Candidate = externalWindow;
        origin = screenPoint;
    }

    public void Move(Point screenPoint, NativeMethods.RECT? currentBounds)
    {
        if (!LineVisible || Candidate == null) return;
        if (!currentBounds.HasValue) { Reset(); return; }
        if (Math.Abs(screenPoint.X-origin.X)<threshold && Math.Abs(screenPoint.Y-origin.Y)<threshold) return;
        PointerDrag = true;
        var initial=Candidate.Bounds;var current=currentBounds.Value;
        // A mouse drag within a page is not proof that its application window moved.
        // Resizing also must not masquerade as moving a window onto the line.
        bool sameSize = current.Right-current.Left == initial.Right-initial.Left && current.Bottom-current.Top == initial.Bottom-initial.Top;
        // Dragging a maximized title bar first restores its normal size. That is a
        // window move too, unlike resizing the border of an ordinary window.
        bool restoredMaximized = Candidate.Placement?.ShowCmd == 3 && current.Right>current.Left && current.Bottom>current.Top;
        WindowMoving |= (sameSize || restoredMaximized) && (Math.Abs(current.Left-initial.Left)>=threshold || Math.Abs(current.Top-initial.Top)>=threshold);
    }

    public void ObserveDataDrag(bool supported) => DataDragging = LineVisible && supported;
    public CapturableWindow? FinishPointer()
    {
        var capture = LineVisible && WindowMoving && !DataDragging ? Candidate : null;
        Reset();
        return capture;
    }
    public void Reset() { Candidate=null;PointerDrag=false;WindowMoving=false;DataDragging=false; }
}
