using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// The words every screen uses about each vessel at the start of the shift: her status line, her list row's two lines,
/// her operations note and its lead, the Berths rows and the queue's berth row. Pinned for all five vessels so the
/// copy can be built from the data's fields without changing a word.
/// </summary>
public class VesselCopyTests
{
    private static readonly string[] Ids = { "aurora", "nordic", "kaida", "baltic", "levant" };

    [TestCase("aurora", "Docked · Berth 04", "Discharging 62%")]
    [TestCase("nordic", "Arriving in 42 min · assigned Berth 07", "")]
    [TestCase("kaida", "Docked · Berth 01", "Loading 74%")]
    [TestCase("baltic", "Departing 21:30 · Berth 06", "")]
    [TestCase("levant", "At anchor · Outer roads", "Waiting for Berth 06")]
    public void The_vessel_list_rows(string id, string line1, string line2)
    {
        var vessels = new VesselsViewModel(new PortState(new ManualClock()));

        var row = vessels.Vessels.Single(v => v.Id == id);

        Assert.That((row.Line1, row.Line2), Is.EqualTo((line1, line2)));
    }

    [Test]
    public void The_status_lines()
    {
        var state = new PortState(new ManualClock());

        Assert.That(Ids.Select(id => state.StatusLine(PortData.Vessel(id))), Is.EqualTo(new[]
        {
            "Docked · Berth 04 · discharging",
            "Arriving in 42 min · assigned Berth 07",
            "Docked · Berth 01 · loading",
            "Departing 21:30 · Berth 06",
            "At anchor · Outer roads"
        }));
    }

    [TestCase("aurora", "4 cranes working · est. completion 03:40", "4 cranes working · est. completion 03:40. Loading begins once bays 01–06 are clear.")]
    [TestCase("nordic", "Pilot boarded 20:35 · 2 tugs assigned", "Pilot boarded 20:35 · 2 tugs assigned. Discharge starts ~22:30 after mooring.")]
    [TestCase("kaida", "Discharge complete 09:12", "Discharge complete 09:12. Loading 74% · 3 cranes · sailing 23:30 with the tide.")]
    [TestCase("baltic", "Cargo complete · lashing certified 20:15", "Cargo complete · lashing certified 20:15. Pilot due 21:10, tugs standing by.")]
    [TestCase("levant", "Waiting for Berth 06 to clear after Baltic Crown departs", "Waiting for Berth 06 to clear after Baltic Crown departs. Assign a berth in Docking.")]
    public void The_notes_and_their_lead(string id, string lead, string full)
    {
        var state = new PortState(new ManualClock());
        state.PickVessel(id);
        var detail = new VesselDetailViewModel(state);
        var vessels = new VesselsViewModel(state);

        Assert.That((detail.Note, vessels.OpsNote), Is.EqualTo((lead, full)));
    }

    [Test]
    public void The_Berths_rows_lead_with_the_note_or_the_pilot()
    {
        var status = new BerthStatusViewModel(new PortState(new ManualClock()));
        string Meta(string slot) => status.Rows.Single(r => r.Slot == slot).Meta;

        Assert.Multiple(() =>
        {
            Assert.That(Meta("01"), Is.EqualTo("Discharge complete 09:12"));
            Assert.That(Meta("04"), Is.EqualTo("4 cranes working · est. completion 03:40"));
            Assert.That(Meta("06"), Is.EqualTo("Cargo complete · lashing certified 20:15"));
            Assert.That(Meta("07"), Is.EqualTo("Pilot aboard · under-keel 0.5 m"));
        });
    }

    [Test]
    public void The_queue_row_for_the_berth_says_when_she_arrives()
    {
        var state = new PortState(new ManualClock());

        var row = state.Decisions.Single(d => d.Kind == DecisionKind.BerthConfirm);

        Assert.That((row.Title, row.Sub, row.When), Is.EqualTo(("Nordic Star needs berth 07 confirmed", "Pilot aboard", "arrives 21:40")));
    }
}
