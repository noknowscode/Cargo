using Cargo.Domain;
using Cargo.Presentation;
using NUnit.Framework;
using Uno.Extensions.Toolkit;

namespace Cargo.Tests;

/// <summary>The frame: the header tabs and their dots, the router kept in step with the store, the layer switcher and the theme choice.</summary>
public class MainViewModelTests
{
    [Test]
    public void The_header_lists_the_five_sections_with_Overview_current()
    {
        var desk = new Desk();

        Assert.That(desk.Main.NavItems.Select(n => n.Id), Is.EqualTo(new[] { "overview", "berths", "cargo", "fleet", "security" }));
        Assert.That(desk.Main.NavItems.Single(n => n.IsCurrent).Id, Is.EqualTo("overview"));
        Assert.That(desk.Main.IsOverview, Is.True);
        Assert.That(desk.Main.NavItems.Single(n => n.Id == "berths").AutomationName, Is.EqualTo("Berths, needs you"));
        Assert.That(desk.Main.NavItems.Single(n => n.Id == "overview").Tooltip, Does.StartWith("Overview · 3 alongside"));
    }

    [Test]
    public void A_jump_in_the_store_asks_the_router_for_its_section_and_tab_and_reports_a_failure()
    {
        // The test navigator has no route resolver, so the router refuses: the models log it and carry on
        var desk = new Desk();

        Assert.DoesNotThrow(() => desk.State.Navigation.OpenInspectionCommand.Execute(null));

        Assert.That(desk.Errors, Does.Contain("Navigation to section security failed: ArgumentNullException Value cannot be null. (Parameter 'provider')"));
        Assert.That(desk.Errors, Has.Some.StartsWith("Navigation to security tab inspection failed"));
        Assert.That(desk.Main.NavItems.Single(n => n.IsCurrent).Id, Is.EqualTo("security"));
        Assert.That(desk.Main.IsOverview, Is.False);
    }

    [Test]
    public void A_route_from_the_header_is_written_back_without_navigating_again()
    {
        var desk = new Desk();

        desk.Routes.Report("cargo");

        Assert.That(desk.State.Navigation.Section, Is.EqualTo("cargo"));
        Assert.That(desk.Navigator.Routes, Is.Empty, "the router is already there");
    }

    [Test]
    public void Unknown_and_bare_routes_are_ignored()
    {
        var desk = new Desk();

        desk.Routes.Report(string.Empty);
        desk.Routes.Report("nowhere");

        Assert.That(desk.State.Navigation.Section, Is.EqualTo("overview"));
    }

    [Test]
    public void The_route_carries_Securitys_tab()
    {
        var state = new PortState(new ManualClock());
        state.Navigation.OpenAccessCommand.Execute(null);

        Assert.That(MainViewModel.RouteFor(state), Is.EqualTo("security/access"));
        state.Navigation.GoCommand.Execute("berths");
        Assert.That(MainViewModel.RouteFor(state), Is.EqualTo("berths"));
    }

    [Test]
    public void A_settled_decision_clears_its_sections_dot()
    {
        var desk = new Desk();
        var berths = desk.Main.NavItems.Single(n => n.Id == "berths");
        Assert.That(berths.NeedsYou, Is.True);

        desk.State.Confirm("nordic");

        Assert.That(berths.NeedsYou, Is.False);
        Assert.That(berths.AutomationName, Is.EqualTo("Berths"));
    }

    [Test]
    public void The_tooltips_follow_the_clock_once_a_minute()
    {
        var desk = new Desk();
        var berths = desk.Main.NavItems.Single(n => n.Id == "berths");
        Assert.That(berths.Tooltip, Does.Contain("Nordic Star in 42 min"));

        desk.Clock.Advance(TimeSpan.FromMinutes(2));

        Assert.That(berths.Tooltip, Does.Contain("Nordic Star in 40 min"));
    }

    [Test]
    public void The_layer_switcher_marks_the_layer_on_the_harbour()
    {
        var desk = new Desk();
        Assert.That(desk.Main.Layers.Select(l => l.Id), Is.EqualTo(new[] { "port", "yard", "security", "traffic" }));
        Assert.That(desk.Main.SelectedLayerIndex, Is.EqualTo(0));

        desk.Main.Layers[2].Command.Execute("security");

        Assert.That(desk.State.Stage.Layer, Is.EqualTo("security"));
        Assert.That(desk.Main.SelectedLayerIndex, Is.EqualTo(2));
        Assert.That(desk.Main.Layers.Single(l => l.IsCurrent).Id, Is.EqualTo("security"));
    }

    [Test]
    public void The_theme_choice_goes_to_the_theme_service()
    {
        var desk = new Desk();
        Assert.That(desk.Main.ThemeIndex, Is.EqualTo(0));

        desk.Main.ThemeIndex = 2;
        Assert.That(desk.Themes.Theme, Is.EqualTo(AppTheme.Dark));
        Assert.That(desk.Main.ThemeIndex, Is.EqualTo(2));

        desk.Main.ThemeIndex = 7;
        Assert.That(desk.Themes.Theme, Is.EqualTo(AppTheme.Dark), "an index out of range is ignored");
    }

    [Test]
    public void The_operator_line_reads_the_duty_roster()
    {
        var desk = new Desk();

        Assert.That(desk.Main.AccountName, Is.EqualTo("Account and settings: E. Lindqvist"));
        Assert.That(desk.Main.OperatorLine, Does.StartWith("Duty operator · WPA 0417 · "));
    }
}
