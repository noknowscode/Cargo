namespace Cargo.Domain;

/// <summary>
/// The river crews' channel on Waterways: the vessel the channel is open to, the draft, and each thread (the
/// seeded messages and what dispatch has sent this session, which is not saved).
/// </summary>
public sealed partial class CrewChat : ObservableObject
{
    private readonly PortSignals _signals;
    private readonly Func<double> _nowHours;
    private readonly Dictionary<string, List<ChatLine>> _sent = new();

    /// <summary>The channel, stamping what is sent with <paramref name="nowHours"/>, the store's clock.</summary>
    public CrewChat(PortSignals signals, Func<double> nowHours)
    {
        _signals = signals;
        _nowHours = nowHours;
    }

    /// <summary>Raised when dispatch sends to a vessel, with her id.</summary>
    public event EventHandler<string>? Sent;

    [ObservableProperty]
    public partial string Selection { get; set; } = "al118";

    partial void OnSelectionChanged(string value) => _signals.Structure();

    [ObservableProperty]
    public partial string Draft { get; set; } = string.Empty;

    public IReadOnlyList<ChatLine> ThreadFor(string vesselId)
    {
        var vessel = PortData.RiverFleet.First(v => v.Id == vesselId);
        return _sent.TryGetValue(vesselId, out var extra)
            ? vessel.Thread.Concat(extra).ToList()
            : vessel.Thread;
    }

    /// <summary>Sends to a vessel and clears the draft; a blank message is not sent.</summary>
    public void Send(string vesselId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!_sent.TryGetValue(vesselId, out var list))
        {
            list = new List<ChatLine>();
            _sent[vesselId] = list;
        }

        list.Add(new ChatLine("dispatch", text.Trim(), _nowHours()));
        Draft = string.Empty;
        Sent?.Invoke(this, vesselId);
        _signals.Structure();
    }
}
