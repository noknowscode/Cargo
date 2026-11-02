namespace Cargo.Domain;

/// <summary>
/// Which berth each vessel holds, and the rules every assignment answers to: a restricted berth takes
/// nobody, a berth shallower than a vessel's draft cannot take her, and two vessels never share a berth
/// in overlapping windows. It reads only <see cref="PortData"/>, so the rules are tested without a UI
/// (Cargo.Tests); <see cref="PortState"/> owns one and raises the change signals around it.
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
    public bool Fits(Vessel vessel, int berthIndex)
    {
        if (berthIndex < 0 || berthIndex >= PortData.Berths.Count)
        {
            return false;
        }

        var berth = PortData.Berths[berthIndex];
        if (berth.State == "restricted" || vessel.Draft > berth.Depth)
        {
            return false;
        }

        var window = PortData.Schedule[vessel.Id];
        return !PortData.Vessels
            .Where(o => o.Id != vessel.Id && BerthOf(o.Id) == berth.Number)
            .Any(o => Overlaps(window, PortData.Schedule[o.Id]));
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
    /// what she had.
    /// </summary>
    public void Optimize()
    {
        var conflicts = Conflicts();
        var movable = PortData.Vessels
            .Where(v => v.Status != "Docked" && v.Status != "Departing")
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
            }
        }
    }
}
