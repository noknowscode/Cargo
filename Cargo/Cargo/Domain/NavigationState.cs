using static Cargo.Domain.DebugHooks;

namespace Cargo.Domain;

/// <summary>
/// Where the dispatcher is: the section on screen and Security's tab, and the jumps that move between them.
/// <c>MainViewModel</c> keeps the router in step with <see cref="Section"/>, and <c>SecurityViewModel</c> with
/// <see cref="SecurityTab"/>. The jumps that also pick a vessel (open her, open her in the planner) belong to the berth
/// desk, <see cref="PortState"/>, which lands them here.
/// </summary>
public sealed partial class NavigationState : ObservableObject
{
    private readonly PortSignals _signals;

    public NavigationState(PortSignals signals) => _signals = signals;

    // The properties a DEBUG hook (DebugHooks) starts take it as an initializer: an initializer sets the backing field
    // without running the setter, so the change hooks stay quiet.

    private static string? StartSection() =>
        Hook("CARGO_START_SECTION") is { Length: > 0 } requested
        && Canonical(requested) is var start
        && PortData.Sections.Any(s => s.Id == start)
            ? start
            : null;

    /// <summary>
    /// Raised when <see cref="Section"/> changes, before the structure change and before its own property change, so
    /// the stage has settled for the section by the time anything rebuilds for it.
    /// </summary>
    public event EventHandler? SectionChanging;

    [ObservableProperty]
    public partial string Section { get; set; } = StartSection() ?? "overview";

    partial void OnSectionChanged(string value)
    {
        SectionChanging?.Invoke(this, EventArgs.Empty);
        _signals.Structure();
    }

    [RelayCommand]
    private void Go(string section) => Section = Canonical(section);

    /// <summary>
    /// Maps a retired section id onto the page that now holds it (Vessels and Docking live on Berths, Containers and
    /// Yard on Cargo, Activity on Overview), so a DEBUG start hook or an old link still lands in the right place.
    /// </summary>
    public static string Canonical(string section) => section switch
    {
        "vessels" or "docking" => "berths",
        "containers" or "yard" => "cargo",
        "activity" => "overview",
        _ => section
    };

    [RelayCommand]
    private void OpenInspection()
    {
        SecurityTab = "inspection";
        Section = "security";
    }

    [RelayCommand]
    private void OpenAccess()
    {
        SecurityTab = "access";
        Section = "security";
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

    [ObservableProperty]
    public partial string SecurityTab { get; set; } = Hook("CARGO_SECURITY_TAB") is { Length: > 0 } tab ? tab : "zones";

    partial void OnSecurityTabChanged(string value) => _signals.Structure();
}
