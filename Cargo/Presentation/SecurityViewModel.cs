using Windows.UI;

namespace Cargo.Presentation;

public sealed class RecordRow
{
    public required string Label { get; init; }
    public required string Count { get; init; }
    public required Brush Tone { get; init; }
}

public sealed class PersonCard
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required string Credential { get; init; }
    public required string Initials { get; init; }
    public required Brush Avatar { get; init; }
    public required Brush Border { get; init; }
    public required ICommand Select { get; init; }

    /// <summary>The card for a screen reader: who, their role, and whether their zones are the ones on the map.</summary>
    public required string AccessibleName { get; init; }
}

public sealed class VehicleCard
{
    public required Color Tint { get; init; }
    public required IReadOnlyList<Fact> Facts { get; init; }
}

/// <summary>One in-page tab; <see cref="Route"/> is its nested route under the page.</summary>
public sealed class PageTab
{
    public required string Label { get; init; }
    public required string Route { get; init; }
    public required Brush Background { get; init; }
    public required Brush Foreground { get; init; }

    /// <summary>The tab for a screen reader, with "current" on the one shown (the strip shows it by its raised ground).</summary>
    public required string AccessibleName { get; init; }
}

/// <summary>
/// Security: zones, access control and inspection, each a nested route in the page's
/// Visibility region. The tab buttons navigate declaratively; the route they land on is written
/// back to <see cref="PortState.SecurityTab"/>, and a tab set from elsewhere in the store (the
/// "Open inspection" deep link) navigates the region to match.
/// </summary>
public sealed partial class SecurityViewModel : ObservableObject
{
    public static readonly (string Id, string Label)[] TabDefinitions =
    {
        ("zones", "Zones"), ("access", "Access control"), ("inspection", "Inspection")
    };

    private readonly INavigator _navigator;
    private readonly ILogger<SecurityViewModel> _logger;

    // The tab the router last reported, so a change that came from the router is not navigated again
    private string? _routedTab;

    public SecurityViewModel(PortState state, INavigator navigator, IRouteNotifier routeNotifier,
        IDispatcher dispatcher, ILogger<SecurityViewModel> logger)
    {
        State = state;
        _navigator = navigator;
        _logger = logger;

        // The router builds models off the UI thread; the rows carry brushes, which must be
        // created on it.
        dispatcher.TryEnqueue(() =>
        {
            BuildStatic();
            BuildTabs();
            BuildPeople();

            state.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(PortState.SecurityTab):
                        BuildTabs();
                        _ = ShowTabAsync(state.SecurityTab);
                        break;
                    case nameof(PortState.SelectedPerson):
                        BuildPeople();
                        break;
                }
            };
            routeNotifier.RouteChanged += (_, e) => dispatcher.TryEnqueue(() => OnRouteChanged(e));
        });
    }

    public PortState State { get; }

    public ObservableCollection<PageTab> Tabs { get; } = new();
    public ObservableCollection<PersonCard> People { get; } = new();

    [ObservableProperty]
    public partial IReadOnlyList<CheckRow> ClearedChecks { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<CheckRow> HeldChecks { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<RecordRow> Records { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<CheckRow> InspectionLog { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<VehicleCard> Vehicles { get; set; } = [];

    [ObservableProperty]
    public partial string ZoneHeading { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ZoneCount { get; set; } = string.Empty;

    /// <summary>The zones the selected person's badge opens, for the access map.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<int> AllowedZones { get; set; } = [];

    [RelayCommand]
    private void SelectPerson(int index) => State.SelectedPerson = index;

    // The inspection sheet's two actions. Each is logged with the time and then stands done: the button disables
    // and says what happened (an inspection is assigned or a re-weigh requested once)

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AssignInspectorCommand))]
    public partial bool InspectorAssigned { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestReweighCommand))]
    public partial bool ReweighRequested { get; set; }

    public string AssignLabel => InspectorAssigned ? "Inspector assigned" : "Assign inspector";

    public string ReweighLabel => ReweighRequested ? "Re-weigh requested" : "Request re-weigh";

    partial void OnInspectorAssignedChanged(bool value) => OnPropertyChanged(nameof(AssignLabel));

    partial void OnReweighRequestedChanged(bool value) => OnPropertyChanged(nameof(ReweighLabel));

    [RelayCommand(CanExecute = nameof(CanAssignInspector))]
    private void AssignInspector()
    {
        InspectorAssigned = true;
        Log("→", "Inspector assigned: Priya Nair (customs), lane 2, for the 21:15 inspection");
    }

    private bool CanAssignInspector() => !InspectorAssigned;

    [RelayCommand(CanExecute = nameof(CanRequestReweigh))]
    private void RequestReweigh()
    {
        ReweighRequested = true;
        Log("→", "Re-weigh requested on weighbridge 1 · driver of TRK 3391 notified");
    }

    private bool CanRequestReweigh() => !ReweighRequested;

    private void Log(string mark, string text) =>
        InspectionLog = InspectionLog
            .Append(new CheckRow { Mark = mark, Label = text, Time = PortState.Format(State.NowHours % 24), Tone = Tokens.Brush("AccentBrush") })
            .ToList();

    private void OnRouteChanged(RouteChangedEventArgs e)
    {
        if (MainViewModel.RouteSegments(e) is not ["security", var tab, ..] || TabDefinitions.All(t => t.Id != tab))
        {
            return;
        }

        _routedTab = tab;
        State.SecurityTab = tab;
    }

    private async Task ShowTabAsync(string tab)
    {
        if (tab == _routedTab)
        {
            return;
        }

        try
        {
            // This model shares Main's navigator (it is created with MainViewModel), so the tab is
            // addressed through its section
            await _navigator.NavigateRouteAsync(this, $"./security/{tab}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to security tab {Tab} failed", tab);
        }
    }

    private void BuildTabs()
    {
        Tabs.Clear();
        foreach (var (id, label) in TabDefinitions)
        {
            var current = State.SecurityTab == id;
            Tabs.Add(new PageTab
            {
                Label = label,
                Route = $"./{id}",
                Background = current ? Tokens.Brush("SurfaceBrush") : Tokens.Transparent,
                Foreground = current ? Tokens.Brush("InkBrush") : Tokens.Brush("TextMutedBrush"),
                AccessibleName = current ? $"{label}, current tab" : label
            });
        }
    }

    private void BuildPeople()
    {
        People.Clear();
        for (var i = 0; i < PortData.People.Count; i++)
        {
            var person = PortData.People[i];
            People.Add(new PersonCard
            {
                Index = i,
                Name = person.Name,
                Role = person.Role,
                Credential = person.Credential,
                Initials = string.Concat(person.Name.Split(' ').Select(part => part[0])),
                Avatar = Tokens.Brush(person.AvatarToken),
                Border = State.SelectedPerson == i ? Tokens.Brush("InkBrush") : Tokens.Brush("HairlineBrush"),
                Select = SelectPersonCommand,
                AccessibleName = $"{person.Name}, {person.Role}" + (State.SelectedPerson == i ? ", zones shown" : string.Empty)
            });
        }

        var selected = PortData.People[State.SelectedPerson];
        ZoneHeading = $"{selected.Name} · authorized zones";
        ZoneCount = $"{selected.Zones.Count} of {PortData.AccessZones.Count}";
        AllowedZones = selected.Zones;
    }

    private void BuildStatic()
    {
        ClearedChecks = new[]
        {
            "Seal verified", "Manifest matched", "Customs cleared", "X-ray inspection complete"
        }.Select(label => new CheckRow
        {
            Mark = "✓",
            Label = label,
            Time = string.Empty,
            Tone = Tokens.Brush("AccentBrush")
        }).ToList();

        HeldChecks = new[]
        {
            ("✓", "Seal verified", "AccentBrush"),
            ("✓", "Manifest matched", "AccentBrush"),
            ("⚠", "Weight variance +1.8 t · manual inspection required", "OrangeInkBrush")
        }.Select(entry => new CheckRow
        {
            Mark = entry.Item1,
            Label = entry.Item2,
            Time = string.Empty,
            Tone = Tokens.Brush(entry.Item3)
        }).ToList();

        Records = PortData.SecurityRecords
            .Select(r => new RecordRow { Label = r.Label, Count = r.Count, Tone = Tokens.Brush(r.Tone) })
            .ToList();

        // The marks are text: a status colour that is too light for text takes its ink variant
        InspectionLog = PortData.InspectionLog
            .Select(l => new CheckRow { Mark = l.Mark, Label = l.Text, Time = l.Time, Tone = Tokens.Brush(l.Tone == "OrangeColor" ? "OrangeInkBrush" : l.Tone) })
            .ToList();

        Vehicles = PortData.Vehicles.Select(v => new VehicleCard
        {
            Tint = Tokens.Paint(v.TintToken),
            Facts = new[]
            {
                new Fact { Key = "Truck", Value = v.Id },
                new Fact { Key = "Driver", Value = v.Driver },
                new Fact { Key = "Container", Value = v.Container },
                new Fact { Key = "Gate", Value = v.Gate },
                new Fact { Key = "Authorization", Value = v.Authorization, Tone = Tokens.Brush(v.AuthToken) },
                new Fact { Key = "Entry", Value = v.Entry }
            }
        }).ToList();
    }
}
