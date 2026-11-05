using SkiaSharp;

namespace Liveline.Helpers;

public static class ColorHelper
{
    public static SKColor WithAlpha(SKColor color, byte alpha)
        => new(color.Red, color.Green, color.Blue, alpha);
}

/// <summary>The colours one frame draws with, resolved from the chart's theme-bound brushes.</summary>
public record ChartPalette(
    SKColor LineColor,
    SKColor FillTop,
    SKColor FillBottom,
    SKColor GridLine,
    SKColor TextDim,
    SKColor BadgeBg,
    SKColor BadgeText,
    SKColor Background)
{
    public static ChartPalette Empty { get; } = new(
        SKColors.Transparent, SKColors.Transparent, SKColors.Transparent, SKColors.Transparent,
        SKColors.Transparent, SKColors.Transparent, SKColors.Transparent, SKColors.Transparent);
}
