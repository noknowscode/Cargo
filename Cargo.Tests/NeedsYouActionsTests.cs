using Cargo.Domain;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// The needs-you rows besides the berth can be settled where they send you: the hold by assigning the inspector, the
/// driver check by verifying the driver at Access control, the late orders by confirming each crew's revised ETA.
/// </summary>
public class NeedsYouActionsTests
{
    private static DecisionKind[] Kinds(PortState state) => state.Decisions.Select(d => d.Kind).ToArray();

    [Test]
    public void The_queue_starts_with_four_rows_and_the_late_orders_dot_on_Waterways()
    {
        var state = new PortState(new ManualClock());

        Assert.That(Kinds(state), Is.EqualTo(new[]
        {
            DecisionKind.InspectionHold, DecisionKind.DriverCheck, DecisionKind.LateOrders, DecisionKind.BerthConfirm
        }));
        Assert.That(state.SectionNeedsYou("fleet"), Is.True);
        Assert.That(state.SectionNeedsYou("cargo"), Is.False);
    }

    [Test]
    public void Assigning_the_inspector_settles_the_hold_once()
    {
        var state = new PortState(new ManualClock());
        var structures = 0;
        state.StructureChanged += (_, _) => structures++;

        Assert.That(state.AssignInspector(), Is.True);
        Assert.That(state.AssignInspector(), Is.False, "a second press does nothing");

        Assert.That(Kinds(state), Does.Not.Contain(DecisionKind.InspectionHold));
        Assert.That(state.NeedsYouCount, Is.EqualTo(3));
        Assert.That(state.SectionNeedsYou("security"), Is.True, "the driver check still waits there");
        Assert.That(structures, Is.EqualTo(1));
    }

    [Test]
    public void Verifying_the_driver_settles_his_row_and_authorizes_the_truck()
    {
        var state = new PortState(new ManualClock());
        var truck = PortData.Vehicles.Single(v => v.Authorization == "Awaiting driver ID");

        Assert.That(state.VerifyDriver("TRK 0000"), Is.False, "an unknown truck");
        Assert.That(state.VerifyDriver(truck.Id), Is.True);
        Assert.That(state.VerifyDriver(truck.Id), Is.False, "already verified");

        Assert.Multiple(() =>
        {
            Assert.That(Kinds(state), Does.Not.Contain(DecisionKind.DriverCheck));
            Assert.That(state.AwaitsDriver(truck), Is.False);
            Assert.That(state.AuthorizationOf(truck), Is.EqualTo("Authorized · driver ID verified"));
        });
    }

    [Test]
    public void Both_settled_leave_Security_without_a_dot()
    {
        var state = new PortState(new ManualClock());

        state.AssignInspector();
        state.VerifyDriver(PortData.Vehicles.Single(v => v.Authorization == "Awaiting driver ID").Id);

        Assert.That(state.SectionNeedsYou("security"), Is.False);
    }

    [Test]
    public void Confirming_each_late_crews_ETA_counts_the_late_orders_down_to_none()
    {
        var state = new PortState(new ManualClock());
        var late = PortData.RiverFleet.Where(v => v.SlipMinutes > 0).ToArray();
        Assert.That(late, Has.Length.EqualTo(2));

        state.ConfirmRevisedEta(late[0].Id);
        var row = state.Decisions.Single(d => d.Kind == DecisionKind.LateOrders);
        Assert.That((row.Title, row.Sub), Is.EqualTo(("1 order late", "In the yard queue")));
        Assert.That(state.Chat.ThreadFor(late[0].Id)[^1].Text, Is.EqualTo(PortState.RevisedEtaMessage(late[0])));

        state.ConfirmRevisedEta(late[1].Id);
        Assert.That(Kinds(state), Does.Not.Contain(DecisionKind.LateOrders));
        Assert.That(state.SectionNeedsYou("fleet"), Is.False);
    }

    [Test]
    public void Confirming_an_on_time_crew_only_sends_the_message()
    {
        var state = new PortState(new ManualClock());
        var onTime = PortData.RiverFleet.First(v => v.SlipMinutes <= 0);
        var count = state.NeedsYouCount;

        state.ConfirmRevisedEta(onTime.Id);

        Assert.That(state.NeedsYouCount, Is.EqualTo(count));
        Assert.That(state.Chat.ThreadFor(onTime.Id)[^1].Who, Is.EqualTo("dispatch"));
    }
}
