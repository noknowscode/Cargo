using Windows.Foundation;

namespace Cargo.Controls.Harbour;

/// <summary>
/// Places the harbour's data tags for one frame, in the order they are offered: each sits on a short leader above its
/// vessel's mast, inside the frame, clear of the tags already placed. Vessels alongside neighbouring berths project close
/// together, most of all in the compact strip and on a phone, so a tag that lands on one already placed moves up above
/// it (its leader grows); with no room above, beside it (right, then left); then below it. When none of those is clear
/// it does not fit, and it gives way for that frame rather than cover another's countdown (D-C6).
/// </summary>
public sealed class TagLayout
{
    /// <summary>The leader's length above the mast, and the clearance kept between tags and from the frame's left edge.</summary>
    public const double Lead = 30, Gap = 6, Edge = 10;

    private readonly List<Rect> _placed = new();
    private readonly double _maxRight;
    private readonly double _minTop;
    private readonly double _maxBottom;

    /// <summary>A frame whose tags stay left of <paramref name="maxRight"/> and between <paramref name="minTop"/> and <paramref name="maxBottom"/>.</summary>
    public TagLayout(double maxRight, double minTop, double maxBottom)
    {
        _maxRight = maxRight;
        _minTop = minTop;
        _maxBottom = maxBottom;
    }

    /// <summary>
    /// Places a tag of the given size for a mast projected at (<paramref name="x"/>, <paramref name="y"/>): centred over
    /// it on its leader, kept inside the frame, moved clear of the tags placed before it. Null when it has no clear spot.
    /// </summary>
    public Rect? Place(double x, double y, double width, double height)
    {
        var left = Math.Clamp(x - width / 2, Edge, Math.Max(Edge, _maxRight - width));
        var top = Math.Max(_minTop, y - Lead - height);
        if (AvoidOverlap(left, top, width, height) is not { } spot)
        {
            return null;
        }

        _placed.Add(spot);
        return spot;
    }

    private Rect? AvoidOverlap(double left, double top, double width, double height)
    {
        for (var guard = 0; guard < _placed.Count * 4 + 1; guard++)
        {
            var hit = _placed.FirstOrDefault(r => left < r.Right + Gap && left + width + Gap > r.Left
                && top < r.Bottom + Gap && top + height + Gap > r.Top, Rect.Empty);
            if (hit.IsEmpty)
            {
                return new Rect(left, top, width, height);
            }

            if (hit.Top - Gap - height >= _minTop)
            {
                top = hit.Top - Gap - height;
            }
            else if (hit.Right + Gap + width <= _maxRight)
            {
                left = hit.Right + Gap;
            }
            else if (hit.Left - Gap - width >= Edge)
            {
                left = hit.Left - Gap - width;
            }
            else if (hit.Bottom + Gap + height <= _maxBottom)
            {
                top = hit.Bottom + Gap;
            }
            else
            {
                return null;
            }
        }

        return null;
    }
}
