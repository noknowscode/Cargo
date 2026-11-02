using Cargo.Domain;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>The yard map's keyboard path: the arrow keys step through a block's slots and stop at its edge.</summary>
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
}
