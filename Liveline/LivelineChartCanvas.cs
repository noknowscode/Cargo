using Liveline.Animation;
using Liveline.Helpers;
using Liveline.Models;
using Liveline.Rendering;
using Microsoft.UI.Xaml;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Liveline;

/// <summary>Every input the canvas renders from, snapshotted from the chart's properties on the UI thread.</summary>
internal sealed record ChartState(
    IList<LivelinePoint>? Data,
    IList<LivelinePoint>? Forecast,
    double Value,
    LivelineTheme? Theme,
    bool ShowGrid, bool ShowBadge, bool ShowFill, bool ShowTrackingLine,
    object? Momentum,
    double LerpSpeed,
    bool IsPaused, bool IsLoading,
    DateTimeOffset WindowStart, DateTimeOffset WindowEnd, DateTimeOffset NowTime,
    double ForecastOpacity,
    SKColor? Background, SKColor? Line, SKColor? FillTop, SKColor? FillBottom, SKColor? LabelHalo,
    double StrokeThickness,
    Thickness PlotPadding,
    double ValueMin, double ValueMax,
    SKTypeface? Typeface,
    IList<LivelineMarker>? Markers);

public class LivelineChartCanvas : SKCanvasElement
{
    private readonly LerpEngine _lerp = new();
    private readonly RenderContext _rc = new();
    private ChartPalette _palette = ColorHelper.DerivePalette("#4CAF50", false);
    private SKColor _background;
    private SKColor? _labelHalo;
    private DateTimeOffset[] _times = [];
    private (LivelineMarker Marker, float Fraction)[] _markers = [];
    private double _currentValue;
    private double _previousValue;
    private bool _showGrid = true;
    private bool _showBadge = true;
    private bool _showFill = true;
    private bool _showTrackingLine = true;
    private double _forecastOpacity = 0.5;
    private MomentumDirection _momentumDirection = MomentumDirection.Off;
    private double _lerpSpeed = 0.04;
    private bool _isPaused;
    private bool _isLoading;
    private bool _hasData;
    private double _breathPhase;
    private const int BreathingPointCount = 60;

    internal void UpdateState(ChartState s)
    {
        _showGrid = s.ShowGrid;
        _showBadge = s.ShowBadge;
        _showFill = s.ShowFill;
        _showTrackingLine = s.ShowTrackingLine;
        _forecastOpacity = s.ForecastOpacity;
        _lerpSpeed = s.LerpSpeed;
        _isPaused = s.IsPaused;
        _isLoading = s.IsLoading;
        _labelHalo = s.LabelHalo;

        var derived = ColorHelper.DerivePalette(s.Theme?.Color ?? "#4CAF50", s.Theme?.IsDark ?? false);
        _palette = derived with
        {
            LineColor = s.Line ?? derived.LineColor,
            FillTop = s.FillTop ?? derived.FillTop,
            FillBottom = s.FillBottom ?? derived.FillBottom,
        };
        _background = s.Background ?? derived.Background;

        _rc.Stroke.StrokeWidth = (float)s.StrokeThickness;
        _rc.PadLeft = (float)s.PlotPadding.Left;
        _rc.PadTop = (float)s.PlotPadding.Top;
        _rc.PadRight = (float)s.PlotPadding.Right;
        _rc.PadBottom = (float)s.PlotPadding.Bottom;
        _rc.SetTypeface(s.Typeface);

        int dataCount = s.Data?.Count ?? 0;
        int count = dataCount + (s.Forecast?.Count ?? 0);
        bool hasWindow = s.WindowEnd > s.WindowStart;

        if (count > 0)
        {
            _previousValue = _currentValue;
            _currentValue = s.Value;

            var yValues = new double[count];
            var times = new DateTimeOffset[count];
            double minY = double.MaxValue, maxY = double.MinValue;

            for (int i = 0; i < count; i++)
            {
                var p = i < dataCount ? s.Data![i] : s.Forecast![i - dataCount];
                yValues[i] = p.Value;
                times[i] = p.Time;
                if (p.Value < minY) minY = p.Value;
                if (p.Value > maxY) maxY = p.Value;
            }

            if (!double.IsNaN(s.ValueMin) && !double.IsNaN(s.ValueMax) && s.ValueMax > s.ValueMin)
            {
                minY = s.ValueMin;
                maxY = s.ValueMax;
            }
            else
            {
                double padding = (maxY - minY) * 0.05;
                if (padding < 0.01) padding = 1;
                minY -= padding;
                maxY += padding;
            }

            _times = times;
            _lerp.SetTargets(yValues, minY, maxY, s.Value);
            _hasData = true;
        }

        // Window mode places x by time; the default spreads points evenly by index.
        var spanStart = hasWindow ? s.WindowStart : _times.Length > 0 ? _times[0] : default;
        var spanEnd = hasWindow ? s.WindowEnd : _times.Length > 0 ? _times[^1] : default;
        double spanTicks = (spanEnd - spanStart).Ticks;
        float Fraction(DateTimeOffset t) => (float)((t - spanStart).Ticks / spanTicks);

        if (hasWindow)
        {
            var fractions = new float[_times.Length];
            for (int i = 0; i < fractions.Length; i++)
                fractions[i] = Fraction(_times[i]);
            _rc.XFractions = fractions;
        }
        else
        {
            _rc.XFractions = null;
        }

        _rc.NowFraction = hasWindow && s.NowTime != DateTimeOffset.MinValue ? Fraction(s.NowTime) : float.NaN;

        _markers = s.Markers is { Count: > 0 } markers && spanTicks > 0
            ? markers.Select(m => (m, Fraction(m.Time))).ToArray()
            : [];

        _momentumDirection = MomentumHelper.Resolve(s.Momentum, _currentValue, _previousValue);

        if (!_hasData && _isLoading)
            _lerp.SeedFlatLine(BreathingPointCount, 0.0);

        Invalidate();
    }

    /// <summary>Advances one frame. Returns false once nothing moves, so the caller can stop pumping frames.</summary>
    public bool TickAnimation()
    {
        if (_isPaused) return false;

        if (!_hasData && _isLoading)
        {
            _breathPhase += 0.03;
            return true;
        }

        if (!_hasData) return false;

        return _lerp.Tick(_lerpSpeed);
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        float w = (float)area.Width;
        float h = (float)area.Height;

        if (_background.Alpha > 0)
            canvas.Clear(_background);

        if (!_hasData && _isLoading)
        {
            DrawBreathingLine(canvas, w, h);
            return;
        }

        if (_lerp.CurrentY.Length < 2)
        {
            DrawLoadingOrEmpty(canvas, w, h);
            return;
        }

        double minY = _lerp.CurrentMinY;
        double maxY = _lerp.CurrentMaxY;

        if (_showGrid)
            GridRenderer.Draw(canvas, w, h, minY, maxY, _times, _palette, _rc);

        double liveValue = LiveValue();

        if (_showTrackingLine)
            GridRenderer.DrawTrackingLine(canvas, w, h, liveValue, minY, maxY, _palette, _rc);

        bool clip = _rc.XFractions is not null;
        if (clip)
        {
            // Points outside the window stay out of the plot.
            var (left, right, _, _) = GridRenderer.GetChartArea(w, h, _rc);
            canvas.Save();
            canvas.ClipRect(new SKRect(left, 0, right, h));
        }

        LineRenderer.Draw(canvas, w, h, _lerp.CurrentY, minY, maxY, _showFill, _forecastOpacity, _palette, _rc);

        if (clip)
            canvas.Restore();

        if (_markers.Length > 0)
            MarkerRenderer.Draw(canvas, w, h, _markers, minY, maxY, _labelHalo, _rc);

        if (_momentumDirection != MomentumDirection.Off)
            MomentumRenderer.Draw(canvas, w, h, liveValue, minY, maxY, _palette, _rc);

        if (_showBadge)
            BadgeRenderer.Draw(canvas, w, h, _currentValue, _lerp.CurrentBadgeY, minY, maxY, _palette, _rc);

        if (_isLoading)
            DrawLoadingOrEmpty(canvas, w, h);
    }

    /// <summary>The line's value at NOW in window mode (interpolated between points), else its last point.</summary>
    private double LiveValue()
    {
        var y = _lerp.CurrentY;
        var f = _rc.XFractions;
        float now = _rc.NowFraction;

        if (f is null || float.IsNaN(now) || f.Length != y.Length)
            return y[^1];

        if (now <= f[0]) return y[0];
        for (int i = 1; i < f.Length; i++)
        {
            if (now <= f[i])
            {
                double t = (now - f[i - 1]) / Math.Max(f[i] - f[i - 1], float.Epsilon);
                return y[i - 1] + (y[i] - y[i - 1]) * t;
            }
        }
        return y[^1];
    }

    private void DrawBreathingLine(SKCanvas canvas, float w, float h)
    {
        var (left, right, top, bottom) = GridRenderer.GetChartArea(w, h, _rc);
        float chartHeight = bottom - top;
        float chartWidth = right - left;
        float centerY = top + chartHeight / 2f;

        float breathAmplitude = (float)(Math.Sin(_breathPhase) * 0.5 + 0.5);
        float waveHeight = 4f + breathAmplitude * 12f;
        byte alpha = (byte)(80 + breathAmplitude * 120);

        using var path = new SKPath();
        path.MoveTo(left, centerY);

        for (int i = 1; i < BreathingPointCount; i++)
        {
            float t = (float)i / (BreathingPointCount - 1);
            float x = left + t * chartWidth;
            float y = centerY + (float)Math.Sin(_breathPhase * 2.0 + t * Math.PI * 2.0) * waveHeight;

            float prevT = (float)(i - 1) / (BreathingPointCount - 1);
            float prevX = left + prevT * chartWidth;
            float prevY = centerY + (float)Math.Sin(_breathPhase * 2.0 + prevT * Math.PI * 2.0) * waveHeight;
            float cpOffset = (x - prevX) * 0.3f;

            path.CubicTo(prevX + cpOffset, prevY, x - cpOffset, y, x, y);
        }

        using var fillPath = new SKPath(path);
        fillPath.LineTo(right, bottom);
        fillPath.LineTo(left, bottom);
        fillPath.Close();

        using var gradient = SKShader.CreateLinearGradient(
            new SKPoint(0, centerY - waveHeight),
            new SKPoint(0, bottom),
            [ColorHelper.WithAlpha(_palette.LineColor, (byte)(alpha / 3)), ColorHelper.WithAlpha(_palette.LineColor, 0)],
            null,
            SKShaderTileMode.Clamp);

        _rc.Fill.Style = SKPaintStyle.Fill;
        _rc.Fill.Color = SKColors.White;
        _rc.Fill.Shader = gradient;
        canvas.DrawPath(fillPath, _rc.Fill);
        _rc.Fill.Shader = null;

        _rc.Stroke.Color = ColorHelper.WithAlpha(_palette.LineColor, alpha);
        canvas.DrawPath(path, _rc.Stroke);

        float dotX = left + chartWidth / 2f;
        float dotY = centerY + (float)Math.Sin(_breathPhase * 2.0 + Math.PI) * waveHeight;

        _rc.Fill.Color = ColorHelper.WithAlpha(_palette.LineColor, (byte)(30 + breathAmplitude * 50));
        canvas.DrawCircle(dotX, dotY, 8f, _rc.Fill);

        _rc.Fill.Color = ColorHelper.WithAlpha(_palette.LineColor, alpha);
        canvas.DrawCircle(dotX, dotY, 4f, _rc.Fill);
    }

    private void DrawLoadingOrEmpty(SKCanvas canvas, float w, float h)
    {
        _rc.Fill.Style = SKPaintStyle.Fill;
        _rc.Fill.Color = _palette.TextDim;

        string msg = _isLoading ? "Loading..." : "No data";
        canvas.DrawText(msg, w / 2, h / 2, SKTextAlign.Center, _rc.MessageFont, _rc.Fill);
    }
}
