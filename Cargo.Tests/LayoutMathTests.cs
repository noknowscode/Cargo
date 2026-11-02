using Cargo.Controls;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>
/// The arithmetic behind the responsive panels: FlowPanel (figures, legends, chips and button rows that wrap) and
/// TileGridPanel (the card and panel grids that change their column count with the width).
/// </summary>
public class LayoutMathTests
{
    [Test]
    public void Flow_wraps_to_a_new_line_when_the_next_child_would_pass_the_edge()
    {
        var (slots, width, height) = LayoutMath.Flow([(100, 20), (100, 30), (100, 20)], 250, 10, 8);

        Assert.That(slots[0], Is.EqualTo((0d, 0d, 100d, 30d)), "a line's children take its tallest height");
        Assert.That(slots[1], Is.EqualTo((110d, 0d, 100d, 30d)));
        Assert.That(slots[2], Is.EqualTo((0d, 38d, 100d, 20d)), "the next line starts under the first plus the row gap");
        Assert.That(width, Is.EqualTo(210));
        Assert.That(height, Is.EqualTo(58));
    }

    [Test]
    public void Flow_keeps_one_line_when_the_width_is_unbounded()
    {
        var (slots, width, height) = LayoutMath.Flow([(100, 20), (100, 20), (100, 20)], double.PositiveInfinity, 10, 8);

        Assert.That(slots.Select(s => s.Y), Is.All.EqualTo(0));
        Assert.That(width, Is.EqualTo(320));
        Assert.That(height, Is.EqualTo(20));
    }

    [Test]
    public void Flow_gives_a_child_wider_than_the_line_a_line_of_its_own_clipped_to_it()
    {
        var (slots, width, _) = LayoutMath.Flow([(50, 10), (400, 10), (50, 10)], 200, 10, 0);

        Assert.That(slots[1], Is.EqualTo((0d, 10d, 200d, 10d)));
        Assert.That(slots[2].Y, Is.EqualTo(20));
        Assert.That(width, Is.EqualTo(200));
    }

    [Test]
    public void Flow_charges_no_gap_for_a_collapsed_child()
    {
        var (slots, width, _) = LayoutMath.Flow([(100, 20), (0, 0), (100, 20)], 400, 10, 8);

        Assert.That(slots[2].X, Is.EqualTo(110));
        Assert.That(width, Is.EqualTo(210));
    }

    [Test]
    public void Flow_of_nothing_is_empty()
    {
        var (slots, width, height) = LayoutMath.Flow([], 300, 10, 8);

        Assert.That(slots, Is.Empty);
        Assert.That(width, Is.Zero);
        Assert.That(height, Is.Zero);
    }

    [TestCase(1200, 260, 40, 4, TestName = "Waterways' row of four at 1280 (1200 of content)")]
    [TestCase(1440, 260, 40, 4, TestName = "Still four at the 1520 cap")]
    [TestCase(920, 400, 40, 2, TestName = "Two by two at 1000")]
    [TestCase(328, 280, 28, 1, TestName = "One column on a 360 phone")]
    [TestCase(296, 120, 16, 2, TestName = "The timeline's figures two by two on a phone")]
    [TestCase(0, 240, 14, 1, TestName = "Never fewer than one column")]
    public void Tile_columns_are_as_many_as_fit(double width, double minTile, double gap, int columns) =>
        Assert.That(LayoutMath.TileColumns(width, minTile, gap), Is.EqualTo(columns));
}
