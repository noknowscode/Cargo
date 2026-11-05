using static Cargo.Domain.DebugHooks;

namespace Cargo.Domain;

/// <summary>
/// The yard at work: every few seconds of the store's clock a stack gains or loses a tier, so the yard's figures are
/// live wherever they show, whether or not the map is on screen; and the stack picked on the map.
/// </summary>
public sealed partial class YardState : ObservableObject
{
    /// <summary>How often the yard works a stack.</summary>
    public static readonly TimeSpan MoveEvery = TimeSpan.FromMilliseconds(3500);

    private readonly IClock _clock;
    private readonly PortSignals _signals;
    private readonly Random _random;
    private readonly Dictionary<string, int> _delta = new();
    private readonly Dictionary<string, DateTimeOffset> _recent = new();
    private DateTimeOffset _lastMove;

    /// <summary>The yard on the store's clock; <paramref name="random"/> picks the stacks (the tests seed one).</summary>
    public YardState(IClock clock, PortSignals signals, Random? random = null)
    {
        _clock = clock;
        _signals = signals;
        _random = random ?? Random.Shared;
        Occupancy = Measure();
    }

    [ObservableProperty]
    public partial string? SelectedStack { get; set; } = Hook("CARGO_STACK");

    partial void OnSelectedStackChanged(string? value) => _signals.Structure();

    /// <summary>Container moves in the yard today.</summary>
    public int Moves { get; private set; } = 1284;

    /// <summary>How full the yard is: the tiers stacked, as a share of five on every slot.</summary>
    public int Occupancy { get; private set; }

    /// <summary>A slot's height now: its starting tiers plus the yard's moves, 0 to 5.</summary>
    public int Height(string key, int baseHeight) =>
        Math.Clamp(baseHeight + (_delta.TryGetValue(key, out var d) ? d : 0), 0, 5);

    /// <summary>The yard worked this slot in the last minute.</summary>
    public bool RecentlyMoved(string key) =>
        _recent.TryGetValue(key, out var at) && (_clock.Now - at).TotalSeconds < 60;

    private int Measure() =>
        (int)Math.Round(YardGrid.Slots.Sum(s => Height(s.Key, s.Base)) * 100d / (YardGrid.Slots.Count * 5));

    /// <summary>On each beat of the clock: works one stack when <see cref="MoveEvery"/> has passed since the last.</summary>
    public void Work()
    {
        if (_clock.Now - _lastMove < MoveEvery)
        {
            return;
        }

        _lastMove = _clock.Now;
        var pick = YardGrid.Slots[_random.Next(YardGrid.Slots.Count)];
        var current = Height(pick.Key, pick.Base);
        var delta = current <= 0 ? 1 : current >= 5 ? -1 : _random.Next(2) == 0 ? -1 : 1;
        _delta[pick.Key] = (_delta.TryGetValue(pick.Key, out var d) ? d : 0) + delta;
        _recent[pick.Key] = _clock.Now;
        Moves++;
        Occupancy = Measure();
        _signals.Yard();
    }
}
