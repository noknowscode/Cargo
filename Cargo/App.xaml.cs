namespace Cargo;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // A desktop Uno app is windowed, so redirected stdout captures nothing:
        // startup failures go to startup.log next to the executable instead of vanishing.
        UnhandledException += (_, e) => Trace("UnhandledException", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Trace("AppDomain", e.ExceptionObject as Exception);

        try
        {
            await LaunchAsync(args);
        }
        catch (Exception ex)
        {
            Trace("OnLaunched", ex);
            throw;
        }
    }

    // UseNavigation and NavigateAsync resolve views and view models by reflection. WebAssembly is the head
    // that trims app code, and Platforms/WebAssembly/LinkerConfig.xml roots the whole Cargo assembly there.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The view and view-model types navigation reflects over live in the Cargo assembly, which LinkerConfig.xml roots.")]
    private async Task LaunchAsync(LaunchActivatedEventArgs args)
    {
        // The code palette starts in the theme the window will open in, so nothing is built light and recoloured
        // after the first frame; Shell keeps it in step with the window from then on
        Tokens.Apply(StartTheme());

        // The store's clock is a DispatcherTimer, so the store is built here on the UI thread;
        // the router resolves page models on a background thread.
        var state = new PortState(new DispatcherClock());

        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                    logBuilder
                        .SetMinimumLevel(context.HostingEnvironment.IsDevelopment() ? LogLevel.Information : LogLevel.Warning)
                        .CoreLogLevel(LogLevel.Warning),
                    enableUnoLogging: true)
                .UseToolkitNavigation()
                .ConfigureServices((context, services) =>
                {
                    // One store for the whole shift: every page model reads and writes the same port
                    services.AddSingleton(state);

                    // Section models, handed to MainViewModel: each section page is built ahead of its first visit
                    services.AddTransient<OverviewViewModel>();
                    services.AddTransient<BerthsViewModel>();
                    services.AddTransient<CargoViewModel>();
                    services.AddTransient<FleetViewModel>();
                    services.AddTransient<SecurityViewModel>();
                })
                .UseNavigation(RegisterRoutes));

        MainWindow = builder.Window;
#if __DESKTOP__
        // The native window shows its own ground before the first frame and while it resizes: give it the page's,
        // so a dark launch does not open on a white window (Skia desktop is the head with this API)
        Uno.UI.Xaml.WindowHelper.SetBackground(MainWindow, Tokens.Brush("PaperBrush"));
#endif

#if DEBUG && !NO_UNO_STUDIO
        // Hot Design blocks window creation when no DevServer is reachable, so headless
        // verification runs opt out with APP_NO_HOTDESIGN=1.
        if (Environment.GetEnvironmentVariable("APP_NO_HOTDESIGN") != "1")
        {
            MainWindow.UseStudio();
        }
#endif
        MainWindow.SetWindowIcon();

#if __DESKTOP__ || WINDOWS
        // The dashboard is authored for a wide desk: open at 1680 x 1020 (the Wide layout), and every size class
        // reflows if the window is made smaller (App.xaml). Only a desktop window can be sized: Android and iOS are
        // full screen and the browser owns the WebAssembly page.
        var size = new Windows.Graphics.SizeInt32 { Width = 1680, Height = 1020 };
#if WINDOWS
        // WinAppSDK knows the screen: no larger than nine tenths of its work area, so a laptop's window never opens
        // past the screen edge (DisplayArea is not implemented on Uno's desktop head, Uno0001)
        if (Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(MainWindow.AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary)?.WorkArea
            is { Width: > 0, Height: > 0 } work)
        {
            size = new Windows.Graphics.SizeInt32
            {
                Width = Math.Min(size.Width, work.Width * 9 / 10),
                Height = Math.Min(size.Height, work.Height * 9 / 10)
            };
        }
#endif
#if DEBUG
        // Layout verification opens at a given size (CARGO_WINDOW=390x844), one launch per size
        if (DebugHooks.Hook("CARGO_WINDOW")?.Split('x') is [var w, var h]
            && int.TryParse(w, out var width) && int.TryParse(h, out var height))
        {
            size = new Windows.Graphics.SizeInt32 { Width = width, Height = height };
        }
#endif
        MainWindow.AppWindow.Resize(size);
#endif

        // Open straight on the section the store starts in (Overview, unless a DEBUG hook says otherwise)
        Host = await builder.NavigateAsync<Shell>(initialNavigate: (services, navigator) =>
            navigator.NavigateRouteAsync(this, $"Main/{MainViewModel.RouteFor(state)}"));

#if DEBUG
        await ApplyThemeFixtureAsync();
#endif
    }

#if DEBUG
    /// <summary>
    /// Verification runs open in a given theme: CARGO_THEME=light|dark|system, set through the window's theme
    /// service as the account menu does (so it is saved, like a user's choice). Prints a VERIFY line.
    /// </summary>
    private async Task ApplyThemeFixtureAsync()
    {
        if (!Enum.TryParse<Uno.Extensions.Toolkit.AppTheme>(DebugHooks.Hook("CARGO_THEME"), ignoreCase: true, out var theme))
        {
            return;
        }

        // The window's scoped services are attached to its content once navigation has set it up
        for (var i = 0; i < 50; i++)
        {
            if (MainWindow?.Content is FrameworkElement root
                && root.FindServiceProvider()?.GetService<Uno.Extensions.Toolkit.IThemeService>() is { } service)
            {
                await service.SetThemeAsync(theme);

                // The palette follows the window on its ActualThemeChanged, a moment after the switch
                for (var wait = 0; wait < 20 && Tokens.Theme != root.ActualTheme; wait++)
                {
                    await Task.Delay(100);
                }

                Console.Error.WriteLine($"VERIFY theme={theme} actual={root.ActualTheme} palette={Tokens.Theme}");
                return;
            }

            await Task.Delay(100);
        }

        Console.Error.WriteLine("VERIFY theme service not found");
    }
#endif

    /// <summary>
    /// The theme the window opens in: the choice Uno.Extensions' theme service saved (it keeps it in LocalSettings
    /// as "CurrentTheme"), or the device's under System or when nothing readable was saved. Read before the window
    /// exists, so the palette and the window's own ground are right from the first frame.
    /// </summary>
    private ElementTheme StartTheme()
    {
        try
        {
            if (ApplicationData.Current.LocalSettings.Values["CurrentTheme"] is string saved
                && Enum.TryParse<Uno.Extensions.Toolkit.AppTheme>(saved, out var theme)
                && theme != Uno.Extensions.Toolkit.AppTheme.System)
            {
                return theme == Uno.Extensions.Toolkit.AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
            }
        }
        catch (Exception)
        {
            // Settings storage can be unavailable (a sandboxed or read-only profile): follow the device
        }

        return RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
    }

    /// <summary>
    /// Main hosts the header, the harbour stage and the section region; each section is a
    /// nested route whose name is its <see cref="PortState.Section"/> id.
    /// </summary>
    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap(ViewModel: typeof(ShellViewModel)),
            new ViewMap<MainPage, MainViewModel>());

        routes.Register(
            new RouteMap("", View: views.FindByViewModel<ShellViewModel>(),
                Nested:
                [
                    new RouteMap("Main", View: views.FindByViewModel<MainViewModel>(), IsDefault: true,
                        Nested:
                        [
                            // Sections are panes declared in MainPage's Visibility region (built in the
                            // background after startup), so these routes carry no views of their own
                            new RouteMap("overview", IsDefault: true),
                            new RouteMap("berths"),
                            new RouteMap("cargo"),
                            new RouteMap("fleet"),
                            new RouteMap("security",
                                Nested:
                                [
                                    // In-page tabs: panes named in SecurityView's Visibility region, no views of their own
                                    new RouteMap("zones", IsDefault: true),
                                    new RouteMap("access"),
                                    new RouteMap("inspection")
                                ])
                        ])
                ]));
    }

    private static void Trace(string source, Exception? ex)
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "startup.log");
            System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {source}: {ex}\n\n");
        }
        catch
        {
            // Tracing must never be the thing that breaks startup.
        }
    }
}
