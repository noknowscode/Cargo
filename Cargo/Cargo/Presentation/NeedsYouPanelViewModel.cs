using System.Collections.Specialized;

namespace Cargo.Presentation;

/// <summary>One row of the Needs-you queue: the decision and the action that resolves it.</summary>
/// <remarks>
/// Values come through the constructor, with no parameterless one: the WinAppSDK XAML compiler writes an activator
/// for any x:DataType with one.
/// </remarks>
public sealed class DecisionRow(Decision decision, ICommand act)
{
    public Decision Decision { get; } = decision;

    /// <summary>Resolves <see cref="Decision"/>, which is its parameter.</summary>
    public ICommand Act { get; } = act;
}

/// <summary>
/// The Needs-you queue on Overview over <see cref="NeedsYouQueue"/>: its rows, the count, and what each row's action
/// does. The hold opens Security's inspection, the driver check Access control, the late orders Waterways (where each
/// is settled); the berth is confirmed here, and a refusal says why on its row. The shift log offers the same actions
/// (<see cref="ActCommand"/>). Created by <see cref="OverviewViewModel"/> on the UI thread.
/// </summary>
public sealed partial class NeedsYouPanelViewModel : ObservableObject
{
    private readonly PortState _state;

    public NeedsYouPanelViewModel(PortState state)
    {
        _state = state;
        Mirror();
        state.NeedsYou.Decisions.CollectionChanged += OnQueueChanged;
        state.NeedsYou.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NeedsYouQueue.Count))
            {
                OnPropertyChanged(nameof(Count));
            }
        };
        state.Navigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NavigationState.NeedsYouReveal))
            {
                RevealRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    /// <summary>The queue's rows, in its order.</summary>
    public ObservableCollection<DecisionRow> Rows { get; } = new();

    /// <summary>What needs you, as the masthead counts it.</summary>
    public int Count => _state.NeedsYou.Count;

    /// <summary>The masthead's Needs-you figure was pressed: the panel scrolls into view and its heading takes focus.</summary>
    public event EventHandler? RevealRequested;

    /// <summary>Resolves a row, by what it is about.</summary>
    [RelayCommand]
    private void Act(Decision? decision)
    {
        switch (decision?.Kind)
        {
            case DecisionKind.InspectionHold:
                _state.Navigation.OpenInspectionCommand.Execute(null);
                break;
            case DecisionKind.DriverCheck:
                _state.Navigation.OpenAccessCommand.Execute(null);
                break;
            case DecisionKind.LateOrders:
                _state.Navigation.GoCommand.Execute("fleet");
                break;
            case DecisionKind.BerthConfirm when decision.VesselId is { } vessel && _state.Confirm(vessel) is { } refusal:
                decision.Refusal = refusal;
                break;
        }
    }

    // The queue is rebuilt by clearing it and adding its rows, so the rows follow it change by change
    private void OnQueueChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { } added)
        {
            var at = e.NewStartingIndex;
            foreach (Decision decision in added)
            {
                Rows.Insert(at++, new DecisionRow(decision, ActCommand));
            }
        }
        else
        {
            Mirror();
        }
    }

    private void Mirror()
    {
        Rows.Clear();
        foreach (var decision in _state.NeedsYou.Decisions)
        {
            Rows.Add(new DecisionRow(decision, ActCommand));
        }
    }
}
