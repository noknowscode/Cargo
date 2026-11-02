namespace Cargo.Domain;

/// <summary>
/// The yard map's keyboard path: stacks are keyed "block-row-column" (A-0-0 is block A's top-left slot), and the arrow
/// keys step to the neighbouring slot in the same block. Kept apart from the map so Cargo.Tests can check it.
/// </summary>
public static class YardGrid
{
    /// <summary>
    /// The slot one row (<paramref name="rows"/>) and/or column (<paramref name="columns"/>) away from
    /// <paramref name="from"/> in its block, or <paramref name="from"/> itself at the block's edge. With nothing
    /// selected (or an unknown key) the first slot is the start.
    /// </summary>
    public static string? Step(IReadOnlyList<string> keys, string? from, int rows, int columns)
    {
        if (keys.Count == 0)
        {
            return from;
        }

        if (from is null || !keys.Contains(from) || Parse(from) is not var (block, row, column))
        {
            return keys[0];
        }

        var next = $"{block}-{row + rows}-{column + columns}";
        return keys.Contains(next) ? next : from;
    }

    private static (string Block, int Row, int Column)? Parse(string key) =>
        key.Split('-') is [var block, var row, var column] && int.TryParse(row, out var r) && int.TryParse(column, out var c)
            ? (block, r, c)
            : null;
}
