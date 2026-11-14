using Cargo.Domain;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>The yard's stacks, the store working them, and the yard map's keyboard path (the arrows stop at a block's edge).</summary>
public class YardGridTests
{
    // Two blocks: A is 2 rows by 3 columns, B a single slot
    private static readonly string[] Keys = ["A-0-0", "A-0-1", "A-0-2", "A-1-0", "A-1-1", "A-1-2", "B-0-0"];

    [Test]
    public void With_nothing_selected_the_first_arrow_selects_the_first_slot() =>
        Assert.That(YardGrid.Step(Keys, null, 0, 1), Is.EqualTo("A-0-0"));

    [Test]
    public void An_unknown_key_starts_over_at_the_first_slot() =>
        Assert.That(YardGrid.Step(Keys, "Z-9-9", 1, 0), Is.EqualTo("A-0-0"));

    [TestCase("A-0-0", 0, 1, "A-0-1")]
    [TestCase("A-0-1", 1, 0, "A-1-1")]
    [TestCase("A-1-1", -1, 0, "A-0-1")]
    [TestCase("A-1-2", 0, -1, "A-1-1")]
    public void An_arrow_steps_to_the_neighbouring_slot(string from, int rows, int columns, string to) =>
        Assert.That(YardGrid.Step(Keys, from, rows, columns), Is.EqualTo(to));

    [TestCase("A-0-2", 0, 1, TestName = "Right at the block's right edge stays put")]
    [TestCase("A-1-0", 1, 0, TestName = "Down at the block's bottom edge stays put")]
    [TestCase("B-0-0", 0, -1, TestName = "A single-slot block keeps its slot")]
    public void An_arrow_at_the_edge_keeps_the_slot(string from, int rows, int columns) =>
        Assert.That(YardGrid.Step(Keys, from, rows, columns), Is.EqualTo(from));

    [Test]
    public void The_real_yard_has_a_first_slot_in_block_A() =>
        Assert.That(YardGrid.Step(PortData.YardZones.Select(z => $"{z.Id}-0-0").ToList(), null, 1, 0), Is.EqualTo("A-0-0"));

    [Test]
    public void The_yard_has_a_slot_per_stack_with_unique_keys_the_arrows_can_reach()
    {
        var keys = YardGrid.Slots.Select(s => s.Key).ToList();

        Assert.That(keys, Is.Unique);
        Assert.That(YardGrid.Slots, Has.All.Matches<(string Key, YardZoneDef Zone, string Slot, int Base, double X, double Y)>(
            s => s.Base is >= 0 and <= 5 && s.Key.StartsWith(s.Zone.Id + "-")));
        Assert.That(YardGrid.Step(keys, null, 0, 0), Is.EqualTo(keys[0]));
    }

    [Test]
    public void The_store_works_the_yard_with_no_map_on_screen()
    {
        var clock = new ManualClock();
        var state = new PortState(clock);
        var moves = state.Yard.Moves;
        var changes = 0;
        state.YardChanged += (_, _) => changes++;

        clock.Advance(TimeSpan.FromSeconds(1)); // the first beat works a stack
        clock.Advance(TimeSpan.FromSeconds(1)); // then not again for 3.5 s
        clock.Advance(TimeSpan.FromSeconds(3));

        Assert.That(changes, Is.EqualTo(2));
        Assert.That(state.Yard.Moves, Is.EqualTo(moves + 2));
        var worked = YardGrid.Slots.Where(s => state.Yard.RecentlyMoved(s.Key)).ToList();
        Assert.That(worked, Has.Count.InRange(1, 2));
        Assert.That(worked, Has.All.Matches<(string Key, YardZoneDef Zone, string Slot, int Base, double X, double Y)>(
            s => Math.Abs(state.Yard.Height(s.Key, s.Base) - s.Base) <= 2));

        var tiers = YardGrid.Slots.Sum(s => state.Yard.Height(s.Key, s.Base));
        Assert.That(state.Yard.Occupancy, Is.EqualTo((int)Math.Round(tiers * 100d / (YardGrid.Slots.Count * 5))));
    }
}
