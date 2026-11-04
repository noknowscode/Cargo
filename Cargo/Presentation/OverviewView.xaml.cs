using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class OverviewView : Page
{
    private bool _attached;

    public OverviewView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public OverviewViewModel? ViewModel => DataContext as OverviewViewModel;

    // xaml-lint: allow codebehind - the clock reaches the page's charts only while Overview is on screen (Live), like
    // every other section; the page stays alive once visited
    private void Attach()
    {
        if (!_attached && ViewModel is { } vm)
        {
            _attached = true;
            this.TickWhenVisible(vm.State, vm.Refresh);
        }

        Bindings.Update();
    }

    /// <summary>Where the first section (Today at North Quay) starts, in the coordinates of <paramref name="content"/>.</summary>
    public double FirstSectionTop(UIElement content) =>
        FirstSection.TransformToVisual(content).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;

    /// <summary>A Next 6 h row's spoken name: "Nordic Star, arrives Berth 07, in 42 min".</summary>
    // The Chart/Table toggle: the chosen segment's label is paper on the ink indicator

    public static Microsoft.UI.Xaml.Media.Brush ChartInk(bool table) => Tokens.Brush(table ? "InkBrush" : "PaperBrush");

    public static Microsoft.UI.Xaml.Media.Brush TableInk(bool table) => Tokens.Brush(table ? "PaperBrush" : "InkBrush");

    // Next 6 h: a lit vessel takes the linked-hover tint (accent at 7%, alpha in the colour for the fade); a movement
    // that has happened is a filled navy dot with a surface ring, one ahead a hollow navy ring; under an hour, in ink

    public static Microsoft.UI.Xaml.Media.Brush LinkGround(bool linked) => Tokens.Tint("AccentBrush", linked ? .07 : 0);

    public static Microsoft.UI.Xaml.Media.Brush DotFill(bool happened) => Tokens.Brush(happened ? "NavyBrush" : "SurfaceBrush");

    public static Microsoft.UI.Xaml.Media.Brush DotRing(bool happened) => Tokens.Brush(happened ? "SurfaceBrush" : "NavyBrush");

    public static double DotRingWidth(bool happened) => happened ? 2 : 1.6;

    public static Microsoft.UI.Xaml.Media.Brush CountdownInk(bool soon) => Tokens.Brush(soon ? "InkBrush" : "TextMutedBrush");

    public static string SixName(string name, string sub, string countdown) => $"{name}, {sub}, {countdown}";

    // The timeline table's rows: future hours on the ahead-of-now shade, the NOW row between two ink rules

    public static Microsoft.UI.Xaml.Media.Brush RowGround(bool future) =>
        future ? Tokens.Brush("AheadShadeBrush") : Tokens.Transparent;

    public static Microsoft.UI.Xaml.Media.Brush RowRule(bool now) =>
        now ? Tokens.Brush("InkBrush") : Tokens.Brush("HairlineBrush");

    public static Thickness RowRuleWidth(bool now) => now ? new Thickness(0, 1.25, 0, 1.25) : new Thickness(0, 1, 0, 0);
}
