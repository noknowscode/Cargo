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
/// A hollow marker is a <see cref="Fill"/> of the background colour with a <see cref="Stroke"/> ring.
/// </summary>
public sealed record LivelineMarker(DateTimeOffset Time, double Value)
{
    public string? Label { get; init; }
    public float Radius { get; init; } = 4f;
    public Color Fill { get; init; } = Color.FromArgb(255, 255, 255, 255);
    public Color Stroke { get; init; } = Color.FromArgb(0, 0, 0, 0);
    public float StrokeThickness { get; init; } = 1.6f;
    public Color LabelColor { get; init; } = Color.FromArgb(200, 0, 0, 0);
    public float LabelSize { get; init; } = 12f;

    /// <summary>Overrides the chart's <c>Typeface</c> for this label (for example a mono face).</summary>
    public SKTypeface? LabelTypeface { get; init; }

    public MarkerLabelPlacement LabelPlacement { get; init; } = MarkerLabelPlacement.Right;
}
