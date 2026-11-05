using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>
/// Children left to right at their own width, wrapping to a new line when the next one would pass the edge:
/// masthead figures, legends, filter chips and button rows that sit on one line on a desk and fold on a phone.
/// WinUI has no wrap panel, and the Toolkit's AutoLayout does not wrap.
/// </summary>
public sealed partial class FlowPanel : Panel
{
    /// <summary>The gap between two children on a line.</summary>
    public double Spacing { get; set; } = 8;

    /// <summary>The gap between two lines.</summary>
    public double RowSpacing { get; set; } = 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        var sizes = new (double Width, double Height)[Children.Count];
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(new Size(availableSize.Width, double.PositiveInfinity));
            sizes[i] = (Children[i].DesiredSize.Width, Children[i].DesiredSize.Height);
        }

        var (_, width, height) = LayoutMath.Flow(sizes, availableSize.Width, Spacing, RowSpacing);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Arrange can be asked for a width Measure never saw (a Stretch parent): place again for it
        var sizes = new (double Width, double Height)[Children.Count];
        for (var i = 0; i < Children.Count; i++)
        {
            sizes[i] = (Children[i].DesiredSize.Width, Children[i].DesiredSize.Height);
        }

        var slots = LayoutMath.Flow(sizes, finalSize.Width, Spacing, RowSpacing).Slots;
        for (var i = 0; i < Children.Count; i++)
        {
            var (x, y, w, h) = slots[i];
            Children[i].Arrange(new Rect(x, y, w, h));
        }

        return finalSize;
    }
}
