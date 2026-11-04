namespace Cargo.Domain;

/// <summary>
/// The store's change signals, shared by <see cref="PortState"/> and its parts, so work stays proportional to what
/// changed: a structure change rebuilds what is on screen, a hover repaints, the plan's scrubber moves the plan and
/// its map only, and the yard's own moves redraw the yard. The parts raise them; views subscribe through
/// <see cref="PortState"/>.
/// </summary>
public sealed class PortSignals
{
    /// <summary>Something structural changed (a section, a selection, an assignment, a filter).</summary>
    public event EventHandler? StructureChanged;

    /// <summary>
    /// Only the pointer moved over something. Hover changes fire far more often than anything else and never change
    /// what is on screen, only how one element is lit, so they repaint in place.
    /// </summary>
    public event EventHandler? HoverChanged;

    /// <summary>
    /// The plan scrubber moved. It steps every quarter hour while it is dragged, and only the berth plan and its map
    /// show the plan hour: as a structure change every step of a scrub would re-record the harbour and rebuild every
    /// Berths panel.
    /// </summary>
    public event EventHandler? PlanHourChanged;

    /// <summary>
    /// The yard worked a stack. It fires on its own every few seconds whether or not anyone is looking at the yard:
    /// one slot changing height is no reason to rebuild the harbour and every other screen.
    /// </summary>
    public event EventHandler? YardChanged;

    public void Structure() => StructureChanged?.Invoke(this, EventArgs.Empty);

    public void Hover() => HoverChanged?.Invoke(this, EventArgs.Empty);

    public void PlanHour() => PlanHourChanged?.Invoke(this, EventArgs.Empty);

    public void Yard() => YardChanged?.Invoke(this, EventArgs.Empty);
}
