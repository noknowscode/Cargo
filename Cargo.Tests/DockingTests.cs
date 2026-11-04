using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// The berth plan's model: the "Move to berth" list (the keyboard and touch path for drag-to-assign), the conflict
/// line and the live/scrubbed state. Built on a store with a manual clock: the model carries no brushes or images.
/// </summary>
public class DockingTests
{
    private static (PortState State, ManualClock Clock, DockingViewModel Plan) Planning(string vessel)
    {
        var clock = new ManualClock();
        var state = new PortState(clock) { DockSelection = vessel };
        return (state, clock, new DockingViewModel(state));
    }

    private static string Captions(DockingViewModel plan) =>
        string.Join(" ", plan.BerthChoices.Select(c => $"{c.Number}:{c.Caption}"));

    [Test]
    public void Every_berth_says_whether_it_takes_Nordic_Star_and_why_not()
    {
        var (_, _, plan) = Planning("nordic");

        Assert.That(plan.CanMove, Is.True);
        // 01 and 04 are held in her window, 03 is closed, 07 is the berth she holds
        Assert.That(Captions(plan), Is.EqualTo("01:taken 02:fits 03:closed 04:taken 05:fits 06:fits 07:her berth 08:fits"));
        Assert.That(plan.BerthChoices.Where(c => c.Fits).Select(c => c.Number), Is.EqualTo(new[] { "02", "05", "06", "07", "08" }));
        Assert.That(plan.BerthChoices[2].AccessibleName, Is.EqualTo($"Berth 03, {12.0:0.0} m: closed"));
    }

    [Test]
    public void A_docked_vessel_offers_no_moves()
    {
        var (_, _, plan) = Planning("aurora");

        Assert.That(plan.CanMove, Is.False);
        Assert.That(plan.HasSelection, Is.True);
    }

    [Test]
    public void The_rows_are_updated_in_place_so_a_focused_one_keeps_focus()
    {
        var (state, clock, plan) = Planning("nordic");
        var rows = plan.BerthChoices.ToArray();

        // The clock's minute (the live plan refreshes on it) and an assignment from the list itself
        clock.Advance(TimeSpan.FromMinutes(1));
        plan.Tick();
        plan.AssignToCommand.Execute("08");
        plan.Refresh(); // the page does this on the structure change

        Assert.That(plan.BerthChoices, Is.EqualTo(rows), "the same eight row objects");
        Assert.That(state.BerthOf("nordic"), Is.EqualTo("08"));
        Assert.That(rows[7].Caption, Is.EqualTo("her berth"));
        Assert.That(rows[6].Caption, Is.EqualTo("fits"));
        Assert.That(plan.Hint, Is.EqualTo("Nordic Star assigned to Berth 08 · pilots and mooring gang notified."));
    }

    [Test]
    public void Choosing_a_berth_that_cannot_take_her_shows_the_same_refusal_a_drop_does()
    {
        var (state, _, plan) = Planning("nordic");

        plan.AssignToCommand.Execute("03");

        Assert.That(plan.Hint, Is.EqualTo("Berth 03 is restricted and takes no vessel."));
        Assert.That(state.BerthOf("nordic"), Is.EqualTo("07"));
    }

    [Test]
    public void The_plan_is_live_until_scrubbed_and_reports_conflicts_in_words()
    {
        var (state, _, plan) = Planning("levant");
        Assert.That(plan.IsLive, Is.True);
        Assert.That(plan.HasConflicts, Is.False);
        Assert.That(plan.ConflictText, Is.EqualTo("No conflicts in the next 36 h"));

        state.PlanHour = 30;
        plan.Scrubbed();
        Assert.That(plan.IsLive, Is.False);
        Assert.That(plan.PlanLabel, Is.EqualTo("Thu 06:00"));

        plan.NowCommand.Execute(null);
        plan.Scrubbed();
        Assert.That(plan.IsLive, Is.True);
    }

    [Test]
    public void Scrubbing_moves_the_line_across_the_same_rows_and_rebuilds_nothing_else()
    {
        var (state, _, plan) = Planning("nordic");
        var rows = plan.PlanRows.ToArray();
        var structure = 0;
        var scrubbed = 0;
        state.StructureChanged += (_, _) => structure++;
        state.PlanHourChanged += (_, _) => scrubbed++;

        state.PlanHour = 18;
        plan.Scrubbed(); // the page does this on PlanHourChanged

        Assert.That(scrubbed, Is.EqualTo(1));
        Assert.That(structure, Is.Zero, "a scrub is not a structure change: the harbour and the other panels stay");
        Assert.That(plan.PlanRows, Is.EqualTo(rows));
        Assert.That(rows.Select(r => r.NowFraction), Has.All.EqualTo(0.5));
        Assert.That(plan.PlanLabel, Is.EqualTo("18:00"));
        Assert.That(plan.Scrub, Is.EqualTo(18));
    }

    [Test]
    public void A_conflict_turns_the_line_to_alert_and_names_both_vessels()
    {
        var state = new PortState(new ManualClock(), new BerthPlan(new Dictionary<string, string> { ["levant"] = "04" }));
        var plan = new DockingViewModel(state);

        Assert.That(plan.HasConflicts, Is.True);
        Assert.That(plan.ConflictText, Is.EqualTo("1 berth conflict: Avren Aurora × Levant Express on 04"));
    }
}
