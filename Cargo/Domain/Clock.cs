namespace Cargo.Domain;

/// <summary>
/// The store's clock: the time now, and a beat once a second. The app runs <see cref="DispatcherClock"/>, so every beat
/// lands on the UI thread; Cargo.Tests drives a manual one, so <see cref="PortState"/> runs without the Uno runtime.
/// </summary>
public interface IClock
{
    /// <summary>The wall-clock time now.</summary>
    DateTimeOffset Now { get; }

    /// <summary>Raised once a second.</summary>
    event EventHandler? Tick;
}

/// <summary>The app's clock: a one-second <see cref="DispatcherTimer"/>, so it is created on the UI thread (App does).</summary>
public sealed class DispatcherClock : IClock
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public DispatcherClock()
    {
        _timer.Tick += (_, _) => Tick?.Invoke(this, EventArgs.Empty);
        _timer.Start();
    }

    public DateTimeOffset Now => DateTimeOffset.Now;

    public event EventHandler? Tick;
}
