using Liveline.Models;
using SkiaSharp;

namespace Liveline.Rendering;

internal static class MarkerRenderer
{
    private const float LabelGap = 6f;
    private const float HaloWidth = 4f;

    public static void Draw(
        SKCanvas canvas,
        float width, float height,
        (LivelineMarker Marker, float Fraction)[] markers,
        double minY, double maxY,
        SKColor? halo,
        RenderContext rc)
    {
        var (left, right, top, bottom) = GridRenderer.GetChartArea(width, height, rc);
        float chartHeight = bottom - top;
        float chartWidth = right - left;
        double range = maxY - minY;
        if (range <= 0) range = 1;

        foreach (var (m, fraction) in markers)
        {
            float x = left + fraction * chartWidth;
            float y = (float)(bottom - (m.Value - minY) / range * chartHeight);

            rc.Fill.Style = SKPaintStyle.Fill;
            rc.Fill.Color = ToSk(m.Fill);
            canvas.DrawCircle(x, y, m.Radius, rc.Fill);

            if (m.Stroke.A > 0 && m.StrokeThickness > 0)
            {
                rc.ThinStroke.StrokeWidth = m.StrokeThickness;
                rc.ThinStroke.Color = ToSk(m.Stroke);
                canvas.DrawCircle(x, y, m.Radius, rc.ThinStroke);
                rc.ThinStroke.StrokeWidth = 1f;
            }

            if (string.IsNullOrEmpty(m.Label)) continue;

            var font = rc.MarkerFont;
            var chartTypeface = font.Typeface;
            if (m.LabelTypeface is not null) font.Typeface = m.LabelTypeface;
            font.Size = m.LabelSize;

            float textWidth = font.MeasureText(m.Label);
            font.GetFontMetrics(out var metrics);
            float centreToBaseline = -(metrics.Ascent + metrics.Descent) / 2f;
            float offset = m.Radius + LabelGap;

            var (tx, ty) = m.LabelPlacement switch
            {
                MarkerLabelPlacement.Left => (x - offset - textWidth, y + centreToBaseline),
                MarkerLabelPlacement.Above => (x - textWidth / 2f, y - offset - metrics.Descent),
                MarkerLabelPlacement.Below => (x - textWidth / 2f, y + offset - metrics.Ascent),
                _ => (x + offset, y + centreToBaseline),
            };

            if (halo is { } h)
            {
                rc.Halo.Color = h;
                rc.Halo.StrokeWidth = HaloWidth;
                canvas.DrawText(m.Label, tx, ty, SKTextAlign.Left, font, rc.Halo);
            }

            rc.Fill.Color = ToSk(m.LabelColor);
            canvas.DrawText(m.Label, tx, ty, SKTextAlign.Left, font, rc.Fill);

            font.Typeface = chartTypeface;
        }
    }

    private static SKColor ToSk(Windows.UI.Color c) => new(c.R, c.G, c.B, c.A);
}
