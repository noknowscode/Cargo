namespace Liveline.Models;

public enum MomentumDirection
{
    Off,
    Up,
    Down,
    Flat
}

public static class MomentumHelper
{
    /// <summary>
    /// Resolves the <c>Momentum</c> property value into a concrete direction.
    /// <c>false</c> → <see cref="MomentumDirection.Off"/>;
    /// <c>"up"</c>/<c>"down"</c>/<c>"flat"</c> → forced direction;
    /// otherwise auto-detect from delta.
    /// </summary>
    public static MomentumDirection Resolve(object? value, double currentValue, double previousValue)
    {
        if (value is false)
            return MomentumDirection.Off;

        if (value is string s)
        {
            return s.ToLowerInvariant() switch
            {
                "up" => MomentumDirection.Up,
                "down" => MomentumDirection.Down,
                "flat" => MomentumDirection.Flat,
                _ => MomentumDirection.Off
            };
        }

        if (value is null)
            return MomentumDirection.Off;

        double delta = currentValue - previousValue;
        double threshold = Math.Max(Math.Abs(currentValue) * 0.001, 0.001);

        if (delta > threshold) return MomentumDirection.Up;
        if (delta < -threshold) return MomentumDirection.Down;
        return MomentumDirection.Flat;
    }
}
