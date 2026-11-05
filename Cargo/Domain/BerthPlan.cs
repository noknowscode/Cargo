namespace Cargo.Domain;

/// <summary>Whether a berth can take a vessel and, when it cannot, the one rule that refuses her.</summary>
public enum BerthFit
{
    /// <summary>Open, deep enough and free for her whole window.</summary>
    Fits,

    /// <summary>The berth is restricted and takes nobody.</summary>
    Closed,

    /// <summary>The berth is shallower than her draft.</summary>
    TooShallow,

    /// <summary>Another vessel holds the berth during part of her window.</summary>
    Taken,

    /// <summary>No berth at that index.</summary>
    Unknown
}

/// <summary>
/// What <see cref="BerthPlan.Optimize"/> did: the vessels it moved (to which berth), and the ones that needed a berth
/// but have none that fits, which keep what they had. <see cref="Summary"/> is the sentence the plan shows.
/// </summary>
public sealed record OptimizeResult(IReadOnlyList<(string VesselId, string Berth)> Moved, IReadOnlyList<string> Unplaced)
{
    public string Summary
    {
        get
        {
            static string Name(string id) => PortData.Vessel(id).Name;
            static string List(IEnumerable<string> names) => names.ToList() switch
            {
                [var one] => one,
                var many => string.Join(", ", many.Take(many.Count - 1)) + " and " + many[^1]
            };

            var unplaced = Unplaced.Count == 0
                ? string.Empty
                : $"No berth fits {List(Unplaced.Select(Name))} for draft and window: " +
                  (Unplaced.Count == 1 ? "she stays where she is." : "they stay where they are.");

            if (Moved.Count == 0)
            {
                return Unplaced.Count == 0
                    ? "Nothing to move: every inbound vessel already has a berth that fits, with no overlaps in the next 36 h."
                    : unplaced;
            }

            var moved = $"Optimized: {List(Moved.Select(m => $"{Name(m.VesselId)} to Berth {m.Berth}"))} · pilots and mooring gangs notified.";
            return Unplaced.Count == 0 ? moved : moved + " " + unplaced;
        }
    }
}

/// <summary>
/// Which berth each vessel holds, and the rules every assignment answers to: a restricted berth takes
/// nobody, a berth shallower than a vessel's draft cannot take her, and two vessels never share a berth
/// in overlapping windows. It reads only <see cref="PortData"/>, so the rules are tested without a UI
/// (the unit tests); <see cref="PortState"/> owns one and raises the change signals around it.
/// </summary>
public sealed class BerthPlan
{
    // Assignments made this shift; a vessel with none keeps the berth the data starts her on
    private readonly Dictionary<string, string> _assigned;

    /// <summary>A plan on the starting data, or on top of given assignments (vessel id to berth number), which are taken as they are, conflicts and all.</summary>
    public BerthPlan(IReadOnlyDictionary<string, string>? assignments = null) =>
        _assigned = assignments is null ? new() : new(assignments);

    /// <summary>The berth number a vessel holds, or null when she has none.</summary>
    public string? BerthOf(string vesselId) =>
        _assigned.TryGetValue(vesselId, out var assigned)
            ? assigned
            : PortData.Berths.FirstOrDefault(b => b.Occupant == vesselId)?.Number;

    /// <summary>True when the berth came from an assignment this shift rather than the starting data.</summary>
    public bool IsAssigned(string vesselId) => _assigned.ContainsKey(vesselId);

    /// <summary>A vessel alongside (docked) or leaving keeps her berth; the rest can be dragged, assigned and optimised.</summary>
    public static bool Movable(Vessel vessel) => vessel.Status is not (VesselStatus.Docked or VesselStatus.Departing);

    /// <summary>Two occupation windows overlap when each starts before the other ends; touching ends do not.</summary>
    public static bool Overlaps((double Start, double End) a, (double Start, double End) b) =>
        a.Start < b.End && b.Start < a.End;

    /// <summary>Every pair of vessels holding the same berth in overlapping windows.</summary>
    public IReadOnlyList<(string Berth, string A, string B)> Conflicts()
    {
        var found = new List<(string, string, string)>();
        foreach (var berth in PortData.Berths)
        {
            var here = PortData.Vessels.Where(v => BerthOf(v.Id) == berth.Number).ToList();
            for (var i = 0; i < here.Count; i++)
            {
                for (var j = i + 1; j < here.Count; j++)
                {
                    if (Overlaps(PortData.Schedule[here[i].Id], PortData.Schedule[here[j].Id]))
                    {
                        found.Add((berth.Number, here[i].Name, here[j].Name));
                    }
                }
            }
        }

        return found;
    }

    /// <summary>Whether the berth at <paramref name="berthIndex"/> can take the vessel: open, deep enough, free for her window.</summary>
    public bool Fits(Vessel vessel, int berthIndex) => Check(vessel, berthIndex) == BerthFit.Fits;

    /// <summary>
    /// The first rule that refuses the vessel at the berth, in the order a dispatcher would give it: closed, too
    /// shallow, taken; <see cref="BerthFit.Fits"/> when none does.
    /// </summary>
    public BerthFit Check(Vessel vessel, int berthIndex)
    {
        if (berthIndex < 0 || berthIndex >= PortData.Berths.Count)
        {
            return BerthFit.Unknown;
        }

        var berth = PortData.Berths[berthIndex];
        if (berth.State == BerthState.Restricted)
        {
            return BerthFit.Closed;
        }

        if (vessel.Draft > berth.Depth)
        {
            return BerthFit.TooShallow;
        }

        return TakenBy(vessel, berthIndex) is null ? BerthFit.Fits : BerthFit.Taken;
    }

    /// <summary>The vessel holding the berth during part of this one's window, or null when it is free for all of it.</summary>
    public Vessel? TakenBy(Vessel vessel, int berthIndex)
    {
        if (berthIndex < 0 || berthIndex >= PortData.Berths.Count)
        {
            return null;
        }

        var number = PortData.Berths[berthIndex].Number;
        var window = PortData.Schedule[vessel.Id];
        return PortData.Vessels
            .Where(o => o.Id != vessel.Id && BerthOf(o.Id) == number)
            .FirstOrDefault(o => Overlaps(window, PortData.Schedule[o.Id]));
    }

    /// <summary>The numbers of the berths that can take the vessel now (<see cref="Fits"/>): open, deep enough, free for her window.</summary>
    public IReadOnlyList<string> BerthsThatFit(Vessel vessel) =>
        Enumerable.Range(0, PortData.Berths.Count).Where(i => Fits(vessel, i)).Select(i => PortData.Berths[i].Number).ToArray();

    /// <summary>What the plan says when a move is refused, or null when the berth fits.</summary>
    public string? Refusal(Vessel vessel, int berthIndex)
    {
        var fit = Check(vessel, berthIndex);
        if (fit is BerthFit.Fits or BerthFit.Unknown)
        {
            return fit == BerthFit.Unknown ? $"There is no such berth for {vessel.Name}." : null;
        }

        var berth = PortData.Berths[berthIndex];
        return fit switch
        {
            BerthFit.Closed => $"Berth {berth.Number} is restricted and takes no vessel.",
            BerthFit.TooShallow => $"Berth {berth.Number} is too shallow for {vessel.Name} ({vessel.Draft} m draft · {berth.Depth} m depth).",
            _ => $"Berth {berth.Number} is taken by {TakenBy(vessel, berthIndex)!.Name} during {vessel.Name}'s window."
        };
    }

    /// <summary>Gives the vessel the berth when it <see cref="Fits"/>; returns whether it did.</summary>
    public bool TryAssign(string vesselId, int berthIndex)
    {
        if (!Fits(PortData.Vessel(vesselId), berthIndex))
        {
            return false;
        }

        _assigned[vesselId] = PortData.Berths[berthIndex].Number;
        return true;
    }

    /// <summary>Drops this shift's assignment; the vessel goes back to the berth the data starts her on, if any.</summary>
    public void Unassign(string vesselId) => _assigned.Remove(vesselId);

    /// <summary>
    /// Gives every movable vessel (not docked or departing) that has no berth, or is in a conflict, the
    /// shallowest berth that still clears her draft and is free for her window. A vessel nothing fits keeps
    /// what she had, and the result names her.
    /// </summary>
    public OptimizeResult Optimize()
    {
        var moved = new List<(string, string)>();
        var unplaced = new List<string>();
        var conflicts = Conflicts();
        var movable = PortData.Vessels
            .Where(Movable)
            .Where(v => BerthOf(v.Id) is null || conflicts.Any(c => c.A == v.Name || c.B == v.Name))
            .ToList();

        foreach (var vessel in movable)
        {
            var best = Enumerable.Range(0, PortData.Berths.Count)
                .Where(i => Fits(vessel, i))
                .OrderBy(i => Math.Abs(PortData.Berths[i].Depth - vessel.Draft))
                .Select(i => (int?)i)
                .FirstOrDefault();

            if (best is { } index)
            {
                _assigned[vessel.Id] = PortData.Berths[index].Number;
                moved.Add((vessel.Id, PortData.Berths[index].Number));
            }
            else
            {
                unplaced.Add(vessel.Id);
            }
        }

        return new OptimizeResult(moved, unplaced);
    }
}
