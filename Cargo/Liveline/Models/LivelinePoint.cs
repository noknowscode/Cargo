namespace Liveline.Models;

/// <summary>A value at a time.</summary>
/// <remarks>
/// Settable rather than the positional record's init-only properties: the WinAppSDK XAML compiler writes type
/// metadata, setters included, for the item type of <c>Data</c> and <c>Forecast</c> (<c>IList&lt;LivelinePoint&gt;</c>),
/// and an init-only setter fails there (CS8852). Nothing in Liveline writes a point after constructing it.
/// </remarks>
public record LivelinePoint(DateTimeOffset Time, double Value)
{
    public DateTimeOffset Time { get; set; } = Time;
    public double Value { get; set; } = Value;
}
