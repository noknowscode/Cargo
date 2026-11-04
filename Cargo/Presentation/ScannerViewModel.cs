namespace Cargo.Presentation;

public sealed class ManifestRow
{
    public required string Name { get; init; }

    /// <summary>The item's imaging class, as its palette key.</summary>
    public required string Tone { get; init; }
    public required string Flag { get; init; }

    /// <summary>The scan flagged the item: the flag reads in the anomaly colour, else sea green.</summary>
    public required bool Flagged { get; init; }
}

public sealed class ScanViewOption
{
    public required string Label { get; init; }
    public required System.Windows.Input.ICommand Select { get; init; }

    /// <summary>The box is turned to this preset: a lit ground.</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>The preset for a screen reader: "Front view", with "current" when the box is turned to it.</summary>
    public required string AccessibleName { get; init; }
}

/// <summary>
/// The full-window container inspection: verdict, manifest, density and the 3D view's
/// presets. The overlay stays in the shell behind <see cref="ScannerCamera.Open"/>, so
/// this model does its work only while the scanner is open. Created by
/// <see cref="MainViewModel"/> on the UI thread.
/// </summary>
public sealed partial class ScannerViewModel : ObservableObject
{
    private static readonly (string Label, double Rx, double Ry)[] ViewAngles =
    {
        ("Iso", -18, -32), ("Front", 0, 0), ("Side", 0, -90), ("Top", -88, 0)
    };

    private int _currentView = -2;

    public ScannerViewModel(PortState state, ScannerCamera camera)
    {
        State = state;
        Camera = camera;
        Refresh();
        state.Scanner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ScannerState.SelectedContainerId))
            {
                Refresh();
            }
        };
        camera.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ScannerCamera.Xray) or nameof(ScannerCamera.Open))
            {
                Refresh();
            }
            else if (e.PropertyName is nameof(ScannerCamera.RotationX) or nameof(ScannerCamera.RotationY))
            {
                // A drag changes the angles on every pointer move; only the preset highlight depends on them
                RefreshViews();
            }
        };
    }

    public PortState State { get; }

    /// <summary>How the scanner looks at the box: open, X-ray, rotation and zoom.</summary>
    public ScannerCamera Camera { get; }

    public ObservableCollection<Fact> ScanFacts { get; } = new();
    public ObservableCollection<ManifestRow> Manifest { get; } = new();
    public ObservableCollection<CompositionSlice> Density { get; } = new();
    public ObservableCollection<ScanViewOption> Views { get; } = new();

    [ObservableProperty] public partial string ContainerId { get; set; } = string.Empty;
    [ObservableProperty] public partial string ContainerMeta { get; set; } = string.Empty;
    [ObservableProperty] public partial string ScanMode { get; set; } = string.Empty;
    [ObservableProperty] public partial string Verdict { get; set; } = string.Empty;
    [ObservableProperty] public partial string VerdictNote { get; set; } = string.Empty;
    /// <summary>The verdict's dot, as the container's security palette key.</summary>
    [ObservableProperty] public partial string VerdictTone { get; set; } = "AccentColor";
    [ObservableProperty] public partial string ManifestHeading { get; set; } = string.Empty;
    [ObservableProperty] public partial string ScanAction { get; set; } = string.Empty;

    /// <summary>The scanner images in X-ray: the toggle lights.</summary>
    [ObservableProperty] public partial bool IsXray { get; set; }
    [ObservableProperty] public partial string XrayName { get; set; } = "X-ray, off";
    [ObservableProperty] public partial IReadOnlyList<(double Weight, string Tone)> DensitySlices { get; set; } = [];

    [RelayCommand]
    private void Close() => Camera.Open = false;

    [RelayCommand]
    private void ToggleXray() => Camera.Xray = !Camera.Xray;

    [RelayCommand]
    private void OpenInspection()
    {
        Camera.Open = false;
        State.Navigation.OpenInspectionCommand.Execute(null);
    }

    [RelayCommand]
    private void ShowView(string? label)
    {
        if (ViewAngles.FirstOrDefault(v => v.Label == label) is not { Label: not null } view)
        {
            return;
        }

        Camera.Turn(view.Rx, view.Ry);
    }

    [RelayCommand]
    private void ZoomIn() => Camera.ZoomIn();

    [RelayCommand]
    private void ZoomOut() => Camera.ZoomOut();

    [RelayCommand]
    private void ResetView()
    {
        Camera.ResetView();
    }

    public void Refresh()
    {
        // The overlay stays in the tree behind a Visibility flag, so skip the work while shut
        if (!Camera.Open)
        {
            return;
        }

        var container = State.Scanner.SelectedContainer;

        ContainerId = container.DisplayId;
        ContainerMeta = $"{container.Size} · {container.Carrier} · {container.Route}";

        ScanMode = Camera.Xray ? "X-RAY · 160 kV · SCANNING" : "OPTICAL · 3D INSPECTION";
        IsXray = Camera.Xray;
        XrayName = Camera.Xray ? "X-ray, on" : "X-ray, off";

        Verdict = container.Warn ? "Anomaly detected" : container.Stage == 3 ? "Under review" : "Clear";
        VerdictTone = container.SecurityToken;
        VerdictNote = container.Warn
            ? "Dense unlisted mass at bay 3 · measured weight +1.8 t over manifest. Physical inspection required before release."
            : container.Stage == 3
                ? "Documentary review in progress · imaging consistent with manifest."
                : "Imaging consistent with manifest · seal intact · weight within tolerance.";
        ScanAction = container.Warn ? "Open inspection →" : "Log scan →";

        var manifest = PortData.Manifests.TryGetValue(container.Id, out var listed)
            ? listed
            : PortData.DefaultManifest;

        ManifestHeading = $"MANIFEST · {manifest.Count} ITEMS";

        Manifest.Clear();
        foreach (var item in manifest)
        {
            Manifest.Add(new ManifestRow
            {
                Name = item.Name,
                Tone = item.TintToken,
                Flag = item.Flagged ? "FLAG" : "ok",
                Flagged = item.Flagged
            });
        }

        BuildDensity(manifest);

        ScanFacts.Clear();
        foreach (var (key, value) in new[]
                 {
                     ("Seal", container.Seal),
                     ("Gross weight", container.Weight),
                     ("Customs", container.Customs),
                     ("Inspection", container.Inspection),
                     ("Vessel", container.Vessel),
                     ("Yard slot", container.YardSlot)
                 })
        {
            ScanFacts.Add(new Fact { Key = key, Value = value });
        }

        _currentView = -2;
        RefreshViews();
    }

    /// <summary>Rebuilds the view presets only when the highlighted preset actually changes.</summary>
    private void RefreshViews()
    {
        if (!Camera.Open)
        {
            return;
        }

        var current = Array.FindIndex(ViewAngles, v =>
            Math.Abs(Camera.RotationX - v.Rx) < 0.5 && Math.Abs(Camera.RotationY - v.Ry) < 0.5);
        if (current == _currentView)
        {
            return;
        }

        _currentView = current;
        Views.Clear();
        for (var i = 0; i < ViewAngles.Length; i++)
        {
            Views.Add(new ScanViewOption
            {
                Label = ViewAngles[i].Label,
                IsCurrent = i == current,
                Select = ShowViewCommand,
                AccessibleName = $"{ViewAngles[i].Label} view" + (i == current ? ", current" : string.Empty)
            });
        }
    }

    private void BuildDensity(IReadOnlyList<CargoItem> manifest)
    {
        (string Label, string Token)[] groups =
        {
            ("Organic", "ScanOrganicColor"),
            ("Metallic", "ScanMetallicColor"),
            ("Mixed / low density", "ScanMixedColor"),
            ("Anomaly · dense", "ScanAnomalyColor")
        };

        Density.Clear();
        var slices = new List<(double, string)>();

        foreach (var (label, token) in groups)
        {
            var count = manifest.Count(item => item.TintToken == token);
            if (count == 0)
            {
                continue;
            }

            var percent = (int)Math.Round(count * 100d / manifest.Count);
            Density.Add(new CompositionSlice
            {
                Label = label,
                Fill = token,
                Count = $"{percent}%"
            });
            slices.Add((percent, token));
        }

        DensitySlices = slices;
    }
}
