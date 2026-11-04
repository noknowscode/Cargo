using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// The Needs-you panel's model: its rows follow the queue, each row's action does what its label says, the count
/// follows the queue, and the masthead's figure asks the panel to come into view. The shift log offers the same actions.
/// </summary>
public class NeedsYouPanelTests
{
    private static DecisionRow Row(NeedsYouPanelViewModel panel, DecisionKind kind) => panel.Rows.Single(r => r.Decision.Kind == kind);

    [Test]
    public void The_rows_are_the_queue_in_its_order()
    {
        var state = new PortState(new ManualClock());
        var panel = new NeedsYouPanelViewModel(state);

        Assert.Multiple(() =>
        {
            Assert.That(panel.Rows.Select(r => r.Decision), Is.EqualTo(state.NeedsYou.Decisions));
            Assert.That(panel.Rows.All(r => r.Act == panel.ActCommand), Is.True);
            Assert.That(panel.Count, Is.EqualTo(4));
        });
    }

    [Test]
    public void A_settled_row_leaves_and_the_count_follows()
    {
        var state = new PortState(new ManualClock());
        var panel = new NeedsYouPanelViewModel(state);
        var raised = new List<string?>();
        panel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        state.NeedsYou.AssignInspector();

        Assert.Multiple(() =>
        {
            Assert.That(panel.Rows.Select(r => r.Decision.Kind),
                Is.EqualTo(new[] { DecisionKind.DriverCheck, DecisionKind.LateOrders, DecisionKind.BerthConfirm }));
            Assert.That(panel.Count, Is.EqualTo(3));
            Assert.That(raised, Does.Contain(nameof(NeedsYouPanelViewModel.Count)));
        });
    }

    [Test]
    public void Open_inspection_lands_on_Security_inspection()
    {
        var state = new PortState(new ManualClock());
        var panel = new NeedsYouPanelViewModel(state);

        panel.ActCommand.Execute(Row(panel, DecisionKind.InspectionHold).Decision);

        Assert.That((state.Navigation.Section, state.Navigation.SecurityTab), Is.EqualTo(("security", "inspection")));
    }

    [Test]
    public void Verify_driver_lands_on_Access_control()
    {
        var state = new PortState(new ManualClock());
        var panel = new NeedsYouPanelViewModel(state);

        panel.ActCommand.Execute(Row(panel, DecisionKind.DriverCheck).Decision);

        Assert.That((state.Navigation.Section, state.Navigation.SecurityTab), Is.EqualTo(("security", "access")));
    }

    [Test]
    public void View_orders_lands_on_Waterways()
    {
        var state = new PortState(new ManualClock());
        var panel = new NeedsYouPanelViewModel(state);

        panel.ActCommand.Execute(Row(panel, DecisionKind.LateOrders).Decision);

        Assert.That(state.Navigation.Section, Is.EqualTo("fleet"));
    }

    [Test]
    public void Confirm_berth_confirms_her_berth_and_the_row_leaves()
    {
        var state = new PortState(new ManualClock());
        var panel = new NeedsYouPanelViewModel(state);

        panel.ActCommand.Execute(Row(panel, DecisionKind.BerthConfirm).Decision);

        Assert.Multiple(() =>
        {
            Assert.That(state.IsConfirmed("nordic"), Is.True);
            Assert.That(panel.Rows.Any(r => r.Decision.Kind == DecisionKind.BerthConfirm), Is.False);
            Assert.That(panel.Count, Is.EqualTo(3));
        });
    }

    [Test]
    public void The_masthead_figure_asks_the_panel_into_view()
    {
        var state = new PortState(new ManualClock());
        var panel = new NeedsYouPanelViewModel(state);
        var asked = 0;
        panel.RevealRequested += (_, _) => asked++;
        state.Navigation.Section = "cargo";

        state.Navigation.RevealNeedsYouCommand.Execute(null);

        Assert.That((asked, state.Navigation.Section), Is.EqualTo((1, "overview")));
    }

    [Test]
    public void The_shift_log_offers_the_rows_action_on_the_entries_about_them()
    {
        var desk = new Desk();
        var log = desk.Overview.Activity!;
        var hold = log.Left.Concat(log.Right).Single(e => e.About?.Kind == DecisionKind.InspectionHold);

        Assert.That(hold.Act, Is.SameAs(desk.Overview.NeedsYou!.ActCommand));
        hold.Act.Execute(hold.About);

        Assert.That((desk.State.Navigation.Section, desk.State.Navigation.SecurityTab), Is.EqualTo(("security", "inspection")));
    }
}
