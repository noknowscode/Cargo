using Liveline.Helpers;
using SkiaSharp;

namespace Liveline.Rendering;

internal static class MomentumRenderer
{
    private const float DotRadius = 5f;

    /// <summary>Draws the live dot at the right edge, or at NOW in window mode.</summary>
    public static void Draw(
        SKCanvas canvas,
        float width, float height,
        double dotValue,
        double minY, double maxY,
        ChartPalette palette,
        RenderContext rc)
    {
        var (left, right, top, bottom) = GridRenderer.GetChartArea(width, height, rc);
        float chartHeight = bottom - top;
        double range = maxY - minY;
        if (range <= 0) range = 1;

        float dotX = float.IsNaN(rc.NowFraction) ? right : left + rc.NowFraction * (right - left);
        float dotY = (float)(bottom - (dotValue - minY) / range * chartHeight);
        dotY = Math.Clamp(dotY, top, bottom);

        rc.Fill.Style = SKPaintStyle.Fill;

        rc.Fill.Color = ColorHelper.WithAlpha(palette.LineColor, 50);
        canvas.DrawCircle(dotX, dotY, DotRadius + 4f, rc.Fill);

        rc.Fill.Color = palette.LineColor;
        canvas.DrawCircle(dotX, dotY, DotRadius, rc.Fill);

        rc.Fill.Color = ColorHelper.WithAlpha(palette.BadgeText, 120); // highlight in the line colour's On colour
        canvas.DrawCircle(dotX - 1.5f, dotY - 1.5f, 2f, rc.Fill);
    }
}
