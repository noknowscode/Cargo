namespace Cargo.Presentation;

// Flat shapes for the lists in the UI. Most are rebuilt wholesale when the state changes, so they carry their states
// rather than raising notifications; the views pick the brushes (Paint and each view's mapping functions).

/// <summary>
/// One section in the header. Unlike the other rows here this one is built once and then
/// updated in place: rebuilding the collection on every navigation would recreate every
/// button, and moving between sections would flicker.
/// </summary>
public sealed partial class NavItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Label { get; init; }

    /// <summary>The section's route inside Main's region ("./berths").</summary>
    public string Route => $"./{Id}";

    [ObservableProperty]
    public partial string Tooltip { get; set; } = string.Empty;

    /// <summary>The tab's spoken name: the label, plus ", needs you" when the dot shows.</summary>
    [ObservableProperty]
    public partial string AutomationName { get; set; } = string.Empty;

    /// <summary>The section on screen: ink, semibold and underlined (the others are muted and medium weight).</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>Something in the section waits on the dispatcher: the alert dot.</summary>
    [ObservableProperty]
    public partial bool NeedsYou { get; set; }
}

/// <summary>One segment of the harbour layer switcher; built once, recoloured in place.</summary>
public sealed partial class LayerItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    /// <summary>The design's 24-unit stroke icon, as SVG path data.</summary>
    public required string IconData { get; init; }

    public required ICommand Command { get; init; }

    /// <summary>The layer on the harbour now: paper on the switcher's ink indicator.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
}

public sealed class Fact
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    /// <summary>The value's ink as a palette key; null for plain ink.</summary>
    public string? Tone { get; init; }
}

public sealed class LegendItem
{
    public required string Label { get; init; }

    /// <summary>The swatch's palette key.</summary>
    public required string Swatch { get; init; }

    /// <summary>How much of the colour the swatch takes (an empty slot is a faint one).</summary>
    public double Strength { get; init; } = 1;
}

public sealed class CompositionSlice
{
    public required string Label { get; init; }

    /// <summary>The slice's palette key.</summary>
    public required string Fill { get; init; }
    public string Count { get; init; } = string.Empty;
}
