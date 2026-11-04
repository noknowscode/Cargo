using System.ComponentModel;

namespace Cargo.Presentation;

/// <summary>
/// The dispatcher's frame: the section header, the harbour layer switcher, the tide readout,
/// and the bridge between the router and <see cref="NavigationState.Section"/>. The header navigates
/// declaratively (uen:Navigation.Request); the route it lands on is written back to Section,
/// which frames the harbour. Moves that start inside the store (opening a vessel, an
/// inspection, a harbour tag) set Section, and this model navigates to match.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly INavigator _navigator;
    private readonly IThemeService _themes;
    private readonly ILogger<MainViewModel> _logger;

    // The section the router last reported, so a Section change that came from the router
    // is not navigated a second time.
    private string? _routedSection;

    public MainViewModel(PortState state, INavigator navigator, IRouteNotifier routeNotifier, IDispatcher dispatcher,
        IThemeService themes, ILogger<MainViewModel> logger, OverviewViewModel overview, BerthsViewModel berths, CargoViewModel cargo,
        FleetViewModel fleet, SecurityViewModel security)
    {
        State = state;
        Overview = overview;
        Berths = berths;
        Cargo = cargo;
        Fleet = fleet;
        Security = security;
        _navigator = navigator;
        _themes = themes;
        _logger = logger;

        // The router builds models off the UI thread; the header items are bound to the views, so they are made and
        // changed on the UI thread.
        dispatcher.TryEnqueue(() =>
        {
            Scanner = new ScannerViewModel(state);
            BuildNav();
            BuildLayers();

            state.PropertyChanged += OnStateChanged;
            state.Navigation.PropertyChanged += OnStateChanged;
            state.Stage.PropertyChanged += OnStateChanged;
            state.NeedsYou.PropertyChanged += OnStateChanged;
            // The tooltips' countdowns and the yard's figure move with the clock; once a minute is enough
            state.Ticked += (_, _) =>
            {
                if ((int)(state.NowHours * 60) != _builtMinute)
                {
                    BuildNav();
                }
            };
            routeNotifier.RouteChanged += (_, e) => dispatcher.TryEnqueue(() => OnRouteChanged(e));
        });
    }

    public PortState State { get; }

    public OverviewViewModel Overview { get; }
    public BerthsViewModel Berths { get; }
    public CargoViewModel Cargo { get; }
    public FleetViewModel Fleet { get; }
    public SecurityViewModel Security { get; }

    /// <summary>The Overview greeting and figures sit above the harbour, on Overview only.</summary>
    public bool IsOverview => State.Navigation.Section == "overview";

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public ObservableCollection<LayerItem> Layers { get; } = new();

    /// <summary>The operator on shift, behind the header's avatar.</summary>
    public DutyOperator Operator => PortData.Operator;

    public string AccountName => $"Account and settings: {Operator.ShortName}";

    public string OperatorLine => $"{Operator.Role} · {Operator.Badge} · {Operator.ClockedInText} to {Operator.ShiftEndText}";

    /// <summary>The account menu's reduced-motion toggle, saved between launches (see <see cref="Motion"/>).</summary>
    public bool ReduceMotion
    {
        get => Motion.Requested;
        set
        {
            if (Motion.Requested != value)
            {
                Motion.Requested = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The account menu's theme: 0 System (follow the device, the default), 1 Light, 2 Dark. Uno.Extensions'
    /// theme service applies it to the window and saves it between launches.
    /// </summary>
    public int ThemeIndex
    {
        get => _themes.Theme switch { AppTheme.Light => 1, AppTheme.Dark => 2, _ => 0 };
        set
        {
            var theme = value switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.System };
            if (value is >= 0 and <= 2 && theme != _themes.Theme)
            {
                _ = SetThemeAsync(theme);
            }
        }
    }

    private async Task SetThemeAsync(AppTheme theme)
    {
        try
        {
            await _themes.SetThemeAsync(theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Switching to the {Theme} theme failed", theme);
        }

        OnPropertyChanged(nameof(ThemeIndex));
    }

    /// <summary>The container inspection that covers the whole window while it is open.</summary>
    [ObservableProperty]
    public partial ScannerViewModel? Scanner { get; set; }

    // The nav and the layer switcher each depend on one or two properties, so they listen for
    // those rather than for every structural change in the app.
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NavigationState.Section):
                OnPropertyChanged(nameof(IsOverview));
                BuildNav();
                _ = ShowSectionAsync(State.Navigation.Section);
                break;
            case nameof(PortState.NeedsDecision) or nameof(NeedsYouQueue.Count):
                // A settled decision (the berth, the hold, the driver, the late orders) clears its section's dot
                BuildNav();
                break;
            case nameof(StageState.Layer):
                BuildLayers();
                break;
        }
    }

    /// <summary>
    /// The segments of the app-level route under Main: "Main/security/zones" gives
    /// ["security", "zones"]. The navigator's own route names only the deepest level, and a
    /// bare "Main" is reported between moves, so the full path is the reliable source.
    /// </summary>
    public static string[] RouteSegments(RouteChangedEventArgs e) =>
        (e.Route?.Path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);

    private void OnRouteChanged(RouteChangedEventArgs e)
    {
        if (RouteSegments(e) is not [var section, ..] || PortData.Sections.All(s => s.Id != section))
        {
            return;
        }

        _routedSection = section;
        State.Navigation.Section = section;
    }

    /// <summary>
    /// The route for the store's section. Security carries its tab, so a deep link such as
    /// "Open inspection" lands on the right pane even on the page's first visit, when the
    /// region would otherwise start on its default tab.
    /// </summary>
    public static string RouteFor(PortState state) =>
        state.Navigation.Section == "security" ? $"security/{state.Navigation.SecurityTab}" : state.Navigation.Section;

    private async Task ShowSectionAsync(string section)
    {
        if (section == _routedSection)
        {
            return;
        }

        try
        {
            // Section ids are the route names, registered under Main in App.RegisterRoutes.
            // "./" targets the section region inside Main; without it the route replaces Main itself.
            await _navigator.NavigateRouteAsync(this, $"./{RouteFor(State)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to section {Section} failed", section);
        }
    }

    /// <summary>
    /// Builds the header items on first use, then only refreshes what navigation
    /// actually changes: replacing the collection would recreate every button and its subtree,
    /// a flicker across the header on each move between sections.
    /// </summary>
    private void BuildNav()
    {
        _builtMinute = (int)(State.NowHours * 60);
        if (NavItems.Count == 0)
        {
            foreach (var section in PortData.Sections)
            {
                NavItems.Add(new NavItem { Id = section.Id, Label = section.Label });
            }
        }

        foreach (var item in NavItems)
        {
            var current = State.Navigation.Section == item.Id;
            var needs = State.NeedsYou.SectionNeedsYou(item.Id);

            item.Tooltip = $"{item.Label} · {SectionSummary(State, item.Id)}";
            item.AutomationName = needs ? $"{item.Label}, needs you" : item.Label;
            item.IsCurrent = current;
            item.NeedsYou = needs;
        }
    }

    // The minute the header was last described for
    private int _builtMinute = -1;

    /// <summary>
    /// A section's tooltip: what it is worth looking at for, read from the store as the screens read it (the
    /// countdowns as the tags and rows count, the plan's berths, the yard as it works, the queue as it stands).
    /// </summary>
    public static string SectionSummary(PortState state, string section)
    {
        string Plural(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";

        switch (section)
        {
            case "overview":
                var alongside = PortData.Vessels.Count(v => v.Status is VesselStatus.Docked or VesselStatus.Departing);
                var inbound = PortData.Vessels.Count(v => v.Status is VesselStatus.Arriving or VesselStatus.AtAnchor);
                return $"{alongside} alongside · {inbound} inbound · {ShiftTimeline.MovesToday(state.NowHours):N0} moves today";

            case "berths":
                var parts = new List<string>();
                if (PortData.Vessels.Where(v => v.Status == VesselStatus.Arriving)
                        .OrderBy(v => ShiftClock.ClockHours(v.Eta)).FirstOrDefault() is { } arrival)
                {
                    var eta = ShiftClock.ClockHours(arrival.Eta);
                    parts.Add(state.MinutesUntil(eta) > 0 ? $"{arrival.Name} {state.Countdown(eta)}" : $"{arrival.Name} due");
                }

                if (PortData.Vessels.Where(v => v.Status is VesselStatus.Docked or VesselStatus.Departing)
                        .Select(v => (Vessel: v, Etd: ShiftClock.ClockHours(v.Etd)))
                        .Where(v => v.Etd > state.NowHours && state.BerthOf(v.Vessel.Id) is not null)
                        .OrderBy(v => v.Etd).FirstOrDefault() is { Vessel: not null } departure)
                {
                    parts.Add($"Berth {state.BerthOf(departure.Vessel.Id)} frees {ShiftClock.Format(departure.Etd)}");
                }

                if (state.PendingDecision is { } pending)
                {
                    parts.Add($"berth {state.BerthOf(pending)} to confirm");
                }

                return parts.Count == 0 ? "No movements ahead" : string.Join(" · ", parts);

            case "cargo":
                var holds = PortData.Containers.Count(c => c.Security == "Hold");
                return $"{PortData.ContainersOnSite:N0} on site · {state.Yard.Occupancy}% yard · {Plural(holds, "hold", "holds")}";

            case "fleet":
                var late = PortData.RiverFleet.Count(v => v.SlipMinutes > 0);
                var toConfirm = PortData.RiverFleet.Count(state.NeedsYou.AwaitsEta);
                return $"{PortData.RiverFleet.Count} underway · {late} running late"
                    + (toConfirm > 0 ? $" · {Plural(toConfirm, "ETA", "ETAs")} to confirm" : string.Empty);

            case "security":
                var incidents = PortData.SecurityRecords.FirstOrDefault(r => r.Label == "Security incidents")?.Count ?? "0";
                var open = state.NeedsYou.Decisions.Count(d => d.Kind is DecisionKind.InspectionHold or DecisionKind.DriverCheck);
                return $"ISPS level {PortData.IspsLevel} · {incidents} {(incidents == "1" ? "incident" : "incidents")}"
                    + (open > 0 ? $" · {Plural(open, "check", "checks")} waiting" : string.Empty);

            default:
                return string.Empty;
        }
    }

    /// <summary>The current layer's place in <see cref="Layers"/>, for the switcher's sliding indicator.</summary>
    [ObservableProperty]
    public partial int SelectedLayerIndex { get; set; } = -1;

    /// <summary>The design's 24-unit stroke icons for the four layers (EXTRACTION §2 Harbour).</summary>
    private static readonly (string Id, string Label, string Icon)[] LayerDefinitions =
    {
        ("port", "Map", "M9 4 3.5 6v14L9 18l6 2 5.5-2V4L15 6zM9 4v14M15 6v14"),
        ("yard", "Yard", "M4.5 7h15a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1h-15a1 1 0 0 1-1-1V8a1 1 0 0 1 1-1zM3.5 12.5h17M9.2 7v11M14.8 7v11"),
        ("security", "Security", "M12 3.2 19 6v5.2c0 4.4-2.9 7.5-7 8.9-4.1-1.4-7-4.5-7-8.9V6z"),
        ("traffic", "Traffic", "M4 12h15M14 7l5 5-5 5")
    };

    private void BuildLayers()
    {
        if (Layers.Count == 0)
        {
            foreach (var (id, label, icon) in LayerDefinitions)
            {
                Layers.Add(new LayerItem { Id = id, Label = label, IconData = icon, Command = State.Stage.SetLayerCommand });
            }
        }

        for (var i = 0; i < Layers.Count; i++)
        {
            var current = State.Stage.Layer == Layers[i].Id;
            Layers[i].IsCurrent = current;
            if (current)
            {
                SelectedLayerIndex = i;
            }
        }
    }
}
