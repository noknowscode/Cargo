using static Cargo.Domain.DebugHooks;

namespace Cargo.Domain;

/// <summary>
/// The container explorer and its inspection scanner: the filter, the box picked, and the scanner's camera (open,
/// X-ray, rotation and zoom). The explorer, the scanner overlay and its 3D box all follow it.
/// </summary>
public sealed partial class ScannerState : ObservableObject
{
    /// <summary>The scanner's default view, the three-quarter "Iso" preset.</summary>
    public const double IsoX = -18, IsoY = -32;

    public const double MinZoom = 0.6, MaxZoom = 1.8, ZoomStep = 0.15;

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

    [ObservableProperty]
    public partial bool Open { get; set; } = Hook("CARGO_SCANNER") == "1";

    [ObservableProperty]
    public partial bool Xray { get; set; } = Hook("CARGO_XRAY") == "1";

    partial void OnXrayChanged(bool value) => _signals.Structure();

    [ObservableProperty]
    public partial double RotationX { get; set; } = IsoX;

    [ObservableProperty]
    public partial double RotationY { get; set; } = IsoY;

    [ObservableProperty]
    public partial double Zoom { get; set; } = 1;

    /// <summary>Opens a container in the scanner at the default view, optical.</summary>
    public void Inspect(string id)
    {
        SelectedContainerId = id;
        Open = true;
        Xray = false;
        RotationX = IsoX;
        RotationY = IsoY;
    }

    /// <summary>Turns the box to a preset.</summary>
    public void Turn(double rx, double ry)
    {
        RotationX = rx;
        RotationY = ry;
    }

    public void ZoomIn() => Zoom = Math.Min(MaxZoom, Zoom + ZoomStep);

    public void ZoomOut() => Zoom = Math.Max(MinZoom, Zoom - ZoomStep);

    /// <summary>Back to the Iso view at 100%.</summary>
    public void ResetView()
    {
        Zoom = 1;
        Turn(IsoX, IsoY);
    }
}
