namespace Cargo.Presentation;

public sealed class RecordRow
{
    public required string Label { get; init; }
    public required string Count { get; init; }

    /// <summary>The record's status, as its palette key.</summary>
    public required string Tone { get; init; }
}

public sealed class PersonCard
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required string Credential { get; init; }
    public required string Initials { get; init; }

    /// <summary>The avatar's ground, as its palette key.</summary>
    public required string Avatar { get; init; }

    /// <summary>The badge whose zones the map shows: an ink edge.</summary>
    public required bool IsSelected { get; init; }
    public required ICommand Select { get; init; }

    /// <summary>The card for a screen reader: who, their role, and whether their zones are the ones on the map.</summary>
    public required string AccessibleName { get; init; }
}

public sealed class VehicleCard
{
    public required string Id { get; init; }

    /// <summary>The truck waits at the barrier on its driver's ID: the card offers Verify driver.</summary>
    public required bool AwaitsDriver { get; init; }

    public required ICommand Verify { get; init; }

    /// <summary>The card for a screen reader: the truck, its gate and its authorization.</summary>
    public required string AccessibleName { get; init; }

    /// <summary>The container's livery, as its palette key (paint, the same in both themes).</summary>
    public required string Tint { get; init; }
    public required IReadOnlyList<Fact> Facts { get; init; }
}

/// <summary>One in-page tab; <see cref="Route"/> is its nested route under the page.</summary>
public sealed class PageTab
{
    public required string Label { get; init; }
    public required string Route { get; init; }

    /// <summary>The tab shown: a raised ground and ink.</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>The tab for a screen reader, with "current" on the one shown (the strip shows it by its raised ground).</summary>
    public required string AccessibleName { get; init; }
}

/// <summary>
/// Security: zones, access control and inspection, each a nested route in the page's
/// Visibility region. The tab buttons navigate declaratively; the route they land on is written
/// back to <see cref="NavigationState.SecurityTab"/>, and a tab set from elsewhere in the store (the
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

        // The router builds models off the UI thread; the rows are bound to the views, so they are built on it.
        dispatcher.TryEnqueue(() =>
        {
            BuildStatic();
            BuildTabs();
            BuildPeople();

            state.Navigation.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NavigationState.SecurityTab))
                {
                    BuildTabs();
                    _ = ShowTabAsync(state.Navigation.SecurityTab);
                }
            };
            state.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PortState.SelectedPerson))
                {
                    BuildPeople();
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

    /// <summary>"SECURITY · ISPS LEVEL 1 · 1 HOLD ACTIVE": the level and the holds from the data.</summary>
    public string Eyebrow
    {
        get
        {
            var holds = PortData.Containers.Count(c => c.Security == "Hold");
            return $"SECURITY · ISPS LEVEL {PortData.IspsLevel} · {holds} {(holds == 1 ? "HOLD" : "HOLDS")} ACTIVE";
        }
    }

    public string AssignLabel => State.InspectorAssigned ? "Inspector assigned" : "Assign inspector";

    public string ReweighLabel => State.ReweighRequested ? "Re-weigh requested" : "Request re-weigh";

    /// <summary>Assigns the inspector, which settles the hold's row in the needs-you queue (the hold stays until 21:15).</summary>
    [RelayCommand(CanExecute = nameof(CanAssignInspector))]
    private void AssignInspector()
    {
        if (State.AssignInspector())
        {
            Log("→", "Inspector assigned: Priya Nair (customs), lane 2, for the 21:15 inspection");
        }

        OnPropertyChanged(nameof(AssignLabel));
        AssignInspectorCommand.NotifyCanExecuteChanged();
    }

    private bool CanAssignInspector() => !State.InspectorAssigned;

    [RelayCommand(CanExecute = nameof(CanRequestReweigh))]
    private void RequestReweigh()
    {
        if (State.RequestReweigh())
        {
            Log("→", "Re-weigh requested on weighbridge 1 · driver of TRK 3391 notified");
        }

        OnPropertyChanged(nameof(ReweighLabel));
        RequestReweighCommand.NotifyCanExecuteChanged();
    }

    private bool CanRequestReweigh() => !State.ReweighRequested;

    /// <summary>Verifies the driver of a truck at the barrier, which settles the driver row in the needs-you queue.</summary>
    [RelayCommand]
    private void VerifyDriver(string truckId)
    {
        if (State.VerifyDriver(truckId))
        {
            Log("✓", $"Driver ID verified at the barrier for {truckId} · gate opened");
            BuildVehicles();
        }
    }

    private void Log(string mark, string text) =>
        InspectionLog = InspectionLog
            .Append(new CheckRow { Mark = mark, Label = text, Time = ShiftClock.Format(State.NowHours % 24), Tone = "AccentBrush" })
            .ToList();

    private void OnRouteChanged(RouteChangedEventArgs e)
    {
        if (MainViewModel.RouteSegments(e) is not ["security", var tab, ..] || TabDefinitions.All(t => t.Id != tab))
        {
            return;
        }

        _routedTab = tab;
        State.Navigation.SecurityTab = tab;
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
            var current = State.Navigation.SecurityTab == id;
            Tabs.Add(new PageTab
            {
                Label = label,
                Route = $"./{id}",
                IsCurrent = current,
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
                Avatar = person.AvatarToken,
                IsSelected = State.SelectedPerson == i,
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
            Tone = "AccentBrush"
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
            Tone = entry.Item3
        }).ToList();

        Records = PortData.SecurityRecords
            .Select(r => new RecordRow { Label = r.Label, Count = r.Count, Tone = r.Tone })
            .ToList();

        // The marks are text: a status colour that is too light for text takes its ink variant
        InspectionLog = PortData.InspectionLog
            .Select(l => new CheckRow { Mark = l.Mark, Label = l.Text, Time = l.Time, Tone = l.Tone == "OrangeColor" ? "OrangeInkBrush" : l.Tone })
            .ToList();

        BuildVehicles();
    }

    private void BuildVehicles()
    {
        Vehicles = PortData.Vehicles.Select(v => new VehicleCard
        {
            Id = v.Id,
            Tint = v.TintToken,
            AwaitsDriver = State.AwaitsDriver(v),
            Verify = VerifyDriverCommand,
            AccessibleName = $"{v.Id}, {v.Gate}, {State.AuthorizationOf(v)}",
            Facts = new[]
            {
                new Fact { Key = "Truck", Value = v.Id },
                new Fact { Key = "Driver", Value = v.Driver },
                new Fact { Key = "Container", Value = v.Container },
                new Fact { Key = "Gate", Value = v.Gate },
                new Fact { Key = "Authorization", Value = State.AuthorizationOf(v) },
                new Fact { Key = "Entry", Value = v.Entry }
            }
        }).ToList();
    }
}
