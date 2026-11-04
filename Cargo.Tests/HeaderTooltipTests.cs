using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>The header tabs' tooltips read the store: the countdowns, the plan, the yard and the queue as they stand.</summary>
public class HeaderTooltipTests
{
    [Test]
    public void Overview_counts_the_vessels_and_the_moves_so_far()
    {
        var state = new PortState(new ManualClock());

        Assert.That(MainViewModel.SectionSummary(state, "overview"),
            Is.EqualTo($"3 alongside · 2 inbound · {ShiftTimeline.MovesToday(state.NowHours):N0} moves today"));
    }

    [Test]
    public void Berths_counts_down_as_the_tags_do_and_names_the_next_berth_to_free()
    {
        var clock = new ManualClock();
        var state = new PortState(clock);

        Assert.That(MainViewModel.SectionSummary(state, "berths"),
            Is.EqualTo("Nordic Star in 42 min · Berth 06 frees 21:30 · berth 07 to confirm"));

        state.Confirm("nordic");
        Assert.That(MainViewModel.SectionSummary(state, "berths"), Is.EqualTo("Nordic Star in 42 min · Berth 06 frees 21:30"));

        // Past Baltic Crown's 21:30 the next berth to free is Kaida Maru's
        clock.Advance(TimeSpan.FromMinutes(33));
        Assert.That(MainViewModel.SectionSummary(state, "berths"), Is.EqualTo("Nordic Star in 9 min · Berth 01 frees 23:30"));
    }

    [Test]
    public void Berths_names_the_berth_the_plan_holds_her_on()
    {
        var state = new PortState(new ManualClock(), new BerthPlan(new Dictionary<string, string> { ["nordic"] = "05" }));

        Assert.That(MainViewModel.SectionSummary(state, "berths"), Does.Not.Contain("berth 07"));
    }

    [Test]
    public void Cargo_reads_the_yard_as_it_works()
    {
        var clock = new ManualClock();
        var state = new PortState(clock);

        clock.Advance(TimeSpan.FromSeconds(4));

        Assert.That(MainViewModel.SectionSummary(state, "cargo"), Is.EqualTo($"4,812 on site · {state.Yard.Occupancy}% yard · 1 hold"));
    }

    [Test]
    public void Waterways_counts_the_late_orders_until_their_ETAs_are_confirmed()
    {
        var state = new PortState(new ManualClock());
        Assert.That(MainViewModel.SectionSummary(state, "fleet"), Is.EqualTo("6 underway · 2 running late · 2 ETAs to confirm"));

        foreach (var late in PortData.RiverFleet.Where(v => v.SlipMinutes > 0))
        {
            state.NeedsYou.ConfirmRevisedEta(late.Id);
        }

        Assert.That(MainViewModel.SectionSummary(state, "fleet"), Is.EqualTo("6 underway · 2 running late"));
    }

    [Test]
    public void Security_reads_the_incident_record_and_the_checks_waiting()
    {
        var state = new PortState(new ManualClock());
        Assert.That(MainViewModel.SectionSummary(state, "security"), Is.EqualTo("ISPS level 1 · 1 incident · 2 checks waiting"));

        state.NeedsYou.AssignInspector();
        Assert.That(MainViewModel.SectionSummary(state, "security"), Is.EqualTo("ISPS level 1 · 1 incident · 1 check waiting"));
    }
}
