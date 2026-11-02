using Cargo.Controls;

namespace Cargo.Presentation;

public sealed class TierRow
{
    public required string Id { get; init; }
    public required string Detail { get; init; }
    public required Brush Tone { get; init; }
}

/// <summary>
/// The yard: live moves and occupancy, and the selected stack's tiers. Created by
/// <see cref="CargoViewModel"/> on the UI thread.
/// </summary>
public sealed partial class YardViewModel : ObservableObject
{
    public YardViewModel(PortState state)
    {
        State = state;
        Legend = new[]
        {
            new LegendItem { Label = "Standard", Swatch = Tokens.Brush("CargoStandardInvariantBrush") },
            new LegendItem { Label = "Refrigerated", Swatch = Tokens.Brush("CargoReeferInvariantBrush") },
            new LegendItem { Label = "Hazardous", Swatch = Tokens.Brush("CargoHazardInvariantBrush") },
            new LegendItem { Label = "Inspection", Swatch = Tokens.Brush("CargoOversizeInvariantBrush") },
            new LegendItem { Label = "Restricted", Swatch = Tokens.Brush("RestrictedBrush") },
            new LegendItem { Label = "Available slot", Swatch = Tokens.Brush("InkColor", 0.12) }
        };

        Refresh();
    }

    public PortState State { get; }

    public IReadOnlyList<LegendItem> Legend { get; }

    public ObservableCollection<TierRow> Tiers { get; } = new();

    [ObservableProperty] public partial string LiveLine { get; set; } = string.Empty;
    [ObservableProperty] public partial string OccupancyLine { get; set; } = string.Empty;
    [ObservableProperty] public partial double Occupancy { get; set; }
    [ObservableProperty] public partial string SlotTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string SlotSubtitle { get; set; } = string.Empty;
    [ObservableProperty] public partial IReadOnlyList<(string Id, string Tag, string Token)> StackBoxes { get; set; } = [];

    [ObservableProperty]
    public partial bool HasStack { get; set; }

    [RelayCommand]
    private void CloseStack() => State.SelectedStack = null;

    public void Refresh()
    {
        LiveLine = $"Live · {State.YardMoves:N0} moves today · 3 RTGs ·";
        OccupancyLine = $"{State.YardOccupancy}% occupied";
        Occupancy = State.YardOccupancy;

        BuildStack();
    }

    private void BuildStack()
    {
        if (State.SelectedStack is not { } key)
        {
            HasStack = false;
            return;
        }

        var slot = YardMap.Slots().FirstOrDefault(s => s.Key == key);
        if (slot.Key is null)
        {
            return;
        }

        var height = State.YardHeight(key, slot.Base);

        HasStack = true;

        SlotTitle = $"Slot {slot.Slot}";
        SlotSubtitle = $"{slot.Zone.Name} · {slot.Zone.Sub} · {height} of 5 tiers";

        // Each tier gets a plausible box: reefers stay reefers, hazmat keeps its IMDG class
        var boxes = new List<(string Id, string Tag, string Token)>();
        Tiers.Clear();

        for (var i = 0; i < height; i++)
        {
            var seed = (int)(slot.X * 3 + slot.Y) + i * 11;
            var tag = slot.Zone.TintToken switch
            {
                "CargoReeferColor" => "Reefer −18°C",
                "CargoHazardColor" => $"IMDG class {3 + (int)(Geo.H(seed) * 6)}",
                "CargoOversizeColor" => "Awaiting exam",
                "RestrictedColor" => "Bonded",
                _ => Geo.H(seed) > 0.7 ? "Empty" : "Laden"
            };

            var token = tag == "Empty" ? "CargoEmptyColor" : slot.Zone.TintToken;
            var id = $"{PortData.ContainerPrefixes[(int)(Geo.H(seed) * 8)]} " +
                     $"{(int)(Geo.H(seed + 5) * 900000 + 100000)} {(int)(Geo.H(seed + 9) * 10)}";

            boxes.Add((id, tag, token));
            Tiers.Add(new TierRow
            {
                Id = id,
                Detail = $"Tier {i + 1} · {tag}",
                Tone = Tokens.Brush(token)
            });
        }

        StackBoxes = boxes;
    }
}
