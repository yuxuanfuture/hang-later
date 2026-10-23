using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HangLater.Windows.Controls;

/// <summary>Keeps an overlay scrollbar visible only during a scroll interaction.</summary>
public static class OverlayScrollBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(OverlayScrollBehavior), new PropertyMetadata(false, OnEnabledChanged));

    private static readonly DependencyPropertyKey IsActivePropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsActive", typeof(bool), typeof(OverlayScrollBehavior), new PropertyMetadata(false));

    public static readonly DependencyProperty IsActiveProperty = IsActivePropertyKey.DependencyProperty;

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(ActivityState), typeof(OverlayScrollBehavior));

    public static bool GetIsEnabled(DependencyObject target) => (bool)target.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject target, bool value) => target.SetValue(IsEnabledProperty, value);
    public static bool GetIsActive(DependencyObject target) => (bool)target.GetValue(IsActiveProperty);

    private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not ScrollViewer viewer) return;
        if (viewer.GetValue(StateProperty) is ActivityState previous) previous.Dispose();
        viewer.ClearValue(StateProperty);
        if ((bool)args.NewValue) viewer.SetValue(StateProperty, new ActivityState(viewer));
    }

    private sealed class ActivityState : IDisposable
    {
        private readonly ScrollViewer viewer;
        private readonly DispatcherTimer idleTimer;
        private bool listening;

        public ActivityState(ScrollViewer viewer)
        {
            this.viewer = viewer;
            idleTimer = new DispatcherTimer(DispatcherPriority.Background, viewer.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1100)
            };
            idleTimer.Tick += OnIdle;
            viewer.Loaded += OnLoaded;
            viewer.Unloaded += OnUnloaded;
            if (viewer.IsLoaded) Listen();
        }

        private void OnLoaded(object sender, RoutedEventArgs args) => Listen();
        private void OnUnloaded(object sender, RoutedEventArgs args) => StopListening();

        private void Listen()
        {
            if (listening) return;
            listening = true;
            viewer.PreviewMouseWheel += OnWheel;
            viewer.ScrollChanged += OnScroll;
            viewer.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnPointerDown), true);
            viewer.AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler(OnCaptureLost), true);
        }

        private void StopListening()
        {
            if (!listening) return;
            listening = false;
            viewer.PreviewMouseWheel -= OnWheel;
            viewer.ScrollChanged -= OnScroll;
            viewer.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnPointerDown));
            viewer.RemoveHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler(OnCaptureLost));
            idleTimer.Stop();
            viewer.SetValue(IsActivePropertyKey, false);
        }

        private void OnWheel(object sender, MouseWheelEventArgs args) => Show();

        private void OnScroll(object sender, ScrollChangedEventArgs args)
        {
            if (ReferenceEquals(args.OriginalSource, viewer) &&
                (Math.Abs(args.VerticalChange) > 0.01 || Math.Abs(args.HorizontalChange) > 0.01)) Show();
        }

        private void OnPointerDown(object sender, MouseButtonEventArgs args)
        {
            if (IsOwnScrollBar(args.OriginalSource as DependencyObject)) Show();
        }

        private void OnCaptureLost(object sender, MouseEventArgs args)
        {
            if (GetIsActive(viewer)) Show();
        }

        private void Show()
        {
            if (viewer.ScrollableHeight <= 0.01 && viewer.ScrollableWidth <= 0.01) return;
            viewer.SetValue(IsActivePropertyKey, true);
            idleTimer.Stop();
            idleTimer.Start();
        }

        private void OnIdle(object? sender, EventArgs args)
        {
            // A visible thumb stays available while the user reaches for it or drags it.
            // Merely hovering the hidden edge never reveals it.
            if (IsOwnScrollBar(Mouse.Captured as DependencyObject) || IsOwnScrollBar(Mouse.DirectlyOver as DependencyObject)) return;
            idleTimer.Stop();
            viewer.SetValue(IsActivePropertyKey, false);
        }

        private bool IsOwnScrollBar(DependencyObject? element)
        {
            while (element != null && !ReferenceEquals(element, viewer))
            {
                if (element is ScrollBar bar) return ReferenceEquals(bar.TemplatedParent, viewer);
                element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
            }
            return false;
        }

        public void Dispose()
        {
            StopListening();
            viewer.Loaded -= OnLoaded;
            viewer.Unloaded -= OnUnloaded;
            idleTimer.Tick -= OnIdle;
        }
    }
}
