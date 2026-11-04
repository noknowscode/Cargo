namespace Cargo.Presentation;

/// <summary>
/// One line of the Berths status list: a berth, or a vessel waiting at anchor. Rows are built
/// once and then updated in place, so a confirm recolours row 07 without rebuilding the list or
/// losing the scroll position.
/// </summary>
public sealed partial class BerthStatusRow : ObservableObject
{
    /// <summary>A berth number ("07"), or for an anchorage row the vessel id.</summary>
    public required string Slot { get; init; }

    public required ICommand Select { get; init; }

    /// <summary>Linked hover through <see cref="StageState.HoverCommand"/>.</summary>
    public required ICommand Hover { get; init; }

    /// <summary>What the row stands for on the harbour: its vessel, else its berth.</summary>
    [ObservableProperty] public partial string LinkKey { get; set; } = string.Empty;

    /// <summary>What the row stands for: a vessel, the one waiting on you, a free berth or a closed one.</summary>
    [ObservableProperty] public partial BerthRowKind Kind { get; set; }

    /// <summary>The row is the vessel or berth being worked: an ink outline.</summary>
    [ObservableProperty] public partial bool IsCurrent { get; set; }

    /// <summary>Its vessel or berth is hovered elsewhere (a tag, the harbour, the bar): the hover ground.</summary>
    [ObservableProperty] public partial bool IsLinked { get; set; }

    /// <summary>The vessel on or bound for this berth; null for a free or closed berth.</summary>
    [ObservableProperty] public partial string? VesselId { get; set; }
    [ObservableProperty] public partial string Plate { get; set; } = string.Empty;
    [ObservableProperty] public partial string StateLabel { get; set; } = string.Empty;
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string Meta { get; set; } = string.Empty;
    [ObservableProperty] public partial string When { get; set; } = string.Empty;
    [ObservableProperty] public partial string AccessibleName { get; set; } = string.Empty;
}

/// <summary>
/// Berth status: the eight berths of North Quay and the anchorage, each with what it holds
/// now or next. Created by <see cref="BerthsViewModel"/> on the UI thread.
/// </summary>
public sealed partial class BerthStatusViewModel : ObservableObject
{
    private readonly PortState _state;

    public BerthStatusViewModel(PortState state)
    {
        _state = state;
        Refresh();
    }

    public ObservableCollection<BerthStatusRow> Rows { get; } = new();

    [ObservableProperty]
    public partial string Count { get; set; } = string.Empty;

    [RelayCommand]
    private void Pick(string slot)
    {
        var row = Rows.FirstOrDefault(r => r.Slot == slot);
        if (row?.VesselId is { } vessel)
        {
            _state.PickVessel(vessel);
        }
        else
        {
            _state.SelectedBerth = slot;
        }
    }

    /// <summary>Called by the page while it is on screen, on structure changes and each tick.</summary>
    public void Refresh()
    {
        var anchored = PortData.Vessels.Where(v => _state.BerthOf(v.Id) is null).ToList();
        var slots = PortData.Berths.Select(b => b.Number).Concat(anchored.Select(v => v.Id)).ToList();

        // The berths never change; only the anchorage rows come and go as vessels get a berth
        if (!Rows.Select(r => r.Slot).SequenceEqual(slots))
        {
            Rows.Clear();
            foreach (var slot in slots)
            {
                Rows.Add(new BerthStatusRow { Slot = slot, Select = PickCommand, Hover = _state.Stage.HoverCommand });
            }
        }

        foreach (var row in Rows)
        {
            if (PortData.Berth(row.Slot) is { } berth)
            {
                FillBerth(row, berth);
            }
            else
            {
                FillAnchorage(row, PortData.Vessel(row.Slot));
            }
        }

        Count = anchored.Count == 0
            ? $"{PortData.Berths.Count} berths"
            : $"{PortData.Berths.Count} berths · {anchored.Count} at anchor";
    }

    private void FillBerth(BerthStatusRow row, BerthDef berth)
    {
        var now = _state.NowHours;
        var vessel = _state.OccupantAt(berth.Number, now) ?? _state.IncomingAt(berth.Number, now);
        row.VesselId = vessel?.Id;
        row.Plate = berth.Number;

        if (vessel is not null)
        {
            var needs = _state.PendingDecision == vessel.Id;
            row.StateLabel = BerthFacts.State(_state, vessel).ToUpperInvariant();
            row.Name = vessel.Name;
            row.Meta = needs
                ? $"Pilot aboard · under-keel {BerthFacts.UnderKeel(vessel, berth):0.0} m"
                : VesselCopy.Note(_state, vessel).Lead;
            row.When = BerthFacts.When(_state, vessel);
            row.AccessibleName = $"Berth {berth.Number}, {vessel.Name}, {BerthFacts.State(_state, vessel)}, {row.When}"
                + (needs ? ", needs confirming" : string.Empty);
            row.Kind = needs ? BerthRowKind.Needs : BerthRowKind.Vessel;
            Paint(row);
            return;
        }

        var closed = berth.State == BerthState.Restricted;
        row.Name = closed ? "Closed" : "Free berth";
        row.StateLabel = closed ? "RESTRICTED" : "AVAILABLE";
        row.Meta = closed
            ? $"{BerthFacts.Restriction(berth)} · {berth.Depth:0.0} m"
            : $"Depth {berth.Depth:0.0} m";
        row.When = string.Empty;
        row.AccessibleName = $"Berth {berth.Number}, {row.Name.ToLowerInvariant()}, {row.Meta}";
        row.Kind = closed ? BerthRowKind.Restricted : BerthRowKind.Free;
        Paint(row);
    }

    private void FillAnchorage(BerthStatusRow row, Vessel vessel)
    {
        row.VesselId = vessel.Id;
        row.Plate = "A";
        row.StateLabel = "AT ANCHOR";
        row.Name = vessel.Name;
        row.Meta = BerthFacts.FitsLine(_state, vessel);
        row.When = ShiftClock.Display(vessel.Eta);
        row.AccessibleName = $"Anchorage, {vessel.Name}, at anchor, arrival window {row.When}, {row.Meta}";
        row.Kind = BerthRowKind.Vessel;
        Paint(row);
    }

    /// <summary>Hover repaint: only the linked ground and outline change, never the text.</summary>
    public void RepaintLinks()
    {
        foreach (var row in Rows)
        {
            Paint(row);
        }
    }

    private void Paint(BerthStatusRow row)
    {
        row.IsCurrent = _state.SelectedBerth is { } picked
            ? row.VesselId is null && picked == row.Slot
            : row.VesselId is not null && row.VesselId == _state.SelectedVesselId;
        var hoveredBerth = _state.Stage.HoveredBerth is { } i ? PortData.Berths[i].Number : null;
        row.IsLinked = row.VesselId is not null ? _state.Stage.HoveredVessel == row.VesselId : hoveredBerth == row.Slot;
        row.LinkKey = row.VesselId ?? row.Slot;
    }
}

/// <summary>What a Berths status row stands for; the view picks its ground, plate and inks from it.</summary>
public enum BerthRowKind { Vessel, Needs, Free, Restricted }

/// <summary>The wording the status list, the vessel detail and the needs-you bar share.</summary>
internal static class BerthFacts
{
    /// <summary>Whole minutes to go, rounded up so the figure changes on the minute: "42 min", then "Due".</summary>
    public static string MinutesTo(PortState state, double hour) =>
        state.MinutesUntil(hour) is > 0 and var minutes ? $"{minutes} min" : "Due";

    public static string State(PortState state, Vessel vessel) => vessel.Status switch
    {
        VesselStatus.Arriving when state.PendingDecision == vessel.Id => "Arriving · confirm berth",
        VesselStatus.Arriving when state.IsConfirmed(vessel.Id) => "Arriving · berth confirmed",
        VesselStatus.Docked when vessel.UnloadPercent is > 0 and < 100 => "Discharging",
        VesselStatus.Docked => "Loading",
        _ => vessel.Status.Label()
    };

    /// <summary>The time a row leads with: the arrival countdown, else when the vessel sails.</summary>
    public static string When(PortState state, Vessel vessel) => vessel.Status switch
    {
        VesselStatus.Arriving => MinutesTo(state, ShiftClock.ClockHours(vessel.Eta)),
        VesselStatus.AtAnchor => ShiftClock.Display(vessel.Eta),
        _ => ShiftClock.Display(vessel.Etd)
    };

    public static double UnderKeel(Vessel vessel, BerthDef berth) => berth.Depth - vessel.Draft;

    public static string Restriction(BerthDef berth) =>
        PortData.BerthHistory.TryGetValue(berth.Number, out var segments)
            ? segments.FirstOrDefault(s => s.Kind == "restricted")?.Label ?? "Closed"
            : "Closed";

    /// <summary>
    /// "Unassigned · fits berths 01, 05, 06": the berths the plan would give her, by the same rules as a drag, the
    /// Move to berth list and the optimiser (open, deep enough, free for her window).
    /// </summary>
    public static string FitsLine(PortState state, Vessel vessel) =>
        state.BerthsThatFit(vessel) is { Count: > 0 } fits
            ? $"Unassigned · fits berths {string.Join(", ", fits)}"
            : "Unassigned · no berth free for her draft and window";
}
