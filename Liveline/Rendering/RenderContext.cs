using SkiaSharp;

namespace Liveline.Rendering;

internal sealed class RenderContext : IDisposable
{
    private static readonly SKTypeface DefaultTypeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal);
    private static readonly SKTypeface DefaultBoldTypeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold);

    public readonly SKPaint Fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    public readonly SKPaint Stroke = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2.5f,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round
    };
    public readonly SKPaint ThinStroke = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f
    };
    public readonly SKPaint Halo = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeJoin = SKStrokeJoin.Round
    };
    public readonly SKFont LabelFont = new(DefaultTypeface, 11f);
    public readonly SKFont BadgeFont = new(DefaultBoldTypeface, 12f);
    public readonly SKFont MessageFont = new(DefaultTypeface, 14f);
    public readonly SKFont MarkerFont = new(DefaultTypeface, 12f);

    // Plot padding: left, top, right, bottom.
    public float PadLeft = 16f, PadTop = 16f, PadRight = 80f, PadBottom = 32f;

    // Window mode: each point's x as a 0..1 fraction of the plot width, and NOW's fraction (NaN = none).
    public float[]? XFractions;
    public float NowFraction = float.NaN;

    public void SetTypeface(SKTypeface? typeface)
    {
        LabelFont.Typeface = typeface ?? DefaultTypeface;
        BadgeFont.Typeface = typeface ?? DefaultBoldTypeface;
        MessageFont.Typeface = typeface ?? DefaultTypeface;
        MarkerFont.Typeface = typeface ?? DefaultTypeface;
    }

    public float X(int index, int count, float left, float width)
        => XFractions is { } f && index < f.Length
            ? left + f[index] * width
            : left + (count > 1 ? (float)index / (count - 1) : 0f) * width;

    public void Dispose()
    {
        Fill.Dispose();
        Stroke.Dispose();
        ThinStroke.Dispose();
        Halo.Dispose();
        LabelFont.Dispose();
        BadgeFont.Dispose();
        MessageFont.Dispose();
        MarkerFont.Dispose();
    }
}
