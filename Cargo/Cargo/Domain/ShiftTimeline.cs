namespace Cargo.Domain;

/// <summary>One movement on the shared axis: an arrival above the vessels baseline, a departure below.</summary>
public sealed partial record TimelineEvent(string Id, string Name, double Hour, bool Arrival);

/// <summary>
/// The maths behind "Today at North Quay": the axis, the day's movements, the tide's turning points and the
/// running moves and cargo figures. Pure (it reads only <see cref="PortData"/> and the hour it is given), so
/// <see cref="Presentation.TimelineViewModel"/> formats it and the unit tests checks it.
/// </summary>
public static class ShiftTimeline
{
    /// <summary>The axis: Wednesday 06:00 to Thursday 06:00, in hours on the app clock.</summary>
    public const double Start = 6, End = 30;

    /// <summary>The tide table's first high water (23:10) and its period, matching <see cref="PortData.TideAt"/>.</summary>
    public const double FirstHighWater = 23.17, TidePeriod = 12.4;

    /// <summary>The next high water at or after <paramref name="now"/>: the tide peaks every 12.4 h from 23:10.</summary>
    public static double NextHighWater(double now)
    {
        var k = Math.Ceiling((now - FirstHighWater) / TidePeriod);
        return FirstHighWater + Math.Max(0, k) * TidePeriod;
    }

    /// <summary>"HW " or "LW " when the tide turns inside the hour starting at <paramref name="hour"/>, else empty.</summary>
    public static string TideTurn(int hour)
    {
        // High water from 23:10 every 12.4 h; low water half a period off
        bool TurnsIn(double first) => Enumerable.Range(-2, 5).Select(k => first + k * TidePeriod).Any(t => t >= hour && t < hour + 1);
        return TurnsIn(FirstHighWater) ? "HW " : TurnsIn(FirstHighWater + TidePeriod / 2) ? "LW " : string.Empty;
    }

    /// <summary>
    /// The day's movements inside the window, in time order: the vessels' own arrival and departure times and
    /// the log (Meridian Sky, Elbe Trader), one entry per vessel and direction.
    /// </summary>
    public static IReadOnlyList<TimelineEvent> Events()
    {
        var events = new List<TimelineEvent>();

        void Add(string name, double hour, bool arrival)
        {
            if (hour >= Start && hour <= End && !events.Any(e => e.Name == name && e.Arrival == arrival))
            {
                var id = PortData.Vessels.FirstOrDefault(v => v.Name == name)?.Id ?? name.ToLowerInvariant().Replace(' ', '-');
                events.Add(new TimelineEvent(id, name, hour, arrival));
            }
        }

        foreach (var vessel in PortData.Vessels)
        {
            Add(vessel.Name, ShiftClock.ClockHours(vessel.Eta), arrival: true);
            Add(vessel.Name, ShiftClock.ClockHours(vessel.Etd), arrival: false);
        }

        foreach (var (name, hour, arrival) in PortData.Movements)
        {
            Add(name.Replace(" dep.", string.Empty), hour, arrival);
        }

        return events.OrderBy(e => e.Hour).ToArray();
    }

    /// <summary>The planned moves in the hour starting at <paramref name="hour"/> (the plan stops at midnight's figure).</summary>
    public static double Volume(int hour) => PortData.Volume[Math.Clamp(hour, 0, 24)];

    /// <summary>
    /// Container moves in the hour so far: the hour's planned volume scaled by how far into it we are, with a little
    /// crane-to-crane jitter.
    /// </summary>
    public static int MovesAt(double hours)
    {
        var hour = (int)Math.Floor(hours);
        var fraction = hours - hour;
        var moves = (int)Math.Round(PortData.Volume[Math.Clamp(hour, 0, 24)] * fraction + Math.Sin(hours * 97) * 3);
        return Math.Max(0, moves);
    }

    /// <summary>Container moves from 06:00 to <paramref name="now"/>: the whole hours, then the current hour so far.</summary>
    public static double MovesToday(double now)
    {
        var hour = (int)Math.Floor(now);
        return Enumerable.Range((int)Start, Math.Max(0, hour - (int)Start)).Sum(Volume) + MovesAt(now);
    }

    /// <summary>The busiest whole hour from 06:00 up to the hour <paramref name="now"/> falls in, and its moves; the first wins a tie.</summary>
    public static (double Hour, double Value) MovesPeak(double now)
    {
        var hour = (int)Math.Floor(now);
        // Fully qualified: Uno.Extensions brings a MaxBy of its own into scope
        return System.Linq.Enumerable.MaxBy(
            Enumerable.Range((int)Start, Math.Max(1, hour - (int)Start + 1)).Select(h => ((double)h, Volume(h))),
            m => m.Item2);
    }

    /// <summary>Cargo in minus cargo out in the hour starting at <paramref name="hour"/>.</summary>
    public static double NetCargo(int hour) =>
        PortData.CargoIn[Math.Clamp(hour, 0, 24)] - PortData.CargoOut[Math.Clamp(hour, 0, 24)];

    /// <summary>
    /// The hour from which cargo out has run ahead of cargo in without a break up to <paramref name="now"/>, or
    /// the current hour when in is ahead now.
    /// </summary>
    public static double OutAheadSince(double now)
    {
        var hour = (int)Math.Floor(now);
        return Enumerable.Range((int)Start, Math.Max(0, hour - (int)Start + 1)).Reverse()
            .TakeWhile(h => NetCargo(h) < 0)
            .DefaultIfEmpty(hour).Min();
    }
}
