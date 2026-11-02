using Cargo.Presentation;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>
/// The timeline's lanes on one time axis (EXTRACTION §2 "Today at North Quay"): vessels, moves and
/// cargo, the 3-hourly grid, the axis, the future shade and the NOW line. The tide lane is a
/// Liveline chart laid over its band. Everything is in the design's viewBox units: 580 tall, the
/// summary column 0-168 (XAML text over it), the plot from <see cref="PlotLeft"/> (168) to 12 short of
/// the right edge. On a phone the summary moves above the chart and the plot starts at 0.
/// </summary>
// xaml-lint: allow builtin - a bespoke canvas by decision (spec: four lanes share one axis, one NOW line and a future shade)
public sealed partial class TimelineLanes : SKCanvasElement
{
    public const double DesignHeight = 580;
    public const float PlotRightInset = 12;
    private const float PlotTop = 28, PlotBottom = 540;
    private const float TideTop = 164, MovesTop = 300, CargoTop = 428;
    private const float Baseline = 92, ArrivalDot = 84, DepartureDot = 100;

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKPaint _halo = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 4, StrokeJoin = SKStrokeJoin.Round };
    private readonly SKFont _label = new(SKTypeface.Default, 12);
    private readonly SKFont _mono = new(SKTypeface.Default, 12);
    private readonly SKFont _monoStrong = new(SKTypeface.Default, 13);
    private readonly SKPathBuilder _pb = new();

    // Palette from Themes/Tokens.xaml (the canvas has no XAML to bind), read again when the theme changes
    private SKColor _ink, _muted, _surface, _navy, _water, _accent, _ground;

    // The vessel dots as drawn, for hit-testing (r12 around each); swapped whole on each render
    private (string Id, SKPoint At)[] _targets = Array.Empty<(string, SKPoint)>();
    private string? _pointerOver;

    public TimelineLanes()
    {
        Height = DesignHeight;
        // Uno 7: SKCanvasElement is a Grid, and a Grid without a background is not hit-testable,
        // so the dot hover would fall through to whatever lies behind the lanes
        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
        ReadPalette();
        this.RebuildOnTheme(() =>
        {
            ReadPalette();
            Invalidate();
        });
        LoadFonts();
    }

    private void ReadPalette() =>
        (_ink, _muted, _surface, _navy, _water, _accent, _ground) = (Sk("InkColor"), Sk("TextMutedColor"), Sk("SurfaceColor"),
            Sk("NavyColor"), Sk("WaterColor"), Sk("AccentColor"), Sk("PaperColor"));

    public static readonly DependencyProperty PlotLeftProperty = DependencyProperty.Register(
        nameof(PlotLeft), typeof(double), typeof(TimelineLanes),
        new PropertyMetadata(168d, (d, e) => ((TimelineLanes)d).OnPlotLeftChanged((double)e.NewValue)));

    private float _plotLeft = 168;

    /// <summary>Where the time axis starts: 168 beside the summary column, 0 when the summary sits above the chart.</summary>
    public double PlotLeft
    {
        get => (double)GetValue(PlotLeftProperty);
        set => SetValue(PlotLeftProperty, value);
    }

    private void OnPlotLeftChanged(double value)
    {
        _plotLeft = (float)value;
        Invalidate();
    }

    public static readonly DependencyProperty HoverCommandProperty = DependencyProperty.Register(
        nameof(HoverCommand), typeof(ICommand), typeof(TimelineLanes), new PropertyMetadata(null));

    /// <summary>Runs with a vessel id when the pointer is over its dot, and with "-id" when it leaves (as HoverLink does).</summary>
    public ICommand? HoverCommand
    {
        get => (ICommand?)GetValue(HoverCommandProperty);
        set => SetValue(HoverCommandProperty, value);
    }

    public static readonly DependencyProperty LitProperty = DependencyProperty.Register(
        nameof(Lit), typeof(string), typeof(TimelineLanes), new PropertyMetadata(null, (d, _) => ((TimelineLanes)d).Invalidate()));

    /// <summary>The vessel lit anywhere on screen: its dot grows from 5 to 7 and its label inks.</summary>
    public string? Lit
    {
        get => (string?)GetValue(LitProperty);
        set => SetValue(LitProperty, value);
    }

    // xaml-lint: allow codebehind - hit-testing dots drawn on a Skia canvas; the canvas has no XAML elements to hover
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
        {
            return;
        }

        var p = e.GetCurrentPoint(this).Position;
        var hit = _targets.FirstOrDefault(t => Math.Abs(t.At.X - p.X) <= 12 && Math.Abs(t.At.Y - p.Y) <= 12).Id;
        if (hit != _pointerOver)
        {
            if (_pointerOver is not null)
            {
                HoverCommand?.Execute("-" + _pointerOver);
            }

            _pointerOver = hit;
            if (hit is not null)
            {
                HoverCommand?.Execute(hit);
            }
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerOver is not null)
        {
            HoverCommand?.Execute("-" + _pointerOver);
            _pointerOver = null;
        }
    }

    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(TimelineFrame), typeof(TimelineLanes),
        new PropertyMetadata(null, (d, _) => ((TimelineLanes)d).Invalidate()));

    /// <summary>The tick's snapshot: NOW, the events, the moves and cargo series. Immutable, read on the render thread.</summary>
    public TimelineFrame? Frame
    {
        get => (TimelineFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    private async void LoadFonts()
    {
        if (await CanvasFonts.Load(CanvasFonts.BodyMedium) is { } body)
        {
            _label.Typeface = body;
        }

        if (await CanvasFonts.Load(CanvasFonts.Mono) is { } mono)
        {
            _mono.Typeface = mono;
        }

        if (await CanvasFonts.Load(CanvasFonts.MonoMedium) is { } monoMedium)
        {
            _monoStrong.Typeface = monoMedium;
        }

        Invalidate();
    }

    private static SKColor Sk(string key)
    {
        var c = Tokens.Color(key);
        return new SKColor(c.R, c.G, c.B, c.A);
    }

    private static SKColor Alpha(SKColor c, double a) => c.WithAlpha((byte)Math.Round(255 * a));

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (Frame is not { } frame)
        {
            return;
        }

        var width = (float)area.Width;
        var right = width - PlotRightInset;
        var perHour = (right - _plotLeft) / (float)(TimelineViewModel.End - TimelineViewModel.Start);
        float X(double hour) => _plotLeft + (float)(hour - TimelineViewModel.Start) * perHour;
        var nowX = X(frame.Now);

        // Ahead of now: a 3% ink shade over every lane
        _fill.Color = Alpha(_ink, .03);
        canvas.DrawRect(new SKRect(nowX, PlotTop, right, PlotBottom), _fill);

        // Gridlines every 3 h; Thursday 00:00 stronger
        for (var h = (int)TimelineViewModel.Start; h <= (int)TimelineViewModel.End; h += 3)
        {
            _stroke.StrokeWidth = 1;
            _stroke.Color = Alpha(_ink, h == 24 ? .18 : .08);
            canvas.DrawLine(X(h), PlotTop, X(h), PlotBottom, _stroke);
        }

        // Lane dividers: the y = 300 rule between the water and the quay is the strong one
        foreach (var (y, a) in new[] { (TideTop, .08), (MovesTop, .18), (CargoTop, .08) })
        {
            _stroke.Color = Alpha(_ink, a);
            canvas.DrawLine(0, y, width, y, _stroke);
        }

        DrawVessels(canvas, frame, X, right);
        DrawMoves(canvas, frame, X, nowX);
        DrawCargo(canvas, frame, X, nowX);
        DrawAxis(canvas, X, nowX);
        DrawNow(canvas, frame, nowX);
    }

    private void DrawVessels(SKCanvas canvas, TimelineFrame frame, Func<double, float> x, float right)
    {
        _stroke.StrokeWidth = 1;
        _stroke.Color = Alpha(_ink, .18);
        canvas.DrawLine(_plotLeft, Baseline, right, Baseline, _stroke);

        // Labels stack into tiers away from the baseline so neighbours never collide
        var targets = new List<(string, SKPoint)>();
        var lit = Lit;
        var arrivalTiers = new List<float>();
        var departureTiers = new List<float>();
        foreach (var e in frame.Events)
        {
            var happened = e.Hour <= frame.Now;
            var cx = x(e.Hour);
            var cy = e.Arrival ? ArrivalDot : DepartureDot;
            var time = PortState.Format(e.Hour);
            var nameWidth = _label.MeasureText(e.Name);
            var labelWidth = nameWidth + 6 + _mono.MeasureText(time);
            var left = Math.Clamp(cx - labelWidth / 2, _plotLeft, right - labelWidth);

            var tiers = e.Arrival ? arrivalTiers : departureTiers;
            var tier = tiers.FindIndex(end => left > end + 10);
            if (tier < 0)
            {
                tier = tiers.Count;
                tiers.Add(0);
            }

            tiers[tier] = left + labelWidth;
            var labelY = e.Arrival ? cy - 16 - tier * 16 : cy + 24 + tier * 16;

            // Leader from the dot to the label
            _stroke.StrokeWidth = 1;
            _stroke.Color = Alpha(_ink, .18);
            canvas.DrawLine(cx, cy, cx, e.Arrival ? labelY + 4 : labelY - 12, _stroke);

            // Happened: filled navy with a surface ring; planned: hollow navy ring. Lit: 7 rather than 5.
            var on = e.Id == lit;
            var r = on ? 7f : 5f;
            _fill.Color = happened ? _navy : _surface;
            canvas.DrawCircle(cx, cy, r, _fill);
            _stroke.StrokeWidth = happened ? 2 : 1.6f;
            _stroke.Color = happened ? _surface : _navy;
            canvas.DrawCircle(cx, cy, happened ? r : r - .8f, _stroke);
            targets.Add((e.Id, new SKPoint(cx, cy)));

            Text(canvas, e.Name, left, labelY, _label, happened || on ? _ink : _muted);
            Text(canvas, time, left + nameWidth + 6, labelY, _mono, _muted);
        }

        _targets = targets.ToArray();
    }

    private void DrawMoves(SKCanvas canvas, TimelineFrame frame, Func<double, float> x, float nowX)
    {
        static float Y(double v) => 412 - .46f * (float)v;

        // The 100 / h reference
        _stroke.StrokeWidth = 1;
        _stroke.Color = Alpha(_ink, .08);
        canvas.DrawLine(_plotLeft, Y(100), nowX, Y(100), _stroke);
        Text(canvas, "100 / h", _plotLeft + 4, Y(100) - 5, _mono, _muted);

        // The line, then the same line closed down to zero for the area under it
        Smooth(frame.Moves.Select(m => new SKPoint(x(m.Hour), Y(m.Value))).ToArray());
        using var line = _pb.Snapshot();
        _pb.LineTo(nowX, Y(0));
        _pb.LineTo(x(frame.Moves[0].Hour), Y(0));
        _pb.Close();
        using (var area = _pb.Detach())
        using (var shader = SKShader.CreateLinearGradient(new SKPoint(0, MovesTop), new SKPoint(0, Y(0)),
                   new[] { Alpha(_accent, .2), Alpha(_accent, 0) }, null, SKShaderTileMode.Clamp))
        {
            _fill.Shader = shader;
            canvas.DrawPath(area, _fill);
            _fill.Shader = null;
        }

        _stroke.StrokeWidth = 2;
        _stroke.Color = _accent;
        canvas.DrawPath(line, _stroke);

        // The peak, hollow, with its value
        var (peakHour, peakValue) = frame.MovesPeak;
        _fill.Color = _surface;
        canvas.DrawCircle(x(peakHour), Y(peakValue), 3.5f, _fill);
        _stroke.StrokeWidth = 1.6f;
        canvas.DrawCircle(x(peakHour), Y(peakValue), 3.5f, _stroke);
        Text(canvas, $"{peakValue:0}", x(peakHour) - 8, Y(peakValue) - 8, _mono, _muted);

        // Now: the live rate
        var now = frame.Moves[^1];
        _fill.Color = _accent;
        canvas.DrawCircle(nowX, Y(now.Value), 5, _fill);
        _stroke.StrokeWidth = 2;
        _stroke.Color = _surface;
        canvas.DrawCircle(nowX, Y(now.Value), 5, _stroke);
        Text(canvas, $"{now.Value:0} / h", nowX + 10, Y(now.Value) + 4, _monoStrong, _ink);
    }

    private void DrawCargo(SKCanvas canvas, TimelineFrame frame, Func<double, float> x, float nowX)
    {
        // Fitted to the lane: the busiest hour (170 out) sits 6 px under the lane's top
        static float Y(double v) => 534 - .52f * (float)v;

        var inPoints = frame.Cargo.Select(c => new SKPoint(x(c.Hour), Y(c.In))).ToArray();
        var outPoints = frame.Cargo.Select(c => new SKPoint(x(c.Hour), Y(c.Out))).ToArray();

        // The band between in and out, in water at 16%
        Smooth(outPoints);
        Smooth(inPoints.Reverse().ToArray(), extend: true);
        _pb.Close();
        using (var band = _pb.Detach())
        {
            _fill.Color = Alpha(_water, .16);
            canvas.DrawPath(band, _fill);
        }

        _stroke.StrokeWidth = 2;
        Smooth(inPoints);
        using (var inLine = _pb.Detach())
        {
            _stroke.Color = _navy;
            canvas.DrawPath(inLine, _stroke);
        }

        Smooth(outPoints);
        using (var outLine = _pb.Detach())
        {
            _stroke.Color = _water;
            canvas.DrawPath(outLine, _stroke);
        }

        var now = frame.Cargo[^1];
        var outAbove = now.Out >= now.In;
        Text(canvas, $"out {now.Out:0}", nowX + 8, Y(now.Out) + (outAbove ? -2 : 12), _mono, _muted);
        Text(canvas, $"in {now.In:0}", nowX + 8, Y(now.In) + (outAbove ? 12 : -2), _mono, _muted);
    }

    private void DrawAxis(SKCanvas canvas, Func<double, float> x, float nowX)
    {
        var start = (int)TimelineViewModel.Start;
        var end = (int)TimelineViewModel.End;
        var labels = new List<(int Hour, float Left, float Width)>();
        for (var h = start; h <= end; h++)
        {
            var major = h % 3 == 0;
            _stroke.StrokeWidth = 1;
            _stroke.Color = Alpha(_ink, .18);
            canvas.DrawLine(x(h), PlotBottom, x(h), PlotBottom + (major ? 8 : 5), _stroke);

            // The label under NOW gives way to the NOW pill's line
            if (!major || (Math.Abs(x(h) - nowX) < 28 && h != start && h != end))
            {
                continue;
            }

            var w = (h == 24 ? _monoStrong : _mono).MeasureText(Label(h));
            labels.Add((h, h == start ? x(h) : h == end ? x(h) - w : x(h) - w / 2, w));
        }

        // A narrow plot cannot hold a label every 3 h: the ends and Thursday 00:00 come first, then each other hour
        // that clears its neighbours by 8 px
        var drawn = new List<(float Left, float Right)>();
        foreach (var (h, left, w) in labels.OrderBy(l => l.Hour == start || l.Hour == end || l.Hour == 24 ? 0 : 1))
        {
            if (drawn.Any(d => left < d.Right + 8 && left + w > d.Left - 8))
            {
                continue;
            }

            drawn.Add((left, left + w));
            Text(canvas, Label(h), left, 568, h == 24 ? _monoStrong : _mono, h == 24 ? _ink : _muted);
        }

        static string Label(int hour) => hour == 24 ? "Thu 00:00" : PortState.Format(hour);
    }

    private void DrawNow(SKCanvas canvas, TimelineFrame frame, float nowX)
    {
        _stroke.StrokeWidth = 1.25f;
        _stroke.Color = _ink;
        canvas.DrawLine(nowX, 20, nowX, 548, _stroke);

        var text = $"NOW {PortState.Format(frame.Now)}";
        var pill = SKRect.Create(nowX - 40, 0, 80, 22);
        _fill.Color = _ink;
        canvas.DrawRoundRect(pill, 11, 11, _fill);
        _fill.Color = _ground;
        var w = _monoStrong.MeasureText(text);
        canvas.DrawText(text, nowX - w / 2, 15.5f, SKTextAlign.Left, _monoStrong, _fill);
    }

    /// <summary>Text on a 4 px surface halo, so it reads over gridlines and areas.</summary>
    private void Text(SKCanvas canvas, string text, float x, float y, SKFont font, SKColor color)
    {
        _halo.Color = _surface;
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, _halo);
        _fill.Color = color;
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, _fill);
    }

    /// <summary>
    /// Adds a smooth line through the points to <see cref="_pb"/> (cubic, control points 30% out). With
    /// <paramref name="extend"/>, a straight segment joins it to the open contour instead of starting a new one.
    /// </summary>
    private void Smooth(IReadOnlyList<SKPoint> points, bool extend = false)
    {
        if (points.Count == 0)
        {
            return;
        }

        if (extend)
        {
            _pb.LineTo(points[0]);
        }
        else
        {
            _pb.MoveTo(points[0]);
        }

        for (var i = 1; i < points.Count; i++)
        {
            var (a, b) = (points[i - 1], points[i]);
            var dx = (b.X - a.X) * .3f;
            _pb.CubicTo(a.X + dx, a.Y, b.X - dx, b.Y, b.X, b.Y);
        }
    }
}
