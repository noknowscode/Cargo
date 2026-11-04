using System.ComponentModel;
using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// The needs-you state: the queue (<see cref="NeedsYouQueue.Decisions"/>), the count, the header's section dots, the
/// harbour marker (<see cref="PortState.PendingDecision"/>) and the needs-you bar must agree after every berth change.
/// The starting data has one berth decision: Nordic Star, arriving, reserved on berth 07.
/// </summary>
public class DecisionTests
{
    private static int Index(string berth) => PortData.Berths.ToList().FindIndex(b => b.Number == berth);

    private static Decision? BerthRow(PortState state) =>
        state.NeedsYou.Decisions.SingleOrDefault(d => d.Kind == DecisionKind.BerthConfirm);

    /// <summary>The property names the store and its queue raised while <paramref name="act"/> ran.</summary>
    private static List<string?> Raised(PortState state, Action act)
    {
        var raised = new List<string?>();
        void OnChanged(object? sender, PropertyChangedEventArgs e) => raised.Add(e.PropertyName);
        state.PropertyChanged += OnChanged;
        state.NeedsYou.PropertyChanged += OnChanged;
        act();
        state.PropertyChanged -= OnChanged;
        state.NeedsYou.PropertyChanged -= OnChanged;
        return raised;
    }

    /// <summary>The queue, the dot, the count, the marker and the bar all say the same about the berth.</summary>
    private static void AssertAgree(PortState state, NeedsYouViewModel bar, string? pendingVessel, string? berth)
    {
        bar.Refresh(); // the page does this on every structure change while it is shown
        var row = BerthRow(state);

        Assert.Multiple(() =>
        {
            Assert.That(state.PendingDecision, Is.EqualTo(pendingVessel), "harbour marker");
            Assert.That(state.NeedsDecision, Is.EqualTo(pendingVessel is not null), "header dot (NeedsDecision)");
            Assert.That(state.NeedsYou.SectionNeedsYou("berths"), Is.EqualTo(pendingVessel is not null), "Berths dot");
            Assert.That(state.NeedsYou.Count, Is.EqualTo(state.NeedsYou.Decisions.Count), "masthead count");
            Assert.That(row?.VesselId, Is.EqualTo(pendingVessel), "queue row");
            Assert.That(bar.IsPending, Is.EqualTo(pendingVessel is not null), "bar");
            if (pendingVessel is not null)
            {
                var name = PortData.Vessel(pendingVessel).Name;
                Assert.That(row!.Title, Is.EqualTo($"{name} needs berth {berth} confirmed"), "queue row title");
                Assert.That(bar.Title, Is.EqualTo($"{name} needs berth {berth} confirmed"), "bar title");
            }
        });
    }

    [Test]
    public void The_queue_starts_with_the_hold_the_driver_the_late_orders_and_the_berth()
    {
        var state = new PortState(new ManualClock());

        Assert.That(state.NeedsYou.Decisions.Select(d => d.Kind), Is.EqualTo(new[]
        {
            DecisionKind.InspectionHold, DecisionKind.DriverCheck, DecisionKind.LateOrders, DecisionKind.BerthConfirm
        }));
        Assert.That(state.NeedsYou.Count, Is.EqualTo(4));
        // The late orders are handled on Waterways, where View orders lands
        Assert.That(new[] { "berths", "fleet", "security" }.All(state.NeedsYou.SectionNeedsYou), Is.True);
        Assert.That(state.NeedsYou.SectionNeedsYou("cargo"), Is.False);
        AssertAgree(state, new NeedsYouViewModel(state), "nordic", "07");
    }

    [Test]
    public void Dragging_the_arrival_off_her_reserved_berth_settles_the_decision_everywhere()
    {
        // The review's repro: Nordic Star dragged from reserved 07 to 08. The bar said nothing needs you while the
        // queue, the count, the dot and the marker still asked for 07, and the queue's Confirm confirmed 08.
        var state = new PortState(new ManualClock());
        var bar = new NeedsYouViewModel(state);

        var raised = Raised(state, () => Assert.That(state.Assign("nordic", Index("08")), Does.Contain("assigned to Berth 08")));

        Assert.That(raised, Does.Contain(nameof(PortState.PendingDecision)));
        Assert.That(raised, Does.Contain(nameof(PortState.NeedsDecision)));
        Assert.That(raised, Does.Contain(nameof(NeedsYouQueue.Count)));
        Assert.That(state.NeedsYou.Count, Is.EqualTo(3));
        AssertAgree(state, bar, null, null);
        Assert.That(bar.Title, Is.EqualTo("Nothing needs you"));

        // Nothing is waiting, so a confirm (from a stale row or a second click) is refused and changes nothing
        Assert.That(state.Confirm("nordic"), Is.EqualTo("Nordic Star has no berth waiting to be confirmed."));
        Assert.That(state.IsConfirmed("nordic"), Is.False);
        Assert.That(state.BerthOf("nordic"), Is.EqualTo("08"));
    }

    [Test]
    public void Releasing_her_back_onto_the_reservation_raises_the_decision_again()
    {
        var state = new PortState(new ManualClock());
        var bar = new NeedsYouViewModel(state);
        state.Assign("nordic", Index("08"));

        var raised = Raised(state, () => state.Unassign("nordic"));

        Assert.That(raised, Does.Contain(nameof(PortState.PendingDecision)));
        Assert.That(state.NeedsYou.Count, Is.EqualTo(4));
        AssertAgree(state, bar, "nordic", "07");
    }

    [Test]
    public void A_move_that_keeps_the_decision_leaves_the_queue_alone()
    {
        var state = new PortState(new ManualClock());
        var row = BerthRow(state);

        // Levant Express to berth 05 has nothing to do with Nordic Star's reservation
        var raised = Raised(state, () => state.Assign("levant", Index("05")));

        Assert.That(raised, Does.Not.Contain(nameof(PortState.PendingDecision)));
        Assert.That(BerthRow(state), Is.SameAs(row), "the row is not rebuilt");
    }

    [Test]
    public void The_optimiser_moving_the_arrival_off_her_reservation_settles_the_decision()
    {
        // Levant Express put on 07 in Nordic Star's window: both are in conflict, and the optimiser moves both
        var state = new PortState(new ManualClock(), new BerthPlan(new Dictionary<string, string> { ["levant"] = "07" }));
        var bar = new NeedsYouViewModel(state);
        AssertAgree(state, bar, "nordic", "07");

        var summary = state.Optimize();

        Assert.That(summary, Does.StartWith("Optimized: Nordic Star to Berth 02"));
        AssertAgree(state, bar, null, null);
    }

    [Test]
    public void Confirm_from_the_queue_clears_the_row_and_Undo_brings_it_back()
    {
        var state = new PortState(new ManualClock());
        var bar = new NeedsYouViewModel(state);
        var queue = new NeedsYouPanelViewModel(state);

        queue.ActCommand.Execute(BerthRow(state));

        Assert.That(state.IsConfirmed("nordic"), Is.True);
        Assert.That(state.BerthOf("nordic"), Is.EqualTo("07"));
        AssertAgree(state, bar, null, null);

        state.UndoConfirm("nordic");

        Assert.That(state.IsConfirmed("nordic"), Is.False);
        AssertAgree(state, bar, "nordic", "07");
    }

    [Test]
    public void A_refused_confirm_from_the_queue_says_why_on_its_row()
    {
        // Levant Express on 07 holds it during Nordic Star's window, so berth 07 cannot be confirmed for her
        var state = new PortState(new ManualClock(), new BerthPlan(new Dictionary<string, string> { ["levant"] = "07" }));
        var queue = new NeedsYouPanelViewModel(state);

        queue.ActCommand.Execute(BerthRow(state));

        Assert.That(BerthRow(state)?.Refusal, Is.EqualTo("Berth 07 is taken by Levant Express during Nordic Star's window."));
        Assert.That(state.IsConfirmed("nordic"), Is.False);
    }

    [Test]
    public void The_clock_updates_the_rows_timing_in_place()
    {
        var clock = new ManualClock();
        var state = new PortState(clock);
        var hold = state.NeedsYou.Decisions.Single(d => d.Kind == DecisionKind.InspectionHold);
        Assert.That(hold.When, Is.EqualTo("in 17 min"));

        clock.Advance(TimeSpan.FromMinutes(20));

        Assert.That(state.NeedsYou.Decisions[0], Is.SameAs(hold));
        Assert.That(hold.When, Is.EqualTo("3 min late"));
        Assert.That(hold.IsLate, Is.True);
    }
}
