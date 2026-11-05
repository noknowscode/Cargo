using static Cargo.Domain.DebugHooks;

namespace Cargo.Presentation;

/// <summary>
/// The inspection scanner's view of the box picked in the container explorer (<see cref="ScannerState"/>): whether the
/// scanner is open, X-ray, the box's rotation and the zoom. View state, shared by the explorer that opens it, the
/// overlay with its presets, and the 3D box that draws and turns it; one for the app, from DI.
/// </summary>
public sealed partial class ScannerCamera : ObservableObject
{
    /// <summary>The scanner's default view, the three-quarter "Iso" preset.</summary>
    public const double IsoX = -18, IsoY = -32;

    public const double MinZoom = 0.6, MaxZoom = 1.8, ZoomStep = 0.15;

    [ObservableProperty]
    public partial bool Open { get; set; } = Hook("CARGO_SCANNER") == "1";

    [ObservableProperty]
    public partial bool Xray { get; set; } = Hook("CARGO_XRAY") == "1";

    [ObservableProperty]
    public partial double RotationX { get; set; } = IsoX;

    [ObservableProperty]
    public partial double RotationY { get; set; } = IsoY;

    [ObservableProperty]
    public partial double Zoom { get; set; } = 1;

    /// <summary>Opens the scanner on the box at the default view, optical.</summary>
    public void Show()
    {
        Open = true;
        Xray = false;
        Turn(IsoX, IsoY);
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
