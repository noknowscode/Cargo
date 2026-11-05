namespace Cargo.Domain;

/// <summary>
/// Deterministic pseudo-noise, the same generator the design uses, so the data's made-up detail (bay stows, yard
/// stacks, container numbers) lands where the design's does, every run.
/// </summary>
public static class Noise
{
    /// <summary>A value in [0, 1) for <paramref name="i"/>, the same for the same i.</summary>
    public static double H(int i) => ((i * 7919 + 13) % 97) / 97d;
}
