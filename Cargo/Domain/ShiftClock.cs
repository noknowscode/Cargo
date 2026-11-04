using System.Globalization;

namespace Cargo.Domain;

/// <summary>
/// The shift's clock arithmetic, in hours on the app clock (from the demo's Wednesday midnight): reading the data's
/// stamps and writing times and countdowns. Pure, so every screen, the schedule and the tests read time one way.
/// </summary>
public static class ShiftClock
{
    /// <summary>
    /// A stamp from the data on the app clock: "Today 21:40" is 21.67, "Thu 02:30" 26.5, "Fri 10:00" 58,
    /// "Tue 23:05" -0.92, and a bare "21:40" is today. NaN when it cannot be read. The one reader of the data's times:
    /// the berth schedule (<see cref="PortData.Schedule"/>), the countdowns, the harbour tags and the shift timeline
    /// all go through it.
    /// </summary>
    public static double ClockHours(string when)
    {
        var parts = when.Split(' ');
        int? day = parts.Length switch
        {
            1 => 0,
            2 => parts[0] switch { "Tue" => -1, "Today" or "Wed" => 0, "Thu" => 1, "Fri" => 2, _ => null },
            _ => null
        };

        return day is { } d && TimeSpan.TryParseExact(parts[^1], @"hh\:mm", CultureInfo.InvariantCulture, out var time)
            ? d * 24 + time.TotalHours
            : double.NaN;
    }

    /// <summary>"21:15": the hour of the day, wrapped past midnight.</summary>
    public static string Format(double hours)
    {
        var s = (int)Math.Round(hours * 3600) % 86400;
        if (s < 0)
        {
            s += 86400;
        }

        return $"{s / 3600:D2}:{s % 3600 / 60:D2}";
    }

    /// <summary>"21:15:07", for the header clock.</summary>
    public static string FormatWithSeconds(double hours)
    {
        var s = (int)Math.Round(hours * 3600) % 86400;
        if (s < 0)
        {
            s += 86400;
        }

        return $"{s / 3600:D2}:{s % 3600 / 60:D2}:{s % 60:D2}";
    }

    /// <summary>The countdown the harbour tags and Next 6 h show for whole minutes to go: "in 42 min", "in 3 h 02", "now" once due.</summary>
    public static string Countdown(int minutes) => minutes switch
    {
        <= 0 => "now",
        < 60 => $"in {minutes} min",
        _ => $"in {minutes / 60} h {minutes % 60:D2}"
    };
}
