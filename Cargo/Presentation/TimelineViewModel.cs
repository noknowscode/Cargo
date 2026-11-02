using Cargo.Controls;
using Liveline.Models;
using SkiaSharp;

namespace Cargo.Presentation;

/// <summary>Everything the lanes canvas draws, snapshotted on the clock tick.</summary>
public sealed record TimelineFrame(
    double Now,
    IReadOnlyList<TimelineEvent> Events,
    IReadOnlyList<(double Hour, double Value)> Moves,
    IReadOnlyList<(double Hour, double In, double Out)> Cargo,
    (double Hour, double Value) MovesPeak);

/// <summary>One hour of the table view: the non-visual alternative to the chart.</summary>
public sealed partial class TimelineRow : ObservableObject
{
    public required double Hour { get; init; }
    public required string Time { get; init; }
    public required string Vessels { get; init; }
    public required string Tide { get; init; }

    [ObservableProperty] private string _moves = string.Empty;
    [ObservableProperty] private string _in = string.Empty;
    [ObservableProperty] private string _out = string.Empty;
    [ObservableProperty] private string _net = string.Empty;
    [ObservableProperty] private bool _isNow;
    [ObservableProperty] private bool _isFuture;
}

/// <summary>
/// "Today at North Quay": one time axis from Wednesday 06:00 to Thursday 06:00 shared by four
/// lanes (vessels, tide, moves, cargo). NOW, the future shade, filled-versus-hollow and every
/// figure in the summary column follow the clock; moves and cargo stop at NOW (no forecast),
/// the tide runs on as the tide-table forecast.
/// </summary>
public sealed partial class TimelineViewModel : ObservableObject
{
    /// <summary>The axis: Wednesday 06:00 to Thursday 06:00, in hours on the app clock (<see cref="ShiftTimeline"/>).</summary>
    public const double Start = ShiftTimeline.Start, End = ShiftTimeline.End;

    /// <summary>The tide lane's samples: every 15 minutes, fixed, so only the solid/forecast split moves.</summary>
    private const double TideStep = .25;

    /// <summary>Liveline places points by DateTimeOffset; the app clock's hour 0 is Wednesday 00:00.</summary>
    private static readonly DateTimeOffset Day = new(2026, 9, 2, 0, 0, 0, TimeSpan.Zero);

    private readonly PortState _state;
    private readonly LivelinePoint[] _tide;

    public TimelineViewModel(PortState state)
    {
        _state = state;
        Events = ShiftTimeline.Events();
        _tide = Enumerable.Range(0, (int)((End - Start) / TideStep) + 1)
            .Select(i => Start + i * TideStep)
            .Select(h => new LivelinePoint(At(h), Math.Round(PortData.TideAt(h), 2)))
            .ToArray();

        for (var h = (int)Start; h <= (int)End; h++)
        {
            Rows.Add(BuildRow(h));
        }

        ShowChart();
        Refresh();
        LoadFonts();
    }

    // ── Chart / Table ───────────────────────────────────────────────────

    [ObservableProperty] private Visibility _chartVisibility;
    [ObservableProperty] private Visibility _tableVisibility;
    [ObservableProperty] private int _viewIndex;
    [ObservableProperty] private Brush? _chartLabel;
    [ObservableProperty] private Brush? _tableLabel;

    [RelayCommand]
    private void ShowChart() => SetView(table: false);

    [RelayCommand]
    private void ShowTable() => SetView(table: true);

    private void SetView(bool table)
    {
        ViewIndex = table ? 1 : 0;
        ChartVisibility = table ? Visibility.Collapsed : Visibility.Visible;
        TableVisibility = table ? Visibility.Visible : Visibility.Collapsed;
        ChartLabel = Tokens.Brush(table ? "InkInvariantBrush" : "PaperInvariantBrush");
        TableLabel = Tokens.Brush(table ? "PaperInvariantBrush" : "InkInvariantBrush");
    }

    // ── Tide lane typefaces ─────────────────────────────────────────────

    private SKTypeface? _mono;

    /// <summary>The tide lane's label face (the "now" level); the HW label takes the mono face.</summary>
    [ObservableProperty] private SKTypeface? _tideTypeface;

    /// <summary>Liveline eases toward each push; under reduced motion it snaps.</summary>
    [ObservableProperty] private double _tideLerp = .04;

    private async void LoadFonts()
    {
        TideTypeface = await CanvasFonts.Load(CanvasFonts.BodyMedium);
        _mono = await CanvasFonts.Load(CanvasFonts.Mono);
        Refresh();
    }

    public IReadOnlyList<TimelineEvent> Events { get; }

    public ObservableCollection<TimelineRow> Rows { get; } = new();

    public DateTimeOffset WindowStart => At(Start);
    public DateTimeOffset WindowEnd => At(End);

    [ObservableProperty] private TimelineFrame? _frame;

    // Summary column
    [ObservableProperty] private string _vesselsValue = string.Empty;
    [ObservableProperty] private string _vesselsSub = string.Empty;
    [ObservableProperty] private string _tideValue = string.Empty;
    [ObservableProperty] private string _tideSub = string.Empty;
    [ObservableProperty] private string _movesValue = string.Empty;
    [ObservableProperty] private string _movesPeak = string.Empty;
    [ObservableProperty] private string _cargoValue = string.Empty;
    [ObservableProperty] private string _cargoSub = string.Empty;
    [ObservableProperty] private string _cargoSince = string.Empty;
    [ObservableProperty] private string _movesTotal = string.Empty;

    // Tide lane (Liveline)
    [ObservableProperty] private DateTimeOffset _nowTime;
    [ObservableProperty] private IList<LivelinePoint>? _tidePast;
    [ObservableProperty] private IList<LivelinePoint>? _tideAhead;
    [ObservableProperty] private IList<LivelineMarker>? _tideMarkers;

    /// <summary>The chart's spoken summary (its automation name); the table is the full alternative.</summary>
    [ObservableProperty] private string _summary = string.Empty;

    public static DateTimeOffset At(double hours) => Day.AddHours(hours);

    public void Refresh()
    {
        var now = Math.Clamp(_state.NowHours, Start, End);
        var hour = (int)Math.Floor(now);

        // Vessels: done, the one departing, the rest to come
        var done = Events.Count(e => e.Hour <= now);
        var departing = PortData.Vessels.Count(v => v.Status == "Departing");
        VesselsValue = $"{done} of {Events.Count} done";
        VesselsSub = $"{departing} departing · {Events.Count - done - departing} to come";

        // Tide: the level now, which way it is going, the next high water
        var level = PortData.TideAt(now);
        var rising = PortData.TideAt(now + .05) > level;
        var highWater = ShiftTimeline.NextHighWater(now);
        TideValue = $"{level:0.0} m";
        TideSub = $"{(rising ? "rising" : "falling")} · HW {PortState.Format(highWater)}";

        // Moves: per hour, 06:00 to now, its total and peak
        var moves = new List<(double, double)>();
        for (var h = (int)Start; h <= hour; h++)
        {
            moves.Add((h, ShiftTimeline.Volume(h)));
        }

        moves.Add((now, ShiftTimeline.Volume(hour)));
        var peak = ShiftTimeline.MovesPeak(now);
        var total = ShiftTimeline.MovesToday(now);
        MovesValue = $"{total:N0} today";
        MovesPeak = $"peak {peak.Value:0} / h at {PortState.Format(peak.Hour)}";
        MovesTotal = $"Totals {total:N0} moves";

        // Cargo: in and out per hour to now, and the net now
        var cargo = new List<(double, double, double)>();
        for (var h = (int)Start; h <= hour; h++)
        {
            cargo.Add((h, PortData.CargoIn[Math.Min(h, 24)], PortData.CargoOut[Math.Min(h, 24)]));
        }

        var cargoIn = PortData.CargoIn[Math.Min(hour, 24)];
        var cargoOut = PortData.CargoOut[Math.Min(hour, 24)];
        cargo.Add((now, cargoIn, cargoOut));
        var net = ShiftTimeline.NetCargo(hour);
        CargoValue = $"Net {(net < 0 ? "−" : "+")}{Math.Abs(net):0} / h";
        CargoSub = $"{cargoIn:0} in · {cargoOut:0} out per hour";
        CargoSince = net < 0 ? $"out ahead since {PortState.Format(ShiftTimeline.OutAheadSince(now))}" : "in ahead now";

        Frame = new TimelineFrame(now, Events, moves, cargo, peak);

        // Tide lane: the same samples every tick; only the split at NOW moves
        TideLerp = Motion.Reduced ? 1 : .04;
        var split = Array.FindLastIndex(_tide, p => p.Time <= At(now)) + 1;
        NowTime = At(now);
        TidePast = _tide[..split];
        TideAhead = _tide[split..];
        TideMarkers = new[]
        {
            new LivelineMarker(At(highWater), Math.Round(PortData.TideAt(highWater), 1))
            {
                Label = $"HW {PortState.Format(highWater)} · {PortData.TideAt(highWater):0.0} m",
                LabelTypeface = _mono,
                Fill = Tokens.Color("SurfaceColor"),
                Stroke = Tokens.Color("WaterColor"),
                LabelColor = Tokens.Color("TextMutedColor"),
                LabelPlacement = MarkerLabelPlacement.Above,
            },
            new LivelineMarker(At(now), level)
            {
                Label = $"{level:0.0} m",
                Fill = Tokens.Color("NavyColor"),
                Stroke = Tokens.Color("SurfaceColor"),
                StrokeThickness = 2,
                LabelColor = Tokens.Color("InkColor"),
            },
        };

        foreach (var row in Rows)
        {
            UpdateRow(row, now);
        }

        Summary = $"Today at North Quay, 06:00 to Thursday 06:00. Now {PortState.Format(now)}. " +
                  $"Vessels {VesselsValue}, {VesselsSub}. Tide {TideValue}, {TideSub}. " +
                  $"Moves {MovesValue}, {MovesPeak}. Cargo {CargoValue}, {CargoSub}.";
    }

    private TimelineRow BuildRow(int hour)
    {
        var inHour = Events.Where(e => e.Hour >= hour && e.Hour < hour + 1).ToArray();
        var level = PortData.TideAt(hour + .5);
        var turn = ShiftTimeline.TideTurn(hour);
        return new TimelineRow
        {
            Hour = hour,
            Time = hour == 24 ? "Thu 00:00" : PortState.Format(hour),
            Vessels = string.Join(", ", inHour.Select(e => $"{e.Name} {(e.Arrival ? "arrives" : "departs")} {PortState.Format(e.Hour)}")),
            Tide = $"{turn}{level:0.0}"
        };
    }

    private static void UpdateRow(TimelineRow row, double now)
    {
        var hour = (int)row.Hour;
        var known = hour <= now;
        row.IsNow = hour == (int)Math.Floor(now);
        row.IsFuture = hour > now;
        row.Moves = known ? $"{PortData.Volume[Math.Min(hour, 24)]:0}" : "—";
        row.In = known ? $"{PortData.CargoIn[Math.Min(hour, 24)]:0}" : "—";
        row.Out = known ? $"{PortData.CargoOut[Math.Min(hour, 24)]:0}" : "—";
        var net = PortData.CargoIn[Math.Min(hour, 24)] - PortData.CargoOut[Math.Min(hour, 24)];
        row.Net = known ? $"{(net < 0 ? "−" : "+")}{Math.Abs(net):0}" : "—";
    }
}
