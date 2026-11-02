using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>One row of the berth timeline: occupancy blocks placed by fraction of the window.</summary>
public sealed partial class ProportionalPanel : Panel
{
    /// <summary>
    /// A block: its label, where it starts and how wide it is (fractions of the window), its fill and label ink, and
    /// an optional edge, dashed or solid. The edge carries what the colour does (a reservation, a past berth, a
    /// conflict) so the timeline does not say it in colour alone; fills are opaque so the label keeps its contrast.
    /// Colours are palette keys (Themes/Tokens.xaml), resolved here, so a view model describes a block without
    /// brushes; no fill is transparent.
    /// </summary>
    public sealed record Segment(string Label, double Start, double Width, string? Fill, string Ink, string? Edge = null, bool Dashed = false);

    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IReadOnlyList<Segment>), typeof(ProportionalPanel),
        new PropertyMetadata(null, (d, _) => ((ProportionalPanel)d).Rebuild()));

    public IReadOnlyList<Segment>? Segments
    {
        get => (IReadOnlyList<Segment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public static readonly DependencyProperty NowFractionProperty = DependencyProperty.Register(
        nameof(NowFraction), typeof(double), typeof(ProportionalPanel),
        new PropertyMetadata(0d, (d, _) => ((ProportionalPanel)d).InvalidateArrange()));

    public double NowFraction
    {
        get => (double)GetValue(NowFractionProperty);
        set => SetValue(NowFractionProperty, value);
    }

    private void Rebuild()
    {
        Children.Clear();
        if (Segments is null)
        {
            return;
        }

        foreach (var segment in Segments)
        {
            var block = new Grid();
            block.Children.Add(new Rectangle
            {
                RadiusX = 7,
                RadiusY = 7,
                Fill = segment.Fill is { } fill ? Tokens.Brush(fill) : Tokens.Transparent,
                Stroke = segment.Edge is { } edge ? Tokens.Brush(edge) : null,
                StrokeThickness = segment.Edge is null ? 0 : 1.5,
                StrokeDashArray = segment.Dashed ? Draw.Dash(4, 3) : null
            });
            block.Children.Add(new TextBlock
            {
                Text = segment.Label,
                Margin = new Thickness(9, 0, 9, 0),
                FontFamily = (FontFamily)Application.Current.Resources["BodyMediumFont"],
                FontSize = 11,
                Foreground = Tokens.Brush(segment.Ink),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Children.Add(block);
        }

        Children.Add(new Rectangle { Width = 1.5, Fill = Tokens.Brush("InkBrush") });
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        return new Size(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 26 : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var segments = Segments ?? Array.Empty<Segment>();

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            Children[i].Arrange(new Rect(
                finalSize.Width * segment.Start,
                0,
                Math.Max(0, finalSize.Width * segment.Width),
                finalSize.Height));
        }

        if (Children.Count > segments.Count)
        {
            Children[^1].Arrange(new Rect(finalSize.Width * NowFraction, -4, 1.5, finalSize.Height + 8));
        }

        return finalSize;
    }
}
