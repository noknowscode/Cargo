namespace Cargo.Domain;

/// <summary>
/// The store's clock: the time now, and a beat once a second. The app runs <c>DispatcherClock</c> (Presentation), so every beat
/// lands on the UI thread; Cargo.Tests drives a manual one, so <see cref="PortState"/> runs without the Uno runtime.
/// </summary>
public interface IClock
{
    /// <summary>The wall-clock time now.</summary>
    DateTimeOffset Now { get; }

    /// <summary>Raised once a second.</summary>
    event EventHandler? Tick;
}
