using static Cargo.Domain.DebugHooks;

namespace Cargo.Domain;

/// <summary>
/// The port's store: the live clock that drives the countdowns, the berth desk (the berth plan, the decisions waiting
/// on the dispatcher, and the vessel, berth, bay and badge being worked), and the signals every view follows. Where
/// the dispatcher is (<see cref="Navigation"/>), the harbour stage (<see cref="Stage"/>), the scanner
/// (<see cref="Scanner"/>), the yard (<see cref="Yard"/>) and the crew channel (<see cref="Chat"/>) are its parts.
/// </summary>
public sealed partial class PortState : ObservableObject
{
    private readonly IClock _clock;
    private readonly DateTimeOffset _started;
    private readonly BerthPlan _plan;
    private readonly PortSignals _signals = new();

    /// <summary>
    /// The store on a clock (the app passes <see cref="DispatcherClock"/>, the tests a manual one), on the starting
    /// berth plan unless one is given; <paramref name="random"/> picks the yard's moves.
    /// </summary>
    public PortState(IClock clock, BerthPlan? plan = null, Random? random = null)
    {
        _clock = clock;
        _started = clock.Now;
        _plan = plan ?? new BerthPlan();

        Navigation = new NavigationState(_signals);
        Stage = new StageState(Navigation, _signals);
        Scanner = new ScannerState(_signals);
        Yard = new YardState(clock, _signals, random);
        Chat = new CrewChat(_signals, () => NowHours);

#if DEBUG
        if (Hook("CARGO_OPTIMIZE") == "1")
        {
            Optimize();
        }
#endif

        RebuildDecisions();
        clock.Tick += (_, _) => Tick();
    }

    /// <summary>The section on screen, Security's tab, and the jumps between them.</summary>
    public NavigationState Navigation { get; }

    /// <summary>The harbour stage the sections share: its size, layer, selection and hover.</summary>
    public StageState Stage { get; }

    /// <summary>The container explorer's filter and pick, and the scanner's camera.</summary>
    public ScannerState Scanner { get; }

    /// <summary>The yard at work and the stack picked on its map.</summary>
    public YardState Yard { get; }

    /// <summary>The river crews' channel.</summary>
    public CrewChat Chat { get; }

    /// <summary>Raised once a second, after the clock advances.</summary>
    public event EventHandler? Ticked;

    /// <inheritdoc cref="PortSignals.StructureChanged"/>
    public event EventHandler? StructureChanged
    {
        add => _signals.StructureChanged += value;
        remove => _signals.StructureChanged -= value;
    }

    /// <inheritdoc cref="PortSignals.HoverChanged"/>
    public event EventHandler? HoverChanged
    {
        add => _signals.HoverChanged += value;
        remove => _signals.HoverChanged -= value;
    }

    /// <inheritdoc cref="PortSignals.PlanHourChanged"/>
    public event EventHandler? PlanHourChanged
    {
        add => _signals.PlanHourChanged += value;
        remove => _signals.PlanHourChanged -= value;
    }

    /// <inheritdoc cref="PortSignals.YardChanged"/>
    public event EventHandler? YardChanged
    {
        add => _signals.YardChanged += value;
        remove => _signals.YardChanged -= value;
    }

    private void Tick()
    {
        Yard.Work();
        OnPropertyChanged(nameof(Now));
        OnPropertyChanged(nameof(NowMinutes));
        OnPropertyChanged(nameof(NowSeconds));
        OnPropertyChanged(nameof(NowHours));
        TickDecisions();
        Ticked?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyStructureChanged() => _signals.Structure();

    // ── Clock ─────────────────────────────────────────────────────────────────

    public double NowHours => PortData.NowHours + (_clock.Now - _started).TotalHours;

    public double ElapsedSeconds => (_clock.Now - _started).TotalSeconds;

    public string Now => ShiftClock.FormatWithSeconds(NowHours);

    /// <summary>The header clock's rolling part, "20:58".</summary>
    public string NowMinutes => Now[..5];

    /// <summary>The header clock's plain part, ":34".</summary>
    public string NowSeconds => Now[5..];

    /// <summary>
    /// Whole minutes from now to <paramref name="targetHour"/>, rounded up so a countdown turns over on the minute
    /// (and a target exactly 42 minutes away reads 42, not 43); zero or less once it is due.
    /// </summary>
    public int MinutesUntil(double targetHour) => (int)Math.Ceiling((targetHour - NowHours) * 60 - 1e-9);

    /// <summary>The countdown the harbour tags and Next 6 h show: "in 42 min", "in 3 h 02", "now" once due.</summary>
    public string Countdown(double targetHour) => ShiftClock.Countdown(MinutesUntil(targetHour));

    // ── Picks that land on Berths ─────────────────────────────────────────────

    [RelayCommand]
    private void OpenVessel(string vesselId)
    {
        SelectedVesselId = vesselId;
        SelectedBay = null;
        Navigation.Section = "berths";
    }

    [RelayCommand]
    private void OpenDocking()
    {
        DockSelection = SelectedVesselId;
        Navigation.Section = "berths";
    }

    /// <summary>Clicking a hull means different things depending on which screen is up.</summary>
    [RelayCommand]
    private void PickHullVessel(string vesselId)
    {
        switch (Navigation.Section)
        {
            case "berths":
                SelectedVesselId = vesselId;
                SelectedBay = null;
                break;
            case "security":
                OpenVessel(vesselId);
                break;
            default:
                Stage.Selection = Stage.Selection == vesselId ? null : vesselId;
                break;
        }
    }

    // ── Vessels ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    public partial string SelectedVesselId { get; set; } = "aurora";

    partial void OnSelectedVesselIdChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedVessel));
        _signals.Structure();
    }

    public Vessel SelectedVessel => PortData.Vessel(SelectedVesselId);

    /// <summary>A berth picked in the Berths status list with no vessel on it; null when a vessel is picked.</summary>
    [ObservableProperty]
    public partial string? SelectedBerth { get; set; }

    partial void OnSelectedBerthChanged(string? value) => _signals.Structure();

    /// <summary>Picks a vessel on the Berths page (the harbour frames it), clearing any berth pick.</summary>
    public void PickVessel(string vesselId)
    {
        SelectedBerth = null;
        SelectedVesselId = vesselId;
    }

    [ObservableProperty]
    public partial int? SelectedBay { get; set; }

    partial void OnSelectedBayChanged(int? value) => _signals.Structure();

    [ObservableProperty]
    public partial int? HoveredBay { get; set; }

    partial void OnHoveredBayChanged(int? value) => _signals.Hover();

    /// <summary>The badge picked on Access control, whose zones the map shows.</summary>
    [ObservableProperty]
    public partial int SelectedPerson { get; set; }

    partial void OnSelectedPersonChanged(int value) => _signals.Structure();

    // ── Berth assignment ──────────────────────────────────────────────────────

    public string? BerthOf(string vesselId) => _plan.BerthOf(vesselId);

    public bool IsAssigned(string vesselId) => _plan.IsAssigned(vesselId);

    public Vessel? OccupantAt(string berth, double hour) => PortData.Vessels.FirstOrDefault(v =>
        BerthOf(v.Id) == berth && PortData.Schedule[v.Id].Start <= hour && PortData.Schedule[v.Id].End > hour);

    public Vessel? IncomingAt(string berth, double hour) => PortData.Vessels.FirstOrDefault(v =>
        BerthOf(v.Id) == berth && PortData.Schedule[v.Id].Start > hour);

    public IReadOnlyList<(string Berth, string A, string B)> Conflicts() => _plan.Conflicts();

    public bool Fits(Vessel vessel, int berthIndex) => _plan.Fits(vessel, berthIndex);

    /// <summary>The berths that can take the vessel now, by the plan's rules (<see cref="BerthPlan.BerthsThatFit"/>).</summary>
    public IReadOnlyList<string> BerthsThatFit(Vessel vessel) => _plan.BerthsThatFit(vessel);

    /// <summary>Returns the message to show in the map hint strip.</summary>
    public string? Assign(string vesselId, int berthIndex)
    {
        var vessel = PortData.Vessel(vesselId);
        var berth = PortData.Berths[berthIndex];

        if (_plan.TryAssign(vesselId, berthIndex))
        {
            OnPlanChanged();
            return $"{vessel.Name} assigned to Berth {berth.Number} · pilots and mooring gang notified.";
        }

        return _plan.Refusal(vessel, berthIndex);
    }

    /// <summary>Whether the berth can take the vessel, and if not, which rule refuses her (the drag and the berth list say so).</summary>
    public BerthFit Check(Vessel vessel, int berthIndex) => _plan.Check(vessel, berthIndex);

    public void Unassign(string vesselId)
    {
        _plan.Unassign(vesselId);
        OnPlanChanged();
    }

    // ── Decisions ─────────────────────────────────────────────────────────────

    private readonly HashSet<string> _confirmed = new();

    /// <summary>
    /// The arrival waiting on the dispatcher: a vessel still arriving at a reserved berth whose
    /// berth nobody has confirmed. In the demo data that is Nordic Star to berth 07.
    /// </summary>
    public string? PendingDecision => PortData.Vessels
        .Where(v => v.Status == VesselStatus.Arriving && !_confirmed.Contains(v.Id))
        .FirstOrDefault(v => BerthOf(v.Id) is { } berth && PortData.Berths.Any(b => b.Number == berth && b.State == BerthState.Reserved))
        ?.Id;

    public bool NeedsDecision => PendingDecision is not null;

    /// <summary>
    /// Whether a section holds something waiting on the dispatcher, for the header's needs-you
    /// dots: Berths the unconfirmed arrival, Cargo the late orders, Security the inspection hold
    /// and the driver check.
    /// </summary>
    public bool SectionNeedsYou(string section) => Decisions.Any(d => d.Kind switch
    {
        DecisionKind.BerthConfirm => section == "berths",
        DecisionKind.LateOrders => section == "cargo",
        _ => section == "security"
    });

    /// <summary>What needs you, counted as the queue lists it.</summary>
    public int NeedsYouCount => Decisions.Count;

    // ── Needs-you queue ─────────────────────────────────────────────────────

    /// <summary>The physical inspection on the held container is booked for 21:15.</summary>
    private const double InspectionDue = 21.25;

    /// <summary>Everything waiting on the dispatcher: the hold, the driver check, the late orders, the berth.</summary>
    public ObservableCollection<Decision> Decisions { get; } = new();

    /// <summary>
    /// Rebuilds the queue from the data and the berth decision, in the design's order. Called at
    /// start and when what needs deciding changes; the clock only updates the rows' timings.
    /// </summary>
    private void RebuildDecisions()
    {
        Decisions.Clear();
        _queuedBerthDecision = BerthDecision();

        if (PortData.Containers.FirstOrDefault(c => c.Security == "Hold") is { } held)
        {
            Decisions.Add(new Decision
            {
                Id = $"hold-{held.Id}", Kind = DecisionKind.InspectionHold,
                Title = $"Hold on {held.DisplayId}", Sub = $"Inspection due {ShiftClock.Format(InspectionDue)}",
                ActionLabel = "Open inspection", IsPrimary = true, Command = Navigation.OpenInspectionCommand, Subject = held.DisplayId
            });
        }

        if (PortData.Vehicles.FirstOrDefault(v => v.Authorization == "Awaiting driver ID") is { } truck)
        {
            Decisions.Add(new Decision
            {
                Id = $"driver-{truck.Id}", Kind = DecisionKind.DriverCheck,
                Title = $"Driver ID pending at {truck.Gate.Split(" · ")[0]}", Sub = $"{truck.Id} at the barrier",
                ActionLabel = "Verify driver", IsPrimary = false, Command = Navigation.OpenAccessCommand, Subject = truck.Id
            });
        }

        var late = PortData.RiverFleet.Count(v => v.SlipMinutes > 0);
        if (late > 0)
        {
            Decisions.Add(new Decision
            {
                Id = "late-orders", Kind = DecisionKind.LateOrders,
                Title = late == 1 ? "1 order late" : $"{late} orders late", Sub = late == 1 ? "In the yard queue" : "Both in the yard queue",
                ActionLabel = "View orders", IsPrimary = false, Command = Navigation.GoCommand, CommandParameter = "fleet"
            });
        }

        if (PendingDecision is { } vessel)
        {
            var v = PortData.Vessel(vessel);
            Decisions.Add(new Decision
            {
                Id = $"berth-{vessel}", Kind = DecisionKind.BerthConfirm,
                Title = $"{v.Name} needs berth {BerthOf(vessel)} confirmed", Sub = "Pilot aboard",
                ActionLabel = "Confirm berth", IsPrimary = false, Command = ConfirmDecisionCommand,
                CommandParameter = vessel, VesselId = vessel
            });
        }

        TickDecisions();
        OnPropertyChanged(nameof(NeedsYouCount));
    }

    /// <summary>Updates each row's timing in place: due or overdue, waiting since, arriving in.</summary>
    private void TickDecisions()
    {
        foreach (var d in Decisions)
        {
            switch (d.Kind)
            {
                case DecisionKind.InspectionHold:
                    var due = (int)Math.Round((InspectionDue - NowHours) * 60);
                    d.IsLate = due < 0;
                    d.When = due < 0 ? $"{-due} min late" : $"in {due} min";
                    break;
                case DecisionKind.DriverCheck:
                    var since = ShiftClock.ClockHours(PortData.Vehicles.First(v => v.Authorization == "Awaiting driver ID").Entry);
                    var waiting = double.IsNaN(since) ? 0 : (int)Math.Round((NowHours - since) * 60);
                    d.IsLate = true;
                    d.When = $"waiting {Math.Max(0, waiting)} min";
                    break;
                case DecisionKind.BerthConfirm:
                    var eta = PortData.Vessel(d.VesselId!).Eta;
                    d.When = $"arrives {eta[^5..]}";
                    break;
            }
        }
    }

    /// <summary>
    /// Confirms the berth from the queue: the row leaves the list, the berth and harbour follow. A berth that cannot
    /// take her says why on the row, as the needs-you bar does.
    /// </summary>
    [RelayCommand]
    private void ConfirmDecision(string vesselId)
    {
        if (Confirm(vesselId) is { } refusal && Decisions.FirstOrDefault(d => d.VesselId == vesselId) is { } row)
        {
            row.Refusal = refusal;
        }
    }

    public bool IsConfirmed(string vesselId) => _confirmed.Contains(vesselId);

    /// <summary>
    /// Confirms the berth the vessel is waiting on (<see cref="PendingDecision"/>) as a real assignment, so the
    /// planner, the conflict check and the harbour all follow. Returns null on success, or why it cannot: she has no
    /// berth waiting to be confirmed (it moved, or it is confirmed already), or the berth cannot take her.
    /// </summary>
    public string? Confirm(string vesselId)
    {
        var vessel = PortData.Vessel(vesselId);
        if (PendingDecision != vesselId || BerthIndex(BerthOf(vesselId)) is not { } index)
        {
            return $"{vessel.Name} has no berth waiting to be confirmed.";
        }

        if (!_plan.TryAssign(vesselId, index))
        {
            return _plan.Refusal(vessel, index);
        }

        _confirmed.Add(vesselId);
        OnPlanChanged();
        return null;
    }

    /// <summary>Reverses <see cref="Confirm"/>: the berth goes back to reserved and the decision is pending again.</summary>
    public void UndoConfirm(string vesselId)
    {
        if (_confirmed.Remove(vesselId))
        {
            _plan.Unassign(vesselId);
            OnPlanChanged();
        }
    }

    private static int? BerthIndex(string? number) =>
        PortData.Berths.Select(b => b.Number).ToList().IndexOf(number ?? string.Empty) is >= 0 and var index ? index : null;

    // The berth decision the queue was last built for: the vessel waiting and the berth she waits on
    private (string? Vessel, string? Berth) _queuedBerthDecision;

    private (string? Vessel, string? Berth) BerthDecision() =>
        PendingDecision is { } vessel ? (vessel, BerthOf(vessel)) : (null, null);

    /// <summary>
    /// Every change to the berth plan ends here (an assignment, a release, the optimiser, a confirm, an undo). Which
    /// arrival waits on a berth is read from the plan, so the queue, the masthead count, the header's dot, the
    /// harbour marker and the needs-you bar are told whenever it changes: a drag that moves the arrival off her
    /// reserved berth settles the decision everywhere, and a move back raises it again.
    /// </summary>
    private void OnPlanChanged()
    {
        if (BerthDecision() != _queuedBerthDecision)
        {
            // The queue first: the header's dots read it when NeedsDecision changes
            RebuildDecisions();
            OnPropertyChanged(nameof(PendingDecision));
            OnPropertyChanged(nameof(NeedsDecision));
        }

        _signals.Structure();
    }

    /// <summary>Runs the optimiser (<see cref="BerthPlan.Optimize"/>) and returns what it did, including the vessels it could not place.</summary>
    public string Optimize()
    {
        var result = _plan.Optimize();
        if (result.Moved.Count > 0)
        {
            OnPlanChanged();
        }

        return result.Summary;
    }

    // ── Docking planner ───────────────────────────────────────────────────────

    [ObservableProperty]
    public partial string? DockSelection { get; set; } = Hook("CARGO_DOCK_SELECT");

    partial void OnDockSelectionChanged(string? value) => _signals.Structure();

    /// <summary>Null means "follow the live clock".</summary>
    [ObservableProperty]
    public partial double? PlanHour { get; set; }

    partial void OnPlanHourChanged(double? value) => _signals.PlanHour();

    public double EffectivePlanHour => PlanHour ?? NowHours;
}
