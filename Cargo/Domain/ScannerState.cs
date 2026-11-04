namespace Cargo.Domain;

/// <summary>
/// The container explorer: its stage filter and the box picked, which the explorer details and the inspection scanner
/// shows. How the scanner looks at the box is the presentation's (<c>ScannerCamera</c>).
/// </summary>
public sealed partial class ScannerState : ObservableObject
{
    private readonly PortSignals _signals;

    public ScannerState(PortSignals signals) => _signals = signals;

    /// <summary>The explorer's stage filter ("All", "Inspection", ...).</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = "All";

    partial void OnFilterChanged(string value) => _signals.Structure();

    [ObservableProperty]
    public partial string SelectedContainerId { get; set; } = "AVRU4821934";

    partial void OnSelectedContainerIdChanged(string value) => _signals.Structure();

    public ContainerDef SelectedContainer => PortData.Containers.First(c => c.Id == SelectedContainerId);

    /// <summary>
    /// Filters the explorer by stage. The detail follows the filter: a box the filter hides gives way to the first it
    /// shows (with none shown, the detail keeps its box beside the empty state).
    /// </summary>
    public void FilterBy(string filter)
    {
        Filter = filter;
        if (PortData.ContainersFor(filter).ToList() is { Count: > 0 } shown && shown.All(c => c.Id != SelectedContainerId))
        {
            SelectedContainerId = shown[0].Id;
        }
    }
}
