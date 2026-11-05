using SkiaSharp;
using Windows.UI;

namespace Liveline.Models;

public enum MarkerLabelPlacement
{
    Right,
    Left,
    Above,
    Below
}

/// <summary>
/// A point drawn on the chart at a time and value, with an optional label.
/// Colours left null follow the chart's theme: <see cref="Fill"/> the line colour, <see cref="LabelColor"/> the
/// label colour. A hollow marker is a <see cref="Fill"/> of the background colour with a <see cref="Stroke"/> ring.
/// </summary>
/// <remarks>
/// Settable rather than init-only: the WinAppSDK XAML compiler writes type metadata, setters included, for the item
/// type of <c>Markers</c> (<c>IList&lt;LivelineMarker&gt;</c>), and an init-only setter fails there (CS8852). Nothing in
/// Liveline writes a marker after constructing it.
/// </remarks>
public sealed record LivelineMarker(DateTimeOffset Time, double Value)
{
    public DateTimeOffset Time { get; set; } = Time;
    public double Value { get; set; } = Value;
    public string? Label { get; set; }
    public float Radius { get; set; } = 4f;
    public Color? Fill { get; set; }

    /// <summary>Ring around the marker. Null draws none.</summary>
    public Color? Stroke { get; set; }
    public float StrokeThickness { get; set; } = 1.6f;
    public Color? LabelColor { get; set; }
    public float LabelSize { get; set; } = 12f;

    /// <summary>Overrides the chart's <c>Typeface</c> for this label (for example a mono face).</summary>
    public SKTypeface? LabelTypeface { get; set; }

    public MarkerLabelPlacement LabelPlacement { get; set; } = MarkerLabelPlacement.Right;
}
