using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

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

    private static readonly DependencyProperty TargetOffsetProperty =
        DependencyProperty.RegisterAttached(
            "TargetOffset",
            typeof(double),
            typeof(SmoothScrollHelper),
            new PropertyMetadata(0.0));

    private static void OnIsSmoothScrollEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer)
        {
            if ((bool)e.NewValue)
            {
                scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            }
            else
            {
                scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
            }
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        e.Handled = true;

        // Controlled scroll step (75 pixels per notch, smooth and restrained)
        var step = -Math.Sign(e.Delta) * 75.0;

        var currentTarget = (double)sv.GetValue(TargetOffsetProperty);
        if (Math.Abs(currentTarget - sv.VerticalOffset) > 200)
        {
            currentTarget = sv.VerticalOffset;
        }

        var newTarget = Math.Clamp(currentTarget + step, 0, sv.ScrollableHeight);
        sv.SetValue(TargetOffsetProperty, newTarget);

        var anim = new ScrollAnimation
        {
            From = sv.VerticalOffset,
            To = newTarget,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        sv.BeginAnimation(AnimatableVerticalOffsetProperty, anim);
    }

    public static readonly DependencyProperty AnimatableVerticalOffsetProperty =
        DependencyProperty.RegisterAttached(
            "AnimatableVerticalOffset",
            typeof(double),
            typeof(SmoothScrollHelper),
            new PropertyMetadata(0.0, OnAnimatableOffsetChanged));

    private static void OnAnimatableOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv)
        {
            sv.ScrollToVerticalOffset((double)e.NewValue);
        }
    }
}

internal class ScrollAnimation : DoubleAnimation
{
}
