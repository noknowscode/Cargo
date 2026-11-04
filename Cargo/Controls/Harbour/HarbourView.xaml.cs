using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;

namespace Cargo.Controls.Harbour;

/// <summary>
/// The harbour stage: the 3D scene, the data tags that ride its vessels, and the camera HUD. What the tags, the facts
/// card and the screen-reader line say is its view model's (<see cref="HarbourViewModel"/>); this view keeps the camera,
/// the input, the band's motion and the per-frame placement of the tags over the projection.
/// </summary>
// xaml-lint: allow codebehind - Skia glue: tag positions follow the camera every frame; presets drive the scene camera
public sealed partial class HarbourView : UserControl
{
    private readonly HarbourScene? _scene;
    private readonly HarbourBand _band;
    private readonly HarbourTagHosts _tags;
    private HarbourViewModel? _model;
    private string? _selection;
    private bool _introPlayed;

    public HarbourView()
    {
        InitializeComponent();
        _band = new HarbourBand(OnBandStep);
        _tags = new HarbourTagHosts(TagLayer, (DataTemplate)Resources["HarbourTagTemplate"]);

        if (SKCanvasElement.IsSupportedOnCurrentPlatform())
        {
            _scene = new HarbourScene();
            _scene.PoseChanged += (_, _) => PlaceOverlays();
            _scene.Interacted += (_, _) => MarkView(null);
            SceneHost.Child = _scene;
            _scene.AttachInput(SceneHost);
            ApplyStartView();
        }

        SizeChanged += (_, _) =>
        {
            SlideBand();
            PlaceOverlays();
        };
        KeyDown += OnKeyDown;
        Loaded += (_, _) =>
        {
            PlayIntro();
            DispatcherQueue.TryEnqueue(PlaceOverlays);
            // The stage height depends on the window's height, which the Auto row never re-measures for
            if (XamlRoot is { } root)
            {
                root.Changed += OnXamlRootChanged;
            }
        };
        Unloaded += (_, _) => { if (XamlRoot is { } root) { root.Changed -= OnXamlRootChanged; } };
    }

    /// <summary>The section, the open strip or the vessel worked on Berths changed: the camera frames what is now selected.</summary>
    private void Reframe()
    {
        SyncScene();
        RefreshTags();
        Frame(animate: true);
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => InvalidateMeasure();

    /// <summary>The harbour's words and marks; set once, by the page, with the store the scene draws.</summary>
    public HarbourViewModel? ViewModel
    {
        get => _model;
        set
        {
            _model = value;
            if (_scene is not null)
            {
                _scene.State = value?.State;
            }

            if (value is null)
            {
                return;
            }

            var state = value.State;
            _selection = value.Selection;
            _stageHeight = value.StageHeight;
            SyncScene();
            BuildTags();
            this.RebuildWhenVisible(state, OnStructureChanged);
            this.TickWhenVisible(state, value.Tick);
            // Hover outlines draw in the live layer, so a hover never re-bakes the still frame
            this.RepaintWhenVisible(state, () =>
            {
                _scene?.Invalidate();
                value.Paint();
            });
            _scene?.SetNeeds(state.PendingDecision, state.NeedsDecision);
            value.PendingChanged += (_, _) => _scene?.SetNeeds(state.PendingDecision, state.NeedsDecision);
            value.ReframeRequested += (_, _) => Reframe();
            value.PropertyChanged += OnFrameChanged;
            if (value.Compact)
            {
                OnModeChanged();
            }

            Retarget(animate: false);
            Bindings.Update();
        }
    }

    // The band height last framed, so a change knows whether it eases from one band to another
    private double _stageHeight = double.NaN;

    /// <summary>The stage moved: a strip and the full stage frame differently, and the band eases between its heights.</summary>
    // xaml-lint: allow codebehind - the Skia band's tween and camera framing follow the view model's stage; no XAML surface drives them
    private void OnFrameChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HarbourViewModel.Compact):
                OnModeChanged();
                break;
            case nameof(HarbourViewModel.StageHeight):
                var from = _stageHeight;
                _stageHeight = StageHeight;
                Retarget(animate: !double.IsNaN(from) && !double.IsNaN(_stageHeight));
                break;
            case nameof(HarbourViewModel.OverlayShown):
                Retarget(animate: true);
                break;
        }
    }

    private bool Compact => _model?.Compact ?? false;

    /// <summary>Overview's fixed band height; NaN keeps the proportional stage of <see cref="MeasureOverride"/>.</summary>
    private double StageHeight => _model?.StageHeight ?? double.NaN;

    private bool Tight => _model?.Tight ?? false;

    /// <summary>How far the view's top edge sits into the rendered scene right now, in band mode.</summary>
    private double _bandOffset;

    /// <summary>
    /// The height of what overlays the band's top on Overview (the greeting, figures and duty card). The band
    /// reaches that far up behind it: the scene renders this much taller and frames the ships below it
    /// (camera TopInset), so the overlay sits on the far quay and never on a vessel.
    /// </summary>
    public double OverlayHeight
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Retarget(animate: false);
            }
        }
    }

    /// <summary>
    /// Moves the band to its target (<see cref="HarbourBand"/>): the height below the overlay and the reach up behind
    /// it, eased between two band states. The scene renders at full height plus the overlay, framed below it.
    /// </summary>
    private void Retarget(bool animate)
    {
        var band = !double.IsNaN(StageHeight);
        SceneHost.Height = TagLayer.Height = band ? HarbourBand.Full + OverlayHeight : double.NaN;
        SceneHost.VerticalAlignment = TagLayer.VerticalAlignment = band ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        if (_scene is not null && _scene.Camera.TopInset != (band ? OverlayHeight : 0))
        {
            _scene.Camera.TopInset = band ? OverlayHeight : 0;
            _scene.RefreshStill();
        }

        _band.MoveTo(StageHeight, band && _model?.OverlayShown == true ? OverlayHeight : 0, animate, DispatcherQueue);
    }

    /// <summary>One band step: the height and the reach, then everything that follows them, in the same frame.</summary>
    // xaml-lint: allow codebehind - the band's per-frame step (CompositionTarget.Rendering has no XAML surface)
    private void OnBandStep(double height, double inset)
    {
        Height = double.IsNaN(height) ? double.NaN : height + inset;
        Margin = new Thickness(0, -inset, 0, 0);
        SlideBand();
        PlaceOverlays();
    }

    /// <summary>The band's height as set this frame (layout catches up after); otherwise the laid-out height.</summary>
    private double VisibleHeight => !double.IsNaN(StageHeight) && !double.IsNaN(Height) ? Height : ActualHeight;

    /// <summary>Slides the scene within the view (<see cref="HarbourBand.Slide"/>). Called per band step, and on SizeChanged.</summary>
    // xaml-lint: allow responsive - follows the band's height animation frame by frame; no size class or threshold
    private void SlideBand()
    {
        var band = !double.IsNaN(StageHeight);
        var visible = VisibleHeight;
        (_bandOffset, var rest) = HarbourBand.Slide(band, visible, _band.Inset, OverlayHeight);
        // Negative margins keep each layer's slot at its full height: a slot shorter than the layer
        // clips it in its own coordinates, before any offset, which left the band's bottom empty
        SceneHost.Margin = TagLayer.Margin = new Thickness(0, -_bandOffset, 0, -rest);
        _scene?.SetWindow(band ? _bandOffset : double.NaN, _bandOffset + visible, _band.Inset);
    }

    /// <summary>
    /// The full stage (a section's opened strip) keeps a landscape proportion, bounded so it never
    /// swallows the page: at most 58% of the window's height, never less than 300 px. Overview's band
    /// sets its own height (<see cref="StageHeight"/>); the strip is fixed.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0 ? 1328 : availableSize.Width;
        var byWidth = Math.Clamp(width * .42, 380, 640);
        var byWindow = XamlRoot is { } root ? Math.Max(300, root.Size.Height * .58) : byWidth;
        // A band height (Overview), possibly mid-animation, wins over the proportional stage: measuring
        // taller than it lays the HUD and the camera frame out below the visible band
        var size = new Size(width, Compact ? 210 : !double.IsNaN(Height) ? Height : Math.Min(byWidth, byWindow));
        base.MeasureOverride(size);
        return size;
    }

    // ── State ──────────────────────────────────────────────────────────────────

    private string? EffectiveSelection => _model?.Selection;

    private void SyncScene()
    {
        if (_scene is null)
        {
            return;
        }

        _scene.SelectedVessel = EffectiveSelection;
        _scene.Layer = _model?.Layer ?? "port";
    }

    private void OnModeChanged()
    {
        InvalidateMeasure();
        RefreshTags();
        Frame(animate: true);
    }

    private void OnStructureChanged()
    {
        if (_model is null)
        {
            return;
        }

        SyncScene();
        var selection = EffectiveSelection;
        if (selection != _selection)
        {
            _selection = selection;
            if (_scene?.World.Vessels.FirstOrDefault(v => v.Id == selection) is { } vessel && !Compact)
            {
                _scene.FlyTo(vessel.X, vessel.Y);
                // Flown off the preset, as a drag or zoom is: the menu says Custom, so Overview frames it again
                MarkView(null);
            }
        }

        // The still scene is not re-recorded here: it records itself when what it draws changes (camera, size,
        // layer, theme); selection and hover draw live over it
        RefreshTags();
    }

    /// <summary>
    /// Moving between sections moves the camera over the same harbour: the wide shot on
    /// Overview, the selected ship on Berths, the yard on Cargo, the terminal perimeter on Security.
    /// </summary>
    private void Frame(bool animate)
    {
        if (_scene is null || _model is null)
        {
            return;
        }

        var section = _model.State.Navigation.Section;
        var ms = animate ? 420 : 0;

        if (!Compact)
        {
            if (section == "overview")
            {
                _scene.GoTo(HarbourCameraPose.Overview, ms);
                MarkView("overview");
            }

            return;
        }

        var vessel = _scene.World.Vessels.FirstOrDefault(v => v.Id == EffectiveSelection);
        _scene.GoTo(HarbourCameraPose.ForStrip(section, vessel is null ? null : (vessel.X, vessel.Y)), ms);
    }

    // ── Tags ───────────────────────────────────────────────────────────────────

    /// <summary>A host per tag on the tag layer; the placement moves them over the projection each frame.</summary>
    private void BuildTags()
    {
        if (_model is null || _scene is null)
        {
            return;
        }

        _tags.Build(_model.Tags);
        _model.Tick();
        RefreshTags();
    }

    private void RefreshTags()
    {
        _model?.Describe();
        _model?.Paint();
        DispatcherQueue.TryEnqueue(PlaceOverlays);
        UpdateSummary();
    }

    // The tag's chrome: the dark tag for the arrival waiting on a decision, else the light one; selected takes a 2 px
    // ink edge, a linked hover a 1 px one (the float shadow is the resting edge)

    public static Visibility LightTag(bool pending) => pending ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility DarkTag(bool pending) => pending ? Visibility.Visible : Visibility.Collapsed;

    public static Brush TagEdge(bool selected, bool lit) => selected || lit ? Tokens.Brush("InkBrush") : Tokens.Transparent;

    public static Thickness TagEdgeWidth(bool selected) => new(selected ? 2 : 1);

    /// <summary>The HUD floats on the full stage only; a short frame has no room for it.</summary>
    public static Visibility Hud(bool tight) => tight ? Visibility.Collapsed : Visibility.Visible;

    // ── Screen readers ─────────────────────────────────────────────────────────

    private string _viewName = "Overview";

    /// <summary>
    /// The canvas itself is opaque to assistive tech, so the harbour describes itself in one line:
    /// the view, the layer, what is alongside and what is selected. It is the view's description
    /// on focus, and a polite live region announces it when a preset, layer or selection changes.
    /// </summary>
    private void UpdateSummary()
    {
        if (_model?.Summary(_viewName) is not { } text || text == Summary.Text)
        {
            return;
        }

        Summary.Text = text;
        AutomationProperties.SetFullDescription(this, text);
        FrameworkElementAutomationPeer.FromElement(Summary)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    /// <summary>
    /// Each frame: the clip, the tags on their leaders over the projection (<see cref="HarbourTagHosts"/>), the north
    /// needle and the scale bar.
    /// </summary>
    // xaml-lint: allow responsive - ActualWidth/Height clamp tags inside the frame; this is projection, not a breakpoint
    private void PlaceOverlays()
    {
        var visible = VisibleHeight;
        // xaml-lint: allow responsive - a zero-size guard before projecting, not a breakpoint
        if (_scene is null || ActualWidth <= 0 || visible <= 0)
        {
            return;
        }

        Clip = new RectangleGeometry { Rect = new Rect(0, 0, ActualWidth, visible) };
        _scene.SyncCamera();
        var camera = _scene.Camera;
        var world = _scene.World;
        (float X, float Y) Project(string vessel) => camera.Project(world.Vessels.First(v => v.Id == vessel).Mast);
        _scene.Leaders = _model is null
            ? new Dictionary<string, (SKPoint, SKPoint)>()
            : _tags.Place(_model.PlacementOrder(t => Project(t.Id).X), Project,
                new HarbourTagFrame(ActualWidth, visible, _bandOffset, _band.Inset, Tight, Compact));

        Needle.Angle = camera.NorthAngle();
        var (metres, width) = camera.ScaleBar();
        ScaleBar.Width = width;
        ScaleLabel.Text = $"{metres} m";
    }

    // ── Camera controls ────────────────────────────────────────────────────────

    [RelayCommand]
    private void View(string name)
    {
        if (_scene is null)
        {
            return;
        }

        _scene.GoTo(HarbourCameraPose.Preset(name, _scene.Camera.Pose));
        MarkView(name);
    }

    [RelayCommand]
    private void Zoom(string direction)
    {
        _scene?.Nudge(zoomFactor: direction == "in" ? 1.3 : 1 / 1.3);
        MarkView(null);
    }

    private void MarkView(string? name)
    {
        _viewName = HarbourViewModel.ViewName(name);
        UpdateSummary();

        // The camera menu: the current preset is checked and named on the button; a drag or a
        // zoom leaves none current
        OverviewItem.IsChecked = name == "overview";
        SeaItem.IsChecked = name == "sea";
        LandItem.IsChecked = name == "land";
        PlanItem.IsChecked = name == "plan";
        CameraLabel.Text = _viewName;
    }

    // xaml-lint: allow codebehind - keyboard orbit for the Skia canvas; the camera has no XAML surface
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_scene is null)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Left: _scene.Nudge(yawDegrees: 15); break;
            case VirtualKey.Right: _scene.Nudge(yawDegrees: -15); break;
            case VirtualKey.Up: _scene.Nudge(pitchDegrees: 6); break;
            case VirtualKey.Down: _scene.Nudge(pitchDegrees: -6); break;
            case VirtualKey.Add or (VirtualKey)187: _scene.Nudge(zoomFactor: 1.25); break;
            case VirtualKey.Subtract or (VirtualKey)189: _scene.Nudge(zoomFactor: 1 / 1.25); break;
            case VirtualKey.Number0 or VirtualKey.NumberPad0: View("overview"); e.Handled = true; return;
            default: return;
        }

        MarkView(null);
        e.Handled = true;
    }

    /// <summary>A slow swing into the overview shows the harbour is a space you can move through.</summary>
    private void PlayIntro()
    {
        if (_introPlayed || _scene is null || Compact)
        {
            return;
        }

        _introPlayed = true;
        if (!_scene.Animate)
        {
            return;
        }
#if DEBUG
        // A verification run that asked for a preset (ApplyStartView) photographs it without the swing.
        if (DebugHooks.Hook("CARGO_HARBOUR_VIEW") is { Length: > 0 })
        {
            return;
        }
#endif

        var target = _scene.Camera.Pose;
        _scene.Camera.Apply(target with { Yaw = target.Yaw - 40 * Math.PI / 180 });
        _scene.GoTo(target, 700);
    }

    /// <summary>Lets a verification run photograph a preset without synthesised input.</summary>
    [System.Diagnostics.Conditional("DEBUG")]
    private void ApplyStartView()
    {
        var view = DebugHooks.Hook("CARGO_HARBOUR_VIEW");
        if (string.IsNullOrEmpty(view) || _scene is null)
        {
            return;
        }

        _scene.GoTo(HarbourCameraPose.Preset(view, HarbourCameraPose.Overview), 0);
        MarkView(view);
    }
}
