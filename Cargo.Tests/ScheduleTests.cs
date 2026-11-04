using Cargo.Domain;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// One source for each fact: the berth windows come from the vessels' ETA and ETD, the berth list's "fits berths"
/// from the plan's rules, and every countdown from <see cref="PortState.Countdown"/>.
/// </summary>
public class ScheduleTests
{
    [Test]
    public void Every_berth_window_runs_from_the_vessels_ETA_to_her_ETD()
    {
        foreach (var vessel in PortData.Vessels)
        {
            var (start, end) = PortData.Schedule[vessel.Id];
            Assert.That(start, Is.EqualTo(ShiftClock.ClockHours(vessel.Eta)), vessel.Name);
            Assert.That(end, Is.EqualTo(ShiftClock.ClockHours(vessel.Etd)), vessel.Name);
            Assert.That(end, Is.GreaterThan(start), vessel.Name);
        }

        Assert.That(PortData.Schedule["levant"], Is.EqualTo((26.5, 58d)), "Thu 02:30 to Fri 10:00");
    }

    [Test]
    public void The_berths_that_fit_follow_the_plans_rules_not_depth_alone()
    {
        var state = new PortState(new ManualClock());

        // 04 is deep enough for Levant Express but Avren Aurora holds it in her window, which a drop refuses
        Assert.That(state.BerthsThatFit(PortData.Vessel("levant")), Is.EqualTo(new[] { "01", "05", "06" }));
        Assert.That(state.Check(PortData.Vessel("levant"), 3), Is.EqualTo(BerthFit.Taken));
    }

    [Test]
    public void A_berth_taken_by_an_assignment_leaves_the_list()
    {
        var state = new PortState(new ManualClock());

        state.Assign("nordic", PortData.Berths.ToList().FindIndex(b => b.Number == "05"));

        Assert.That(state.BerthsThatFit(PortData.Vessel("levant")), Is.EqualTo(new[] { "01", "06" }));
    }

    [Test]
    public void Countdowns_turn_over_on_the_minute()
    {
        var clock = new ManualClock();
        var state = new PortState(clock);
        var nordic = ShiftClock.ClockHours("Today 21:40");

        Assert.That(state.Countdown(nordic), Is.EqualTo("in 42 min"), "exactly 42 minutes reads 42, not 43");
        Assert.That(state.MinutesUntil(nordic), Is.EqualTo(42));

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.That(state.Countdown(nordic), Is.EqualTo("in 42 min"), "41.5 minutes rounds up");

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.That(state.Countdown(nordic), Is.EqualTo("in 41 min"));

        Assert.That(state.Countdown(ShiftClock.ClockHours("Thu 00:01")), Is.EqualTo("in 3 h 02"));

        clock.Advance(TimeSpan.FromMinutes(41));
        Assert.That(state.Countdown(nordic), Is.EqualTo("now"));
        Assert.That(state.MinutesUntil(nordic), Is.EqualTo(0));
    }

    [TestCase("Today 21:40", "21:40")]
    [TestCase("Thu 02:30", "Thu 02:30")]
    [TestCase("21:40", "21:40")]
    public void A_stamp_reads_as_the_screens_write_it(string stamp, string shown) =>
        Assert.That(ShiftClock.Display(stamp), Is.EqualTo(shown));

    [TestCase("01", 0)]
    [TestCase("07", 6)]
    [TestCase("08", 7)]
    [TestCase("09", null)]
    [TestCase(null, null)]
    [TestCase("nordic", null)]
    public void A_berth_number_finds_its_place_on_the_quay(string? number, int? index) =>
        Assert.That(PortData.BerthIndex(number), Is.EqualTo(index));

    [Test]
    public void The_operator_shift_reads_in_clock_time() =>
        Assert.That((PortData.Operator.ClockedInText, PortData.Operator.ShiftEndText), Is.EqualTo(("18:00", "06:00")));
}
