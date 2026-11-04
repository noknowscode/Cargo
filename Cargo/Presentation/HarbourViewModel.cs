using Cargo.Controls.Harbour;

namespace Cargo.Presentation;

/// <summary>One vessel's data tag on the harbour, bound by <c>HarbourTagTemplate</c>; states only, the view picks the chrome.</summary>
/// <remarks>
/// Every value comes through the constructor rather than <c>required init</c> members: the WinAppSDK XAML compiler
/// writes <c>new HarbourTag()</c> into its type metadata for any type with a parameterless constructor, and a type
/// with required members fails there (CS9035). With no parameterless constructor it writes no activator.
/// </remarks>
public sealed partial class HarbourTag(Vessel vessel, ICommand toggle, ICommand open, ICommand hover) : ObservableObject
{
    public string Id { get; } = vessel.Id;
    public string Name { get; } = vessel.Name;

    /// <summary>Hours on the app clock when the vessel arrives, for the dark tag's countdown.</summary>
    public double EtaHours { get; } = ShiftClock.ClockHours(vessel.Eta);

    /// <summary>The docked facts card's figures.</summary>
    public IReadOnlyList<Fact> Facts { get; } = new[]
    {
        new Fact { Key = "Arrival", Value = vessel.Eta },
        new Fact { Key = "Departure", Value = vessel.Etd },
        new Fact { Key = "Length · draft", Value = $"{vessel.Length} m · {vessel.Draft} m" },
        new Fact { Key = "Security", Value = vessel.Security }
    };

    public ICommand Toggle { get; } = toggle;
    public ICommand Open { get; } = open;
    public ICommand Hover { get; } = hover;

    /// <summary>The light tag's status, by the berth the plan gives her: "B04 · discharging".</summary>
    [ObservableProperty]
    public partial string Sub { get; set; } = string.Empty;

    /// <summary>The dark tag's second line for the arrival waiting on its berth: "B07 · pilot aboard".</summary>
    [ObservableProperty]
    public partial string PendingSub { get; set; } = string.Empty;

    /// <summary>The tag for a screen reader: her name and her status line as the plan stands.</summary>
    [ObservableProperty]
    public partial string AccessibleName { get; set; } = string.Empty;

    /// <summary>"in 41 min" on the dark tag.</summary>
    [ObservableProperty]
    public partial string Countdown { get; set; } = string.Empty;

    /// <summary>The arrival still waiting on a berth decision: she wears the dark tag; confirmed, a light one.</summary>
    [ObservableProperty]
    public partial bool IsPending { get; set; }

    /// <summary>The vessel picked on the harbour (or worked on Berths): a 2 px ink edge.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Pointed at from a list row, another tag or the needs-you bar: a 1 px ink edge.</summary>
    [ObservableProperty]
    public partial bool IsLit { get; set; }
}

/// <summary>
/// What the harbour stage says over its 3D scene: a data tag per vessel (its words, its countdown, pending, selected or
/// lit), the selected vessel's facts card, the layer and selection the scene shows for the section, and the one-line
/// description a screen reader hears. <see cref="Controls.Harbour.HarbourView"/> keeps the camera, the input and the
/// drawing, and calls <see cref="Describe"/>, <see cref="Paint"/> and <see cref="Tick"/> as the store changes while it is
/// on screen. Created by <see cref="MainViewModel"/>.
/// </summary>
public sealed partial class HarbourViewModel : ObservableObject
{
    public HarbourViewModel(PortState state)
    {
        State = state;
        Tags = PortData.Vessels
            .Select(v => new HarbourTag(v, state.PickHullVesselCommand, state.OpenVesselCommand, state.Stage.HoverCommand))
            .ToArray();
        FollowStage();
        Tick();
        Paint();

        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PortState.PendingDecision))
            {
                Paint();
                PendingChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (e.PropertyName is nameof(PortState.SelectedVesselId))
            {
                ReframeRequested?.Invoke(this, EventArgs.Empty);
            }
        };
        state.Navigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(NavigationState.Section))
            {
                ReframeRequested?.Invoke(this, EventArgs.Empty);
            }
        };
        state.Stage.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(StageState.Compact) or nameof(StageState.Expanded) or nameof(StageState.MastheadShown))
            {
                FollowStage();
            }

            if (e.PropertyName is nameof(StageState.Open))
            {
                ReframeRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public PortState State { get; }

    /// <summary>The arrival waiting on a berth changed (the tags are repainted): the scene's marker follows.</summary>
    public event EventHandler? PendingChanged;

    /// <summary>The section, the open strip or the vessel worked on Berths changed: the camera frames what is now selected.</summary>
    public event EventHandler? ReframeRequested;

    /// <summary>A tag per vessel, in the data's order.</summary>
    public IReadOnlyList<HarbourTag> Tags { get; }

    /// <summary>Berths has its own selection (the vessel being worked); elsewhere the harbour's selection applies.</summary>
    public string? Selection => State.Navigation.Section == "berths" ? State.SelectedVesselId : State.Stage.Selection;

    /// <summary>Security shows the perimeter and Cargo the yard zones; on Overview the layer switcher decides.</summary>
    public string Layer => State.Navigation.Section switch
    {
        "security" => "security",
        "cargo" => "yard",
        _ => State.Stage.Layer
    };

    // ── The stage's frame ────────────────────────────────────────────────────

    /// <summary>The same harbour as a strip above a section: no HUD, no facts card, framed by the section.</summary>
    [ObservableProperty]
    public partial bool Compact { get; private set; }

    /// <summary>
    /// Overview's band height below its overlay: 340, or 491 expanded (the design), or the 180 mini while scrolled
    /// (SPEC-OVERVIEW-SCROLL). NaN elsewhere: the stage sizes itself.
    /// </summary>
    [ObservableProperty]
    public partial double StageHeight { get; private set; } = double.NaN;

    /// <summary>The greeting and figures overlay the band's top (Overview at its top); folded, the band's reach goes with them.</summary>
    [ObservableProperty]
    public partial bool OverlayShown { get; private set; }

    /// <summary>A short frame (a section's strip, Overview's mini band): no HUD, no facts card, tight tag insets.</summary>
    public bool Tight => Compact || StageHeight <= HarbourBand.Mini;

    /// <summary>The stage's toggle: Overview grows or restores its band, another section opens or closes its strip.</summary>
    public string ToggleLabel => State.Navigation.Section == "overview"
        ? State.Stage.OverviewCondensed ? "Show harbour" : State.Stage.OverviewExpanded ? "Collapse" : "Expand"
        : State.Stage.Open ? "Collapse harbour" : "Expand harbour";

    partial void OnCompactChanged(bool value) => OnPropertyChanged(nameof(Tight));

    partial void OnStageHeightChanged(double value) => OnPropertyChanged(nameof(Tight));

    /// <summary>The stage moved (a section, its strip, Overview's band): the frame, the toggle and the facts card follow.</summary>
    private void FollowStage()
    {
        Compact = State.Stage.Compact;
        StageHeight = State.Navigation.Section != "overview" ? double.NaN
            : State.Stage.OverviewCondensed ? HarbourBand.Mini
            : State.Stage.OverviewExpanded ? HarbourBand.Full
            : HarbourBand.Collapsed;
        OverlayShown = State.Stage.MastheadShown;
        OnPropertyChanged(nameof(ToggleLabel));
        Paint();
    }

    /// <summary>The vessel the docked facts card describes; kept while the card fades out so it never blanks.</summary>
    [ObservableProperty]
    public partial HarbourTag? SelectedTag { get; set; }

    /// <summary>The status line the selected vessel's tag is showing (its pending line while a decision waits).</summary>
    [ObservableProperty]
    public partial string SelectedLine { get; set; } = string.Empty;

    /// <summary>Whether the facts card is up: a vessel is selected and the frame has room for it.</summary>
    [ObservableProperty]
    public partial bool HasSelectedTag { get; set; }

    /// <summary>
    /// The tags' words as the plan stands: a drag or the optimiser moves an arrival's berth, and her tag, the docked
    /// card and the screen reader's line say so. (The 3D ships stay at the data's berths: the harbour is the data's
    /// picture of 20:58.)
    /// </summary>
    public void Describe()
    {
        foreach (var tag in Tags)
        {
            var vessel = PortData.Vessel(tag.Id);
            var words = VesselCopy.Words(State, vessel);
            tag.Sub = words.Tag;
            tag.PendingSub = $"B{State.BerthOf(vessel.Id)} · pilot aboard";
            tag.AccessibleName = $"{vessel.Name}, {words.StatusLine}";
        }
    }

    /// <summary>"in 41 min", "in 1 h 55": minutes only, so the dark tag changes once a minute; an arrival's line counts down too.</summary>
    public void Tick()
    {
        foreach (var tag in Tags)
        {
            tag.Countdown = State.Countdown(tag.EtaHours);
        }

        Describe();
    }

    /// <summary>Which tag is dark, selected or lit, and the facts card that follows the selection.</summary>
    public void Paint()
    {
        HarbourTag? shown = null;
        var selection = Selection;
        foreach (var tag in Tags)
        {
            tag.IsPending = State.PendingDecision == tag.Id;
            tag.IsSelected = selection == tag.Id;
            tag.IsLit = State.Stage.HoveredVessel == tag.Id;
            if (tag.IsSelected && !Tight)
            {
                shown = tag;
                SelectedLine = tag.IsPending ? tag.PendingSub : tag.Sub;
            }
        }

        if (shown is not null)
        {
            SelectedTag = shown;
        }

        HasSelectedTag = shown is not null;
    }

    /// <summary>
    /// The order tags are placed in: the arrival waiting on a decision first, then the selected vessel, then left to
    /// right by <paramref name="x"/>, so a tag that gives way (<see cref="Controls.Harbour.TagLayout"/>) is never the
    /// dark tag with its countdown.
    /// </summary>
    public IEnumerable<HarbourTag> PlacementOrder(Func<HarbourTag, double> x)
    {
        var pending = State.PendingDecision;
        var selection = Selection;
        return Tags.OrderBy(t => t.Id == pending ? 0 : t.Id == selection ? 1 : 2).ThenBy(x);
    }

    /// <summary>The camera preset's name, as the menu button and the description say it; a drag or a zoom is Custom.</summary>
    public static string ViewName(string? preset) => preset switch
    {
        "overview" => "Overview",
        "sea" => "From sea",
        "land" => "From land",
        "plan" => "Plan",
        _ => "Custom"
    };

    /// <summary>
    /// The canvas is opaque to assistive tech, so the harbour describes itself in one line: the camera view, the
    /// layer, what is alongside and what is selected.
    /// </summary>
    public string Summary(string viewName)
    {
        var alongside = PortData.Alongside;
        var arriving = PortData.Vessels.Count(v => v.Status == VesselStatus.Arriving);
        var selected = PortData.Vessels.FirstOrDefault(v => v.Id == Selection);
        var layer = Layer switch { "yard" => "Yard", "security" => "Security", "traffic" => "Traffic", _ => "Map" };
        return $"{viewName} view, {layer} layer. {alongside} vessels alongside, {arriving} arriving."
            + (selected is null ? string.Empty : $" Selected: {selected.Name}, {VesselCopy.StatusLine(State, selected)}.");
    }
}
