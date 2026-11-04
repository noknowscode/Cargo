using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>Cargo: the masthead, the container explorer (filters, cards, journey), the yard's stack and the scanner.</summary>
public class CargoViewModelTests
{
    [Test]
    public void The_masthead_reads_the_data_and_the_yard()
    {
        var desk = new Desk();

        Assert.That((desk.Cargo.OnSite, desk.Cargo.Holds, desk.Cargo.HoldsLabel), Is.EqualTo(("4,812", "1", "hold")));
        Assert.That(desk.Cargo.Occupancy, Is.EqualTo($"{desk.State.Yard.Occupancy}%"));
        Assert.That(desk.Cargo.Moves, Is.EqualTo(desk.State.Yard.Moves.ToString("N0")));
    }

    [Test]
    public void A_filter_lights_its_chip_and_keeps_its_boxes()
    {
        var state = new PortState(new ManualClock());
        var explorer = new ContainersViewModel(state);
        Assert.That(explorer.Filters.Single(f => f.IsCurrent).Label, Is.EqualTo("All"));
        Assert.That(explorer.Cards, Has.Count.EqualTo(PortData.Containers.Count));

        explorer.FilterCommand.Execute("Inspection");
        explorer.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(explorer.Filters.Single(f => f.IsCurrent).Label, Is.EqualTo("Inspection"));
            Assert.That(explorer.Filters.Single(f => f.IsCurrent).AccessibleName, Is.EqualTo("Inspection, filter on"));
            Assert.That(explorer.Cards.Select(c => c.Id), Is.EqualTo(PortData.ContainersFor("Inspection").Select(c => c.Id)));
            Assert.That(explorer.IsEmpty, Is.False);
        });
    }

    [Test]
    public void A_card_says_whether_its_box_needs_attention()
    {
        var explorer = new ContainersViewModel(new PortState(new ManualClock()));

        var held = explorer.Cards.Single(c => c.Id == "MRVU9182041");
        var ready = explorer.Cards.Single(c => c.Id == "AVRU4821934");
        Assert.That((held.Warn, ready.Warn), Is.EqualTo((true, false)));
        Assert.That(ready.IsSelected, Is.True);
        Assert.That(ready.AccessibleName, Is.EqualTo("AVRU 482193 4, Westhaven → Rotterdam, Ready for loading. Opens the scanner"));
    }

    [Test]
    public void Inspecting_a_box_opens_the_scanner_at_the_iso_view()
    {
        var state = new PortState(new ManualClock());
        var explorer = new ContainersViewModel(state);
        state.Scanner.Xray = true;
        state.Scanner.Turn(0, -90);

        explorer.InspectCommand.Execute("MRVU9182041");

        Assert.Multiple(() =>
        {
            Assert.That(state.Scanner.Open, Is.True);
            Assert.That(state.Scanner.Xray, Is.False);
            Assert.That(state.Scanner.SelectedContainerId, Is.EqualTo("MRVU9182041"));
            Assert.That((state.Scanner.RotationX, state.Scanner.RotationY), Is.EqualTo((ScannerState.IsoX, ScannerState.IsoY)));
        });
    }

    [Test]
    public void The_journey_marks_the_steps_behind_at_and_ahead_of_the_box()
    {
        var state = new PortState(new ManualClock());
        var explorer = new ContainersViewModel(state);

        // AVRU 482193 4 is in the yard (stage 2)
        Assert.That(explorer.Journey.Select(s => s.State), Is.EqualTo(new[]
        {
            StepState.Done, StepState.Done, StepState.Current, StepState.Ahead, StepState.Ahead
        }));
        Assert.That(explorer.Journey.Select(s => s.IsLast), Is.EqualTo(new[] { false, false, false, false, true }));

        state.Scanner.SelectedContainerId = "MRVU9182041";
        explorer.Refresh();
        Assert.That(explorer.Journey[3].State, Is.EqualTo(StepState.Warned), "the held box stands warned at Inspection");
        Assert.That(explorer.SelectedIso, Is.Not.Empty);
        Assert.That(explorer.Facts.Select(f => f.Key), Does.Contain("Customs").And.Contain("Seal"));
    }

    [Test]
    public void The_yard_shows_a_picked_stack_tier_by_tier_until_it_closes()
    {
        var state = new PortState(new ManualClock());
        var yard = new YardViewModel(state);
        Assert.That(yard.HasStack, Is.False);
        Assert.That(yard.Legend.Single(l => l.Label == "Available slot").Strength, Is.EqualTo(0.12));

        var slot = YardGrid.Slots.First(s => s.Base > 0);
        state.Yard.SelectedStack = slot.Key;
        yard.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(yard.HasStack, Is.True);
            Assert.That(yard.Tiers, Has.Count.EqualTo(state.Yard.Height(slot.Key, slot.Base)));
            Assert.That(yard.StackBoxes, Has.Count.EqualTo(yard.Tiers.Count));
            Assert.That(yard.SlotSubtitle, Does.EndWith($"{yard.Tiers.Count} of 5 tiers"));
            Assert.That(yard.OccupancyLine, Is.EqualTo($"{state.Yard.Occupancy}% occupied"));
        });

        yard.CloseStackCommand.Execute(null);
        yard.Refresh();
        Assert.That(yard.HasStack, Is.False);
    }

    [Test]
    public void The_scanner_does_nothing_while_shut()
    {
        var state = new PortState(new ManualClock());
        var scanner = new ScannerViewModel(state);

        Assert.That(scanner.ContainerId, Is.Empty);
        Assert.That(scanner.Views, Is.Empty);
    }

    [Test]
    public void The_scanner_reads_the_verdict_the_manifest_and_the_density()
    {
        var state = new PortState(new ManualClock());
        var scanner = new ScannerViewModel(state);

        state.Scanner.Inspect("MRVU9182041");

        Assert.Multiple(() =>
        {
            Assert.That(scanner.ContainerId, Is.EqualTo("MRVU 918204 1"));
            Assert.That(scanner.Verdict, Is.EqualTo("Anomaly detected"));
            Assert.That(scanner.ScanAction, Is.EqualTo("Open inspection →"));
            Assert.That(scanner.VerdictTone, Is.EqualTo(PortData.Containers.Single(c => c.Id == "MRVU9182041").SecurityToken));
            Assert.That(scanner.Manifest.Any(m => m.Flagged), Is.True);
            Assert.That(scanner.ManifestHeading, Is.EqualTo($"MANIFEST · {scanner.Manifest.Count} ITEMS"));
            Assert.That(scanner.DensitySlices.Sum(s => s.Weight), Is.InRange(98, 102), "rounded percentages");
            Assert.That(scanner.Views.Single(v => v.IsCurrent).Label, Is.EqualTo("Iso"));
            Assert.That(scanner.IsXray, Is.False);
        });
    }

    [Test]
    public void The_presets_x_ray_and_zoom_turn_the_box()
    {
        var state = new PortState(new ManualClock());
        var scanner = new ScannerViewModel(state);
        state.Scanner.Inspect("AVRU4821934");

        scanner.ShowViewCommand.Execute("Front");
        Assert.That((state.Scanner.RotationX, state.Scanner.RotationY), Is.EqualTo((0d, 0d)));
        Assert.That(scanner.Views.Single(v => v.IsCurrent).AccessibleName, Is.EqualTo("Front view, current"));

        scanner.ToggleXrayCommand.Execute(null);
        Assert.That((scanner.IsXray, scanner.XrayName, scanner.ScanMode), Is.EqualTo((true, "X-ray, on", "X-RAY · 160 kV · SCANNING")));

        for (var i = 0; i < 10; i++)
        {
            scanner.ZoomInCommand.Execute(null);
        }

        Assert.That(state.Scanner.Zoom, Is.EqualTo(ScannerState.MaxZoom));

        scanner.ResetViewCommand.Execute(null);
        Assert.That((state.Scanner.Zoom, state.Scanner.RotationX, state.Scanner.RotationY), Is.EqualTo((1d, ScannerState.IsoX, ScannerState.IsoY)));

        for (var i = 0; i < 10; i++)
        {
            scanner.ZoomOutCommand.Execute(null);
        }

        Assert.That(state.Scanner.Zoom, Is.EqualTo(ScannerState.MinZoom).Within(1e-9));
    }

    [Test]
    public void Open_inspection_shuts_the_scanner_on_Securitys_inspection_tab()
    {
        var state = new PortState(new ManualClock());
        var scanner = new ScannerViewModel(state);
        state.Scanner.Inspect("MRVU9182041");

        scanner.OpenInspectionCommand.Execute(null);

        Assert.That(state.Scanner.Open, Is.False);
        Assert.That((state.Navigation.Section, state.Navigation.SecurityTab), Is.EqualTo(("security", "inspection")));
    }
}
