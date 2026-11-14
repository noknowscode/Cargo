using Cargo.Domain;

namespace Cargo.Tests;

/// <summary>A clock the test moves: it starts at a fixed instant and beats only when told to.</summary>
public sealed class ManualClock : IClock
{
    public DateTimeOffset Now { get; private set; } = new(2026, 9, 2, 20, 58, 0, TimeSpan.Zero);

    public event EventHandler? Tick;

    /// <summary>Moves the time on and beats once, as the app's clock does every second.</summary>
    public void Advance(TimeSpan by)
    {
        Now += by;
        Tick?.Invoke(this, EventArgs.Empty);
    }
}
