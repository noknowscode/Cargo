using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class VesselRow : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Her status as the plan stands, "Arriving in 42 min · assigned Berth 07"; the countdown moves with the clock.</summary>
    [ObservableProperty] public partial string Line1 { get; set; } = string.Empty;

    [ObservableProperty] public partial string Line2 { get; set; } = string.Empty;

    /// <summary>The vessel's accent, as its palette key.</summary>
    public required string Tone { get; init; }

    /// <summary>The vessel being worked: an accent edge.</summary>
    public required bool IsSelected { get; init; }

    /// <summary>Her side render, by sprite name (Assets/Sprites).</summary>
    public required string Silhouette { get; init; }
    public required ICommand Select { get; init; }
}

/// <summary>Rebuilt only when the vessel changes; the live figures tick in place.</summary>
public sealed partial class MiniCard : ObservableObject
{
    public required string Key { get; init; }

    /// <summary>The card's icon ink, as its palette key.</summary>
    public required string Tone { get; init; }

    /// <summary>The card's 24-unit stroke icon, as SVG path data.</summary>
    public required string IconData { get; init; }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Detail { get; set; } = string.Empty;
}

public sealed class CheckRow
{
    public required string Mark { get; init; }
    public required string Label { get; init; }
    public required string Time { get; init; }

    /// <summary>The mark's ink, as its palette key.</summary>
    public required string Tone { get; init; }
}

/// <summary>
/// The vessel being worked, under the berth plan: its profile and bays, cargo operations,
/// composition and clearance. <see cref="Refresh"/> follows structural changes (a different
/// vessel or bay); <see cref="RefreshLive"/> follows the clock. Created by
/// <see cref="BerthsViewModel"/> on the UI thread.
/// </summary>
public sealed partial class VesselsViewModel : ObservableObject
{
    public VesselsViewModel(PortState state)
    {
        State = state;
        CargoLegend = Enum.GetValues<CargoClass>()
            .Select(c => new LegendItem { Label = c.Label(), Swatch = c.Token() })
            .Append(new LegendItem { Label = "Already ashore", Swatch = "TextFaintBrush" })
            .ToList();

        Refresh();
        RefreshLive();
    }

    public PortState State { get; }

    public string Eyebrow => $"VESSELS · {PortData.Vessels.Count} IN PORT TODAY";

    public ObservableCollection<VesselRow> Vessels { get; } = new();
    public ObservableCollection<MiniCard> MiniCards { get; } = new();
    public ObservableCollection<CompositionSlice> Composition { get; } = new();
    public ObservableCollection<CompositionSlice> BayComposition { get; } = new();
    public ObservableCollection<Fact> Facts { get; } = new();
    public ObservableCollection<CheckRow> Checks { get; } = new();
    public IReadOnlyList<LegendItem> CargoLegend { get; }

    /// <summary>The vessel whose cranes the operations scene animates.</summary>
    [ObservableProperty]
    public partial Vessel? Vessel { get; set; }

    [ObservableProperty]
    public partial BayView? Bay { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<(double Weight, string Tone)> CompositionSlices { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<(double Weight, string Tone)> BaySlices { get; set; } = [];

    [ObservableProperty]
    public partial string BayContainerImage { get; set; } = string.Empty;

    [ObservableProperty] public partial string VesselName { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProfileHint { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayLabel { get; set; } = string.Empty;
    [ObservableProperty] public partial string Voyage { get; set; } = string.Empty;
    [ObservableProperty] public partial string Security { get; set; } = string.Empty;
    /// <summary>Her security is cleared: a navy chip, else an amber one.</summary>
    [ObservableProperty] public partial bool IsCleared { get; set; }
    [ObservableProperty] public partial string TeuLabel { get; set; } = string.Empty;
    [ObservableProperty] public partial string OpsNote { get; set; } = string.Empty;
    [ObservableProperty] public partial string MovesLabel { get; set; } = string.Empty;
    [ObservableProperty] public partial string UnloadLive { get; set; } = string.Empty;
    [ObservableProperty] public partial string LoadLive { get; set; } = string.Empty;
    [ObservableProperty] public partial double UnloadPercent { get; set; }
    [ObservableProperty] public partial double LoadPercent { get; set; }

    [ObservableProperty] public partial string BayHeading { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayTotal { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayRemaining { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayOpsHeading { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayCompositionHeading { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayUnloaded { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayLeft { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayNextMove { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayNextEta { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayContainerId { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayContainerType { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayContainerWeight { get; set; } = string.Empty;
    [ObservableProperty] public partial string BayContainerStatus { get; set; } = string.Empty;

    // ── Structure ─────────────────────────────────────────────────────────────

    public void Refresh()
    {
        var vessel = State.SelectedVessel;

        VesselName = vessel.Name;
        ProfileHint = $"{vessel.Length} m · {vessel.Bays} bays · Click a bay";
        Voyage = vessel.Voyage;
        Security = vessel.Security;

        IsCleared = vessel.Security == "Cleared";

        TeuLabel = vessel.Containers.ToString("N0");
        OpsNote = VesselCopy.Note(State, vessel).Full;

        BuildVesselList();
        BuildFacts(vessel);
        BuildChecks(vessel);
        BuildComposition(vessel);
        BuildBay(vessel);

        // A bay click or a hover is a structure change too; the scene restarts its crane loop
        // only when this is a different vessel.
        Vessel = vessel;
    }

    private void BuildVesselList()
    {
        Vessels.Clear();
        foreach (var vessel in PortData.Vessels)
        {
            var selected = vessel.Id == State.SelectedVesselId;
            var words = VesselCopy.Words(State, vessel);

            Vessels.Add(new VesselRow
            {
                Id = vessel.Id,
                Name = vessel.Name,
                Line1 = words.RowLine,
                Line2 = words.Detail,
                Tone = vessel.AccentToken,
                IsSelected = selected,
                Silhouette = Sprites.Side(vessel.Id),
                Select = State.PickHullVesselCommand
            });
        }
    }

    private void BuildFacts(Vessel vessel)
    {
        Facts.Clear();
        var berth = State.BerthOf(vessel.Id);
        foreach (var (key, value) in new[]
                 {
                     ("Operator", vessel.Operator),
                     ("IMO", vessel.Imo),
                     ("Arrival", vessel.Eta),
                     ("Departure", vessel.Etd),
                     ("Berth", berth is null ? "Unassigned" : $"Berth {berth}"),
                     ("Containers", $"{vessel.Containers:N0} TEU"),
                     ("Length", $"{vessel.Length} m"),
                     ("Draft", $"{vessel.Draft} m")
                 })
        {
            Facts.Add(new Fact { Key = key, Value = value });
        }
    }

    private void BuildChecks(Vessel vessel)
    {
        Checks.Clear();
        foreach (var check in vessel.Checks)
        {
            Checks.Add(new CheckRow
            {
                Mark = check.Mark,
                Label = check.Label,
                Time = check.Time,
                Tone = check.Tone
            });
        }
    }

    private void BuildComposition(Vessel vessel)
    {
        Composition.Clear();
        var slices = new List<(double, string)>();

        foreach (var (cargo, percent) in vessel.Mix.Parts())
        {
            Composition.Add(new CompositionSlice
            {
                Label = cargo.Label(),
                Fill = cargo.Token(),
                Count = $"{percent}%"
            });
            slices.Add((percent, cargo.Token()));
        }

        CompositionSlices = slices;
    }

    private void BuildBay(Vessel vessel)
    {
        var selected = BayView.Open(vessel, State.SelectedBay);
        var view = BayView.For(vessel, selected);

        BayLabel = BayView.Summary(vessel, State.HoveredBay ?? selected);
        BayHeading = $"Bay {view.Number}";
        BayTotal = $"· {BayView.Capacity} containers";
        BayRemaining = $"· {view.Remaining} remaining aboard";
        BayOpsHeading = $"Bay {view.Number} operations";
        BayCompositionHeading = $"Bay {view.Number} composition";
        BayUnloaded = view.Unloaded.ToString();
        BayLeft = view.Remaining.ToString();
        BayNextMove = view.NextMove;
        BayNextEta = $"Crane 03 · ETA {view.NextEta}";
        BayContainerId = view.ContainerId;
        BayContainerType = view.ContainerType;
        BayContainerWeight = view.Weight;
        BayContainerStatus = view.Status;
        BayContainerImage = Sprites.Container(view.ContainerClass);

        BayComposition.Clear();
        var slices = new List<(double, string)>();
        foreach (var (cargo, count, percent) in view.Composition)
        {
            BayComposition.Add(new CompositionSlice
            {
                Label = cargo.Label(),
                Fill = cargo.Token(),
                Count = $"{count} ({percent}%)"
            });
            slices.Add((count, cargo.Token()));
        }

        BaySlices = slices;
        Bay = view;
    }

    // ── Live ──────────────────────────────────────────────────────────────────

    public void RefreshLive()
    {
        var vessel = State.SelectedVessel;
        var elapsed = State.ElapsedSeconds;

        // The arrivals' countdowns in the list
        foreach (var row in Vessels)
        {
            var words = VesselCopy.Words(State, PortData.Vessel(row.Id));
            (row.Line1, row.Line2) = (words.RowLine, words.Detail);
        }

        var unload = vessel.UnloadPercent is > 0 and < 100
            ? Math.Min(100, vessel.UnloadPercent + elapsed * 0.012)
            : vessel.UnloadPercent;
        var load = vessel.LoadPercent is > 0 and < 100
            ? Math.Min(100, vessel.LoadPercent + elapsed * 0.006)
            : vessel.LoadPercent;

        UnloadPercent = unload;
        LoadPercent = load;
        UnloadLive = $"{unload:0.0}%";
        LoadLive = $"{load:0.0}%";

        var moves = vessel.UnloadPercent is > 0 and < 100
            ? vessel.LoadPercent > 0 ? 58 : 41
            : vessel.LoadPercent is > 0 and < 100 ? 36 : 0;
        MovesLabel = $"Live · {moves} moves/h";

        var ashore = Math.Round(vessel.Containers * vessel.UnloadPercent / 100d);
        (string Key, string Value, string Detail, string Tone, string Icon)[] cards =
        {
            ("Length overall", $"{vessel.Length} m", $"Draft {vessel.Draft} m", "InkBrush",
                "M3 12h18M3 12l4-4M3 12l4 4M21 12l-4-4M21 12l-4 4"),
            ("Discharged", UnloadLive, $"{ashore:N0} discharged", "SeaGreenBrush",
                "M12 3v12M12 15l-4-4M12 15l4-4M4 21h16"),
            ("Loaded", LoadLive, $"{moves} moves/h", "AccentBrush",
                "M12 21V9M12 9l-4 4M12 9l4 4M4 3h16"),
            ("Departure", vessel.Etd, State.BerthOf(vessel.Id) is { } b ? $"Berth {b}" : "Unassigned", "InkBrush",
                "M3 15l2 5h14l2-5zM5 15V9h14v6M9 9V5h6v4")
        };

        if (MiniCards.Count != cards.Length)
        {
            MiniCards.Clear();
            foreach (var card in cards)
            {
                MiniCards.Add(new MiniCard
                {
                    Key = card.Key,
                    Tone = card.Tone,
                    IconData = card.Icon
                });
            }
        }

        for (var i = 0; i < cards.Length; i++)
        {
            MiniCards[i].Value = cards[i].Value;
            MiniCards[i].Detail = cards[i].Detail;
        }
    }
}
