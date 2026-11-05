namespace Cargo.Domain;

/// <summary>
/// The terminal's inspection log (Security, Inspection): the day's entries from the data, then what the dispatcher logs
/// this shift (an inspector assigned, a re-weigh booked, a container scanned), stamped on the store's clock. Not saved.
/// </summary>
public sealed class InspectionLog
{
    private readonly Func<double> _nowHours;
    private readonly List<InspectionLogDef> _entries = new(PortData.InspectionLog);
    private readonly HashSet<string> _scanned = new();

    /// <summary>The log on the store's clock (<paramref name="nowHours"/>).</summary>
    public InspectionLog(Func<double> nowHours) => _nowHours = nowHours;

    /// <summary>Raised when an entry is added.</summary>
    public event EventHandler? Changed;

    /// <summary>Oldest first.</summary>
    public IReadOnlyList<InspectionLogDef> Entries => _entries;

    /// <summary>Adds an entry at the time now; <paramref name="tone"/> is its mark's palette key.</summary>
    public void Add(string mark, string text, string tone)
    {
        _entries.Add(new InspectionLogDef(ShiftClock.Format(_nowHours()), mark, text, tone));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A scan of the container has been logged this shift.</summary>
    public bool Scanned(string containerId) => _scanned.Contains(containerId);

    /// <summary>Logs a scan of the container, once: false when it is logged already.</summary>
    public bool LogScan(string containerId, string text)
    {
        if (!_scanned.Add(containerId))
        {
            return false;
        }

        Add("✓", text, "PrimaryBrush");
        return true;
    }
}
