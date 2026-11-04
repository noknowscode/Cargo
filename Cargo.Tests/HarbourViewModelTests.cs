using Cargo.Controls.Harbour;
using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;
using Windows.Foundation;

namespace Cargo.Tests;

/// <summary>
/// The harbour stage's model: the tags' words, which tag is dark, selected or lit, the facts card, the layer and
/// selection the scene shows per section, the screen-reader line; and the pure helpers the view places them with (the
/// tag layout, the band's slide, the camera's poses).
/// </summary>
public class HarbourViewModelTests
{
    private static HarbourTag Tag(HarbourViewModel harbour, string id) => harbour.Tags.Single(t => t.Id == id);

    [Test]
    public void Every_vessel_has_a_tag_in_the_plans_words()
    {
        var harbour = new HarbourViewModel(new PortState(new ManualClock()));
        var nordic = Tag(harbour, "nordic");

        Assert.Multiple(() =>
        {
            Assert.That(harbour.Tags.Select(t => t.Sub), Is.EqualTo(new[]
            {
                "B04 · discharging", "B07 · arriving 21:40", "B01 · loading", "B06 · departing", "anchored · unassigned"
            }));
            Assert.That((nordic.IsPending, nordic.PendingSub, nordic.Countdown), Is.EqualTo((true, "B07 · pilot aboard", "in 42 min")));
            Assert.That(nordic.AccessibleName, Is.EqualTo("Nordic Star, Arriving in 42 min · assigned Berth 07"));
            Assert.That(harbour.Tags.Count(t => t.IsPending), Is.EqualTo(1));
            Assert.That(nordic.Facts.Select(f => f.Value), Is.EqualTo(new[] { "Today 21:40", "Thu 18:00", "299 m · 12.9 m", "Pending" }));
        });
    }

    [Test]
    public void The_clock_counts_the_dark_tag_down()
    {
        var clock = new ManualClock();
        var harbour = new HarbourViewModel(new PortState(clock));

        clock.Advance(TimeSpan.FromMinutes(1));
        harbour.Tick();

        var nordic = Tag(harbour, "nordic");
        Assert.That((nordic.Countdown, nordic.AccessibleName), Is.EqualTo(("in 41 min", "Nordic Star, Arriving in 41 min · assigned Berth 07")));
    }

    [Test]
    public void A_selected_vessel_opens_the_facts_card_unless_the_frame_is_short()
    {
        var state = new PortState(new ManualClock());
        var harbour = new HarbourViewModel(state);
        Assert.That(harbour.HasSelectedTag, Is.False, "nothing is picked on Overview at the start");

        state.Stage.Selection = "aurora";
        harbour.Paint();
        Assert.Multiple(() =>
        {
            Assert.That(Tag(harbour, "aurora").IsSelected, Is.True);
            Assert.That((harbour.HasSelectedTag, harbour.SelectedTag?.Id, harbour.SelectedLine), Is.EqualTo((true, "aurora", "B04 · discharging")));
        });

        harbour.Tight = true;
        Assert.That((harbour.HasSelectedTag, harbour.SelectedTag?.Id), Is.EqualTo((false, "aurora")), "the card fades out on its last vessel");
    }

    [Test]
    public void The_waiting_arrivals_card_reads_her_pending_line()
    {
        var state = new PortState(new ManualClock());
        var harbour = new HarbourViewModel(state);

        state.Stage.Selection = "nordic";
        harbour.Paint();

        Assert.That(harbour.SelectedLine, Is.EqualTo("B07 · pilot aboard"));
    }

    [Test]
    public void A_linked_hover_lights_its_tag()
    {
        var state = new PortState(new ManualClock());
        var harbour = new HarbourViewModel(state);

        state.Stage.HoverCommand.Execute("kaida");
        harbour.Paint();

        Assert.That(harbour.Tags.Where(t => t.IsLit).Select(t => t.Id), Is.EqualTo(new[] { "kaida" }));
    }

    [Test]
    public void A_confirmed_berth_turns_the_dark_tag_light_and_tells_the_scene()
    {
        var state = new PortState(new ManualClock());
        var harbour = new HarbourViewModel(state);
        var told = 0;
        harbour.PendingChanged += (_, _) => told++;

        state.Confirm("nordic");

        Assert.That((told, Tag(harbour, "nordic").IsPending), Is.EqualTo((1, false)));
    }

    [TestCase("overview", "port", null)]
    [TestCase("berths", "port", "aurora")]
    [TestCase("cargo", "yard", null)]
    [TestCase("security", "security", null)]
    public void Each_section_shows_its_layer_and_selection(string section, string layer, string? selection)
    {
        var state = new PortState(new ManualClock());
        var harbour = new HarbourViewModel(state);

        state.Navigation.Section = section;

        Assert.That((harbour.Layer, harbour.Selection), Is.EqualTo((layer, selection)));
    }

    [Test]
    public void Moving_between_sections_or_vessels_asks_for_a_new_frame()
    {
        var state = new PortState(new ManualClock());
        var harbour = new HarbourViewModel(state);
        var asked = 0;
        harbour.ReframeRequested += (_, _) => asked++;

        state.Navigation.Section = "berths";
        state.PickVessel("kaida");
        state.Stage.Open = true;

        Assert.That(asked, Is.EqualTo(3));
    }

    [Test]
    public void The_screen_reader_line_names_the_view_the_layer_and_the_selection()
    {
        var state = new PortState(new ManualClock());
        var harbour = new HarbourViewModel(state);
        Assert.That(harbour.Summary("Overview"), Is.EqualTo("Overview view, Map layer. 3 vessels alongside, 1 arriving."));

        state.Stage.Layer = "traffic";
        state.Stage.Selection = "aurora";

        Assert.That(harbour.Summary(HarbourViewModel.ViewName("sea")),
            Is.EqualTo("From sea view, Traffic layer. 3 vessels alongside, 1 arriving. Selected: Avren Aurora, Docked · Berth 04 · discharging."));
        Assert.That(HarbourViewModel.ViewName(null), Is.EqualTo("Custom"));
    }

    [Test]
    public void The_dark_tag_is_placed_first_then_the_selection_then_left_to_right()
    {
        var state = new PortState(new ManualClock());
        var harbour = new HarbourViewModel(state);
        state.Stage.Selection = "levant";
        var x = new Dictionary<string, double> { ["aurora"] = 300, ["nordic"] = 500, ["kaida"] = 100, ["baltic"] = 200, ["levant"] = 900 };

        var order = harbour.PlacementOrder(t => x[t.Id]).Select(t => t.Id);

        Assert.That(order, Is.EqualTo(new[] { "nordic", "levant", "kaida", "baltic", "aurora" }));
    }

    // ── The view's helpers ───────────────────────────────────────────────────

    [Test]
    public void A_tag_sits_centred_on_its_leader_above_the_mast()
    {
        var layout = new TagLayout(maxRight: 800, minTop: 50, maxBottom: 600);

        Assert.That(layout.Place(200, 300, 100, 30), Is.EqualTo(new Rect(150, 240, 100, 30)));
        Assert.That(layout.Place(20, 300, 100, 30)?.X, Is.EqualTo(TagLayout.Edge), "kept inside the frame's left edge");
    }

    [Test]
    public void A_tag_that_lands_on_another_moves_above_then_beside()
    {
        var roomy = new TagLayout(maxRight: 800, minTop: 50, maxBottom: 600);
        roomy.Place(200, 300, 100, 30);
        Assert.That(roomy.Place(210, 300, 100, 30), Is.EqualTo(new Rect(160, 204, 100, 30)), "above, on a longer leader");

        var low = new TagLayout(maxRight: 800, minTop: 230, maxBottom: 600);
        low.Place(200, 300, 100, 30);
        Assert.That(low.Place(200, 300, 100, 30), Is.EqualTo(new Rect(256, 240, 100, 30)), "no room above: to the right");
    }

    [Test]
    public void A_tag_with_no_clear_spot_gives_way()
    {
        var narrow = new TagLayout(maxRight: 200, minTop: 230, maxBottom: 280);
        Assert.That(narrow.Place(200, 300, 100, 30), Is.Not.Null);

        Assert.That(narrow.Place(200, 300, 100, 30), Is.Null);
    }

    [TestCase(491, 0, 0, 0)]
    [TestCase(340, 110, 41, 0)]
    [TestCase(180, 180, 131, 0)]
    [TestCase(461, 110, 41, 121)]
    public void The_band_slides_the_scene_by_its_height(double visible, double offset, double below, double overlay)
    {
        Assert.That(HarbourBand.Slide(band: true, visible, inset: overlay, overlay), Is.EqualTo((offset, below)));
        Assert.That(HarbourBand.Slide(band: false, visible, inset: overlay, overlay), Is.EqualTo((0d, 0d)));
    }

    [Test]
    public void The_camera_presets_and_the_strips_frames()
    {
        var overview = HarbourCameraPose.Overview;

        Assert.Multiple(() =>
        {
            Assert.That(HarbourCameraPose.Preset("sea", overview), Is.EqualTo(HarbourCameraPose.FromSea(overview)));
            Assert.That(HarbourCameraPose.Preset("plan", overview), Is.EqualTo(HarbourCameraPose.Plan(overview)));
            Assert.That(HarbourCameraPose.Preset(null, HarbourCameraPose.Plan(overview)), Is.EqualTo(overview));
            Assert.That((HarbourCameraPose.ForStrip("security", (1, 2)).TargetX, HarbourCameraPose.ForStrip("cargo", null).TargetY), Is.EqualTo((40d, -66d)));
            Assert.That(HarbourCameraPose.ForStrip("berths", (10, 20)), Is.EqualTo(new HarbourCameraPose(-14 * Math.PI / 180, 26 * Math.PI / 180, 4.6, 10, 22)));
            Assert.That(HarbourCameraPose.ForStrip("berths", null), Is.EqualTo(overview with { Pitch = 26 * Math.PI / 180 }));
        });
    }
}
