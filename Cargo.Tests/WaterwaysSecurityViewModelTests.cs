using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>Waterways (the fleet, the crew channel) and Security (tabs, badges, the inspection sheet, the gates).</summary>
public class WaterwaysSecurityViewModelTests
{
    [Test]
    public void The_fleet_lists_every_vessel_with_the_channel_open_to_one()
    {
        var desk = new Desk();
        var fleet = desk.Fleet;

        Assert.Multiple(() =>
        {
            Assert.That(fleet.Fleet, Has.Count.EqualTo(PortData.RiverFleet.Count));
            Assert.That(fleet.Fleet.Single(f => f.IsCurrent).Id, Is.EqualTo("al118"));
            Assert.That(fleet.Fleet.Count(f => f.IsLate), Is.EqualTo(2));
            Assert.That(fleet.CrewHeading, Is.EqualTo("Alder Trader · crew"));
            Assert.That((fleet.IsLate, fleet.SlipLabel), Is.EqualTo((true, "+25 min")));
            Assert.That(fleet.ThreadEmpty, Is.False);
            Assert.That(fleet.Lockings.Select(l => l.Standing), Does.Contain(Standing.Done).And.Contain(Standing.Late));
            Assert.That(fleet.Gauges.All(g => g.Spark.Count == 12 && g.SparkArea.Count == 14), Is.True);
        });
    }

    [Test]
    public void Picking_a_vessel_opens_the_channel_to_her()
    {
        var desk = new Desk();

        desk.Fleet.SelectCommand.Execute("ws204");

        Assert.That(desk.Fleet.CrewHeading, Is.EqualTo("Westhaven Spirit · crew"));
        Assert.That((desk.Fleet.IsLate, desk.Fleet.SlipLabel), Is.EqualTo((false, "On time")));
        Assert.That(desk.Fleet.Fleet.Single(f => f.IsCurrent).Id, Is.EqualTo("ws204"));
    }

    [Test]
    public void The_draft_is_sent_on_the_right_and_the_box_clears()
    {
        var desk = new Desk();
        var before = desk.Fleet.Thread.Count;
        desk.State.Chat.Draft = "Slow ahead past the dredger";

        desk.Fleet.SendDraftCommand.Execute(null);

        Assert.That(desk.Fleet.Thread, Has.Count.EqualTo(before + 1));
        Assert.That(desk.Fleet.Thread[^1].FromCrew, Is.False);
        Assert.That(desk.Fleet.Thread[^1].Meta, Is.EqualTo("Dispatch · 20:58"));
        Assert.That(desk.State.Chat.Draft, Is.Empty);
    }

    [Test]
    public void Confirm_new_ETA_to_a_late_crew_handles_her_late_order()
    {
        var desk = new Desk();
        var eta = desk.Fleet.QuickMessages.Single(q => q.Id == "eta");
        Assert.That(eta.AccessibleName, Is.EqualTo("Confirm new ETA: send to Alder Trader, handles her late order"));

        eta.Send.Execute(eta.Id);

        Assert.That(desk.State.Decisions.Single(d => d.Kind == DecisionKind.LateOrders).Title, Is.EqualTo("1 order late"));
        Assert.That(desk.Fleet.Thread[^1].Text, Does.StartWith("Please confirm revised ETA"));
        Assert.That(desk.Fleet.QuickMessages.Single(q => q.Id == "eta").AccessibleName, Is.EqualTo("Confirm new ETA: send to Alder Trader"));
    }

    [Test]
    public void The_other_quick_messages_just_send()
    {
        var desk = new Desk();
        var count = desk.State.NeedsYouCount;

        desk.Fleet.SendQuickCommand.Execute("hold");

        Assert.That(desk.Fleet.Thread[^1].Text, Does.StartWith("Hold at "));
        Assert.That(desk.State.NeedsYouCount, Is.EqualTo(count));
    }

    [Test]
    public void Securitys_tabs_follow_the_store_and_the_router()
    {
        var desk = new Desk();
        Assert.That(desk.Security.Tabs.Single(t => t.IsCurrent).Label, Is.EqualTo("Zones"));
        Assert.That(desk.Security.Eyebrow, Is.EqualTo("SECURITY · ISPS LEVEL 1 · 1 HOLD ACTIVE"));

        desk.Routes.Report("security/access");
        Assert.That(desk.State.Navigation.SecurityTab, Is.EqualTo("access"));
        Assert.That(desk.Security.Tabs.Single(t => t.IsCurrent).AccessibleName, Is.EqualTo("Access control, current tab"));
    }

    [Test]
    public void Picking_a_badge_shows_its_zones()
    {
        var desk = new Desk();

        desk.Security.SelectPersonCommand.Execute(2);

        var person = PortData.People[2];
        Assert.That(desk.Security.People.Single(p => p.IsSelected).Name, Is.EqualTo(person.Name));
        Assert.That(desk.Security.AllowedZones, Is.EqualTo(person.Zones));
        Assert.That(desk.Security.ZoneCount, Is.EqualTo($"{person.Zones.Count} of {PortData.AccessZones.Count}"));
    }

    [Test]
    public void The_inspection_sheets_actions_log_once_and_stand_done()
    {
        var desk = new Desk();
        var logged = desk.Security.InspectionLog.Count;

        desk.Security.AssignInspectorCommand.Execute(null);
        desk.Security.RequestReweighCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(desk.Security.InspectionLog, Has.Count.EqualTo(logged + 2));
            Assert.That(desk.Security.InspectionLog[^2].Label, Does.StartWith("Inspector assigned"));
            Assert.That(desk.Security.InspectionLog[^1].Time, Is.EqualTo("20:58"));
            Assert.That((desk.Security.AssignLabel, desk.Security.ReweighLabel), Is.EqualTo(("Inspector assigned", "Re-weigh requested")));
            Assert.That(desk.Security.AssignInspectorCommand.CanExecute(null), Is.False);
            Assert.That(desk.Security.RequestReweighCommand.CanExecute(null), Is.False);
            Assert.That(desk.State.Decisions.Any(d => d.Kind == DecisionKind.InspectionHold), Is.False);
        });
    }

    [Test]
    public void Verifying_the_waiting_driver_authorizes_his_truck()
    {
        var desk = new Desk();
        var waiting = desk.Security.Vehicles.Single(v => v.AwaitsDriver);

        waiting.Verify.Execute(waiting.Id);

        var card = desk.Security.Vehicles.Single(v => v.Id == waiting.Id);
        Assert.That(card.AwaitsDriver, Is.False);
        Assert.That(card.Facts.Single(f => f.Key == "Authorization").Value, Is.EqualTo("Authorized · driver ID verified"));
        Assert.That(card.AccessibleName, Does.EndWith("Authorized · driver ID verified"));
        Assert.That(desk.State.Decisions.Any(d => d.Kind == DecisionKind.DriverCheck), Is.False);
    }
}
