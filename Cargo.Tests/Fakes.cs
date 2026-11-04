using Cargo.Domain;
using Cargo.Presentation;
using Microsoft.Extensions.Logging;

namespace Cargo.Tests;

/// <summary>A dispatcher that runs the work at once, on the test's thread, as the UI thread would in order.</summary>
public sealed class InlineDispatcher : IDispatcher
{
    public bool HasThreadAccess => true;

    public bool TryEnqueue(Action action)
    {
        action();
        return true;
    }

    public ValueTask<TResult> ExecuteAsync<TResult>(AsyncFunc<TResult> action, CancellationToken cancellation) => action(cancellation);
}

/// <summary>
/// A navigator with no services behind it: Uno's NavigateRouteAsync needs a route resolver from the navigator's
/// services, so a request through it fails, which is how the tests see that a failed navigation is reported. A request
/// that reaches it is recorded.
/// </summary>
public sealed class RecordingNavigator : INavigator
{
    public List<string> Routes { get; } = new();

    public Route? Route => null;

    public Task<bool> CanNavigate(Route route) => Task.FromResult(true);

    public Task<NavigationResponse?> NavigateAsync(NavigationRequest request)
    {
        Routes.Add(request.Route.ToString());
        return Task.FromResult<NavigationResponse?>(null);
    }
}

/// <summary>The router's notices, raised by the test.</summary>
public sealed class FakeRouteNotifier : IRouteNotifier
{
    public event EventHandler<RouteChangedEventArgs>? RouteChanged;

    /// <summary>Reports a route under Main, as the router does: "security/inspection" is Main's path.</summary>
    public void Report(string path) =>
        RouteChanged?.Invoke(this, new RouteChangedEventArgs(null!, null!, new Route(string.Empty, "Main", path, null, false)));
}

/// <summary>A theme service that keeps the choice in memory.</summary>
public sealed class FakeThemeService : IThemeService
{
    public bool IsDark => Theme == AppTheme.Dark;

    public AppTheme Theme { get; private set; } = AppTheme.System;

    public event EventHandler<AppTheme>? ThemeChanged;

    public Task<bool> SetThemeAsync(AppTheme theme)
    {
        Theme = theme;
        ThemeChanged?.Invoke(this, theme);
        return Task.FromResult(true);
    }

    public Task InitializeAsync() => Task.CompletedTask;
}

/// <summary>The app's frame on fakes: the store on a manual clock and every page model, as the host builds them.</summary>
public sealed class Desk
{
    public Desk(PortState? state = null)
    {
        Clock = new ManualClock();
        State = state ?? new PortState(Clock);
        var dispatcher = new InlineDispatcher();
        Overview = new OverviewViewModel(State, dispatcher);
        Berths = new BerthsViewModel(State, dispatcher);
        Cargo = new CargoViewModel(State, Camera, dispatcher);
        Fleet = new FleetViewModel(State, dispatcher);
        Security = new SecurityViewModel(State, Navigator, Routes, dispatcher, Log<SecurityViewModel>());
        Main = new MainViewModel(State, Camera, Navigator, Routes, dispatcher, Themes, Log<MainViewModel>(),
            Overview, Berths, Cargo, Fleet, Security);
    }

    /// <summary>What the models logged as errors (a failed navigation, a failed theme switch).</summary>
    public List<string> Errors { get; } = new();

    private ILogger<T> Log<T>() => new ListLogger<T>(Errors);

    public ManualClock Clock { get; }
    public PortState State { get; }
    public ScannerCamera Camera { get; } = new();
    public RecordingNavigator Navigator { get; } = new();
    public FakeRouteNotifier Routes { get; } = new();
    public FakeThemeService Themes { get; } = new();
    public OverviewViewModel Overview { get; }
    public BerthsViewModel Berths { get; }
    public CargoViewModel Cargo { get; }
    public FleetViewModel Fleet { get; }
    public SecurityViewModel Security { get; }
    public MainViewModel Main { get; }
}

/// <summary>A logger that keeps the errors it is given.</summary>
public sealed class ListLogger<T>(List<string> errors) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            errors.Add(formatter(state, exception) + (exception is null ? string.Empty : $": {exception.GetType().Name} {exception.Message}"));
        }
    }
}
