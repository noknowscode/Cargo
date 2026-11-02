using Cargo.Domain;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// The berth rules behind drag-to-assign, Confirm berth and the optimiser. The starting data: Kaida Maru on 01,
/// MSC Aurora on 04 (14:10 to Thu 06:10), Baltic Crown on 06, Nordic Star reserved on 07, Levant Express at anchor
/// with no berth (Thu 02:30 onwards, 14.2 m draft); 03 is restricted.
/// </summary>
public class BerthPlanTests
{
    private static int Index(string berth) => PortData.Berths.ToList().FindIndex(b => b.Number == berth);

    [Test]
    public void The_starting_plan_has_no_conflicts_and_leaves_Levant_at_anchor()
    {
        var plan = new BerthPlan();

        Assert.That(plan.Conflicts(), Is.Empty);
        Assert.That(plan.BerthOf("levant"), Is.Null);
        Assert.That(plan.BerthOf("nordic"), Is.EqualTo("07"));
        Assert.That(plan.IsAssigned("nordic"), Is.False);
    }

    [TestCase("03", false, TestName = "Restricted berth 03 takes nobody")]
    [TestCase("02", false, TestName = "Berth 02 (13.0 m) is too shallow for a 14.2 m draft")]
    [TestCase("04", false, TestName = "Berth 04 is taken through MSC Aurora's window")]
    [TestCase("05", true, TestName = "Berth 05 (14.6 m) is deep enough and free")]
    [TestCase("01", true, TestName = "Berth 01 frees at 23:30, before Levant arrives")]
    public void Fits_checks_restriction_depth_and_overlap(string berth, bool fits)
    {
        var plan = new BerthPlan();

        Assert.That(plan.Fits(PortData.Vessel("levant"), Index(berth)), Is.EqualTo(fits));
    }

    [TestCase(-1)]
    [TestCase(8)]
    public void Fits_refuses_a_berth_that_does_not_exist(int index) =>
        Assert.That(new BerthPlan().Fits(PortData.Vessel("levant"), index), Is.False);

    [Test]
    public void Windows_that_only_touch_do_not_overlap()
    {
        Assert.That(BerthPlan.Overlaps((0, 10), (10, 20)), Is.False);
        Assert.That(BerthPlan.Overlaps((0, 10), (9.9, 20)), Is.True);
        Assert.That(BerthPlan.Overlaps((5, 6), (0, 20)), Is.True);
    }

    [Test]
    public void A_refused_assignment_leaves_the_plan_unchanged()
    {
        var plan = new BerthPlan();

        // Nordic Star (21:40 to Thu 18:00) against MSC Aurora on 04 until Thu 06:10
        Assert.That(plan.TryAssign("nordic", Index("04")), Is.False);
        Assert.That(plan.BerthOf("nordic"), Is.EqualTo("07"));
        Assert.That(plan.IsAssigned("nordic"), Is.False);
    }

    [Test]
    public void Unassigning_returns_a_vessel_to_her_starting_berth()
    {
        var plan = new BerthPlan();

        Assert.That(plan.TryAssign("nordic", Index("05")), Is.True);
        Assert.That(plan.BerthOf("nordic"), Is.EqualTo("05"));
        Assert.That(plan.IsAssigned("nordic"), Is.True);

        plan.Unassign("nordic");
        Assert.That(plan.BerthOf("nordic"), Is.EqualTo("07"));
    }

    [Test]
    public void A_vessel_on_an_occupied_berth_in_her_window_is_a_conflict()
    {
        var plan = new BerthPlan(new Dictionary<string, string> { ["levant"] = "04" });

        Assert.That(plan.Conflicts(), Is.EqualTo(new[] { ("04", "MSC Aurora", "Levant Express") }));
    }

    [Test]
    public void The_optimiser_gives_Levant_the_shallowest_berth_that_clears_her_draft()
    {
        var plan = new BerthPlan();

        plan.Optimize();

        // 05 (14.6 m) beats 06 (15.0 m) and 01 (15.2 m) for a 14.2 m draft; 04 is taken in her window
        Assert.That(plan.BerthOf("levant"), Is.EqualTo("05"));
        Assert.That(plan.BerthOf("nordic"), Is.EqualTo("07"), "a vessel with a berth and no conflict stays put");
        Assert.That(plan.Conflicts(), Is.Empty);
    }

    [Test]
    public void The_optimiser_resolves_a_conflict_by_moving_the_inbound_vessel()
    {
        var plan = new BerthPlan(new Dictionary<string, string> { ["levant"] = "04" });

        plan.Optimize();

        Assert.That(plan.BerthOf("levant"), Is.EqualTo("05"));
        Assert.That(plan.BerthOf("aurora"), Is.EqualTo("04"), "a docked vessel is never moved");
        Assert.That(plan.Conflicts(), Is.Empty);
    }

    [Test]
    public void The_optimiser_never_moves_docked_or_departing_vessels()
    {
        // Baltic Crown (departing) put on Kaida Maru's berth: both are alongside, so neither is movable
        var plan = new BerthPlan(new Dictionary<string, string> { ["baltic"] = "01" });

        plan.Optimize();

        Assert.That(plan.BerthOf("baltic"), Is.EqualTo("01"));
        Assert.That(plan.Conflicts(), Has.Count.EqualTo(1));
    }
}
