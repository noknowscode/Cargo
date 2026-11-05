using Microsoft.UI.Dispatching;

namespace Cargo.Controls.Harbour;

/// <summary>
/// Overview's harbour band in motion: its height below the overlay (340, 491 expanded, the 180 mini while scrolled) and
/// its reach up behind the overlay (the overlay's height while shown, 0 folded). Between two band states both ease over
/// DurationExpandMs on EaseSmooth, stepped per frame, and each step hands them to the view, which sets the height, the
/// reach, the scene's slide, the clip and the edge fades together (driving Height and following it from SizeChanged put
/// those a frame behind and shimmered). The scene renders once at full height plus the overlay and is only clipped and
/// slid (<see cref="Slide"/>), so no step re-bakes or re-frames it.
/// </summary>
public sealed class HarbourBand
{
    /// <summary>The band's full height (expanded) and how far the scene rides up when collapsed to 340 (the design).</summary>
    public const double Full = 491, Collapsed = 340, Rise = 110;

    /// <summary>
    /// The mini band Overview condenses to while scrolled (SPEC-OVERVIEW-SCROLL), and how far the scene sits up in it:
    /// the slice from scene y 180 to 360 holds every pin and most of each hull; tags clamp to its top edge.
    /// </summary>
    public const double Mini = 180, MiniRise = 180;

    private readonly Action<double, double> _step;
    private double _heightFrom, _heightTo, _insetFrom, _insetTo;
    private DateTimeOffset _start;
    private bool _ticking;

    /// <summary>A band that hands each step's height and reach to <paramref name="step"/>.</summary>
    public HarbourBand(Action<double, double> step) => _step = step;

    /// <summary>The band's height below the overlay as of the last step; NaN when the stage is not a band.</summary>
    public double Height { get; private set; } = double.NaN;

    /// <summary>How far the band reaches up behind the overlay as of the last step.</summary>
    public double Inset { get; private set; }

    /// <summary>
    /// Moves the band to a height and a reach: at once when <paramref name="animate"/> is false, when either end is
    /// not a band (NaN) or under reduced motion; otherwise eased, one step per frame.
    /// </summary>
    public void MoveTo(double height, double inset, bool animate, DispatcherQueue dispatcher)
    {
        Stop();
        if (!animate || double.IsNaN(height) || double.IsNaN(Height) || Motion.Reduced)
        {
            Set(height, inset);
            return;
        }

        (_heightFrom, _heightTo, _insetFrom, _insetTo, _start) = (Height, height, Inset, inset, DateTimeOffset.Now);
        CompositionTarget.Rendering += OnFrame;
        _ticking = true;

        // Rendering stops when a frame changes nothing on screen, so the last step is guaranteed by a timer
        var start = _start;
        var finish = dispatcher.CreateTimer();
        finish.Interval = Motion.Duration("DurationExpandMs") + TimeSpan.FromMilliseconds(50);
        finish.IsRepeating = false;
        finish.Tick += (_, _) =>
        {
            if (_ticking && _start == start)
            {
                Set(_heightTo, _insetTo);
                Stop();
            }
        };
        finish.Start();
    }

    /// <summary>
    /// Where the scene sits in the band: below the overlay, flush at 491, the design's 110 px up at 340, 180 up at the
    /// mini band; the overlay's full height sits above that in the rendered scene, and the view reaches up into it by
    /// <paramref name="inset"/>. <c>Offset</c> is how far the view's top sits into the scene; <c>Below</c> how much of the
    /// scene lies below the view. Both are 0 when the stage is not a band.
    /// </summary>
    public static (double Offset, double Below) Slide(bool band, double visible, double inset, double overlay)
    {
        if (!band)
        {
            return (0, 0);
        }

        var below = Math.Max(0, visible - inset);
        var rise = visible <= 0
            ? 0
            : below >= Collapsed
                ? Math.Clamp((Full - below) * Rise / (Full - Collapsed), 0, Rise)
                : Math.Clamp(Rise + (Collapsed - below) * (MiniRise - Rise) / (Collapsed - Mini), Rise, MiniRise);
        var offset = rise + overlay - inset;
        return (offset, Math.Max(0, Full + overlay - visible - offset));
    }

    private void OnFrame(object? sender, object e)
    {
        var k = (DateTimeOffset.Now - _start).TotalMilliseconds / Motion.Duration("DurationExpandMs").TotalMilliseconds;
        var eased = Motion.Curve(Math.Min(1, k));
        Set(_heightFrom + (_heightTo - _heightFrom) * eased, _insetFrom + (_insetTo - _insetFrom) * eased);
        if (k >= 1)
        {
            Stop();
        }
    }

    private void Stop()
    {
        if (_ticking)
        {
            CompositionTarget.Rendering -= OnFrame;
            _ticking = false;
        }
    }

    private void Set(double height, double inset)
    {
        (Height, Inset) = (height, double.IsNaN(height) ? 0 : inset);
        _step(Height, Inset);
    }
}
