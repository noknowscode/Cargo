using Liveline.Helpers;
using SkiaSharp;

namespace Liveline.Rendering;

internal static class LineRenderer
{
    public static void Draw(
        SKCanvas canvas,
        float width, float height,
        double[] yValues,
        double minY, double maxY,
        bool showFill,
        double forecastOpacity,
        ChartPalette palette,
        RenderContext rc)
    {
        if (yValues.Length < 2) return;

        var (left, right, top, bottom) = GridRenderer.GetChartArea(width, height, rc);
        float chartHeight = bottom - top;
        float chartWidth = right - left;
        double range = maxY - minY;
        if (range <= 0) range = 1;

        int count = yValues.Length;
        float invRange = (float)(1.0 / range);

        var pb = rc.Path;
        float firstX = rc.X(0, count, left, chartWidth);
        float firstY = (float)(bottom - (yValues[0] - minY) / range * chartHeight);
        pb.MoveTo(firstX, firstY);

        float prevX = firstX, prevY = firstY;
        for (int i = 1; i < count; i++)
        {
            float x = rc.X(i, count, left, chartWidth);
            float y = bottom - ((float)yValues[i] - (float)minY) * invRange * chartHeight;
            float cpOffset = (x - prevX) * 0.3f;

            pb.CubicTo(prevX + cpOffset, prevY, x - cpOffset, y, x, y);
            prevX = x;
            prevY = y;
        }

        // The line, then the same line closed down to the baseline for the fill.
        using var path = pb.Snapshot();
        pb.LineTo(prevX, bottom);
        pb.LineTo(firstX, bottom);
        pb.Close();
        using var fillPath = pb.Detach();

        if (showFill)
        {
            using var gradient = SKShader.CreateLinearGradient(
                new SKPoint(0, top),
                new SKPoint(0, bottom),
                [palette.FillTop, palette.FillBottom],
                null,
                SKShaderTileMode.Clamp);

            rc.Fill.Color = SKColors.White;
            rc.Fill.Style = SKPaintStyle.Fill;
            rc.Fill.Shader = gradient;
            canvas.DrawPath(fillPath, rc.Fill);
            rc.Fill.Shader = null;
        }

        if (float.IsNaN(rc.NowFraction))
        {
            rc.Stroke.Color = palette.LineColor;
            canvas.DrawPath(path, rc.Stroke);
            return;
        }

        // Solid up to NOW, forecast opacity after it. One path, two clips, so the line has no seam.
        float nowX = left + rc.NowFraction * chartWidth;
        float pad = rc.Stroke.StrokeWidth;

        canvas.Save();
        canvas.ClipRect(new SKRect(left - pad, top - pad, nowX, bottom + pad));
        rc.Stroke.Color = palette.LineColor;
        canvas.DrawPath(path, rc.Stroke);
        canvas.Restore();

        canvas.Save();
        canvas.ClipRect(new SKRect(nowX, top - pad, right + pad, bottom + pad));
        rc.Stroke.Color = ColorHelper.WithAlpha(palette.LineColor, (byte)(palette.LineColor.Alpha * Math.Clamp(forecastOpacity, 0, 1)));
        canvas.DrawPath(path, rc.Stroke);
        canvas.Restore();
    }
}
