using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NPUtil.App.Common;

public static class FluentAnimationHelper
{
    public static void AnimateEntrance(FrameworkElement element)
    {
        if (element == null) return;

        var translate = new TranslateTransform(0, 14);
        element.RenderTransform = translate;

        var sb = new Storyboard();

        // 1. Fade in
        var fadeAnim = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fadeAnim, element);
        Storyboard.SetTargetProperty(fadeAnim, new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(fadeAnim);

        // 2. Slide up
        var slideAnim = new DoubleAnimation
        {
            From = 14.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slideAnim, element);
        Storyboard.SetTargetProperty(slideAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        sb.Children.Add(slideAnim);

        sb.Begin();
    }
}