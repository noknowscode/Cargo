using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;

namespace Cargo.Tests;

/// <summary>The needs-you bar's state machine: needs → busy → done (Undo) → needs, and what it says when refused.</summary>
public class NeedsYouBarTests
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(40);

    [Test]
    public async Task Confirm_goes_busy_then_done_and_Undo_returns_to_needs()
    {
        var state = new PortState(new ManualClock());
        var bar = new NeedsYouViewModel(state, Latency);

        Assert.That(bar.IsPending, Is.True);
        Assert.That(bar.Title, Is.EqualTo("Nordic Star needs berth 07 confirmed"));
        Assert.That(bar.ConfirmLabel, Is.EqualTo("Confirm berth 07"));
        Assert.That(bar.CanReview, Is.True);
        Assert.That(bar.CanUndo, Is.False);

        var confirming = bar.ConfirmCommand.ExecuteAsync(null);
        Assert.That(bar.IsBusy, Is.True);
        Assert.That(bar.ConfirmLabel, Is.EqualTo("Confirming"));
        await confirming;

        Assert.That(bar.IsBusy, Is.False);
        Assert.That(bar.IsDone, Is.True);
        Assert.That(bar.Title, Is.EqualTo("Berth 07 confirmed for Nordic Star"));
        Assert.That(bar.Meta, Does.StartWith("Confirmed 20:58 · arrives 21:40"));
        Assert.That(bar.CanUndo, Is.True);
        Assert.That(state.IsConfirmed("nordic"), Is.True);

        bar.UndoCommand.Execute(null);

        Assert.That(bar.IsPending, Is.True);
        Assert.That(bar.CanUndo, Is.False);
        Assert.That(bar.Title, Is.EqualTo("Nordic Star needs berth 07 confirmed"));
        Assert.That(state.IsConfirmed("nordic"), Is.False);
    }

    [Test]
    public async Task A_drag_while_the_confirm_is_in_flight_drops_the_confirm()
    {
        var state = new PortState(new ManualClock());
        var bar = new NeedsYouViewModel(state, Latency);

        var confirming = bar.ConfirmCommand.ExecuteAsync(null);
        state.Assign("nordic", PortData.Berths.ToList().FindIndex(b => b.Number == "08"));
        await confirming;

        Assert.That(state.IsConfirmed("nordic"), Is.False, "berth 08 was never shown, so it is not confirmed");
        Assert.That(state.BerthOf("nordic"), Is.EqualTo("08"));
        Assert.That(bar.IsDone, Is.True);
        Assert.That(bar.CanUndo, Is.False);
    }

    [Test]
    public async Task A_refused_confirm_stays_pending_and_says_why()
    {
        var state = new PortState(new ManualClock(), new BerthPlan(new Dictionary<string, string> { ["levant"] = "07" }));
        var bar = new NeedsYouViewModel(state, Latency);

        await bar.ConfirmCommand.ExecuteAsync(null);

        Assert.That(bar.IsPending, Is.True);
        Assert.That(bar.IsRefused, Is.True);
        Assert.That(bar.Meta, Is.EqualTo("Berth 07 is taken by Levant Express during Nordic Star's window."));
        Assert.That(bar.CanUndo, Is.False);
    }

    [Test]
    public async Task With_nothing_pending_it_reads_done_and_offers_nothing()
    {
        var state = new PortState(new ManualClock());
        state.Assign("nordic", PortData.Berths.ToList().FindIndex(b => b.Number == "05"));
        var bar = new NeedsYouViewModel(state, Latency);

        Assert.That(bar.IsDone, Is.True);
        Assert.That(bar.IsPending, Is.False);
        Assert.That(bar.Title, Is.EqualTo("Nothing needs you"));
        Assert.That(bar.CanReview, Is.False);
        Assert.That(bar.CanUndo, Is.False);

        // A stray confirm (a keyboard repeat on a button that just hid) does nothing
        await bar.ConfirmCommand.ExecuteAsync(null);
        Assert.That(state.IsConfirmed("nordic"), Is.False);
        Assert.That(bar.IsDone, Is.True);
    }
}
