using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>Berths: the masthead's counts, the status rows' states, picks and hovers, and the detail panel.</summary>
public class BerthsViewModelTests
{
    private static int Index(string berth) => PortData.Berths.ToList().FindIndex(b => b.Number == berth);

    [Test]
    public void The_masthead_counts_the_berths_and_the_conflicts()
    {
        var desk = new Desk();
        var berths = desk.Berths;

        Assert.That((berths.Alongside, berths.Arriving, berths.AtAnchor, berths.Conflicts, berths.HasConflicts),
            Is.EqualTo(("3", "1", "1", "0", false)));
    }

    [Test]
    public void A_conflict_in_the_plan_reads_in_the_masthead()
    {
        var state = new PortState(new ManualClock(), new BerthPlan(new Dictionary<string, string> { ["levant"] = "06", ["kaida"] = "06" }));
        var desk = new Desk(state);

        Assert.That(desk.Berths.HasConflicts, Is.True);
        Assert.That(int.Parse(desk.Berths.Conflicts), Is.GreaterThan(0));
    }

    [Test]
    public void Each_row_says_what_it_stands_for()
    {
        var status = new BerthStatusViewModel(new PortState(new ManualClock()));
        BerthStatusRow Row(string slot) => status.Rows.Single(r => r.Slot == slot);

        Assert.Multiple(() =>
        {
            Assert.That(status.Rows, Has.Count.EqualTo(9), "eight berths and Levant Express at anchor");
            Assert.That(status.Count, Is.EqualTo("8 berths · 1 at anchor"));
            Assert.That(Row("07").Kind, Is.EqualTo(BerthRowKind.Needs));
            Assert.That(Row("07").StateLabel, Is.EqualTo("ARRIVING · CONFIRM BERTH"));
            Assert.That(Row("07").When, Is.EqualTo("42 min"));
            Assert.That(Row("03").Kind, Is.EqualTo(BerthRowKind.Restricted));
            Assert.That(Row("02").Kind, Is.EqualTo(BerthRowKind.Free));
            Assert.That(Row("04").Kind, Is.EqualTo(BerthRowKind.Vessel));
            Assert.That(Row("04").IsCurrent, Is.True, "Avren Aurora is the vessel worked at the start");
            Assert.That(Row("levant").Plate, Is.EqualTo("A"));
            Assert.That(Row("levant").Meta, Is.EqualTo("Unassigned · fits berths 02, 05, 08").Or.StartWith("Unassigned · fits berths"));
        });
    }

    [Test]
    public void Picking_a_free_berth_works_the_berth_and_a_vessel_row_the_vessel()
    {
        var state = new PortState(new ManualClock());
        var status = new BerthStatusViewModel(state);

        status.PickCommand.Execute("02");
        status.Refresh();
        Assert.That(state.SelectedBerth, Is.EqualTo("02"));
        Assert.That(status.Rows.Single(r => r.IsCurrent).Slot, Is.EqualTo("02"));

        status.PickCommand.Execute("01");
        status.Refresh();
        Assert.That((state.SelectedBerth, state.SelectedVesselId), Is.EqualTo(((string?)null, "kaida")));
        Assert.That(status.Rows.Single(r => r.IsCurrent).Slot, Is.EqualTo("01"));
    }

    [Test]
    public void A_hover_elsewhere_lights_the_row_in_place()
    {
        var state = new PortState(new ManualClock());
        var status = new BerthStatusViewModel(state);
        var row = status.Rows.Single(r => r.Slot == "06");

        state.Stage.HoverCommand.Execute("baltic");
        status.RepaintLinks();
        Assert.That(row.IsLinked, Is.True);

        state.Stage.HoverCommand.Execute("-baltic");
        status.RepaintLinks();
        Assert.That(row.IsLinked, Is.False);
        Assert.That(status.Rows.Single(r => r.Slot == "06"), Is.SameAs(row), "rows update in place");
    }

    [Test]
    public void A_confirm_turns_the_needs_row_into_a_vessel_row()
    {
        var state = new PortState(new ManualClock());
        var status = new BerthStatusViewModel(state);

        state.Confirm("nordic");
        status.Refresh();

        var row = status.Rows.Single(r => r.Slot == "07");
        Assert.That(row.Kind, Is.EqualTo(BerthRowKind.Vessel));
        Assert.That(row.StateLabel, Is.EqualTo("ARRIVING · BERTH CONFIRMED"));
    }

    [Test]
    public void The_detail_shows_the_waiting_arrival_in_alert()
    {
        var state = new PortState(new ManualClock());
        state.PickVessel("nordic");
        var detail = new VesselDetailViewModel(state);

        Assert.Multiple(() =>
        {
            Assert.That(detail.Name, Is.EqualTo("Nordic Star"));
            Assert.That(detail.NeedsYou, Is.True);
            Assert.That(detail.IsRestricted, Is.False);
            Assert.That(detail.HasCargo, Is.True);
            Assert.That((detail.Big, detail.BigSub), Is.EqualTo(("42 min", "ETA 21:40")));
            Assert.That(detail.Facts.Single(f => f.Key == "Under-keel").Tone, Is.EqualTo("AlertInkBrush"), "0.5 m under her keel");
        });
    }

    [Test]
    public void The_detail_shows_a_closed_berth_and_its_map_button_opens_the_harbour()
    {
        var state = new PortState(new ManualClock());
        state.SelectedBerth = "03";
        var detail = new VesselDetailViewModel(state);

        Assert.That((detail.Name, detail.StateLabel, detail.IsRestricted, detail.HasCargo), Is.EqualTo(("Berth 03", "RESTRICTED", true, false)));

        state.Navigation.GoCommand.Execute("berths");
        detail.ShowOnMapCommand.Execute(null);
        Assert.That(state.Stage.Open, Is.True);
        Assert.That(state.SelectedBerth, Is.EqualTo("03"), "a berth stays picked");
    }

    [Test]
    public void The_vessel_panel_opens_a_bay_and_counts_the_live_work()
    {
        var state = new PortState(new ManualClock());
        var vessels = new VesselsViewModel(state);

        Assert.Multiple(() =>
        {
            Assert.That(vessels.VesselName, Is.EqualTo("Avren Aurora"));
            Assert.That(vessels.IsCleared, Is.True);
            Assert.That(vessels.Vessels.Single(v => v.IsSelected).Id, Is.EqualTo("aurora"));
            Assert.That(vessels.CompositionSlices.Sum(s => s.Weight), Is.EqualTo(100));
            Assert.That(vessels.Composition.Select(c => c.Fill), Is.EqualTo(Enum.GetValues<CargoClass>().Select(c => c.Token())));
        });

        state.SelectedBay = 3;
        vessels.Refresh();
        Assert.That(vessels.BayHeading, Is.EqualTo($"Bay {BayView.For(PortData.Vessel("aurora"), 3).Number}"));

        vessels.RefreshLive();
        Assert.That(vessels.MiniCards.Select(c => c.Key), Is.EqualTo(new[] { "Length overall", "Discharged", "Loaded", "Departure" }));
        Assert.That(vessels.MiniCards[3].Detail, Is.EqualTo("Berth 04"));
        Assert.That(vessels.UnloadLive, Is.EqualTo("62.0%"));
    }

    [Test]
    public void The_masthead_bar_and_panels_follow_a_drag()
    {
        var desk = new Desk();

        desk.State.Assign("levant", Index("05"));
        desk.Berths.RefreshPanels();

        Assert.That(desk.Berths.Status!.Count, Is.EqualTo("8 berths"));
        Assert.That(desk.Berths.NeedsYou!.IsPending, Is.True, "Nordic Star still waits on 07");
    }
}
