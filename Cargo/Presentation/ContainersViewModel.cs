using Cargo.Controls;

namespace Cargo.Presentation;

public sealed class FilterChip
{
    public required string Label { get; init; }
    public required ICommand Apply { get; init; }

    /// <summary>The filter on now: the pill fills with ink.</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>The chip for a screen reader: its stage, and whether it is the filter on now (shown by its inverse fill).</summary>
    public required string AccessibleName { get; init; }
}

public sealed class ContainerCard
{
    public required string Id { get; init; }
    public required string IdPretty { get; init; }
    public required string Meta { get; init; }
    public required string Route { get; init; }
    public required string State { get; init; }

    /// <summary>The box needs attention (held, late, flagged): its state reads in orange, else in the accent.</summary>
    public required bool Warn { get; init; }

    /// <summary>The box open in the detail: an ink edge.</summary>
    public required bool IsSelected { get; init; }

    /// <summary>The box's render, by sprite name (Assets/Sprites).</summary>
    public required string Render { get; init; }
    public required ICommand Select { get; init; }

    /// <summary>The card for a screen reader: the box, its route and state, and what pressing it does.</summary>
    public required string AccessibleName { get; init; }
}

/// <summary>Where a step of the box's journey stands against the box.</summary>
public enum StepState { Done, Current, Warned, Ahead }

public sealed class JourneyStep
{
    public required string Label { get; init; }

    /// <summary>The step's 24-unit stroke icon, as SVG path data.</summary>
    public required string IconData { get; init; }

    public required StepState State { get; init; }

    /// <summary>The line on to the next step; the last step has none.</summary>
    public required bool IsLast { get; init; }
}

/// <summary>
/// The container explorer: filters, the container cards and the selected box's journey and
/// facts. Created by <see cref="CargoViewModel"/> on the UI thread.
/// </summary>
public sealed partial class ContainersViewModel : ObservableObject
{
    public ContainersViewModel(PortState state)
    {
        State = state;
        Refresh();
    }

    public PortState State { get; }

    public ObservableCollection<FilterChip> Filters { get; } = new();
    public ObservableCollection<ContainerCard> Cards { get; } = new();
    public ObservableCollection<JourneyStep> Journey { get; } = new();
    public ObservableCollection<Fact> Facts { get; } = new();

    [ObservableProperty] public partial string SelectedId { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedMeta { get; set; } = string.Empty;
    /// <summary>The selected box's render, by sprite name (Assets/Sprites).</summary>
    [ObservableProperty] public partial string SelectedIso { get; set; } = string.Empty;

    /// <summary>True when the filter matches no container on the terminal: the explorer says so instead of showing nothing.</summary>
    [ObservableProperty] public partial bool IsEmpty { get; set; }

    [ObservableProperty] public partial string EmptyText { get; set; } = string.Empty;

    [RelayCommand]
    private void Filter(string? value) => State.Scanner.FilterBy(value ?? "All");

    /// <summary>Selects a container and opens it in the scanner at the default view.</summary>
    [RelayCommand]
    private void Inspect(string? id)
    {
        if (id is null)
        {
            return;
        }

        State.Scanner.Inspect(id);
    }

    [RelayCommand]
    private void OpenScanner() => State.Scanner.Open = true;

    public void Refresh()
    {
        var container = State.Scanner.SelectedContainer;

        BuildFilters();
        BuildCards();
        BuildDetail(container);

        SelectedIso = Sprites.ContainerByTone(container.TintToken);
    }

    private void BuildFilters()
    {
        Filters.Clear();
        foreach (var filter in PortData.ContainerFilters)
        {
            var current = State.Scanner.Filter == filter;
            Filters.Add(new FilterChip
            {
                Label = filter,
                IsCurrent = current,
                Apply = FilterCommand,
                AccessibleName = current ? $"{filter}, filter on" : filter
            });
        }
    }

    private void BuildCards()
    {
        Cards.Clear();
        foreach (var container in PortData.ContainersFor(State.Scanner.Filter))
        {
            Cards.Add(new ContainerCard
            {
                Id = container.Id,
                IdPretty = container.DisplayId,
                Meta = $"{container.Size} · {container.Carrier.ToUpperInvariant()}",
                Route = container.Route,
                State = container.State,
                Warn = container.Warn,
                IsSelected = State.Scanner.SelectedContainerId == container.Id,
                Render = Sprites.ContainerByTone(container.TintToken),
                Select = InspectCommand,
                AccessibleName = $"{container.DisplayId}, {container.Route}, {container.State}. Opens the scanner"
            });
        }

        IsEmpty = Cards.Count == 0;
        EmptyText = $"No containers under {State.Scanner.Filter} right now. Choose All to see every box on the terminal.";
    }

    private void BuildDetail(ContainerDef container)
    {
        SelectedId = container.DisplayId;
        SelectedMeta = $"{container.Size} · {container.Carrier} · {container.Route}";

        Journey.Clear();
        for (var i = 0; i < PortData.JourneyStages.Count; i++)
        {
            Journey.Add(new JourneyStep
            {
                Label = PortData.JourneyStages[i],
                IconData = PortData.JourneyIcons[i],
                State = i < container.Stage ? StepState.Done
                    : i > container.Stage ? StepState.Ahead
                    : container.Warn ? StepState.Warned : StepState.Current,
                IsLast = i == PortData.JourneyStages.Count - 1
            });
        }

        Facts.Clear();
        foreach (var (key, value) in new[]
                 {
                     ("Weight", container.Weight),
                     ("Dimensions", container.Dimensions),
                     ("Carrier", container.Carrier),
                     ("Vessel", container.Vessel),
                     ("Origin", container.Origin),
                     ("Destination", container.Destination),
                     ("Yard location", container.YardSlot),
                     ("Customs", container.Customs),
                     ("Inspection", container.Inspection),
                     ("Seal", container.Seal),
                     ("Security", container.Security)
                 })
        {
            Facts.Add(new Fact { Key = key, Value = value });
        }
    }
}
