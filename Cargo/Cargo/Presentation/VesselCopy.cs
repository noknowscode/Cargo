namespace Cargo.Presentation;

/// <summary>
/// A vessel's status in the words the screens use, read once from her data and the plan: the status line's head and
/// place ("Docked", "Berth 04"), the work alongside ("discharging"), the harbour tag's line ("B04 · discharging") and the
/// list row's second line ("Discharging 62%").
/// </summary>
public sealed record VesselWords(string Head, string Place, string? Work, string Tag, string Detail)
{
    /// <summary>"Docked · Berth 04 · discharging", "Arriving in 42 min · assigned Berth 07".</summary>
    public string StatusLine => Work is null ? $"{Head} · {Place}" : $"{Head} · {Place} · {Work}";

    /// <summary>The list row's first line: the status line without the work, which the second line gives with its figure.</summary>
    public string RowLine => $"{Head} · {Place}";
}

/// <summary>
/// The words about a vessel as the berth plan stands, written from her data's fields: her status line, her harbour
/// tag, her list row and her operations note. An arrival's line counts down and names the berth the plan gives her; a
/// vessel alongside keeps her own berth, which cannot move.
/// </summary>
public static class VesselCopy
{
    /// <summary>Her status in every form the screens use; the one reading of her status.</summary>
    public static VesselWords Words(PortState state, Vessel vessel)
    {
        var planned = state.BerthOf(vessel.Id);
        switch (vessel.Status)
        {
            case VesselStatus.Docked:
                var discharging = vessel.UnloadPercent is > 0 and < 100;
                var work = discharging ? "discharging" : "loading";
                return new VesselWords("Docked", $"Berth {vessel.HomeBerth}", work, $"B{vessel.HomeBerth} · {work}",
                    discharging ? $"Discharging {vessel.UnloadPercent}%" : $"Loading {vessel.LoadPercent}%");

            case VesselStatus.Departing:
                return new VesselWords($"Departing {ShiftClock.Display(vessel.Etd)}", $"Berth {vessel.HomeBerth}", null,
                    $"B{vessel.HomeBerth} · departing", string.Empty);

            case VesselStatus.Arriving:
                var arriving = $"arriving {ShiftClock.Display(vessel.Eta)}";
                return new VesselWords($"Arriving {state.Countdown(ShiftClock.ClockHours(vessel.Eta))}",
                    planned is null ? "no berth assigned" : $"assigned Berth {planned}", null,
                    planned is null ? $"no berth · {arriving}" : $"B{planned} · {arriving}", string.Empty);

            default:
                return planned is null
                    ? new VesselWords("At anchor", vessel.Anchorage ?? "anchorage", null, "anchored · unassigned",
                        vessel.WaitingFor is { } awaited ? $"Waiting for Berth {awaited}" : "Waiting for a berth")
                    : new VesselWords("At anchor", $"assigned Berth {planned}", null, $"anchored · B{planned} assigned", string.Empty);
        }
    }

    /// <summary>
    /// Her status line as the plan stands: an arrival counts down to her ETA and names the berth the plan gives her
    /// ("Arriving in 42 min · assigned Berth 08"), a vessel at anchor names hers once she has one ("At anchor ·
    /// assigned Berth 05").
    /// </summary>
    public static string StatusLine(PortState state, Vessel vessel) => Words(state, vessel).StatusLine;

    /// <summary>
    /// Her operations note as the plan stands: the data's note was written for the berth the data gives her, so once
    /// the plan moves her it says where she is going instead.
    /// </summary>
    public static VesselNote Note(PortState state, Vessel vessel) => state.BerthOf(vessel.Id) is var berth && berth != vessel.HomeBerth
        ? new VesselNote(berth is null
            ? "Released from her berth · she waits at anchor for one"
            : $"Assigned to Berth {berth} · pilots and mooring gang notified")
        : vessel.Note;
}
