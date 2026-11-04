namespace Cargo.Presentation;

/// <summary>One movement in Next 6 h. Built once per membership change; the countdown ticks in place.</summary>
public sealed partial class NextSixRow : ObservableObject
{
    public required string Id { get; init; }
    public required double Hour { get; init; }
    public required string Time { get; init; }
    public required string Name { get; init; }
    public required string Sub { get; init; }
    public required bool Arrival { get; init; }
    public required double Length { get; init; }

    /// <summary>Opens the vessel; hovering or focusing the row lights it on the map and the chart.</summary>
    public required System.Windows.Input.ICommand Open { get; init; }
    public required System.Windows.Input.ICommand Hover { get; init; }

    /// <summary>The vessel is lit anywhere (a tag, a row, the chart): the linked-hover tint.</summary>
    [ObservableProperty] public partial bool IsLinked { get; set; }

    [ObservableProperty] public partial bool Happened { get; set; }
    [ObservableProperty] public partial string Countdown { get; set; } = string.Empty;

    /// <summary>Under an hour to go: the countdown reads in ink rather than muted.</summary>
    [ObservableProperty] public partial bool IsSoon { get; set; }
    [ObservableProperty] public partial bool Departing { get; set; }
}

/// <summary>
/// Next 6 h: the movements from 15 minutes ago to six hours ahead, each ship drawn to scale. The
/// window reaches back 15 minutes so a ship that has just left stays above the NOW row.
/// </summary>
public sealed partial class NextSixHoursViewModel : ObservableObject
{
    private const double Back = .25, Ahead = 6;

    private readonly PortState _state;
    private readonly IReadOnlyList<TimelineEvent> _events;
    private string _membership = string.Empty;

    public NextSixHoursViewModel(PortState state, IReadOnlyList<TimelineEvent> events)
    {
        _state = state;
        _events = events;
        state.HoverChanged += (_, _) => PaintHover();
        Refresh();
    }

    private void PaintHover()
    {
        foreach (var row in Past.Concat(Upcoming))
        {
            row.IsLinked = row.Id == _state.Stage.HoveredVessel;
        }
    }

    /// <summary>Movements that have happened inside the window, above the NOW row.</summary>
    public ObservableCollection<NextSixRow> Past { get; } = new();

    /// <summary>Movements to come, below the NOW row.</summary>
    public ObservableCollection<NextSixRow> Upcoming { get; } = new();

    [ObservableProperty] public partial string Sub { get; set; } = string.Empty;
    /// <summary>Nothing moves in the next six hours: the list says so.</summary>
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    public void Refresh()
    {
        var now = _state.NowHours;
        var inWindow = _events.Where(e => e.Hour >= now - Back && e.Hour <= now + Ahead).ToArray();
        // A row says which berth she arrives at, so an assignment changes the rows as much as the clock does
        var membership = string.Join("|", inWindow.Select(e => $"{e.Id}{e.Arrival}{e.Hour <= now}{_state.BerthOf(e.Id)}"));
        if (membership != _membership)
        {
            _membership = membership;
            Past.Clear();
            Upcoming.Clear();
            foreach (var e in inWindow)
            {
                (e.Hour <= now ? Past : Upcoming).Add(Row(e));
            }

            PaintHover();
        }

        foreach (var row in Past.Concat(Upcoming))
        {
            Tick(row);
        }

        var count = inWindow.Length;
        Sub = $"{ShiftClock.Format(now)} → {ShiftClock.Format(now + Ahead)} · {(count == 1 ? "1 movement" : $"{count} movements")} · ships to scale";
        IsEmpty = count == 0;
    }

    private NextSixRow Row(TimelineEvent e)
    {
        var vessel = PortData.Vessels.FirstOrDefault(v => v.Id == e.Id);
        var berth = vessel is null ? null : _state.BerthOf(vessel.Id);
        var sub = e.Arrival
            ? $"Arrives {(e.Hour >= 24 ? "Thu" : berth is null ? "today" : $"Berth {berth}")}"
              + (berth is null ? " · no berth yet" : string.Empty)
              + (vessel?.PilotAboard == true ? " · pilot aboard" : string.Empty)
            : $"Departs {(berth is null ? "the quay" : $"Berth {berth}")}";

        return new NextSixRow
        {
            Id = e.Id,
            Hour = e.Hour,
            Time = ShiftClock.Format(e.Hour),
            Name = e.Name,
            Sub = sub,
            Arrival = e.Arrival,
            Length = vessel?.Length ?? 250,
            Open = _state.OpenVesselCommand,
            Hover = _state.Stage.HoverCommand
        };
    }

    private void Tick(NextSixRow row)
    {
        row.Happened = row.Hour <= _state.NowHours;
        row.Departing = row.Happened && !row.Arrival;
        row.Countdown = row.Happened ? (row.Arrival ? "arrived" : "departing") : _state.Countdown(row.Hour);
        row.IsSoon = _state.MinutesUntil(row.Hour) < 60;
    }
}
