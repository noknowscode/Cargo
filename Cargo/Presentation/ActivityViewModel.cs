using System.Collections.Specialized;

namespace Cargo.Presentation;

/// <summary>
/// One shift-log row. Clicking it opens its detail (the full line, how long ago, and the open
/// decision it is about, with that decision's action); the other rows in its column blur back.
/// </summary>
public sealed partial class FeedEntry : ObservableObject
{
    public required string Time { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    /// <summary>The entry kind's 24-unit stroke icon, as SVG path data (an entry that needs action wears the queue's icon).</summary>
    public required string IconData { get; init; }

    /// <summary>The open decision the entry is about, whose action the detail offers; null when none is.</summary>
    public Decision? About { get; init; }

    /// <summary>The entry is about an open decision: the NEEDS ACTION chip, the alert icon and the decision's action.</summary>
    public bool NeedsAction => About is not null;

    public required ICommand Toggle { get; init; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Another row in the same column is open.</summary>
    [ObservableProperty]
    public partial bool IsDimmed { get; set; }

    /// <summary>"Logged 20:41 · 17 min ago", set when the row opens so it is current.</summary>
    [ObservableProperty]
    public partial string Logged { get; set; } = string.Empty;
}

/// <summary>
/// The shift log, newest first in two columns. An entry about an open decision (its subject named
/// in the entry) carries a NEEDS ACTION chip and the queue's alert icon; resolving the decision
/// drops both. Created on the UI thread by <see cref="OverviewViewModel"/>.
/// </summary>
public sealed partial class ActivityViewModel : ObservableObject
{
    private readonly PortState _state;

    public ActivityViewModel(PortState state)
    {
        _state = state;
        state.Decisions.CollectionChanged += OnQueueChanged;
        Build();
    }

    [ObservableProperty] public partial IReadOnlyList<FeedEntry> Left { get; set; } = Array.Empty<FeedEntry>();
    [ObservableProperty] public partial IReadOnlyList<FeedEntry> Right { get; set; } = Array.Empty<FeedEntry>();
    [ObservableProperty] public partial string Sub { get; set; } = string.Empty;

    /// <summary>No entry in the log yet today: the panel says so.</summary>
    public bool IsEmpty => PortData.Activity.Count == 0;

    private void OnQueueChanged(object? sender, NotifyCollectionChangedEventArgs e) => Build();

    private void Build()
    {
        var opened = Left.Concat(Right).FirstOrDefault(f => f.IsExpanded)?.Title;
        var open = _state.Decisions.Where(d => d.Subject is not null).ToArray();
        var feed = PortData.Activity.Select(entry =>
        {
            var about = open.FirstOrDefault(d => entry.Title.Contains(d.Subject!) || entry.Detail.Contains(d.Subject!));
            return new FeedEntry
            {
                Time = entry.Time,
                Title = entry.Title,
                Detail = entry.Detail,
                IconData = PortData.ActivityIcons[entry.Kind].Icon,
                About = about,
                Toggle = ToggleCommand
            };
        }).ToList();

        // Newest first, down the left column and then the right
        var half = (feed.Count + 1) / 2;
        Left = feed.Take(half).ToArray();
        Right = feed.Skip(half).ToArray();
        // A rebuild (a decision resolved) keeps the open row open
        if (feed.FirstOrDefault(f => f.Title == opened) is { } reopen)
        {
            Open(reopen);
        }

        var needs = feed.Count(f => f.NeedsAction);
        Sub = $"{feed.Count} today · {needs} need action · newest first";
    }

    /// <summary>Opens a row, or closes it when it is already open; one open row at a time.</summary>
    [RelayCommand]
    private void Toggle(FeedEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        if (entry.IsExpanded)
        {
            foreach (var row in Left.Concat(Right))
            {
                row.IsExpanded = row.IsDimmed = false;
            }

            return;
        }

        Open(entry);
    }

    private void Open(FeedEntry entry)
    {
        var column = Left.Contains(entry) ? Left : Right;
        foreach (var row in Left.Concat(Right))
        {
            row.IsExpanded = row == entry;
            row.IsDimmed = row != entry && column.Contains(row);
        }

        entry.Logged = $"Logged {entry.Time} · {Ago(entry.Time)}";
    }

    private string Ago(string time)
    {
        var at = ShiftClock.ClockHours(time);
        if (double.IsNaN(at))
        {
            return string.Empty;
        }

        var minutes = (int)Math.Round((_state.NowHours - at) * 60);
        return minutes < 1 ? "just now" : minutes < 60 ? $"{minutes} min ago" : $"{minutes / 60} h {minutes % 60} min ago";
    }
}
