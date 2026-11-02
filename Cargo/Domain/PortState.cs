using System.Globalization;

namespace Cargo.Domain;

/// <summary>
/// Everything the harbour knows about itself right now: which section is on screen,
/// what is selected where, which vessels have been given a berth, and the live clock
/// that drives the countdowns and the crane counters.
/// </summary>
public sealed partial class PortState : ObservableObject
{
    private readonly IClock _clock;
    private readonly DateTimeOffset _started;
    private readonly BerthPlan _plan;
    private readonly Dictionary<string, List<ChatLine>> _sentMessages = new();

    private DateTimeOffset _lastYardShuffle;
    private readonly Dictionary<string, int> _yardDelta = new();
    private readonly Dictionary<string, DateTimeOffset> _yardRecent = new();

    /// <summary>
    /// The store on a clock (the app passes <see cref="DispatcherClock"/>, the tests a manual one), on the starting
    /// berth plan unless one is given.
    /// </summary>
    public PortState(IClock clock, BerthPlan? plan = null)
    {
        _clock = clock;
        _started = clock.Now;
        _plan = plan ?? new BerthPlan();

#if DEBUG
        if (Hook("CARGO_OPTIMIZE") == "1")
        {
            Optimize();
        }
#endif

        RebuildDecisions();
        YardOccupancy = Occupancy();
        clock.Tick += (_, _) => Tick();
    }

    // DEBUG verification hooks (CARGO_*). They let a headless run open straight onto a section, a selection or a
    // hover, which Windows will not let a background process reach with synthesized input. The properties they
    // start take them as initializers: an initializer sets the backing field without running the setter, so the
    // change hooks stay quiet, as the constructor's field writes did before the properties became partial.
    // Release never reads the environment.
#if DEBUG
    private static string? Hook(string name) => Environment.GetEnvironmentVariable(name);
#else
    private static string? Hook(string name) => null;
#endif

    private static string? StartSection() =>
        Hook("CARGO_START_SECTION") is { Length: > 0 } requested
        && Canonical(requested) is var start
        && PortData.Sections.Any(s => s.Id == start)
            ? start
            : null;

    /// <summary>Raised once a second, after the clock advances.</summary>
    public event EventHandler? Ticked;

    /// <summary>Raised when something structural changed (selection, assignment, filter).</summary>
    public event EventHandler? StructureChanged;

    /// <summary>
    /// Raised when only the pointer moved over something. Hover changes fire far more often
    /// than anything else and never change what is on screen, only how one element is lit,
    /// so they are kept off <see cref="StructureChanged"/> and repaint in place instead.
    /// </summary>
    public event EventHandler? HoverChanged;

    /// <summary>
    /// Raised when the plan scrubber moves. It steps every quarter hour while it is dragged, and only the berth plan
    /// and its map show the plan hour, so it stays off <see cref="StructureChanged"/>: on that signal a scrub
    /// re-recorded the harbour and rebuilt the vessel profile and every Berths panel at each step.
    /// </summary>
    public event EventHandler? PlanHourChanged;

    /// <summary>
    /// Raised when the yard works a stack. It fires on its own every few seconds whether or
    /// not anyone is looking at the yard, so it stays off <see cref="StructureChanged"/>:
    /// one slot changing height is no reason to rebuild the harbour and every other screen.
    /// </summary>
    public event EventHandler? YardChanged;

    private void Tick()
    {
        ShuffleYard();
        OnPropertyChanged(nameof(Now));
        OnPropertyChanged(nameof(NowMinutes));
        OnPropertyChanged(nameof(NowSeconds));
        OnPropertyChanged(nameof(NowHours));
        TickDecisions();
        Ticked?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyStructureChanged() => StructureChanged?.Invoke(this, EventArgs.Empty);

    private void NotifyHoverChanged() => HoverChanged?.Invoke(this, EventArgs.Empty);

    private void NotifyYardChanged() => YardChanged?.Invoke(this, EventArgs.Empty);

    // ── Clock ─────────────────────────────────────────────────────────────────

    public double NowHours => PortData.NowHours + (_clock.Now - _started).TotalHours;

    public double ElapsedSeconds => (_clock.Now - _started).TotalSeconds;

    public string Now => FormatWithSeconds(NowHours);

    /// <summary>The header clock's rolling part, "20:58".</summary>
    public string NowMinutes => Now[..5];

    /// <summary>The header clock's plain part, ":34".</summary>
    public string NowSeconds => Now[5..];

    /// <summary>Container moves in the hour so far: the hour's planned volume scaled by how
    /// far into it we are, with a little crane-to-crane jitter.</summary>
    public static int MovesAt(double hours)
    {
        var hour = (int)Math.Floor(hours);
        var fraction = hours - hour;
        var moves = (int)Math.Round(PortData.Volume[Math.Clamp(hour, 0, 24)] * fraction + Math.Sin(hours * 97) * 3);
        return Math.Max(0, moves);
    }

    /// <summary>
    /// A stamp from the data on the app clock, in hours from the demo's Wednesday midnight: "Today 21:40" is 21.67,
    /// "Thu 02:30" 26.5, "Fri 10:00" 58, "Tue 23:05" -0.92, and a bare "21:40" is today. NaN when it cannot be read.
    /// The one reader of the data's times: the berth schedule (<see cref="PortData.Schedule"/>), the countdowns, the
    /// harbour tags and the shift timeline all go through it.
    /// </summary>
    public static double ClockHours(string when)
    {
        var parts = when.Split(' ');
        int? day = parts.Length switch
        {
            1 => 0,
            2 => parts[0] switch { "Tue" => -1, "Today" or "Wed" => 0, "Thu" => 1, "Fri" => 2, _ => null },
            _ => null
        };

        return day is { } d && TimeSpan.TryParseExact(parts[^1], @"hh\:mm", CultureInfo.InvariantCulture, out var time)
            ? d * 24 + time.TotalHours
            : double.NaN;
    }

    public static string Format(double hours)
    {
        var s = (int)Math.Round(hours * 3600) % 86400;
        if (s < 0)
        {
            s += 86400;
        }

        return $"{s / 3600:D2}:{s % 3600 / 60:D2}";
    }

    public static string FormatWithSeconds(double hours)
    {
        var s = (int)Math.Round(hours * 3600) % 86400;
        if (s < 0)
        {
            s += 86400;
        }

        return $"{s / 3600:D2}:{s % 3600 / 60:D2}:{s % 60:D2}";
    }

    /// <summary>
    /// Whole minutes from now to <paramref name="targetHour"/>, rounded up so a countdown turns over on the minute
    /// (and a target exactly 42 minutes away reads 42, not 43); zero or less once it is due.
    /// </summary>
    public int MinutesUntil(double targetHour) => (int)Math.Ceiling((targetHour - NowHours) * 60 - 1e-9);

    /// <summary>The countdown the harbour tags and Next 6 h show: "in 42 min", "in 3 h 02", "now" once due.</summary>
    public string Countdown(double targetHour) => MinutesUntil(targetHour) switch
    {
        <= 0 => "now",
        < 60 and var minutes => $"in {minutes} min",
        var minutes => $"in {minutes / 60} h {minutes % 60:D2}"
    };

    // ── Navigation ────────────────────────────────────────────────────────────

    [ObservableProperty]
    public partial string Section { get; set; } = StartSection() ?? "overview";

    partial void OnSectionChanged(string value)
    {
        StageOpen = false;
        OverviewCondensed = false;
        OnPropertyChanged(nameof(ShowStage));
        OnPropertyChanged(nameof(StageFull));
        OnPropertyChanged(nameof(StageCompact));
        OnPropertyChanged(nameof(ShowStageToggle));
        OnPropertyChanged(nameof(LayerSwitchShown));
        OnPropertyChanged(nameof(MastheadShown));
        OnStageToggleChanged();
        NotifyStructureChanged();
    }

    [RelayCommand]
    private void Go(string section) => Section = Canonical(section);

    /// <summary>
    /// Maps a retired section id onto the page that now holds it (Vessels and Docking live on
    /// Berths, Containers and Yard on Cargo, Activity on Overview), so a DEBUG start hook or an
    /// old link still lands in the right place.
    /// </summary>
    public static string Canonical(string section) => section switch
    {
        "vessels" or "docking" => "berths",
        "containers" or "yard" => "cargo",
        "activity" => "overview",
        _ => section
    };

    [RelayCommand]
    private void OpenVessel(string vesselId)
    {
        SelectedVesselId = vesselId;
        SelectedBay = null;
        Section = "berths";
    }

    [RelayCommand]
    private void OpenDocking()
    {
        DockSelection = SelectedVesselId;
        Section = "berths";
    }

    [RelayCommand]
    private void OpenInspection()
    {
        SecurityTab = "inspection";
        Section = "security";
    }

    [RelayCommand]
    private void OpenSecurity()
    {
        SecurityTab = "zones";
        Section = "security";
    }

    // ── Shared harbour stage ──────────────────────────────────────────────────

    public bool ShowStage => Section is "overview" or "berths" or "cargo" or "security";

    public bool StageFull => Section == "overview" || StageOpen;

    public bool StageCompact => ShowStage && !StageFull;

    [ObservableProperty]
    public partial bool StageOpen { get; set; }

    partial void OnStageOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(StageFull));
        OnPropertyChanged(nameof(LayerSwitchShown));
        OnPropertyChanged(nameof(StageCompact));
        OnStageToggleChanged();
        NotifyStructureChanged();
    }

    /// <summary>Overview's harbour band at its expanded height (491 rather than 340).</summary>
    [ObservableProperty]
    public partial bool OverviewExpanded { get; set; }

    partial void OnOverviewExpandedChanged(bool value) => OnStageToggleChanged();

    /// <summary>
    /// Overview scrolled: the masthead folds and the band snaps to its 180 px mini (SPEC-OVERVIEW-SCROLL).
    /// Set by the page from its scroll offset; the band's own height (340 or 491) comes back when it clears.
    /// </summary>
    [ObservableProperty]
    public partial bool OverviewCondensed { get; set; }

    partial void OnOverviewCondensedChanged(bool value)
    {
        OnStageToggleChanged();
        OnPropertyChanged(nameof(MastheadShown));
        OnPropertyChanged(nameof(LayerSwitchShown));
    }

    /// <summary>Asks the page to scroll Overview back to its top, which restores the full band.</summary>
    public event EventHandler? ScrollToTopRequested;

    public bool MastheadShown => Section == "overview" && !OverviewCondensed;

    /// <summary>The layer switcher floats on the full band only: the mini band has no room for the HUD.</summary>
    public bool LayerSwitchShown => StageFull && !(Section == "overview" && OverviewCondensed);

    /// <summary>
    /// Overview's harbour band: 340, or 491 expanded (the design), or the 180 mini while scrolled. NaN elsewhere:
    /// the stage sizes itself.
    /// </summary>
    public double StageHeight => Section == "overview" ? (OverviewCondensed ? 180 : OverviewExpanded ? 491 : 340) : double.NaN;

    /// <summary>Whether the toggle would collapse: Overview's band is expanded, or another section's strip is open.</summary>
    public bool StageExpanded => Section == "overview" ? OverviewExpanded && !OverviewCondensed : StageOpen;

    public bool StageCollapsed => !StageExpanded;

    public string StageToggleLabel => Section == "overview"
        ? (OverviewCondensed ? "Show harbour" : OverviewExpanded ? "Collapse" : "Expand")
        : (StageOpen ? "Collapse harbour" : "Expand harbour");

    public bool ShowStageToggle => ShowStage;

    /// <summary>Overview grows its band; the other harbour sections open their strip to the full stage.</summary>
    [RelayCommand]
    private void ToggleStage()
    {
        if (Section == "overview" && OverviewCondensed)
        {
            ScrollToTopRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (Section == "overview")
        {
            OverviewExpanded = !OverviewExpanded;
        }
        else
        {
            StageOpen = !StageOpen;
        }
    }

    private void OnStageToggleChanged()
    {
        OnPropertyChanged(nameof(StageHeight));
        OnPropertyChanged(nameof(StageExpanded));
        OnPropertyChanged(nameof(StageCollapsed));
        OnPropertyChanged(nameof(StageToggleLabel));
    }

    [ObservableProperty]
    public partial string HarbourLayer { get; set; } = Hook("CARGO_LAYER") ?? "port";

    partial void OnHarbourLayerChanged(string value) => NotifyStructureChanged();

    [RelayCommand]
    private void SetLayer(string layer) => HarbourLayer = layer;

    [ObservableProperty]
    public partial string? HarbourSelection { get; set; }

    partial void OnHarbourSelectionChanged(string? value) => NotifyStructureChanged();

    [ObservableProperty]
    public partial int? HoveredBerth { get; set; } = int.TryParse(Hook("CARGO_HOVER_BERTH"), out var berth) ? berth : null;

    partial void OnHoveredBerthChanged(int? value) => NotifyHoverChanged();

    /// <summary>The vessel a list row, tag or the needs-you bar is pointing at. Repaints, never rebuilds.</summary>
    [ObservableProperty]
    public partial string? HoveredVessel { get; set; }

    partial void OnHoveredVesselChanged(string? value) => NotifyHoverChanged();

    /// <summary>
    /// Linked hover from a list row, a harbour tag or the needs-you bar. The key is a vessel id or
    /// a berth number; "-key" ends that hover, and only if it is still the current one, since the
    /// next row's enter can arrive before the last row's leave.
    /// </summary>
    [RelayCommand]
    private void Hover(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (key[0] == '-')
        {
            if (HoverKey == key[1..])
            {
                HoveredVessel = null;
                HoveredBerth = null;
            }

            return;
        }

        var berth = PortData.Berths.Select(b => b.Number).ToList().IndexOf(key);
        HoveredVessel = berth < 0 ? key : null;
        HoveredBerth = berth < 0 ? null : berth;
    }

    private string? HoverKey => HoveredVessel ?? (HoveredBerth is { } i ? PortData.Berths[i].Number : null);

    /// <summary>Clicking a hull means different things depending on which screen is up.</summary>
    [RelayCommand]
    private void PickHullVessel(string vesselId)
    {
        switch (Section)
        {
            case "berths":
                SelectedVesselId = vesselId;
                SelectedBay = null;
                break;
            case "security":
                OpenVessel(vesselId);
                break;
            default:
                HarbourSelection = HarbourSelection == vesselId ? null : vesselId;
                break;
        }
    }

    // ── Vessels ───────────────────────────────────────────────────────────────

    [ObservableProperty]
    public partial string SelectedVesselId { get; set; } = "aurora";

    partial void OnSelectedVesselIdChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedVessel));
        NotifyStructureChanged();
    }

    public Vessel SelectedVessel => PortData.Vessel(SelectedVesselId);

    /// <summary>A berth picked in the Berths status list with no vessel on it; null when a vessel is picked.</summary>
    [ObservableProperty]
    public partial string? SelectedBerth { get; set; }

    partial void OnSelectedBerthChanged(string? value) => NotifyStructureChanged();

    /// <summary>Picks a vessel on the Berths page (the harbour frames it), clearing any berth pick.</summary>
    public void PickVessel(string vesselId)
    {
        SelectedBerth = null;
        SelectedVesselId = vesselId;
    }

    [ObservableProperty]
    public partial int? SelectedBay { get; set; }

    partial void OnSelectedBayChanged(int? value) => NotifyStructureChanged();

    [ObservableProperty]
    public partial int? HoveredBay { get; set; }

    partial void OnHoveredBayChanged(int? value) => NotifyHoverChanged();

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
                Title = $"Hold on {held.DisplayId}", Sub = $"Inspection due {Clock(InspectionDue)}",
                ActionLabel = "Open inspection", IsPrimary = true, Command = OpenInspectionCommand, Subject = held.DisplayId
            });
        }

        if (PortData.Vehicles.FirstOrDefault(v => v.Authorization == "Awaiting driver ID") is { } truck)
        {
            Decisions.Add(new Decision
            {
                Id = $"driver-{truck.Id}", Kind = DecisionKind.DriverCheck,
                Title = $"Driver ID pending at {truck.Gate.Split(" · ")[0]}", Sub = $"{truck.Id} at the barrier",
                ActionLabel = "Verify driver", IsPrimary = false, Command = OpenAccessCommand, Subject = truck.Id
            });
        }

        var late = PortData.RiverFleet.Count(v => v.SlipMinutes > 0);
        if (late > 0)
        {
            Decisions.Add(new Decision
            {
                Id = "late-orders", Kind = DecisionKind.LateOrders,
                Title = late == 1 ? "1 order late" : $"{late} orders late", Sub = late == 1 ? "In the yard queue" : "Both in the yard queue",
                ActionLabel = "View orders", IsPrimary = false, Command = GoCommand, CommandParameter = "fleet"
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
                    var since = ClockHours(PortData.Vehicles.First(v => v.Authorization == "Awaiting driver ID").Entry);
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

    private static string Clock(double hours) => TimeSpan.FromHours(hours % 24).ToString(@"hh\:mm");

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

    /// <summary>Bumped by the masthead's Needs-you figure; the queue scrolls into view and takes focus.</summary>
    [ObservableProperty]
    public partial int NeedsYouReveal { get; set; }

    [RelayCommand]
    private void RevealNeedsYou()
    {
        Section = "overview";
        NeedsYouReveal++;
    }

    [RelayCommand]
    private void OpenAccess()
    {
        SecurityTab = "access";
        Section = "security";
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

        NotifyStructureChanged();
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

    partial void OnDockSelectionChanged(string? value) => NotifyStructureChanged();

    /// <summary>Null means "follow the live clock".</summary>
    [ObservableProperty]
    public partial double? PlanHour { get; set; }

    partial void OnPlanHourChanged(double? value) => PlanHourChanged?.Invoke(this, EventArgs.Empty);

    public double EffectivePlanHour => PlanHour ?? NowHours;

    // ── Containers ────────────────────────────────────────────────────────────

    [ObservableProperty]
    public partial string ContainerFilter { get; set; } = "All";

    partial void OnContainerFilterChanged(string value) => NotifyStructureChanged();

    [ObservableProperty]
    public partial string SelectedContainerId { get; set; } = "AVRU4821934";

    partial void OnSelectedContainerIdChanged(string value) => NotifyStructureChanged();

    public ContainerDef SelectedContainer =>
        PortData.Containers.First(c => c.Id == SelectedContainerId);

    [ObservableProperty]
    public partial bool ScannerOpen { get; set; } = Hook("CARGO_SCANNER") == "1";

    [ObservableProperty]
    public partial bool Xray { get; set; } = Hook("CARGO_XRAY") == "1";

    partial void OnXrayChanged(bool value) => NotifyStructureChanged();

    [ObservableProperty]
    public partial double ScanRotationX { get; set; } = -18;

    [ObservableProperty]
    public partial double ScanRotationY { get; set; } = -32;

    [ObservableProperty]
    public partial double ScanZoom { get; set; } = 1;

    // ── Yard ──────────────────────────────────────────────────────────────────

    [ObservableProperty]
    public partial string? SelectedStack { get; set; } = Hook("CARGO_STACK");

    partial void OnSelectedStackChanged(string? value) => NotifyStructureChanged();

    public int YardMoves { get; private set; } = 1284;

    /// <summary>How full the yard is: the tiers stacked, as a share of five on every slot.</summary>
    public int YardOccupancy { get; private set; }

    public int YardHeight(string key, int baseHeight) =>
        Math.Clamp(baseHeight + (_yardDelta.TryGetValue(key, out var d) ? d : 0), 0, 5);

    public bool YardRecentlyMoved(string key) =>
        _yardRecent.TryGetValue(key, out var at) && (_clock.Now - at).TotalSeconds < 60;

    private int Occupancy() =>
        (int)Math.Round(YardGrid.Slots.Sum(s => YardHeight(s.Key, s.Base)) * 100d / (YardGrid.Slots.Count * 5));

    /// <summary>
    /// The yard works a stack every few seconds: one slot gains or loses a tier. It runs on the store's clock whether
    /// or not the yard map is on screen, so the yard's figures are live everywhere they are shown.
    /// </summary>
    private void ShuffleYard()
    {
        if ((_clock.Now - _lastYardShuffle).TotalMilliseconds < 3500)
        {
            return;
        }

        _lastYardShuffle = _clock.Now;
        var pick = YardGrid.Slots[Random.Shared.Next(YardGrid.Slots.Count)];
        var current = YardHeight(pick.Key, pick.Base);
        var delta = current <= 0 ? 1 : current >= 5 ? -1 : Random.Shared.Next(2) == 0 ? -1 : 1;
        _yardDelta[pick.Key] = (_yardDelta.TryGetValue(pick.Key, out var d) ? d : 0) + delta;
        _yardRecent[pick.Key] = _clock.Now;
        YardMoves++;
        YardOccupancy = Occupancy();
        NotifyYardChanged();
    }

    // ── Security ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    public partial string SecurityTab { get; set; } = Hook("CARGO_SECURITY_TAB") is { Length: > 0 } tab ? tab : "zones";

    partial void OnSecurityTabChanged(string value)
    {
        OnPropertyChanged(nameof(SecurityTitle));
        NotifyStructureChanged();
    }

    public string SecurityTitle => SecurityTab switch
    {
        "access" => "Access control",
        "inspection" => "Cargo inspection",
        _ => "Port security zones"
    };

    [ObservableProperty]
    public partial int SelectedPerson { get; set; }

    partial void OnSelectedPersonChanged(int value) => NotifyStructureChanged();

    // ── Fleet ─────────────────────────────────────────────────────────────────

    [ObservableProperty]
    public partial string FleetSelection { get; set; } = "al118";

    partial void OnFleetSelectionChanged(string value) => NotifyStructureChanged();

    [ObservableProperty]
    public partial string ChatDraft { get; set; } = string.Empty;

    public IReadOnlyList<ChatLine> ThreadFor(string vesselId)
    {
        var vessel = PortData.RiverFleet.First(v => v.Id == vesselId);
        return _sentMessages.TryGetValue(vesselId, out var extra)
            ? vessel.Thread.Concat(extra).ToList()
            : vessel.Thread;
    }

    public void Send(string vesselId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!_sentMessages.TryGetValue(vesselId, out var list))
        {
            list = new List<ChatLine>();
            _sentMessages[vesselId] = list;
        }

        list.Add(new ChatLine("dispatch", text.Trim(), NowHours));
        ChatDraft = string.Empty;
        NotifyStructureChanged();
    }
}
