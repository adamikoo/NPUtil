using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NPUtil.App.Common;

public static class SmoothScrollHelper
{
    public static readonly DependencyProperty IsSmoothScrollEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsSmoothScrollEnabled",
            typeof(bool),
            typeof(SmoothScrollHelper),
            new PropertyMetadata(false, OnIsSmoothScrollEnabledChanged));

    public static bool GetIsSmoothScrollEnabled(DependencyObject obj) =>
        (bool)obj.GetValue(IsSmoothScrollEnabledProperty);

    public static void SetIsSmoothScrollEnabled(DependencyObject obj, bool value) =>
        obj.SetValue(IsSmoothScrollEnabledProperty, value);

    private static readonly DependencyProperty ControllerProperty =
        DependencyProperty.RegisterAttached(
            "Controller",
            typeof(SmoothScrollController),
            typeof(SmoothScrollHelper),
            new PropertyMetadata(null));

    private static void OnIsSmoothScrollEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer)
        {
            if ((bool)e.NewValue)
            {
                var controller = new SmoothScrollController(scrollViewer);
                scrollViewer.SetValue(ControllerProperty, controller);
            }
            else
            {
                if (scrollViewer.GetValue(ControllerProperty) is SmoothScrollController controller)
                {
                    controller.Dispose();
                    scrollViewer.ClearValue(ControllerProperty);
                }
            }
        }
    }
}

internal class SmoothScrollController : IDisposable
{
    private readonly ScrollViewer _sv;
    private double _targetOffset;
    private bool _isRendering;

    public SmoothScrollController(ScrollViewer sv)
    {
        _sv = sv;
        _targetOffset = sv.VerticalOffset;
        _sv.PreviewMouseWheel += OnPreviewMouseWheel;
        _sv.ScrollChanged += OnScrollChanged;
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // When user manually drags the scrollbar thumb, keep target synchronized
        if (!_isRendering)
        {
            _targetOffset = _sv.VerticalOffset;
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_sv.ScrollableHeight <= 0) return;

        e.Handled = true;

        // Controlled, comfortable scrolling velocity:
        // Discrete mouse wheel ticks (|delta| >= 120): ~48 pixels per notch.
        // Precision touchpads (|delta| < 120): proportional gentle scaling.
        double step;
        if (Math.Abs(e.Delta) >= 120)
        {
            step = -Math.Sign(e.Delta) * 48.0;
        }
        else
        {
            step = -e.Delta * 0.45;
        }

        // Clamp target offset to valid scrollable range
        _targetOffset = Math.Clamp(_targetOffset + step, 0, _sv.ScrollableHeight);

        StartRendering();
    }

    private void StartRendering()
    {
        if (!_isRendering)
        {
            _isRendering = true;
            CompositionTarget.Rendering += OnRendering;
        }
    }

    private void StopRendering()
    {
        if (_isRendering)
        {
            _isRendering = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        double current = _sv.VerticalOffset;
        double diff = _targetOffset - current;

        // Snapping threshold when very close to target
        if (Math.Abs(diff) < 0.4)
        {
            _sv.ScrollToVerticalOffset(_targetOffset);
            StopRendering();
            return;
        }

        // Exponential damping tailored for 60Hz and 120Hz displays
        double next = current + diff * 0.22;
        _sv.ScrollToVerticalOffset(next);
    }

    public void Dispose()
    {
        StopRendering();
        _sv.PreviewMouseWheel -= OnPreviewMouseWheel;
        _sv.ScrollChanged -= OnScrollChanged;
    }
}