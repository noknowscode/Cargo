using Cargo.Controls.Harbour;
using Cargo.Domain;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>The container explorer's filters, the vessel profile's bay views and the harbour's geometry helpers.</summary>
public class CargoDataTests
{
    [TestCase("All")]
    [TestCase("Nope")]
    public void All_or_an_unknown_filter_shows_every_container(string filter) =>
        Assert.That(PortData.ContainersFor(filter), Is.EqualTo(PortData.Containers));

    [Test]
    public void Each_filter_shows_only_containers_at_its_stages()
    {
        foreach (var (filter, stages) in PortData.FilterStages)
        {
            var shown = PortData.ContainersFor(filter).ToList();
            Assert.That(shown, Is.Not.Empty, filter);
            Assert.That(shown, Has.All.Matches<ContainerDef>(c => stages.Contains(c.Stage)), filter);
            Assert.That(shown.Count, Is.EqualTo(PortData.Containers.Count(c => stages.Contains(c.Stage))), filter);
        }
    }

    [Test]
    public void Every_chip_is_All_or_a_known_filter_and_together_they_cover_every_container()
    {
        Assert.That(PortData.ContainerFilters.Where(f => f != "All"), Is.EquivalentTo(PortData.FilterStages.Keys));

        var covered = PortData.FilterStages.Keys.SelectMany(PortData.ContainersFor).Distinct();
        Assert.That(covered, Is.EquivalentTo(PortData.Containers));
    }

    [Test]
    public void A_bay_view_accounts_for_every_slot()
    {
        foreach (var vessel in PortData.Vessels)
        {
            for (var bay = 0; bay < vessel.Bays; bay++)
            {
                var view = BayView.For(vessel, bay);
                var label = $"{vessel.Name} bay {bay}";

                Assert.That(view.Cells, Has.Count.EqualTo(BayView.Capacity), label);
                Assert.That(view.Unloaded + view.Remaining, Is.EqualTo(BayView.Capacity), label);
                Assert.That(view.Cells.Count(c => c.Ashore), Is.EqualTo(view.Unloaded), label);
                Assert.That(view.Composition.Sum(c => c.Count), Is.EqualTo(view.Remaining), label);
                Assert.That(view.Status, Is.EqualTo(view.Remaining > 0 ? "Aboard" : "Ashore"), label);
            }
        }
    }

    [Test]
    public void A_fully_discharged_vessel_has_every_bay_ashore()
    {
        var done = PortData.Vessels.First(v => v.UnloadPercent == 100);

        for (var bay = 0; bay < done.Bays; bay++)
        {
            Assert.That(BayView.For(done, bay).Remaining, Is.Zero, $"bay {bay}");
        }
    }

    [Test]
    public void A_bay_view_is_the_same_every_time() =>
        Assert.That(BayView.Summary(PortData.Vessel("aurora"), 3), Is.EqualTo(BayView.Summary(PortData.Vessel("aurora"), 3)));

    [Test]
    public void A_rectangle_is_counter_clockwise_with_its_full_area()
    {
        var rect = Poly.Rect(10, 20, 4, 3, Math.PI / 6);

        Assert.That(Poly.Area(rect), Is.EqualTo(12).Within(1e-9));
    }

    [Test]
    public void The_convex_hull_drops_interior_points()
    {
        var points = new[] { new P2(0, 0), new P2(4, 0), new P2(4, 4), new P2(0, 4), new P2(2, 2), new P2(1, 3) };

        var hull = Poly.ConvexHull(points);

        Assert.That(hull, Is.EquivalentTo(new[] { new P2(0, 0), new P2(4, 0), new P2(4, 4), new P2(0, 4) }));
        Assert.That(Poly.Area(hull), Is.EqualTo(16).Within(1e-9), "counter-clockwise");
    }

    [Test]
    public void Clipping_at_the_quay_line_keeps_one_side()
    {
        var square = Poly.Rect(0, 0, 4, 4);

        Assert.That(Poly.Area(Poly.ClipToQuayLine(square, keepLand: true)), Is.EqualTo(8).Within(1e-9));
        Assert.That(Poly.Area(Poly.ClipToQuayLine(square, keepLand: false)), Is.EqualTo(8).Within(1e-9));
        Assert.That(Poly.ClipToQuayLine(square, keepLand: true), Has.All.Matches<P2>(p => p.Y <= 0));
    }

    [Test]
    public void Point_in_polygon()
    {
        var triangle = new (float X, float Y)[] { (0, 0), (10, 0), (0, 10) };

        Assert.That(Poly.Contains(triangle, 2, 2), Is.True);
        Assert.That(Poly.Contains(triangle, 8, 8), Is.False);
    }

    [Test]
    public void The_seeded_random_repeats_and_stays_in_range()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);

        for (var i = 0; i < 1000; i++)
        {
            var x = a.Next();
            Assert.That(x, Is.EqualTo(b.Next()));
            Assert.That(x, Is.InRange(0d, 1d).And.LessThan(1d));
        }
    }

    [Test]
    public void The_profile_opens_on_the_boundary_bay_until_one_is_picked()
    {
        var aurora = PortData.Vessel("aurora"); // 14 bays, 62 % discharged

        Assert.That(BayView.Boundary(aurora), Is.EqualTo(8));
        Assert.That(BayView.Open(aurora, null), Is.EqualTo(8));
        Assert.That(BayView.Open(aurora, 3), Is.EqualTo(3));
        Assert.That(BayView.Open(aurora, 14), Is.EqualTo(8), "a bay she does not have (picked on a longer ship)");
    }

    [Test]
    public void The_arrow_keys_step_through_the_bays_and_stop_at_the_ends()
    {
        var aurora = PortData.Vessel("aurora");

        Assert.That(BayView.Step(aurora, null, 1), Is.EqualTo(9), "from the boundary bay");
        Assert.That(BayView.Step(aurora, 3, -1), Is.EqualTo(2));
        Assert.That(BayView.Step(aurora, 0, -1), Is.Zero);
        Assert.That(BayView.Step(aurora, 13, 1), Is.EqualTo(13));
    }

    [Test]
    public void Every_IMO_number_is_invented_and_carries_a_valid_check_digit()
    {
        foreach (var vessel in PortData.Vessels)
        {
            var digits = vessel.Imo.Select(c => c - '0').ToArray();
            var check = digits.Take(6).Select((d, i) => d * (7 - i)).Sum() % 10;

            Assert.That(digits, Has.Length.EqualTo(7), vessel.Name);
            Assert.That(digits[6], Is.EqualTo(check), $"{vessel.Name}: IMO {vessel.Imo} check digit");
            Assert.That(vessel.Imo, Does.StartWith("1"), $"{vessel.Name}: ships' own numbers start at 5 million");
        }
    }
}
