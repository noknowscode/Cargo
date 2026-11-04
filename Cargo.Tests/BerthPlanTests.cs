using Cargo.Domain;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// The berth rules behind drag-to-assign, Confirm berth and the optimiser. The starting data: Kaida Maru on 01,
/// Avren Aurora on 04 (14:10 to Thu 06:10), Baltic Crown on 06, Nordic Star reserved on 07, Levant Express at anchor
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
    [TestCase("04", false, TestName = "Berth 04 is taken through Avren Aurora's window")]
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

        // Nordic Star (21:40 to Thu 18:00) against Avren Aurora on 04 until Thu 06:10
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

        Assert.That(plan.Conflicts(), Is.EqualTo(new[] { ("04", "Avren Aurora", "Levant Express") }));
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

    [TestCase("nordic", "03", BerthFit.Closed, TestName = "A restricted berth refuses first, even when it is also too shallow")]
    [TestCase("levant", "02", BerthFit.TooShallow, TestName = "Berth 02 (13.0 m) is too shallow for Levant (14.2 m)")]
    [TestCase("levant", "04", BerthFit.Taken, TestName = "Berth 04 is taken in Levant's window")]
    [TestCase("levant", "05", BerthFit.Fits, TestName = "Berth 05 fits Levant")]
    [TestCase("nordic", "07", BerthFit.Fits, TestName = "A vessel fits the berth she already holds")]
    public void Check_names_the_rule_that_refuses(string vessel, string berth, BerthFit expected) =>
        Assert.That(new BerthPlan().Check(PortData.Vessel(vessel), Index(berth)), Is.EqualTo(expected));

    [Test]
    public void Check_reports_a_berth_that_does_not_exist() =>
        Assert.That(new BerthPlan().Check(PortData.Vessel("levant"), 8), Is.EqualTo(BerthFit.Unknown));

    [Test]
    public void TakenBy_names_the_vessel_in_the_way()
    {
        var plan = new BerthPlan();

        Assert.That(plan.TakenBy(PortData.Vessel("levant"), Index("04"))?.Id, Is.EqualTo("aurora"));
        Assert.That(plan.TakenBy(PortData.Vessel("levant"), Index("05")), Is.Null);
    }

    [Test]
    public void Every_refusal_says_why_and_a_fit_says_nothing()
    {
        var plan = new BerthPlan();
        var levant = PortData.Vessel("levant");

        Assert.That(plan.Refusal(PortData.Vessel("nordic"), Index("03")), Is.EqualTo("Berth 03 is restricted and takes no vessel."));
        Assert.That(plan.Refusal(levant, Index("02")), Is.EqualTo("Berth 02 is too shallow for Levant Express (14.2 m draft · 13 m depth)."));
        Assert.That(plan.Refusal(levant, Index("04")), Is.EqualTo("Berth 04 is taken by Avren Aurora during Levant Express's window."));
        Assert.That(plan.Refusal(levant, Index("05")), Is.Null);
    }

    [TestCase("aurora", false)]
    [TestCase("baltic", false)]
    [TestCase("nordic", true)]
    [TestCase("levant", true)]
    public void Only_vessels_not_alongside_or_leaving_can_be_moved(string vessel, bool movable) =>
        Assert.That(BerthPlan.Movable(PortData.Vessel(vessel)), Is.EqualTo(movable));

    [Test]
    public void The_optimiser_reports_what_it_moved_and_a_second_run_has_nothing_to_do()
    {
        var plan = new BerthPlan();

        var first = plan.Optimize();
        Assert.That(first.Moved, Is.EqualTo(new[] { ("levant", "05") }));
        Assert.That(first.Unplaced, Is.Empty);
        Assert.That(first.Summary, Is.EqualTo("Optimized: Levant Express to Berth 05 · pilots and mooring gangs notified."));

        var second = plan.Optimize();
        Assert.That(second.Moved, Is.Empty);
        Assert.That(second.Summary, Does.StartWith("Nothing to move"));
    }

    [Test]
    public void The_summary_names_the_vessels_nothing_fits()
    {
        Assert.That(new OptimizeResult([], ["levant"]).Summary,
            Is.EqualTo("No berth fits Levant Express for draft and window: she stays where she is."));
        Assert.That(new OptimizeResult([], ["nordic", "levant"]).Summary,
            Is.EqualTo("No berth fits Nordic Star and Levant Express for draft and window: they stay where they are."));
        Assert.That(new OptimizeResult([("nordic", "08")], ["levant"]).Summary,
            Is.EqualTo("Optimized: Nordic Star to Berth 08 · pilots and mooring gangs notified. " +
                       "No berth fits Levant Express for draft and window: she stays where she is."));
    }
}
