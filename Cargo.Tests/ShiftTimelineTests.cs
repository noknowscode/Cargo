using Cargo.Domain;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>The maths behind "Today at North Quay" and the app clock's formatting.</summary>
public class ShiftTimelineTests
{
    private const double Minute = 1 / 60d;

    [TestCase(6.0, 23.17, TestName = "Morning: the evening high water")]
    [TestCase(20.97, 23.17, TestName = "Before 23:10: tonight's")]
    [TestCase(23.17, 23.17, TestName = "At high water: this one")]
    [TestCase(23.2, 35.57, TestName = "Just after: the next, 12.4 h on")]
    public void Next_high_water(double now, double expected) =>
        Assert.That(ShiftTimeline.NextHighWater(now), Is.EqualTo(expected).Within(1e-9));

    [Test]
    public void High_water_is_where_the_tide_table_peaks()
    {
        var hw = ShiftTimeline.FirstHighWater;
        Assert.That(PortData.TideAt(hw), Is.GreaterThan(PortData.TideAt(hw - .5)));
        Assert.That(PortData.TideAt(hw), Is.GreaterThan(PortData.TideAt(hw + .5)));
        Assert.That(PortData.TideAt(hw + ShiftTimeline.TidePeriod / 2), Is.LessThan(PortData.TideAt(hw)));
    }

    [TestCase(23, "HW ", TestName = "HW 23:10")]
    [TestCase(10, "HW ", TestName = "HW 10:46, a period earlier")]
    [TestCase(16, "LW ", TestName = "LW 16:58")]
    [TestCase(29, "LW ", TestName = "LW Thu 05:22")]
    [TestCase(12, "", TestName = "No turn at noon")]
    public void The_table_marks_the_hours_the_tide_turns(int hour, string expected) =>
        Assert.That(ShiftTimeline.TideTurn(hour), Is.EqualTo(expected));

    [Test]
    public void Events_are_in_time_order_inside_the_window_and_never_repeat()
    {
        var events = ShiftTimeline.Events();

        Assert.That(events, Is.Not.Empty);
        Assert.That(events.Select(e => e.Hour), Is.Ordered);
        Assert.That(events, Has.All.Matches<TimelineEvent>(e => e.Hour >= ShiftTimeline.Start && e.Hour <= ShiftTimeline.End));
        Assert.That(events.Select(e => (e.Name, e.Arrival)), Is.Unique);
    }

    [Test]
    public void Nordic_Star_arrives_at_21_40_under_her_vessel_id()
    {
        var arrival = ShiftTimeline.Events().Single(e => e.Name == "Nordic Star" && e.Arrival);

        Assert.That(arrival.Id, Is.EqualTo("nordic"));
        Assert.That(arrival.Hour, Is.EqualTo(21 + 40 * Minute).Within(1e-9));
    }

    [Test]
    public void Moves_today_are_the_whole_hours_since_06_00_plus_the_current_hour_so_far()
    {
        Assert.That(ShiftTimeline.MovesToday(6), Is.EqualTo(PortState.MovesAt(6)));

        var expected = ShiftTimeline.Volume(6) + ShiftTimeline.Volume(7) + ShiftTimeline.Volume(8) + PortState.MovesAt(9.5);
        Assert.That(ShiftTimeline.MovesToday(9.5), Is.EqualTo(expected));
    }

    [Test]
    public void Moves_today_never_fall_as_the_shift_goes_on()
    {
        var previous = double.MinValue;
        for (var now = ShiftTimeline.Start; now < 24; now += 0.25)
        {
            var moves = ShiftTimeline.MovesToday(now);
            Assert.That(moves, Is.GreaterThanOrEqualTo(previous - 6), $"at {now}");
            previous = moves;
        }
    }

    [Test]
    public void The_peak_is_the_first_busiest_whole_hour_so_far()
    {
        const double now = 20.97;
        var hours = Enumerable.Range(6, 15).ToArray();
        var max = hours.Max(ShiftTimeline.Volume);

        var (hour, value) = ShiftTimeline.MovesPeak(now);

        Assert.That(value, Is.EqualTo(max));
        Assert.That(hour, Is.EqualTo(hours.First(h => ShiftTimeline.Volume(h) == max)));
    }

    [Test]
    public void Out_ahead_since_starts_an_unbroken_run_of_negative_hours()
    {
        for (var hour = 6; hour < 24; hour++)
        {
            var since = ShiftTimeline.OutAheadSince(hour + .5);
            if (ShiftTimeline.NetCargo(hour) >= 0)
            {
                Assert.That(since, Is.EqualTo(hour), $"in ahead at {hour}");
                continue;
            }

            for (var h = (int)since; h <= hour; h++)
            {
                Assert.That(ShiftTimeline.NetCargo(h), Is.LessThan(0), $"run from {since} to {hour}");
            }

            Assert.That(since == ShiftTimeline.Start || ShiftTimeline.NetCargo((int)since - 1) >= 0, $"run start at {hour}");
        }
    }

    [TestCase("Today 21:40", 21 + 40 / 60d)]
    [TestCase("Thu 02:30", 26.5)]
    [TestCase("Tue 23:05", 23 + 5 / 60d - 24)]
    public void Schedule_times_read_onto_the_app_clock(string when, double hours) =>
        Assert.That(PortState.ClockHours(when), Is.EqualTo(hours).Within(1e-9));

    [TestCase("")]
    [TestCase("soon")]
    public void An_unreadable_schedule_time_is_NaN(string when) =>
        Assert.That(PortState.ClockHours(when), Is.NaN);

    [TestCase(20.5, "20:30")]
    [TestCase(24.5, "00:30")]
    [TestCase(-0.5, "23:30")]
    [TestCase(23 + 59.6 / 60, "23:59", TestName = "Minutes truncate: 23:59:36 reads 23:59")]
    [TestCase(24 - 0.1 / 3600, "00:00", TestName = "23:59:59.9 rounds to the second, then wraps")]
    public void Clock_times_wrap_at_midnight(double hours, string expected) =>
        Assert.That(PortState.Format(hours), Is.EqualTo(expected));

    [Test]
    public void The_header_clock_carries_seconds() =>
        Assert.That(PortState.FormatWithSeconds(20 + 58 / 60d + 20 / 3600d), Is.EqualTo("20:58:20"));
}
