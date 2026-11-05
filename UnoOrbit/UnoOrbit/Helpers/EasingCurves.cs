using Microsoft.UI.Xaml.Media.Animation;

namespace UnoOrbit.Helpers;

public static class Easings
{
    // Material Decelerate: cubic-bezier(0.4, 0, 0.2, 1)
    public static readonly KeySpline MaterialDecelerateSpline = new() { ControlPoint1 = new Windows.Foundation.Point(0.4, 0), ControlPoint2 = new Windows.Foundation.Point(0.2, 1) };

    // Spring Overshoot: cubic-bezier(0.34, 1.56, 0.64, 1)
    // Note: KeySpline control points are clamped to [0,1] range, so spring overshoot
    // can't be exactly represented. We'll use QuadraticEase for close approximation
    // and handle overshoot with slightly larger target values where needed.
    public static readonly KeySpline SpringApproxSpline = new() { ControlPoint1 = new Windows.Foundation.Point(0.34, 1.0), ControlPoint2 = new Windows.Foundation.Point(0.64, 1) };

    public static Storyboard CreateSlideAnimation(
        DependencyObject target, string property,
        double to, double durationMs, KeySpline spline, double delayMs = 0)
    {
        var anim = new DoubleAnimationUsingKeyFrames
        {
            BeginTime = delayMs > 0 ? TimeSpan.FromMilliseconds(delayMs) : null,
        };
        anim.KeyFrames.Add(new SplineDoubleKeyFrame
        {
            KeyTime = TimeSpan.FromMilliseconds(durationMs),
            Value = to,
            KeySpline = new KeySpline
            {
                ControlPoint1 = spline.ControlPoint1,
                ControlPoint2 = spline.ControlPoint2,
            },
        });

        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, property);

        var sb = new Storyboard();
        sb.Children.Add(anim);
        return sb;
    }

    public static Storyboard CreateDoubleAnimation(
        DependencyObject target, string property,
        double to, double durationMs, double delayMs = 0)
    {
        var anim = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            BeginTime = delayMs > 0 ? TimeSpan.FromMilliseconds(delayMs) : null,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };

        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, property);

        var sb = new Storyboard();
        sb.Children.Add(anim);
        return sb;
    }
}
