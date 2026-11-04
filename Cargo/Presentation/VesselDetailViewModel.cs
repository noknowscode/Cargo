namespace Cargo.Presentation;

/// <summary>
/// The panel beside the status list: the picked vessel (or free berth) as one big figure, a
/// note, cargo progress and four facts. Follows <see cref="PortState.SelectedVesselId"/> and
/// <see cref="PortState.SelectedBerth"/>. Created by <see cref="BerthsViewModel"/> on the UI thread.
/// </summary>
public sealed partial class VesselDetailViewModel : ObservableObject
{
    private readonly PortState _state;

    public VesselDetailViewModel(PortState state)
    {
        _state = state;
        Refresh();
    }

    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string Where { get; set; } = string.Empty;
    [ObservableProperty] public partial string StateLabel { get; set; } = string.Empty;
    [ObservableProperty] public partial string Big { get; set; } = string.Empty;
    [ObservableProperty] public partial string BigSub { get; set; } = string.Empty;
    [ObservableProperty] public partial string Note { get; set; } = string.Empty;
    [ObservableProperty] public partial double CargoPercent { get; set; }
    [ObservableProperty] public partial string CargoLabel { get; set; } = string.Empty;
    /// <summary>A vessel is shown (a berth has no cargo bar).</summary>
    [ObservableProperty] public partial bool HasCargo { get; set; }

    /// <summary>The vessel waiting on you: her state and figure read in alert ink.</summary>
    [ObservableProperty] public partial bool NeedsYou { get; set; }

    /// <summary>A closed berth: its state and depth read in the restricted ink.</summary>
    [ObservableProperty] public partial bool IsRestricted { get; set; }
    [ObservableProperty] public partial IReadOnlyList<Fact> Facts { get; set; } = [];

    [RelayCommand]
    private void ShowOnMap()
    {
        if (_state.SelectedBerth is null)
        {
            _state.PickVessel(_state.SelectedVesselId);
        }

        _state.Stage.Open = true;
    }

    /// <summary>Called by the page while it is on screen, on structure changes and each tick.</summary>
    public void Refresh()
    {
        if (_state.SelectedBerth is { } number && PortData.Berths.FirstOrDefault(b => b.Number == number) is { } berth)
        {
            ShowBerth(berth);
        }
        else
        {
            ShowVessel(_state.SelectedVessel);
        }
    }

    private void ShowVessel(Vessel vessel)
    {
        var berthNumber = _state.BerthOf(vessel.Id);
        var berth = PortData.Berths.FirstOrDefault(b => b.Number == berthNumber);
        var needs = _state.PendingDecision == vessel.Id;

        Name = vessel.Name;
        Where = berth is null ? "Anchorage" : $"Berth {berth.Number}";
        StateLabel = BerthFacts.State(_state, vessel).ToUpperInvariant();
        NeedsYou = needs;
        IsRestricted = false;
        Note = BerthFacts.Note(_state, vessel);

        (Big, BigSub) = vessel.Status switch
        {
            VesselStatus.Arriving => (BerthFacts.MinutesTo(_state, ShiftClock.ClockHours(vessel.Eta)), $"ETA {BerthFacts.Clock(vessel.Eta)}"),
            VesselStatus.AtAnchor => ("Unassigned", $"arrival window {BerthFacts.Clock(vessel.Eta)}"),
            VesselStatus.Departing => (BerthFacts.Clock(vessel.Etd), "departure"),
            _ when vessel.UnloadPercent is > 0 and < 100 => ($"{vessel.UnloadPercent}%", $"discharged · departs {BerthFacts.Clock(vessel.Etd)}"),
            _ => ($"{vessel.LoadPercent}%", $"loaded · sails {BerthFacts.Clock(vessel.Etd)}")
        };

        // Progress of the work the vessel is here for; nothing has started on an arrival
        (CargoPercent, CargoLabel) = vessel.Status switch
        {
            VesselStatus.Arriving or VesselStatus.AtAnchor => (0d, $"{vessel.Containers:N0} boxes to discharge · not started"),
            _ when vessel.UnloadPercent is > 0 and < 100 => (vessel.UnloadPercent, $"Discharged {vessel.UnloadPercent}%"),
            _ => (vessel.LoadPercent, $"Loaded {vessel.LoadPercent}%")
        };
        HasCargo = true;

        var facts = new List<Fact>
        {
            Fact("Length", $"{vessel.Length} m"),
            Fact("Draft", $"{vessel.Draft:0.0} m")
        };
        if (berth is null)
        {
            facts.Add(Fact("Berth depth", "—"));
            facts.Add(Fact("Under-keel", "—"));
        }
        else
        {
            var ukc = BerthFacts.UnderKeel(vessel, berth);
            facts.Add(Fact("Berth depth", $"{berth.Depth:0.0} m"));
            // A tight under-keel on the pending arrival is part of the decision, so it takes the status ink
            facts.Add(Fact("Under-keel", $"{ukc:0.0} m", needs && ukc < 1 ? "AlertInkBrush" : null));
        }

        SetFacts(facts);
    }

    // Refresh runs on every clock tick; a new list each time would re-create the facts grid every second
    private void SetFacts(IReadOnlyList<Fact> facts)
    {
        if (!facts.Select(f => (f.Key, f.Value, f.Tone)).SequenceEqual(Facts.Select(f => (f.Key, f.Value, f.Tone))))
        {
            Facts = facts;
        }
    }

    private static Fact Fact(string key, string value, string? tone = null) =>
        new() { Key = key, Value = value, Tone = tone };

    private void ShowBerth(BerthDef berth)
    {
        var restricted = berth.State == BerthState.Restricted;
        var next = _state.IncomingAt(berth.Number, _state.NowHours);

        Name = $"Berth {berth.Number}";
        Where = "North Quay";
        StateLabel = restricted ? "RESTRICTED" : "AVAILABLE";
        NeedsYou = false;
        IsRestricted = restricted;
        Big = $"{berth.Depth:0.0} m";
        BigSub = "alongside depth";
        Note = restricted
            ? BerthFacts.Restriction(berth)
            : next is null ? "No vessel booked in the next 36 h" : $"{next.Name} booked from {BerthFacts.Clock(next.Eta)}";
        HasCargo = false;
        // An array, not a collection expression: one typed as IReadOnlyList has no concrete type under WinRT AOT (CsWinRT1032)
        SetFacts(new[]
        {
            Fact("Alongside depth", $"{berth.Depth:0.0} m"),
            Fact("Next booking", next is null ? "None in 36 h" : BerthFacts.Clock(next.Eta))
        });
    }
}
