namespace Cargo.Presentation;

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
