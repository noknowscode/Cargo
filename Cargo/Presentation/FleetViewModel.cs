using Windows.Foundation;

namespace Cargo.Presentation;

public sealed class LockingRow
{
    public required string Time { get; init; }
    public required string Lock { get; init; }
    public required string Vessel { get; init; }
    public required string State { get; init; }
    public required Standing Standing { get; init; }
}

public sealed class GaugeRow
{
    public required string Name { get; init; }
    public required string Chainage { get; init; }
    public required string Level { get; init; }

    /// <summary>The trace through the last twelve readings, in the gauge's 80 x 22 frame.</summary>
    public required IReadOnlyList<Point> Spark { get; init; }

    /// <summary>The area under the trace, closed along the frame's foot.</summary>
    public required IReadOnlyList<Point> SparkArea { get; init; }

    /// <summary>The gauge's status, as its palette key; the reading takes its ink variant.</summary>
    public required string Tone { get; init; }
}

public sealed class NoticeRow
{
    public required string Title { get; init; }
    public required string Detail { get; init; }

    /// <summary>The notice's status, as its palette key.</summary>
    public required string Tone { get; init; }
}

public sealed class FleetRow
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required string Eta { get; init; }
    public required string Slip { get; init; }

    /// <summary>Running behind her order: orange, else sea green.</summary>
    public required bool IsLate { get; init; }

    /// <summary>The vessel the crew channel is open to: a sunk ground.</summary>
    public required bool IsCurrent { get; init; }
    public required ICommand Select { get; init; }
}

public sealed class ChatBubble
{
    public required string Text { get; init; }
    public required string Meta { get; init; }

    /// <summary>The crew's message, on the left in a sunk bubble; dispatch's sit on the right in ink.</summary>
    public required bool FromCrew { get; init; }
}

public sealed class QuickMessage
{
    /// <summary>"eta", "hold" or "level": what the message is for.</summary>
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Text { get; init; }
    public required ICommand Send { get; init; }

    /// <summary>The button for a screen reader: what it sends, and to whom.</summary>
    public required string AccessibleName { get; init; }
}

/// <summary>
/// Waterways: the river fleet, lockings, gauges, notices and the crew chat for the selected
/// barge. Everything on the page follows <see cref="CrewChat.Selection"/> and the messages
/// this page sends, so it refreshes on those two and ignores the rest of the port.
/// </summary>
public sealed partial class FleetViewModel : ObservableObject
{
    public FleetViewModel(PortState state, IDispatcher dispatcher)
    {
        State = state;

        // The router builds models off the UI thread; the rows are bound to the views, so they are built on it.
        dispatcher.TryEnqueue(() =>
        {
            BuildStatic();
            Refresh();
            state.Chat.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CrewChat.Selection))
                {
                    Refresh();
                }
            };
        });
    }

    public PortState State { get; }

    [ObservableProperty]
    public partial IReadOnlyList<LegendItem> Legend { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<LockingRow> Lockings { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<GaugeRow> Gauges { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<NoticeRow> Notices { get; set; } = [];

    public ObservableCollection<FleetRow> Fleet { get; } = new();
    public ObservableCollection<ChatBubble> Thread { get; } = new();
    public ObservableCollection<QuickMessage> QuickMessages { get; } = new();

    public string BookedLabel => $"Booked passages · now {ShiftClock.Format(PortData.NowHours)}";
    public string NoticeCount => $"{PortData.Notices.Count} active";
    public string FleetCount => $"{PortData.RiverFleet.Count} vessels";

    // Masthead figures
    public string UnderwayCount => PortData.RiverFleet.Count.ToString();
    public string LateCount => PortData.RiverFleet.Count(v => v.SlipMinutes > 0).ToString();
    public string LockCount => PortData.Lockings.Count.ToString();
    public string NoticeTotal => PortData.Notices.Count.ToString();

    [ObservableProperty]
    public partial string CrewHeading { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CrewSubtitle { get; set; } = string.Empty;

    /// <summary>Empty: no message with the selected vessel yet this shift; the channel says so instead of a blank box.</summary>
    [ObservableProperty]
    public partial bool ThreadEmpty { get; set; }

    [ObservableProperty]
    public partial string ThreadEmptyText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SlipLabel { get; set; } = string.Empty;

    /// <summary>The vessel the channel is open to runs late.</summary>
    [ObservableProperty]
    public partial bool IsLate { get; set; }

    private RiverVessel Selected =>
        PortData.RiverFleet.FirstOrDefault(v => v.Id == State.Chat.Selection) ?? PortData.RiverFleet[0];

    [RelayCommand]
    private void Select(string id) => State.Chat.Selection = id;

    [RelayCommand]
    private void SendDraft() => SendToSelected(State.Chat.Draft);

    /// <summary>
    /// Sends a quick message. "Confirm new ETA" to a late crew also handles her late order in the needs-you queue
    /// (<see cref="NeedsYouQueue.ConfirmRevisedEta"/>).
    /// </summary>
    [RelayCommand]
    private void SendQuick(string id)
    {
        if (id == "eta")
        {
            State.NeedsYou.ConfirmRevisedEta(Selected.Id);
            Refresh();
        }
        else if (QuickMessages.FirstOrDefault(q => q.Id == id) is { } message)
        {
            SendToSelected(message.Text);
        }
    }

    private void SendToSelected(string text)
    {
        State.Chat.Send(Selected.Id, text);
        Refresh();
    }

    /// <summary>
    /// A 12-point trace that drifts with the gauge's trend arrow, as the line and the area under
    /// it, both in the gauge's real 80 x 22 frame so they register without stretching.
    /// </summary>
    public static (IReadOnlyList<Point> Line, IReadOnlyList<Point> Area) Spark(GaugeDef gauge, int index)
    {
        var drift = gauge.Trend == "↓" ? -0.02 : gauge.Trend == "↑" ? 0.02 : 0;
        var values = Enumerable.Range(0, 12)
            .Select(j => gauge.Level + Math.Sin(j * 0.9 + index) * 0.12 + j * drift)
            .ToList();

        var min = values.Min();
        var max = values.Max();
        var span = max - min;
        if (span <= 0)
        {
            span = 1;
        }

        const double width = 80, height = 22, pad = 2;
        var points = values
            .Select((v, j) => new Point(j * width / 11d, height - pad - (v - min) / span * (height - pad * 2 - 2)))
            .ToList();
        var area = points.Prepend(new Point(0, height)).Append(new Point(width, height)).ToList();
        return (points, area);
    }

    private void BuildStatic()
    {
        Legend = new[]
        {
            new LegendItem { Label = "On time", Swatch = "SeaGreenBrush" },
            new LegendItem { Label = "Running late", Swatch = "OrangeBrush" },
            new LegendItem { Label = "Lock", Swatch = "OnSurfaceBrush" },
            new LegendItem { Label = "Warning", Swatch = "ErrorBrush" }
        };

        Lockings = PortData.Lockings.Select(l => new LockingRow
        {
            Time = l.Time,
            Lock = l.Lock,
            Vessel = l.Vessel,
            State = l.State,
            Standing = l.State switch
            {
                "Done" => Standing.Done,
                "Queued" or "At risk" => Standing.Late,
                "After maintenance" => Standing.Blocked,
                _ => Standing.OnTime
            }
        }).ToList();

        // Every trace is water, in the river's colour; the gauge's status stays on its reading
        Gauges = PortData.Gauges.Select((g, i) =>
        {
            var (line, area) = Spark(g, i);
            return new GaugeRow
            {
                Name = g.Name,
                Chainage = $"km {g.Km:0}",
                Level = $"{g.Level:0.0} m {g.Trend}",
                Spark = line,
                SparkArea = area,
                Tone = g.Tone
            };
        }).ToList();

        Notices = PortData.Notices.Select(n => new NoticeRow
        {
            Title = n.Title,
            Detail = n.Detail,
            Tone = n.Tone
        }).ToList();
    }

    private void Refresh()
    {
        var selected = Selected;

        Fleet.Clear();
        foreach (var vessel in PortData.RiverFleet)
        {
            var late = vessel.SlipMinutes > 0;
            var current = vessel.Id == selected.Id;

            Fleet.Add(new FleetRow
            {
                Id = vessel.Id,
                Name = vessel.Name,
                Detail = $"Order {vessel.Order} · km {vessel.Km:0.0} · {vessel.Speed} kn",
                Eta = $"ETA {ShiftClock.Format(vessel.Eta + vessel.SlipMinutes / 60d)}",
                Slip = late ? $"+{vessel.SlipMinutes} min" : "On time",
                IsLate = late,
                IsCurrent = current,
                Select = SelectCommand
            });
        }

        var isLate = selected.SlipMinutes > 0;
        CrewHeading = $"{selected.Name} · crew";
        CrewSubtitle = $"{selected.Master} · VHF 12 · " +
                       (selected.Direction > 0 ? "upbound · " : "downbound · ") + selected.Next;
        SlipLabel = isLate ? $"+{selected.SlipMinutes} min" : "On time";
        IsLate = isLate;

        Thread.Clear();
        foreach (var line in State.Chat.ThreadFor(selected.Id))
        {
            var fromCrew = line.Who == "crew";
            Thread.Add(new ChatBubble
            {
                Text = line.Text,
                Meta = $"{(fromCrew ? selected.Master.Split(' ').Last() : "Dispatch")} · {ShiftClock.Format(line.Hour)}",
                FromCrew = fromCrew
            });
        }

        ThreadEmpty = Thread.Count == 0;
        ThreadEmptyText = $"No messages with {selected.Name} this shift. Send one below or pick a quick message.";

        QuickMessages.Clear();
        foreach (var (id, label, text) in new[]
                 {
                     ("eta", "Confirm new ETA", NeedsYouQueue.RevisedEtaMessage(selected)),
                     ("hold", "Hold at next lock", $"Hold at {selected.Next} until further notice — traffic ahead. Acknowledge."),
                     ("level", "Report water level", $"Report observed water level and any squat at your position, km {selected.Km:0.0}.")
                 })
        {
            QuickMessages.Add(new QuickMessage
            {
                Id = id, Label = label, Text = text, Send = SendQuickCommand,
                AccessibleName = $"{label}: send to {selected.Name}" + (id == "eta" && State.NeedsYou.AwaitsEta(selected) ? ", handles her late order" : string.Empty)
            });
        }
    }
}
