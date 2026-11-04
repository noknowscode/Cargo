namespace Cargo.Presentation;

/// <summary>
/// The needs-you bar: the one decision waiting on the dispatcher, made in place.
/// needs → busy (the confirm is in flight, ~600 ms) → done (with Undo) → needs again on Undo.
/// With nothing pending it reads "Nothing needs you" in the done style; it is never hidden,
/// so the masthead does not jump. It exposes states, not brushes: <see cref="NeedsYouBar"/> maps
/// them to the theme's brushes, so Cargo.Tests drives the state machine without a UI.
/// </summary>
public sealed partial class NeedsYouViewModel : ObservableObject
{
    /// <summary>How long a confirm is in flight (the berth office acknowledging it), shown as "Confirming".</summary>
    public static readonly TimeSpan ConfirmLatency = TimeSpan.FromMilliseconds(600);

    private readonly PortState _state;
    private readonly TimeSpan _latency;
    private string? _confirmed;
    private string? _confirmedAt;
    private string? _error;

    public NeedsYouViewModel(PortState state)
        : this(state, ConfirmLatency)
    {
    }

    /// <summary>The bar with a given confirm latency (the tests use a short one).</summary>
    public NeedsYouViewModel(PortState state, TimeSpan confirmLatency)
    {
        _state = state;
        _latency = confirmLatency;
        Refresh();
    }

    [ObservableProperty] public partial string Title { get; set; } = string.Empty;
    [ObservableProperty] public partial string Meta { get; set; } = string.Empty;
    [ObservableProperty] public partial string ConfirmLabel { get; set; } = string.Empty;

    /// <summary>A berth waits on the dispatcher: the alert ground and square, and Confirm.</summary>
    [ObservableProperty] public partial bool IsPending { get; set; }

    /// <summary>Nothing waits (the opposite of <see cref="IsPending"/>): the surface ground and a check.</summary>
    [ObservableProperty] public partial bool IsDone { get; set; }

    /// <summary>The last confirm was refused: the meta line says why, in alert ink.</summary>
    [ObservableProperty] public partial bool IsRefused { get; set; }

    [ObservableProperty] public partial bool CanReview { get; set; }
    [ObservableProperty] public partial bool CanUndo { get; set; }

    /// <summary>A confirm is in flight.</summary>
    [ObservableProperty] public partial bool IsBusy { get; set; }

    /// <summary>Hovering the bar outlines its vessel, lights its tag and its row.</summary>
    public System.Windows.Input.ICommand HoverCommand => _state.Stage.HoverCommand;

    /// <summary>The vessel the bar is about, for hover linking; null when nothing is pending.</summary>
    public string? Subject => _confirmed ?? _state.PendingDecision;

    [RelayCommand]
    private async Task Confirm()
    {
        if (IsBusy || _state.PendingDecision is not { } vessel)
        {
            return;
        }

        // The berth the dispatcher was shown when they pressed it: if a drag moves her while the confirm is in
        // flight, the confirm is dropped rather than confirming a berth nobody looked at
        var berth = _state.BerthOf(vessel);
        IsBusy = true;
        _error = null;
        Refresh();

        await Task.Delay(_latency);

        IsBusy = false;
        if (_state.PendingDecision == vessel && _state.BerthOf(vessel) == berth)
        {
            _error = _state.Confirm(vessel);
            if (_error is null)
            {
                _confirmed = vessel;
                _confirmedAt = ShiftClock.Format(_state.NowHours);
            }
        }

        Refresh();
    }

    [RelayCommand]
    private void Review()
    {
        if (Subject is { } vessel)
        {
            _state.PickVessel(vessel);
        }
    }

    [RelayCommand]
    private void Undo()
    {
        if (_confirmed is { } vessel)
        {
            _confirmed = null;
            _state.UndoConfirm(vessel);
            Refresh();
        }
    }

    /// <summary>Called by the page while it is on screen, on structure changes and each tick.</summary>
    public void Refresh()
    {
        var pending = _state.PendingDecision;
        if (pending is null)
        {
            // A refusal belongs to the decision it answered; once that is settled it no longer applies
            _error = null;
        }

        if (_confirmed is { } confirmed)
        {
            var vessel = PortData.Vessel(confirmed);
            var berth = _state.BerthOf(confirmed);
            Title = $"Berth {berth} confirmed for {vessel.Name}";
            Meta = $"Confirmed {_confirmedAt} · arrives {ShiftClock.Display(vessel.Eta)}";
        }
        else if (pending is { } id)
        {
            var vessel = PortData.Vessel(id);
            var berth = PortData.Berth(_state.BerthOf(id)) ?? throw new InvalidOperationException($"{id} waits on no berth");
            Title = $"{vessel.Name} needs berth {berth.Number} confirmed";
            Meta = _error ?? $"Arrives {ShiftClock.Display(vessel.Eta)} · under-keel {BerthFacts.UnderKeel(vessel, berth):0.0} m · pilot aboard";
            ConfirmLabel = IsBusy ? "Confirming" : $"Confirm berth {berth.Number}";
        }
        else
        {
            Title = "Nothing needs you";
            Meta = "Every arrival has a confirmed berth";
        }

        IsPending = pending is not null;
        IsDone = pending is null;
        IsRefused = _error is not null;
        CanReview = Subject is not null;
        CanUndo = _confirmed is not null;
        OnPropertyChanged(nameof(Subject));
    }
}
