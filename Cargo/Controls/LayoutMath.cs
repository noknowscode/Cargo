namespace Cargo.Controls;

/// <summary>
/// The arithmetic behind <see cref="FlowPanel"/> and <see cref="TileGridPanel"/>, kept apart from the panels so
/// the unit tests can check it without creating a XAML object.
/// </summary>
public static class LayoutMath
{
    /// <summary>
    /// Where each child goes: left to right, a new line when the next would pass <paramref name="width"/>; each line
    /// as tall as its tallest child, and every child that height (so a line's children share one baseline row). A
    /// child wider than the line gets a line of its own, clipped to it. Collapsed children (no size) take no gap.
    /// </summary>
    public static ((double X, double Y, double Width, double Height)[] Slots, double Width, double Height) Flow(
        IReadOnlyList<(double Width, double Height)> sizes, double width, double spacing, double rowSpacing)
    {
        var slots = new (double X, double Y, double Width, double Height)[sizes.Count];
        var limit = double.IsInfinity(width) || double.IsNaN(width) ? double.MaxValue : width;

        double x = 0, y = 0, lineHeight = 0, used = 0;
        var lineStart = 0;
        var any = false;

        void EndLine(int end)
        {
            for (var j = lineStart; j < end; j++)
            {
                slots[j].Height = sizes[j].Width > 0 ? lineHeight : 0;
            }
        }

        for (var i = 0; i < sizes.Count; i++)
        {
            var (w, h) = sizes[i];
            if (w <= 0 && h <= 0)
            {
                slots[i] = (x, y, 0, 0);
                continue;
            }

            var fitted = Math.Min(w, limit);
            var start = any ? x + spacing : 0;
            if (any && start + fitted > limit + 0.5)
            {
                EndLine(i);
                y += lineHeight + rowSpacing;
                lineStart = i;
                lineHeight = 0;
                start = 0;
            }

            slots[i] = (start, y, fitted, h);
            x = start + fitted;
            lineHeight = Math.Max(lineHeight, h);
            used = Math.Max(used, x);
            any = true;
        }

        EndLine(sizes.Count);
        return (slots, any ? used : 0, any ? y + lineHeight : 0);
    }

    /// <summary>How many columns a width holds: as many tiles of the minimum width as fit with their gaps, at least one.</summary>
    public static int TileColumns(double width, double minTileWidth, double gap) =>
        Math.Max(1, (int)Math.Floor((width + gap) / (minTileWidth + gap)));
}
