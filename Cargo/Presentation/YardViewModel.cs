namespace Cargo.Presentation;

public sealed class TierRow
{
    public required string Id { get; init; }
    public required string Detail { get; init; }
    /// <summary>The box's cargo class, as its palette key.</summary>
    public required string Tone { get; init; }
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
            new LegendItem { Label = "Standard", Swatch = "CargoStandardColor" },
            new LegendItem { Label = "Refrigerated", Swatch = "CargoReeferColor" },
            new LegendItem { Label = "Hazardous", Swatch = "CargoHazardColor" },
            new LegendItem { Label = "Inspection", Swatch = "CargoOversizeColor" },
            new LegendItem { Label = "Restricted", Swatch = "ErrorBrush" },
            new LegendItem { Label = "Available slot", Swatch = "OnSurfaceColor", Strength = 0.12 }
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
    private void CloseStack() => State.Yard.SelectedStack = null;

    public void Refresh()
    {
        LiveLine = $"Live · {State.Yard.Moves:N0} moves today · 3 RTGs ·";
        OccupancyLine = $"{State.Yard.Occupancy}% occupied";
        Occupancy = State.Yard.Occupancy;

        BuildStack();
    }

    private void BuildStack()
    {
        if (State.Yard.SelectedStack is not { } key)
        {
            HasStack = false;
            return;
        }

        var slot = YardGrid.Slots.FirstOrDefault(s => s.Key == key);
        if (slot.Key is null)
        {
            return;
        }

        var height = State.Yard.Height(key, slot.Base);

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
                "CargoHazardColor" => $"IMDG class {3 + (int)(Noise.H(seed) * 6)}",
                "CargoOversizeColor" => "Awaiting exam",
                "ErrorColor" => "Bonded",
                _ => Noise.H(seed) > 0.7 ? "Empty" : "Laden"
            };

            var token = tag == "Empty" ? "CargoEmptyColor" : slot.Zone.TintToken;
            var id = $"{PortData.ContainerPrefixes[(int)(Noise.H(seed) * 8)]} " +
                     $"{(int)(Noise.H(seed + 5) * 900000 + 100000)} {(int)(Noise.H(seed + 9) * 10)}";

            boxes.Add((id, tag, token));
            Tiers.Add(new TierRow
            {
                Id = id,
                Detail = $"Tier {i + 1} · {tag}",
                Tone = token
            });
        }

        StackBoxes = boxes;
    }
}
