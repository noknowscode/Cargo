namespace Cargo.Domain;

/// <summary>
/// The yard's stacks and the yard map's keyboard path: stacks are keyed "block-row-column" (A-0-0 is block A's top-left
/// slot), and the arrow keys step to the neighbouring slot in the same block. Kept apart from the map so the store works
/// the stacks without it and the unit tests can check it.
/// </summary>
public static class YardGrid
{
    /// <summary>
    /// Every stack slot, block by block: its key ("A-0-0"), block, label ("A-01-01"), the tiers it starts with and its
    /// place on the yard map. The store works these stacks whether or not the map is on screen.
    /// </summary>
    public static IReadOnlyList<(string Key, YardZoneDef Zone, string Slot, int Base, double X, double Y)> Slots { get; } =
        BuildSlots().ToArray();

    private static IEnumerable<(string Key, YardZoneDef Zone, string Slot, int Base, double X, double Y)> BuildSlots()
    {
        for (var z = 0; z < PortData.YardZones.Count; z++)
        {
            var zone = PortData.YardZones[z];
            var columns = (int)Math.Floor((zone.W - 28) / 32);
            var rows = (int)Math.Floor((zone.H - 60) / 24);

            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < columns; c++)
                {
                    var seed = z * 131 + r * 17 + c * 3;

                    // The bonded and inspection blocks are deliberately sparse
                    var height = z == 5
                        ? Noise.H(seed) > 0.6 ? (int)(Noise.H(seed + 1) * 3) + 1 : 0
                        : z == 4
                            ? Noise.H(seed) > 0.5 ? 1 : 0
                            : (int)(Noise.H(seed) * 6);

                    yield return (
                        $"{zone.Id}-{r}-{c}",
                        zone,
                        $"{zone.Id}-{r + 1:D2}-{c + 1:D2}",
                        height,
                        zone.X + 14 + c * 32,
                        zone.Y + 50 + r * 24);
                }
            }
        }
    }

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
