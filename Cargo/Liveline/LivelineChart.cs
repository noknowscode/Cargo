using Liveline.Models;
using SkiaSharp;

namespace Liveline;

/// <summary>
/// Real-time animated line chart. Frames are driven by <see cref="CompositionTarget.Rendering"/>
/// only while the line is still easing toward its targets; a settled chart costs no frames.
/// Its colours come from the app's theme: the default style (Themes/Generic.xaml) sets every brush to a
/// palette role with <c>{ThemeResource}</c>, so the chart follows Light, Dark and any palette with no wiring.
/// Set a brush property to override one.
/// </summary>
[TemplatePart(Name = CanvasPartName, Type = typeof(LivelineChartCanvas))]
public partial class LivelineChart : Control
{
    private const string CanvasPartName = "PART_Canvas";

    private LivelineChartCanvas? _canvas;
    private bool _isLoaded;
    private bool _animationAttached;

    public LivelineChart()
    {
        DefaultStyleKey = typeof(LivelineChart);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _canvas = GetTemplateChild(CanvasPartName) as LivelineChartCanvas;
        PushState();
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(IList<LivelinePoint>), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public IList<LivelinePoint>? Data
    {
        get => (IList<LivelinePoint>?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(LivelineChart),
            new PropertyMetadata(0.0, OnRenderPropertyChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Draws the value and time axis labels.</summary>
    public static readonly DependencyProperty ShowGridProperty =
        DependencyProperty.Register(nameof(ShowGrid), typeof(bool), typeof(LivelineChart),
            new PropertyMetadata(true, OnRenderPropertyChanged));

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public static readonly DependencyProperty ShowBadgeProperty =
        DependencyProperty.Register(nameof(ShowBadge), typeof(bool), typeof(LivelineChart),
            new PropertyMetadata(true, OnRenderPropertyChanged));

    public bool ShowBadge
    {
        get => (bool)GetValue(ShowBadgeProperty);
        set => SetValue(ShowBadgeProperty, value);
    }

    public static readonly DependencyProperty ShowFillProperty =
        DependencyProperty.Register(nameof(ShowFill), typeof(bool), typeof(LivelineChart),
            new PropertyMetadata(true, OnRenderPropertyChanged));

    public bool ShowFill
    {
        get => (bool)GetValue(ShowFillProperty);
        set => SetValue(ShowFillProperty, value);
    }

    /// <summary>The dashed horizontal line at the live value.</summary>
    public static readonly DependencyProperty ShowTrackingLineProperty =
        DependencyProperty.Register(nameof(ShowTrackingLine), typeof(bool), typeof(LivelineChart),
            new PropertyMetadata(true, OnRenderPropertyChanged));

    public bool ShowTrackingLine
    {
        get => (bool)GetValue(ShowTrackingLineProperty);
        set => SetValue(ShowTrackingLineProperty, value);
    }

    /// <summary>
    /// Controls the momentum indicator on the live dot.
    /// <c>true</c> = auto-detect direction, <c>false</c> = off,
    /// or <c>"up"</c>/<c>"down"</c>/<c>"flat"</c> to force.
    /// </summary>
    public static readonly DependencyProperty MomentumProperty =
        DependencyProperty.Register(nameof(Momentum), typeof(object), typeof(LivelineChart),
            new PropertyMetadata(true, OnRenderPropertyChanged));

    public object? Momentum
    {
        get => GetValue(MomentumProperty);
        set => SetValue(MomentumProperty, value);
    }

    /// <summary>Fraction of the remaining distance covered per frame. 1 snaps (reduced motion).</summary>
    public static readonly DependencyProperty LerpSpeedProperty =
        DependencyProperty.Register(nameof(LerpSpeed), typeof(double), typeof(LivelineChart),
            new PropertyMetadata(0.04, OnRenderPropertyChanged));

    public double LerpSpeed
    {
        get => (double)GetValue(LerpSpeedProperty);
        set => SetValue(LerpSpeedProperty, value);
    }

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(LivelineChart),
            new PropertyMetadata(false, OnRenderPropertyChanged));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public static readonly DependencyProperty IsPausedProperty =
        DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(LivelineChart),
            new PropertyMetadata(false, OnRenderPropertyChanged));

    public bool IsPaused
    {
        get => (bool)GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    /// <summary>
    /// Start of a fixed time window. When <see cref="WindowEnd"/> is after it, points are placed by
    /// time across the plot instead of evenly by index.
    /// </summary>
    public static readonly DependencyProperty WindowStartProperty =
        DependencyProperty.Register(nameof(WindowStart), typeof(DateTimeOffset), typeof(LivelineChart),
            new PropertyMetadata(DateTimeOffset.MinValue, OnRenderPropertyChanged));

    public DateTimeOffset WindowStart
    {
        get => (DateTimeOffset)GetValue(WindowStartProperty);
        set => SetValue(WindowStartProperty, value);
    }

    public static readonly DependencyProperty WindowEndProperty =
        DependencyProperty.Register(nameof(WindowEnd), typeof(DateTimeOffset), typeof(LivelineChart),
            new PropertyMetadata(DateTimeOffset.MinValue, OnRenderPropertyChanged));

    public DateTimeOffset WindowEnd
    {
        get => (DateTimeOffset)GetValue(WindowEndProperty);
        set => SetValue(WindowEndProperty, value);
    }

    /// <summary>
    /// In window mode, the line is solid up to this time and drawn at <see cref="ForecastOpacity"/>
    /// after it; the live dot sits here.
    /// </summary>
    public static readonly DependencyProperty NowTimeProperty =
        DependencyProperty.Register(nameof(NowTime), typeof(DateTimeOffset), typeof(LivelineChart),
            new PropertyMetadata(DateTimeOffset.MinValue, OnRenderPropertyChanged));

    public DateTimeOffset NowTime
    {
        get => (DateTimeOffset)GetValue(NowTimeProperty);
        set => SetValue(NowTimeProperty, value);
    }

    /// <summary>Points after <see cref="Data"/>, appended to the same line (for example a tide-table forecast).</summary>
    public static readonly DependencyProperty ForecastProperty =
        DependencyProperty.Register(nameof(Forecast), typeof(IList<LivelinePoint>), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public IList<LivelinePoint>? Forecast
    {
        get => (IList<LivelinePoint>?)GetValue(ForecastProperty);
        set => SetValue(ForecastProperty, value);
    }

    public static readonly DependencyProperty ForecastOpacityProperty =
        DependencyProperty.Register(nameof(ForecastOpacity), typeof(double), typeof(LivelineChart),
            new PropertyMetadata(0.5, OnRenderPropertyChanged));

    public double ForecastOpacity
    {
        get => (double)GetValue(ForecastOpacityProperty);
        set => SetValue(ForecastOpacityProperty, value);
    }

    /// <summary>Fill behind the plot. The default style sets <c>SurfaceBrush</c>; a transparent brush draws nothing.</summary>
    public static readonly DependencyProperty ChartBackgroundProperty =
        DependencyProperty.Register(nameof(ChartBackground), typeof(Brush), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public Brush? ChartBackground
    {
        get => (Brush?)GetValue(ChartBackgroundProperty);
        set => SetValue(ChartBackgroundProperty, value);
    }

    /// <summary>Line, badge and live dot colour. The default style sets <c>PrimaryBrush</c>.</summary>
    public static readonly DependencyProperty LineBrushProperty =
        DependencyProperty.Register(nameof(LineBrush), typeof(Brush), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public Brush? LineBrush
    {
        get => (Brush?)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    /// <summary>Top stop of the area gradient. Null uses the line colour, faded.</summary>
    public static readonly DependencyProperty FillTopBrushProperty =
        DependencyProperty.Register(nameof(FillTopBrush), typeof(Brush), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public Brush? FillTopBrush
    {
        get => (Brush?)GetValue(FillTopBrushProperty);
        set => SetValue(FillTopBrushProperty, value);
    }

    /// <summary>Bottom stop of the area gradient. Null uses the line colour, fully transparent.</summary>
    public static readonly DependencyProperty FillBottomBrushProperty =
        DependencyProperty.Register(nameof(FillBottomBrush), typeof(Brush), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public Brush? FillBottomBrush
    {
        get => (Brush?)GetValue(FillBottomBrushProperty);
        set => SetValue(FillBottomBrushProperty, value);
    }

    /// <summary>Stroke drawn behind marker labels so they read over the line. The default style sets <c>SurfaceBrush</c>; null draws none.</summary>
    public static readonly DependencyProperty LabelHaloBrushProperty =
        DependencyProperty.Register(nameof(LabelHaloBrush), typeof(Brush), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public Brush? LabelHaloBrush
    {
        get => (Brush?)GetValue(LabelHaloBrushProperty);
        set => SetValue(LabelHaloBrushProperty, value);
    }

    /// <summary>Grid lines. The default style sets <c>OutlineVariantBrush</c>.</summary>
    public static readonly DependencyProperty GridLineBrushProperty =
        DependencyProperty.Register(nameof(GridLineBrush), typeof(Brush), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public Brush? GridLineBrush
    {
        get => (Brush?)GetValue(GridLineBrushProperty);
        set => SetValue(GridLineBrushProperty, value);
    }

    /// <summary>Axis labels, marker labels and the loading / no-data message. The default style sets <c>OnSurfaceVariantBrush</c>.</summary>
    public static readonly DependencyProperty LabelBrushProperty =
        DependencyProperty.Register(nameof(LabelBrush), typeof(Brush), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public Brush? LabelBrush
    {
        get => (Brush?)GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    /// <summary>The value badge's text and the live dot's highlight, on the line colour. The default style sets <c>OnPrimaryBrush</c>, the pair of <c>PrimaryBrush</c>; set both together.</summary>
    public static readonly DependencyProperty BadgeTextBrushProperty =
        DependencyProperty.Register(nameof(BadgeTextBrush), typeof(Brush), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public Brush? BadgeTextBrush
    {
        get => (Brush?)GetValue(BadgeTextBrushProperty);
        set => SetValue(BadgeTextBrushProperty, value);
    }

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(LivelineChart),
            new PropertyMetadata(2.5, OnRenderPropertyChanged));

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Space between the control's edges and the plot. The right side holds the value labels and badge.</summary>
    public static readonly DependencyProperty PlotPaddingProperty =
        DependencyProperty.Register(nameof(PlotPadding), typeof(Thickness), typeof(LivelineChart),
            new PropertyMetadata(new Thickness(16, 16, 80, 32), OnRenderPropertyChanged));

    public Thickness PlotPadding
    {
        get => (Thickness)GetValue(PlotPaddingProperty);
        set => SetValue(PlotPaddingProperty, value);
    }

    /// <summary>Fixed bottom of the value axis. With <see cref="ValueMax"/> set, replaces auto-fit.</summary>
    public static readonly DependencyProperty ValueMinProperty =
        DependencyProperty.Register(nameof(ValueMin), typeof(double), typeof(LivelineChart),
            new PropertyMetadata(double.NaN, OnRenderPropertyChanged));

    public double ValueMin
    {
        get => (double)GetValue(ValueMinProperty);
        set => SetValue(ValueMinProperty, value);
    }

    public static readonly DependencyProperty ValueMaxProperty =
        DependencyProperty.Register(nameof(ValueMax), typeof(double), typeof(LivelineChart),
            new PropertyMetadata(double.NaN, OnRenderPropertyChanged));

    public double ValueMax
    {
        get => (double)GetValue(ValueMaxProperty);
        set => SetValue(ValueMaxProperty, value);
    }

    /// <summary>Typeface for every label. Null uses Segoe UI.</summary>
    public static readonly DependencyProperty TypefaceProperty =
        DependencyProperty.Register(nameof(Typeface), typeof(SKTypeface), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public SKTypeface? Typeface
    {
        get => (SKTypeface?)GetValue(TypefaceProperty);
        set => SetValue(TypefaceProperty, value);
    }

    public static readonly DependencyProperty MarkersProperty =
        DependencyProperty.Register(nameof(Markers), typeof(IList<LivelineMarker>), typeof(LivelineChart),
            new PropertyMetadata(null, OnRenderPropertyChanged));

    public IList<LivelineMarker>? Markers
    {
        get => (IList<LivelineMarker>?)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LivelineChart chart)
            chart.PushState();
    }

    private void PushState()
    {
        if (_canvas is null)
            return;

        _canvas.UpdateState(new ChartState(
            Data, Forecast, Value,
            ShowGrid, ShowBadge, ShowFill, ShowTrackingLine,
            Momentum,
            LerpSpeed, IsPaused, IsLoading,
            WindowStart, WindowEnd, NowTime,
            ForecastOpacity,
            ToColor(ChartBackground), ToColor(LineBrush), ToColor(FillTopBrush), ToColor(FillBottomBrush), ToColor(LabelHaloBrush),
            ToColor(GridLineBrush), ToColor(LabelBrush), ToColor(BadgeTextBrush),
            StrokeThickness,
            PlotPadding,
            ValueMin, ValueMax,
            Typeface,
            Markers));

        AttachAnimation();
    }

    private void AttachAnimation()
    {
        if (_isLoaded && !_animationAttached && _canvas is not null)
        {
            CompositionTarget.Rendering += OnCompositionRendering;
            _animationAttached = true;
        }
    }

    private void DetachAnimation()
    {
        if (_animationAttached)
        {
            CompositionTarget.Rendering -= OnCompositionRendering;
            _animationAttached = false;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        PushState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        DetachAnimation();
    }

    private void OnCompositionRendering(object? sender, object e)
    {
        // A held Rendering subscription makes the compositor produce frames at display refresh,
        // so let go as soon as the line settles. The next PushState re-attaches.
        if (_canvas is not null && _canvas.TickAnimation())
            _canvas.Invalidate();
        else
            DetachAnimation();
    }

    private static SKColor? ToColor(Brush? brush)
        => brush is SolidColorBrush b
            ? new SKColor(b.Color.R, b.Color.G, b.Color.B, (byte)Math.Round(b.Color.A * Math.Clamp(b.Opacity, 0, 1)))
            : null;
}
