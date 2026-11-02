using Cargo.Controls;

namespace Cargo.Presentation;

public sealed class PlanRow
{
    public required string Berth { get; init; }

    /// <summary>The row in words for a screen reader: the berth and who holds it when.</summary>
    public required string AccessibleName { get; init; }
    public required IReadOnlyList<ProportionalPanel.Segment> Segments { get; init; }
    public required double NowFraction { get; init; }
}

/// <summary>
/// One berth in the selected vessel's "Move to berth" list: the keyboard and touch path for drag-to-assign. Every
/// berth is listed; one that cannot take her says why in words (not only in colour), and choosing it anyway shows
/// the same refusal a drop would.
/// </summary>
public sealed class BerthChoice
{
    public required string Number { get; init; }
    public required string Caption { get; init; }
    public required string AccessibleName { get; init; }
    public required Brush Edge { get; init; }
    public required Brush CaptionInk { get; init; }
    public required ICommand Assign { get; init; }
}

/// <summary>
/// The berth plan: the timeline, the scrubber over the next 36 hours, conflicts, and the
/// selected vessel's panel. <see cref="Refresh"/> follows structural changes; <see cref="Tick"/>
/// follows the clock. Created by <see cref="BerthsViewModel"/> on the UI thread.
/// </summary>
public sealed partial class DockingViewModel : ObservableObject
{
    public const double PlanHours = 36;

    private const string DefaultHint =
        "Drag an inbound vessel (dashed outline) onto a berth · scrub the timeline to preview the harbour later today.";

    private int _liveMinute = -1;

    public DockingViewModel(PortState state)
    {
        State = state;
        Legend = new[]
        {
            new LegendItem { Label = "Available", Swatch = Tokens.Brush("AccentColor", 0.35) },
            new LegendItem { Label = "Reserved", Swatch = Tokens.Brush("AccentBrush") },
            new LegendItem { Label = "Occupied", Swatch = Tokens.Brush("NavyBrush") },
            new LegendItem { Label = "Restricted", Swatch = Tokens.Brush("RestrictedBrush") },
            new LegendItem { Label = "Conflict", Swatch = Tokens.Brush("AlertBrush") }
        };

        Refresh();
    }

    public PortState State { get; }

    public IReadOnlyList<LegendItem> Legend { get; }

    public ObservableCollection<PlanRow> PlanRows { get; } = new();

    public ObservableCollection<Fact> SelectedFacts { get; } = new();

    public ObservableCollection<BerthChoice> BerthChoices { get; } = new();

    [ObservableProperty] private string _planLabel = string.Empty;
    [ObservableProperty] private string _conflictText = string.Empty;
    [ObservableProperty] private Brush? _conflictTone;
    [ObservableProperty] private string _hint = DefaultHint;
    [ObservableProperty] private string _selectedName = string.Empty;
    [ObservableProperty] private string _selectedStatus = string.Empty;
    [ObservableProperty] private string _selectedNote = string.Empty;
    [ObservableProperty] private ImageSource? _selectedProfile;
    [ObservableProperty] private bool _canRelease;
    [ObservableProperty] private bool _canMove;
    [ObservableProperty] private Brush? _nowBackground;
    [ObservableProperty] private Brush? _nowForeground;

    [ObservableProperty]
    private bool _hasSelection;

    /// <summary>
    /// The scrubber position in hours. A value equal to the time already shown is ignored, so
    /// the slider following the live clock never pins the plan to a fixed hour.
    /// </summary>
    public double Scrub
    {
        get => Math.Clamp(State.EffectivePlanHour, 0, PlanHours);
        set
        {
            if (Math.Abs(value - Scrub) > 0.01)
            {
                State.PlanHour = value;
            }
        }
    }

    [RelayCommand]
    private void Now() => State.PlanHour = null;

    [RelayCommand]
    private void Optimize() => Hint = State.Optimize();

    [RelayCommand]
    private void ClearSelection() => State.DockSelection = null;

    [RelayCommand]
    private void Release()
    {
        if (State.DockSelection is { } id)
        {
            State.Unassign(id);
        }
    }

    /// <summary>Assigns the selected vessel to a berth from the list; a refused move shows its reason in the hint.</summary>
    [RelayCommand]
    private void AssignTo(string berth)
    {
        var index = PortData.Berths.Select(b => b.Number).ToList().IndexOf(berth);
        if (State.DockSelection is { } id && index >= 0)
        {
            Hint = State.Assign(id, index) ?? DefaultHint;
        }
    }

    [RelayCommand]
    private void OpenProfile()
    {
        if (State.DockSelection is { } id)
        {
            State.OpenVesselCommand.Execute(id);
        }
    }

    /// <summary>Only the live view moves with the clock, and the plan shows minutes, so this
    /// refreshes once a minute rather than on every one-second tick.</summary>
    public void Tick()
    {
        var minute = (int)(State.NowHours * 60);
        if (State.PlanHour is null && minute != _liveMinute)
        {
            _liveMinute = minute;
            Refresh();
        }
    }

    public void Refresh()
    {
        var planHour = State.EffectivePlanHour;
        PlanLabel = (planHour >= 24 ? "Thu " : string.Empty) + PortState.Format(planHour % 24);
        OnPropertyChanged(nameof(Scrub));

        NowBackground = State.PlanHour is null ? Tokens.Brush("InkBrush") : Tokens.Brush("SurfaceBrush");
        NowForeground = State.PlanHour is null ? Tokens.Brush("PaperBrush") : Tokens.Brush("InkBrush");

        var conflicts = State.Conflicts();
        ConflictText = conflicts.Count == 0
            ? "No conflicts in the next 36 h"
            : $"{conflicts.Count} berth conflict{(conflicts.Count > 1 ? "s" : string.Empty)}: " +
              string.Join(" · ", conflicts.Select(c => $"{c.A} × {c.B} on {c.Berth}"));
        ConflictTone = conflicts.Count == 0 ? Tokens.Brush("SeaGreenInkBrush") : Tokens.Brush("AlertInkBrush");

        BuildTimeline(conflicts);
        BuildSelection();

        if (string.IsNullOrEmpty(Hint))
        {
            Hint = DefaultHint;
        }
    }

    private void BuildTimeline(IReadOnlyList<(string Berth, string A, string B)> conflicts)
    {
        var now = State.NowHours;
        var nowFraction = Math.Clamp(State.EffectivePlanHour / PlanHours, 0, 1);

        PlanRows.Clear();
        foreach (var berth in PortData.Berths)
        {
            var segments = new List<ProportionalPanel.Segment>();
            var spoken = new List<string>();

            // What was here earlier today (outlined, muted: the past), and any planned closure
            if (PortData.BerthHistory.TryGetValue(berth.Number, out var history))
            {
                foreach (var segment in history.Where(s => s.Kind is "prev" or "restricted"))
                {
                    var past = segment.Kind == "prev";
                    segments.Add(new ProportionalPanel.Segment(
                        segment.Label,
                        segment.Start / PlanHours,
                        (Math.Min(segment.End, PlanHours) - segment.Start) / PlanHours,
                        past ? Tokens.Transparent : Tokens.Brush("RestrictedBrush"),
                        past ? Tokens.Brush("TextMutedBrush") : Tokens.Brush("SurfaceBrush"),
                        past ? Tokens.Brush("TextFaintBrush") : null));
                    spoken.Add(past ? $"earlier {segment.Label}" : $"closed, {segment.Label}");
                }
            }

            // Live assignments, which move as vessels are dragged around
            foreach (var vessel in PortData.Vessels.Where(v => State.BerthOf(v.Id) == berth.Number))
            {
                var window = PortData.Schedule[vessel.Id];
                var start = Math.Max(0, window.Start);
                var end = Math.Min(PlanHours, window.End);
                if (end <= start)
                {
                    continue;
                }

                // Alongside: navy. Reserved for an arrival: accent with a dashed edge. Two in one window: alert
                // with an ink edge
                var conflicted = conflicts.Any(c => c.Berth == berth.Number && (c.A == vessel.Name || c.B == vessel.Name));
                var reserved = window.Start > now;
                segments.Add(new ProportionalPanel.Segment(
                    vessel.Name,
                    start / PlanHours,
                    (end - start) / PlanHours,
                    Tokens.Brush(conflicted ? "AlertInkBrush" : reserved ? "AccentBrush" : "NavyBrush"),
                    Tokens.Brush("SurfaceBrush"),
                    conflicted || reserved ? Tokens.Brush("InkBrush") : null,
                    Dashed: reserved && !conflicted));
                spoken.Add($"{vessel.Name} {(conflicted ? "in conflict" : reserved ? "reserved" : "alongside")} " +
                           $"{PortState.Format(start % 24)} to {PortState.Format(end % 24)}");
            }

            PlanRows.Add(new PlanRow
            {
                Berth = berth.Number,
                AccessibleName = spoken.Count == 0 ? $"Berth {berth.Number}: free" : $"Berth {berth.Number}: {string.Join(", ", spoken)}",
                Segments = segments,
                NowFraction = nowFraction
            });
        }
    }

    private void BuildSelection()
    {
        if (State.DockSelection is not { } id)
        {
            HasSelection = false;
            return;
        }

        var vessel = PortData.Vessel(id);
        var berth = State.BerthOf(id);

        HasSelection = true;
        SelectedName = vessel.Name;
        SelectedStatus = $"{vessel.Operator} · {vessel.StatusLine}";
        SelectedNote = berth is null
            ? "Not yet berthed. Drag her onto a berth marked Fits, pick one under Move to berth, or Optimize."
            : State.IsAssigned(id)
                ? $"Assigned to Berth {berth} · mooring gang notified."
                : vessel.OpsNote;
        SelectedProfile = Sprites.Source(Sprites.Side(vessel.Id));
        CanRelease = State.IsAssigned(id);

        BuildChoices(vessel, berth);

        SelectedFacts.Clear();
        foreach (var (key, value) in new[]
                 {
                     ("ETA", vessel.Eta),
                     ("Berth", berth is null ? "Unassigned" : $"Berth {berth}"),
                     ("Length", $"{vessel.Length} m"),
                     ("Draft", $"{vessel.Draft} m"),
                     ("Cargo", $"{vessel.Containers:N0} TEU"),
                     ("Security status", vessel.Security)
                 })
        {
            SelectedFacts.Add(new Fact { Key = key, Value = value });
        }
    }

    private void BuildChoices(Vessel vessel, string? current)
    {
        CanMove = BerthPlan.Movable(vessel);
        BerthChoices.Clear();
        if (!CanMove)
        {
            return;
        }

        for (var i = 0; i < PortData.Berths.Count; i++)
        {
            var berth = PortData.Berths[i];
            var fit = State.Check(vessel, i);
            var caption = berth.Number == current ? "her berth" : fit switch
            {
                BerthFit.Fits => "fits",
                BerthFit.Closed => "closed",
                BerthFit.TooShallow => "too shallow",
                _ => "taken"
            };

            BerthChoices.Add(new BerthChoice
            {
                Number = berth.Number,
                Caption = caption,
                AccessibleName = $"Berth {berth.Number}, {berth.Depth:0.0} m: {caption}",
                Edge = fit == BerthFit.Fits ? Tokens.Brush("AccentBrush") : Tokens.Brush("HairlineStrongBrush"),
                CaptionInk = fit == BerthFit.Fits ? Tokens.Brush("AccentDarkBrush") : Tokens.Brush("TextMutedBrush"),
                Assign = AssignToCommand
            });
        }
    }
}
