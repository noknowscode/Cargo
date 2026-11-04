using Cargo.Domain;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// The store's parts other than the berth desk: where the dispatcher is (sections, jumps, Security's tab), the harbour
/// stage (its heights, the toggle, layers, hovers and the hull pick), the scanner's camera, the yard and the crew chat.
/// </summary>
public class StoreTests
{
    private static PortState Store() => new(new ManualClock());

    // ── Navigation ──────────────────────────────────────────────────────────

    [Test]
    public void Go_maps_a_retired_section_onto_the_page_that_holds_it()
    {
        var state = Store();

        state.Navigation.GoCommand.Execute("vessels");
        Assert.That(state.Navigation.Section, Is.EqualTo("berths"));

        state.Navigation.GoCommand.Execute("yard");
        Assert.That(state.Navigation.Section, Is.EqualTo("cargo"));

        state.Navigation.GoCommand.Execute("activity");
        Assert.That(state.Navigation.Section, Is.EqualTo("overview"));
    }

    [Test]
    public void Opening_a_vessel_picks_her_on_Berths_with_no_bay_open()
    {
        var state = Store();
        state.SelectedBay = 3;

        state.OpenVesselCommand.Execute("kaida");

        Assert.Multiple(() =>
        {
            Assert.That(state.Navigation.Section, Is.EqualTo("berths"));
            Assert.That(state.SelectedVesselId, Is.EqualTo("kaida"));
            Assert.That(state.SelectedBay, Is.Null);
        });
    }

    [Test]
    public void Opening_docking_puts_the_selected_vessel_in_the_planner()
    {
        var state = Store();
        state.OpenVesselCommand.Execute("levant");
        state.Navigation.GoCommand.Execute("overview");

        state.OpenDockingCommand.Execute(null);

        Assert.That(state.Navigation.Section, Is.EqualTo("berths"));
        Assert.That(state.DockSelection, Is.EqualTo("levant"));
    }

    [Test]
    public void The_security_jumps_land_on_their_tab()
    {
        var state = Store();

        state.Navigation.OpenInspectionCommand.Execute(null);
        Assert.That((state.Navigation.Section, state.Navigation.SecurityTab, state.Navigation.SecurityTitle), Is.EqualTo(("security", "inspection", "Cargo inspection")));

        state.Navigation.OpenAccessCommand.Execute(null);
        Assert.That((state.Navigation.Section, state.Navigation.SecurityTab, state.Navigation.SecurityTitle), Is.EqualTo(("security", "access", "Access control")));
    }

    [Test]
    public void Revealing_needs_you_goes_to_Overview_and_bumps_the_reveal()
    {
        var state = Store();
        state.Navigation.GoCommand.Execute("fleet");

        state.Navigation.RevealNeedsYouCommand.Execute(null);
        state.Navigation.RevealNeedsYouCommand.Execute(null);

        Assert.That(state.Navigation.Section, Is.EqualTo("overview"));
        Assert.That(state.Navigation.NeedsYouReveal, Is.EqualTo(2));
    }

    [Test]
    public void A_section_change_is_a_structure_change()
    {
        var state = Store();
        var raised = 0;
        state.StructureChanged += (_, _) => raised++;

        state.Navigation.GoCommand.Execute("cargo");

        Assert.That(raised, Is.EqualTo(1));
    }

    // ── Stage ───────────────────────────────────────────────────────────────

    [Test]
    public void Overview_has_the_full_band_at_its_three_heights()
    {
        var state = Store();

        Assert.Multiple(() =>
        {
            Assert.That(state.Stage.Shown && state.Stage.Full && !state.Stage.Compact, Is.True);
            Assert.That(state.Stage.Height, Is.EqualTo(340));
            Assert.That(state.Stage.ToggleLabel, Is.EqualTo("Expand"));
            Assert.That(state.Stage.MastheadShown && state.Stage.LayerSwitchShown, Is.True);
        });

        state.Stage.ToggleCommand.Execute(null);
        Assert.That((state.Stage.Height, state.Stage.Expanded, state.Stage.ToggleLabel), Is.EqualTo((491d, true, "Collapse")));

        state.Stage.OverviewCondensed = true;
        Assert.Multiple(() =>
        {
            Assert.That(state.Stage.Height, Is.EqualTo(180));
            Assert.That(state.Stage.Collapsed, Is.True);
            Assert.That(state.Stage.ToggleLabel, Is.EqualTo("Show harbour"));
            Assert.That(state.Stage.MastheadShown || state.Stage.LayerSwitchShown, Is.False);
        });
    }

    [Test]
    public void The_toggle_on_a_condensed_Overview_asks_for_the_top()
    {
        var state = Store();
        state.Stage.OverviewCondensed = true;
        var asked = 0;
        state.Stage.ScrollToTopRequested += (_, _) => asked++;

        state.Stage.ToggleCommand.Execute(null);

        Assert.That(asked, Is.EqualTo(1));
        Assert.That(state.Stage.OverviewExpanded, Is.False);
    }

    [Test]
    public void Other_sections_open_their_strip_and_a_move_closes_it()
    {
        var state = Store();
        state.Navigation.GoCommand.Execute("berths");

        Assert.Multiple(() =>
        {
            Assert.That(state.Stage.Compact, Is.True);
            Assert.That(double.IsNaN(state.Stage.Height), Is.True);
            Assert.That(state.Stage.ToggleLabel, Is.EqualTo("Expand harbour"));
            Assert.That(state.Stage.LayerSwitchShown, Is.False);
        });

        state.Stage.ToggleCommand.Execute(null);
        Assert.That((state.Stage.Open, state.Stage.Full, state.Stage.Compact, state.Stage.ToggleLabel),
            Is.EqualTo((true, true, false, "Collapse harbour")));

        state.Navigation.GoCommand.Execute("cargo");
        Assert.That(state.Stage.Open, Is.False);
        Assert.That(state.Stage.Compact, Is.True);
    }

    [Test]
    public void Waterways_has_no_stage()
    {
        var state = Store();
        state.Navigation.GoCommand.Execute("fleet");

        Assert.That((state.Stage.Shown, state.Stage.ShowToggle, state.Stage.Compact), Is.EqualTo((false, false, false)));
    }

    [Test]
    public void Leaving_Overview_clears_its_condensed_band()
    {
        var state = Store();
        state.Stage.OverviewCondensed = true;

        state.Navigation.GoCommand.Execute("berths");

        Assert.That(state.Stage.OverviewCondensed, Is.False);
    }

    [Test]
    public void The_layer_switcher_sets_the_layer_as_a_structure_change()
    {
        var state = Store();
        var raised = 0;
        state.StructureChanged += (_, _) => raised++;

        state.Stage.SetLayerCommand.Execute("yard");

        Assert.That(state.Stage.Layer, Is.EqualTo("yard"));
        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public void Hover_links_a_vessel_or_a_berth_and_only_its_own_leave_ends_it()
    {
        var state = Store();
        var hovers = 0;
        var structures = 0;
        state.HoverChanged += (_, _) => hovers++;
        state.StructureChanged += (_, _) => structures++;

        state.Stage.HoverCommand.Execute("nordic");
        Assert.That((state.Stage.HoveredVessel, state.Stage.HoveredBerth), Is.EqualTo(("nordic", (int?)null)));

        state.Stage.HoverCommand.Execute("07");
        Assert.That((state.Stage.HoveredVessel, state.Stage.HoveredBerth), Is.EqualTo(((string?)null, (int?)6)));

        // The previous row's leave arrives after the next row's enter: it must not clear the new hover
        state.Stage.HoverCommand.Execute("-nordic");
        Assert.That(state.Stage.HoveredBerth, Is.EqualTo(6));

        state.Stage.HoverCommand.Execute("-07");
        Assert.That((state.Stage.HoveredVessel, state.Stage.HoveredBerth), Is.EqualTo(((string?)null, (int?)null)));

        Assert.That(hovers, Is.GreaterThan(0));
        Assert.That(structures, Is.Zero, "a hover repaints, it never rebuilds");
    }

    [Test]
    public void A_hull_pick_means_what_the_section_means()
    {
        var state = Store();

        state.PickHullVesselCommand.Execute("baltic");
        Assert.That(state.Stage.Selection, Is.EqualTo("baltic"), "Overview selects on the harbour");
        state.PickHullVesselCommand.Execute("baltic");
        Assert.That(state.Stage.Selection, Is.Null, "a second pick clears it");

        state.Navigation.GoCommand.Execute("berths");
        state.SelectedBay = 2;
        state.PickHullVesselCommand.Execute("kaida");
        Assert.That((state.SelectedVesselId, state.SelectedBay), Is.EqualTo(("kaida", (int?)null)), "Berths works the vessel");

        state.Navigation.GoCommand.Execute("security");
        state.PickHullVesselCommand.Execute("aurora");
        Assert.That((state.Navigation.Section, state.SelectedVesselId), Is.EqualTo(("berths", "aurora")), "Security opens her on Berths");
    }

    // ── Scanner ─────────────────────────────────────────────────────────────

    [Test]
    public void The_scanner_starts_shut_on_the_first_container_at_the_iso_view()
    {
        var state = Store();

        Assert.Multiple(() =>
        {
            Assert.That(state.Scanner.Open, Is.False);
            Assert.That(state.Scanner.Xray, Is.False);
            Assert.That(state.Scanner.Filter, Is.EqualTo("All"));
            Assert.That(state.Scanner.SelectedContainer.Id, Is.EqualTo("AVRU4821934"));
            Assert.That((state.Scanner.RotationX, state.Scanner.RotationY, state.Scanner.Zoom), Is.EqualTo((-18d, -32d, 1d)));
        });
    }

    [Test]
    public void The_filter_and_the_container_are_structure_changes()
    {
        var state = Store();
        var raised = 0;
        state.StructureChanged += (_, _) => raised++;

        state.Scanner.Filter = "Inspection";
        state.Scanner.SelectedContainerId = PortData.Containers[1].Id;
        state.Scanner.Xray = true;

        Assert.That(raised, Is.EqualTo(3));
        Assert.That(state.Scanner.SelectedContainer, Is.SameAs(PortData.Containers[1]));
    }

    [Test]
    public void A_filter_that_hides_the_open_box_moves_the_detail_to_its_first()
    {
        var state = Store();
        var first = PortData.ContainersFor("Inspection").First();

        state.Scanner.FilterBy("Inspection");
        Assert.That(state.Scanner.SelectedContainerId, Is.EqualTo(first.Id));

        state.Scanner.SelectedContainerId = PortData.ContainersFor("Inspection").Last().Id;
        state.Scanner.FilterBy("All");
        Assert.That(state.Scanner.SelectedContainerId, Is.EqualTo(PortData.ContainersFor("Inspection").Last().Id), "a box still shown stays open");
    }

    // ── Yard ────────────────────────────────────────────────────────────────

    [Test]
    public void The_yard_works_a_stack_every_few_seconds_and_says_so()
    {
        var clock = new ManualClock();
        var state = new PortState(clock);
        var moves = state.Yard.Moves;
        var raised = 0;
        state.YardChanged += (_, _) => raised++;

        clock.Advance(TimeSpan.FromSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.That(state.Yard.Moves, Is.EqualTo(moves + 1), "the first beat works a stack, the next waits");

        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.That(state.Yard.Moves, Is.EqualTo(moves + 2));
        Assert.That(raised, Is.EqualTo(2));
        Assert.That(state.Yard.Occupancy, Is.InRange(0, 100));
    }

    [Test]
    public void A_stack_pick_is_a_structure_change()
    {
        var state = Store();
        var raised = 0;
        state.StructureChanged += (_, _) => raised++;

        state.Yard.SelectedStack = "A-0-0";

        Assert.That(raised, Is.EqualTo(1));
    }

    // ── Crew chat ───────────────────────────────────────────────────────────

    [Test]
    public void A_sent_message_joins_the_thread_with_the_time_and_clears_the_draft()
    {
        var clock = new ManualClock();
        var state = new PortState(clock);
        var vessel = PortData.RiverFleet[0];
        var seeded = state.Chat.ThreadFor(vessel.Id).Count;
        state.Chat.Draft = "Hold at the lock";
        clock.Advance(TimeSpan.FromMinutes(3));

        state.Chat.Send(vessel.Id, "  Hold at the lock ");

        var thread = state.Chat.ThreadFor(vessel.Id);
        Assert.Multiple(() =>
        {
            Assert.That(thread, Has.Count.EqualTo(seeded + 1));
            Assert.That(thread[^1].Who, Is.EqualTo("dispatch"));
            Assert.That(thread[^1].Text, Is.EqualTo("Hold at the lock"));
            Assert.That(ShiftClock.Format(thread[^1].Hour), Is.EqualTo("21:01"));
            Assert.That(state.Chat.Draft, Is.Empty);
            Assert.That(state.Chat.ThreadFor(PortData.RiverFleet[1].Id), Has.Count.EqualTo(PortData.RiverFleet[1].Thread.Count));
        });
    }

    [Test]
    public void A_blank_message_is_not_sent()
    {
        var state = Store();
        var vessel = PortData.RiverFleet[0];
        var seeded = state.Chat.ThreadFor(vessel.Id).Count;
        state.Chat.Draft = "   ";

        state.Chat.Send(vessel.Id, "   ");

        Assert.That(state.Chat.ThreadFor(vessel.Id), Has.Count.EqualTo(seeded));
        Assert.That(state.Chat.Draft, Is.EqualTo("   "));
    }
}
