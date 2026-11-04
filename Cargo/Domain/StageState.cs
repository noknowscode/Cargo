using static Cargo.Domain.DebugHooks;

namespace Cargo.Domain;

/// <summary>
/// The harbour stage every harbour section shares: full on Overview (340, 491 expanded, a 180 mini while scrolled), a
/// strip elsewhere that opens to the full stage; its overlay layer, its own selection on Overview, and the linked
/// hover between list rows, tags and the 3D berths. Follows <see cref="NavigationState.Section"/>.
/// </summary>
public sealed partial class StageState : ObservableObject
{
    private readonly NavigationState _navigation;
    private readonly PortSignals _signals;

    public StageState(NavigationState navigation, PortSignals signals)
    {
        _navigation = navigation;
        _signals = signals;
        navigation.SectionChanging += (_, _) => OnSectionChanging();
    }

    private string Section => _navigation.Section;

    private void OnSectionChanging()
    {
        Open = false;
        OverviewCondensed = false;
        OnPropertyChanged(nameof(Shown));
        OnPropertyChanged(nameof(Full));
        OnPropertyChanged(nameof(Compact));
        OnPropertyChanged(nameof(ShowToggle));
        OnPropertyChanged(nameof(LayerSwitchShown));
        OnPropertyChanged(nameof(MastheadShown));
        OnToggleChanged();
    }

    /// <summary>Every section but Waterways sits under the harbour.</summary>
    public bool Shown => Section is "overview" or "berths" or "cargo" or "security";

    public bool Full => Section == "overview" || Open;

    public bool Compact => Shown && !Full;

    /// <summary>Another section's strip opened to the full stage.</summary>
    [ObservableProperty]
    public partial bool Open { get; set; }

    partial void OnOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(Full));
        OnPropertyChanged(nameof(LayerSwitchShown));
        OnPropertyChanged(nameof(Compact));
        OnToggleChanged();
        _signals.Structure();
    }

    /// <summary>Overview's harbour band at its expanded height (491 rather than 340).</summary>
    [ObservableProperty]
    public partial bool OverviewExpanded { get; set; }

    partial void OnOverviewExpandedChanged(bool value) => OnToggleChanged();

    /// <summary>
    /// Overview scrolled: the masthead folds and the band snaps to its 180 px mini (SPEC-OVERVIEW-SCROLL). Set by the
    /// page from its scroll offset; the band's own height (340 or 491) comes back when it clears.
    /// </summary>
    [ObservableProperty]
    public partial bool OverviewCondensed { get; set; }

    partial void OnOverviewCondensedChanged(bool value)
    {
        OnToggleChanged();
        OnPropertyChanged(nameof(MastheadShown));
        OnPropertyChanged(nameof(LayerSwitchShown));
    }

    /// <summary>Asks the page to scroll Overview back to its top, which restores the full band.</summary>
    public event EventHandler? ScrollToTopRequested;

    public bool MastheadShown => Section == "overview" && !OverviewCondensed;

    /// <summary>The layer switcher floats on the full band only: the mini band has no room for the HUD.</summary>
    public bool LayerSwitchShown => Full && !(Section == "overview" && OverviewCondensed);

    /// <summary>
    /// Overview's harbour band: 340, or 491 expanded (the design), or the 180 mini while scrolled. NaN elsewhere: the
    /// stage sizes itself.
    /// </summary>
    public double Height => Section == "overview" ? (OverviewCondensed ? 180 : OverviewExpanded ? 491 : 340) : double.NaN;

    /// <summary>Whether the toggle would collapse: Overview's band is expanded, or another section's strip is open.</summary>
    public bool Expanded => Section == "overview" ? OverviewExpanded && !OverviewCondensed : Open;

    public bool Collapsed => !Expanded;

    public string ToggleLabel => Section == "overview"
        ? (OverviewCondensed ? "Show harbour" : OverviewExpanded ? "Collapse" : "Expand")
        : (Open ? "Collapse harbour" : "Expand harbour");

    public bool ShowToggle => Shown;

    /// <summary>Overview grows its band; the other harbour sections open their strip to the full stage.</summary>
    [RelayCommand]
    private void Toggle()
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
            Open = !Open;
        }
    }

    private void OnToggleChanged()
    {
        OnPropertyChanged(nameof(Height));
        OnPropertyChanged(nameof(Expanded));
        OnPropertyChanged(nameof(Collapsed));
        OnPropertyChanged(nameof(ToggleLabel));
    }

    // ── Layer and selection ───────────────────────────────────────────────────

    /// <summary>The overlay on Overview's harbour: port, yard, security or traffic.</summary>
    [ObservableProperty]
    public partial string Layer { get; set; } = Hook("CARGO_LAYER") ?? "port";

    partial void OnLayerChanged(string value) => _signals.Structure();

    [RelayCommand]
    private void SetLayer(string layer) => Layer = layer;

    /// <summary>The vessel picked on the harbour outside Berths (Berths frames the vessel it works).</summary>
    [ObservableProperty]
    public partial string? Selection { get; set; }

    partial void OnSelectionChanged(string? value) => _signals.Structure();

    // ── Linked hover ──────────────────────────────────────────────────────────

    [ObservableProperty]
    public partial int? HoveredBerth { get; set; } = int.TryParse(Hook("CARGO_HOVER_BERTH"), out var berth) ? berth : null;

    partial void OnHoveredBerthChanged(int? value) => _signals.Hover();

    /// <summary>The vessel a list row, tag or the needs-you bar is pointing at. Repaints, never rebuilds.</summary>
    [ObservableProperty]
    public partial string? HoveredVessel { get; set; }

    partial void OnHoveredVesselChanged(string? value) => _signals.Hover();

    /// <summary>
    /// Linked hover from a list row, a harbour tag or the needs-you bar. The key is a vessel id or a berth number;
    /// "-key" ends that hover, and only if it is still the current one, since the next row's enter can arrive before
    /// the last row's leave.
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
}
