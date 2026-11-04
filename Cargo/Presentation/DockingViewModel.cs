using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class PlanRow : ObservableObject
{
    public required string Berth { get; init; }

    /// <summary>The row in words for a screen reader: the berth and who holds it when.</summary>
    public required string AccessibleName { get; init; }
    public required IReadOnlyList<ProportionalPanel.Segment> Segments { get; init; }

    /// <summary>Where the plan hour's line crosses the row (a fraction of the 36 h); the scrubber moves it in place.</summary>
    [ObservableProperty] public partial double NowFraction { get; set; }
}

/// <summary>
/// One berth in the selected vessel's "Move to berth" list: the keyboard and touch path for drag-to-assign. Every
/// berth is listed; one that cannot take her says why in words (not only in colour), and choosing it anyway shows
/// the same refusal a drop would. The eight rows are made once and updated in place, so the button that has
/// keyboard focus keeps it when the clock or an assignment changes what the rows say.
/// </summary>
public sealed partial class BerthChoice : ObservableObject
{
    public required string Number { get; init; }
    public required ICommand Assign { get; init; }

    [ObservableProperty] public partial string Caption { get; set; } = string.Empty;
    [ObservableProperty] public partial string AccessibleName { get; set; } = string.Empty;

    /// <summary>The berth takes her: an accent edge and caption (the view picks the brushes).</summary>
    [ObservableProperty] public partial bool Fits { get; set; }
}

/// <summary>
/// The berth plan: the timeline, the scrubber over the next 36 hours, conflicts, and the
/// selected vessel's panel. <see cref="Refresh"/> follows structural changes, <see cref="Scrubbed"/>
/// the scrubber and <see cref="Tick"/> the clock. Created by <see cref="BerthsViewModel"/> on the UI thread.
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
        Refresh();
    }

    public PortState State { get; }

    public ObservableCollection<PlanRow> PlanRows { get; } = new();

    public ObservableCollection<Fact> SelectedFacts { get; } = new();

    public ObservableCollection<BerthChoice> BerthChoices { get; } = new();

    [ObservableProperty] public partial string PlanLabel { get; set; } = string.Empty;
    [ObservableProperty] public partial string ConflictText { get; set; } = string.Empty;

    /// <summary>Two vessels share a berth somewhere in the next 36 h: the conflict line reads in alert ink.</summary>
    [ObservableProperty] public partial bool HasConflicts { get; set; }

    [ObservableProperty] public partial string Hint { get; set; } = DefaultHint;
    [ObservableProperty] public partial string SelectedName { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedStatus { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedNote { get; set; } = string.Empty;

    /// <summary>The selected vessel's side render, by sprite name (Assets/Sprites); the view loads the image.</summary>
    [ObservableProperty] public partial string SelectedProfile { get; set; } = string.Empty;

    [ObservableProperty] public partial bool CanRelease { get; set; }
    [ObservableProperty] public partial bool CanMove { get; set; }

    /// <summary>The plan follows the live clock (Now is lit) rather than a scrubbed hour.</summary>
    [ObservableProperty] public partial bool IsLive { get; set; }

    [ObservableProperty]
    public partial bool HasSelection { get; set; }

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
        var conflicts = State.Conflicts();
        ConflictText = conflicts.Count == 0
            ? "No conflicts in the next 36 h"
            : $"{conflicts.Count} berth conflict{(conflicts.Count > 1 ? "s" : string.Empty)}: " +
              string.Join(" · ", conflicts.Select(c => $"{c.A} × {c.B} on {c.Berth}"));
        HasConflicts = conflicts.Count > 0;

        BuildTimeline(conflicts);
        BuildSelection();
        Scrubbed();

        if (string.IsNullOrEmpty(Hint))
        {
            Hint = DefaultHint;
        }
    }

    /// <summary>
    /// The scrubber moved (or Now was pressed): only the plan hour's label, the Now button and the line across the
    /// timeline rows follow it, so the rows are kept and their line moves in place.
    /// </summary>
    public void Scrubbed()
    {
        var planHour = State.EffectivePlanHour;
        PlanLabel = (planHour >= 24 ? "Thu " : string.Empty) + ShiftClock.Format(planHour % 24);
        OnPropertyChanged(nameof(Scrub));
        IsLive = State.PlanHour is null;

        var nowFraction = Math.Clamp(planHour / PlanHours, 0, 1);
        foreach (var row in PlanRows)
        {
            row.NowFraction = nowFraction;
        }
    }

    private void BuildTimeline(IReadOnlyList<(string Berth, string A, string B)> conflicts)
    {
        var now = State.NowHours;

        PlanRows.Clear();
        foreach (var berth in PortData.Berths)
        {
            var segments = new List<ProportionalPanel.Segment>();
            var spoken = new List<string>();

            // What was here earlier today (outlined, muted: the past), and any planned closure
            if (PortData.BerthHistory.TryGetValue(berth.Number, out var history))
            {
                foreach (var segment in history)
                {
                    var past = segment.Kind == "prev";
                    segments.Add(new ProportionalPanel.Segment(
                        segment.Label,
                        segment.Start / PlanHours,
                        (Math.Min(segment.End, PlanHours) - segment.Start) / PlanHours,
                        past ? null : "RestrictedBrush",
                        past ? "TextMutedBrush" : "SurfaceBrush",
                        past ? "TextFaintBrush" : null));
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
                    conflicted ? "AlertInkBrush" : reserved ? "AccentBrush" : "NavyBrush",
                    "SurfaceBrush",
                    conflicted || reserved ? "InkBrush" : null,
                    Dashed: reserved && !conflicted));
                spoken.Add($"{vessel.Name} {(conflicted ? "in conflict" : reserved ? "reserved" : "alongside")} " +
                           $"{ShiftClock.Format(start % 24)} to {ShiftClock.Format(end % 24)}");
            }

            PlanRows.Add(new PlanRow
            {
                Berth = berth.Number,
                AccessibleName = spoken.Count == 0 ? $"Berth {berth.Number}: free" : $"Berth {berth.Number}: {string.Join(", ", spoken)}",
                Segments = segments
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
        SelectedProfile = Sprites.Side(vessel.Id);
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

        // The berths never change: one row each, made once, then only what they say is updated
        if (BerthChoices.Count == 0)
        {
            foreach (var berth in PortData.Berths)
            {
                BerthChoices.Add(new BerthChoice { Number = berth.Number, Assign = AssignToCommand });
            }
        }

        for (var i = 0; i < PortData.Berths.Count; i++)
        {
            var berth = PortData.Berths[i];
            var fit = State.Check(vessel, i);
            var choice = BerthChoices[i];
            choice.Caption = berth.Number == current ? "her berth" : fit switch
            {
                BerthFit.Fits => "fits",
                BerthFit.Closed => "closed",
                BerthFit.TooShallow => "too shallow",
                _ => "taken"
            };
            choice.AccessibleName = $"Berth {berth.Number}, {berth.Depth:0.0} m: {choice.Caption}";
            choice.Fits = fit == BerthFit.Fits;
        }
    }
}
