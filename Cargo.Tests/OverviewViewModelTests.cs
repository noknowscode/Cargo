using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>Overview: the masthead's figures, the day's timeline (Chart and Table), Next 6 h and the shift log.</summary>
public class OverviewViewModelTests
{
    [Test]
    public void The_masthead_greets_the_operator_and_counts_what_needs_her()
    {
        var desk = new Desk();
        var overview = desk.Overview;

        Assert.Multiple(() =>
        {
            Assert.That(overview.GreetingName, Is.EqualTo("Elin."));
            Assert.That(overview.NeedsYouText, Is.EqualTo("4"));
            Assert.That(overview.NeedsYouLink, Is.EqualTo("4 need you, show the queue"));
            Assert.That(overview.ShiftProgress, Is.InRange(0.2, 0.3), "18:00 to 06:00, at 20:58");
        });

        desk.State.NeedsYou.AssignInspector();
        Assert.That(overview.NeedsYouText, Is.EqualTo("3"));
    }

    [Test]
    public void The_timeline_switches_between_the_chart_and_its_table()
    {
        var timeline = new TimelineViewModel(new PortState(new ManualClock()));
        Assert.That((timeline.IsTable, timeline.ViewIndex), Is.EqualTo((false, 0)));

        timeline.ShowTableCommand.Execute(null);
        Assert.That((timeline.IsTable, timeline.ViewIndex), Is.EqualTo((true, 1)));

        timeline.ShowChartCommand.Execute(null);
        Assert.That(timeline.IsTable, Is.False);
    }

    [Test]
    public void The_table_has_an_hour_a_row_with_the_hour_now_marked_and_the_future_blank()
    {
        var timeline = new TimelineViewModel(new PortState(new ManualClock()));

        Assert.Multiple(() =>
        {
            Assert.That(timeline.Rows.First().Time, Is.EqualTo("06:00"));
            Assert.That(timeline.Rows.Last().Time, Is.EqualTo("06:00"));
            Assert.That(timeline.Rows.Single(r => r.IsNow).Time, Is.EqualTo("20:00"));
            Assert.That(timeline.Rows.Where(r => r.IsFuture).All(r => r.Moves == "—"), Is.True);
            Assert.That(timeline.Rows.Where(r => !r.IsFuture).All(r => r.Moves != "—"), Is.True);
        });
    }

    [Test]
    public void The_tide_lane_marks_high_water_and_the_level_now()
    {
        var clock = new ManualClock();
        var timeline = new TimelineViewModel(new PortState(clock));
        var tide = timeline.Tide!;

        Assert.That(tide.Now, Is.EqualTo(TimelineViewModel.At(PortData.NowHours)));
        Assert.That(tide.HighWaterLabel, Does.StartWith("HW "));
        Assert.That(tide.NowLabel, Does.EndWith(" m"));
        Assert.That(timeline.TidePast!.Count + timeline.TideAhead!.Count, Is.EqualTo(97), "a sample every 15 minutes over 24 h");

        clock.Advance(TimeSpan.FromMinutes(20));
        timeline.Refresh();
        Assert.That(timeline.Tide!.Now, Is.GreaterThan(tide.Now));
        Assert.That(timeline.Summary, Does.Contain("Now 21:18"));
    }

    [Test]
    public void Next_six_hours_splits_what_has_happened_from_what_is_ahead()
    {
        var clock = new ManualClock();
        var state = new PortState(clock);
        var next = new NextSixHoursViewModel(state, ShiftTimeline.Events());

        var nordic = next.Upcoming.Single(r => r.Id == "nordic" && r.Arrival);
        Assert.Multiple(() =>
        {
            Assert.That(nordic.Countdown, Is.EqualTo("in 42 min"));
            Assert.That(nordic.IsSoon, Is.True);
            Assert.That(nordic.Sub, Is.EqualTo("Arrives Berth 07 · pilot aboard"));
            Assert.That(next.IsEmpty, Is.False);
        });

        state.Stage.HoverCommand.Execute("nordic");
        Assert.That(nordic.IsLinked, Is.True, "a hover elsewhere lights the row");

        clock.Advance(TimeSpan.FromMinutes(45));
        next.Refresh();
        Assert.That(next.Past.Select(r => r.Id), Does.Contain("nordic"));
        Assert.That(next.Past.Single(r => r.Id == "nordic" && r.Arrival).Countdown, Is.EqualTo("arrived"));
    }

    [Test]
    public void Next_six_hours_names_the_berth_the_plan_gives_an_arrival()
    {
        var state = new PortState(new ManualClock());
        state.Assign("nordic", PortData.Berths.ToList().FindIndex(b => b.Number == "08"));
        var next = new NextSixHoursViewModel(state, ShiftTimeline.Events());

        Assert.That(next.Upcoming.Single(r => r.Id == "nordic" && r.Arrival).Sub, Is.EqualTo("Arrives Berth 08 · pilot aboard"));
    }

    [Test]
    public void The_log_marks_the_entries_about_open_decisions_until_they_settle()
    {
        var state = new PortState(new ManualClock());
        var log = new ActivityViewModel(state, new NeedsYouPanelViewModel(state).ActCommand);
        var feed = log.Left.Concat(log.Right).ToList();

        Assert.That(feed.Count(f => f.NeedsAction), Is.EqualTo(2), "the hold and the driver check");
        Assert.That(log.Sub, Is.EqualTo($"{feed.Count} today · 2 need action · newest first"));
        Assert.That(log.IsEmpty, Is.False);

        state.NeedsYou.AssignInspector();
        var after = log.Left.Concat(log.Right).ToList();
        Assert.That(after.Count(f => f.NeedsAction), Is.EqualTo(1));
        Assert.That(after.Single(f => f.NeedsAction).About?.Kind, Is.EqualTo(DecisionKind.DriverCheck));
    }

    [Test]
    public void Opening_a_log_entry_dims_its_column_and_a_second_press_closes_it()
    {
        var state = new PortState(new ManualClock());
        var log = new ActivityViewModel(state, new NeedsYouPanelViewModel(state).ActCommand);
        var entry = log.Left[1];

        log.ToggleCommand.Execute(entry);
        Assert.Multiple(() =>
        {
            Assert.That(entry.IsExpanded, Is.True);
            Assert.That(entry.Logged, Does.StartWith($"Logged {entry.Time} · "));
            Assert.That(log.Left.Where(e => e != entry).All(e => e.IsDimmed), Is.True);
            Assert.That(log.Right.Any(e => e.IsDimmed), Is.False);
        });

        log.ToggleCommand.Execute(entry);
        Assert.That(log.Left.Concat(log.Right).Any(e => e.IsExpanded || e.IsDimmed), Is.False);
    }
}
