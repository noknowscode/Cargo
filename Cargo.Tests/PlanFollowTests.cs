using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// The words about a vessel follow the berth plan: after a drag or the optimiser her status line, her note, the Berths
/// status row, the docking card and the vessel list name the berth the plan gives her, not the data's.
/// </summary>
public class PlanFollowTests
{
    private static int Index(string berth) => PortData.Berths.ToList().FindIndex(b => b.Number == berth);

    [Test]
    public void At_the_start_the_lines_read_as_the_data_does()
    {
        var state = new PortState(new ManualClock());

        Assert.Multiple(() =>
        {
            Assert.That(VesselCopy.StatusLine(state, PortData.Vessel("nordic")), Is.EqualTo("Arriving in 42 min · assigned Berth 07"));
            Assert.That(VesselCopy.StatusLine(state, PortData.Vessel("levant")), Is.EqualTo("At anchor · Outer roads"));
            Assert.That(VesselCopy.StatusLine(state, PortData.Vessel("aurora")), Is.EqualTo("Docked · Berth 04 · discharging"));
            Assert.That(VesselCopy.Note(state, PortData.Vessel("levant")), Is.EqualTo(PortData.Vessel("levant").Note));
        });
    }

    [Test]
    public void An_arrival_counts_down_in_her_status_line()
    {
        var clock = new ManualClock();
        var state = new PortState(clock);

        clock.Advance(TimeSpan.FromMinutes(40));
        Assert.That(VesselCopy.StatusLine(state, PortData.Vessel("nordic")), Is.EqualTo("Arriving in 2 min · assigned Berth 07"));

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.That(VesselCopy.StatusLine(state, PortData.Vessel("nordic")), Is.EqualTo("Arriving now · assigned Berth 07"));
    }

    [Test]
    public void A_drag_moves_her_berth_in_every_line()
    {
        var state = new PortState(new ManualClock());
        var docking = new DockingViewModel(state);
        state.DockSelection = "nordic";

        state.Assign("nordic", Index("08"));
        docking.Refresh();
        var vessels = new VesselsViewModel(state);

        Assert.Multiple(() =>
        {
            Assert.That(VesselCopy.StatusLine(state, PortData.Vessel("nordic")), Is.EqualTo("Arriving in 42 min · assigned Berth 08"));
            Assert.That(VesselCopy.Note(state, PortData.Vessel("nordic")).Full, Is.EqualTo("Assigned to Berth 08 · pilots and mooring gang notified."));
            Assert.That(docking.SelectedStatus, Is.EqualTo("Ferrand-Lowe · Arriving in 42 min · assigned Berth 08"));
            Assert.That(vessels.Vessels.Single(v => v.Id == "nordic").Line1, Is.EqualTo("Arriving in 42 min · assigned Berth 08"));
        });
    }

    [Test]
    public void The_optimiser_berths_the_anchored_vessel_and_her_row_says_so()
    {
        var state = new PortState(new ManualClock());
        var status = new BerthStatusViewModel(state);

        Assert.That(state.Optimize(), Does.StartWith("Optimized: Levant Express to Berth 05"));
        status.Refresh();

        var row = status.Rows.Single(r => r.Slot == "05");
        Assert.Multiple(() =>
        {
            Assert.That(row.Name, Is.EqualTo("Levant Express"));
            Assert.That(row.Meta, Is.EqualTo("Assigned to Berth 05 · pilots and mooring gang notified"));
            Assert.That(VesselCopy.StatusLine(state, PortData.Vessel("levant")), Is.EqualTo("At anchor · assigned Berth 05"));
            Assert.That(status.Rows.Any(r => r.Slot == "levant"), Is.False, "she has left the anchorage rows");
        });
    }

    [Test]
    public void The_vessel_list_says_what_an_unberthed_vessel_waits_for()
    {
        var state = new PortState(new ManualClock());
        var vessels = new VesselsViewModel(state);

        var levant = vessels.Vessels.Single(v => v.Id == "levant");
        Assert.That((levant.Line1, levant.Line2), Is.EqualTo(("At anchor · Outer roads", "Waiting for Berth 06")));

        state.Optimize();
        vessels.RefreshLive();
        Assert.That((levant.Line1, levant.Line2), Is.EqualTo(("At anchor · assigned Berth 05", string.Empty)));
    }

    [Test]
    public void The_vessel_detail_note_follows_the_plan()
    {
        var state = new PortState(new ManualClock());
        state.Optimize();
        state.PickVessel("levant");
        var detail = new VesselDetailViewModel(state);

        Assert.That(detail.Note, Is.EqualTo("Assigned to Berth 05 · pilots and mooring gang notified"));
        Assert.That(detail.Where, Is.EqualTo("Berth 05"));
    }
}
