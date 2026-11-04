using Cargo.Controls;

namespace Cargo.Presentation;

/// <summary>Berths: the decision waiting on you and every berth's status, above the plan and the vessel you are working.</summary>
public sealed partial class BerthsView : Page
{
    public BerthsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    private bool _attached;

    public BerthsViewModel? ViewModel => DataContext as BerthsViewModel;

    // The models say what each row and figure is; the theme's brushes for each are picked here

    public static Brush ConflictInk(bool conflicts) => Tokens.Brush(conflicts ? "AlertInkBrush" : "InkBrush");

    /// <summary>
    /// The row's ground: the needs row keeps its alert tint, a linked hover the hover ground. Alpha in the colour, not
    /// the brush: the ground fades through a BrushTransition.
    /// </summary>
    public static Brush RowGround(BerthRowKind kind, bool linked) =>
        kind == BerthRowKind.Needs ? Tokens.Tint("AlertBrush", .05) : Tokens.Tint("InkBrush", linked ? .06 : 0);

    /// <summary>Ink round the row being worked; the needs row takes a thin alert edge on a linked hover instead.</summary>
    public static Brush RowOutline(BerthRowKind kind, bool current, bool linked) =>
        current ? Tokens.Brush("InkBrush") : linked && kind == BerthRowKind.Needs ? Tokens.Brush("AlertBrush") : Tokens.Brush("InkBrush", 0);

    /// <summary>The needs plate is the count badge: white on alert ink (5.6:1).</summary>
    public static Brush PlateGround(BerthRowKind kind) => Tokens.Brush(kind == BerthRowKind.Needs ? "AlertInkBrush" : "SurfaceSunkBrush");

    public static Brush PlateInk(BerthRowKind kind) => kind switch
    {
        BerthRowKind.Needs => Tokens.Brush("OnAccentBrush"),
        BerthRowKind.Restricted => Tokens.Brush("RestrictedBrush"),
        _ => Tokens.Brush("InkBrush")
    };

    public static Brush PlateEdge(BerthRowKind kind) => kind switch
    {
        BerthRowKind.Needs => Tokens.Brush("InkBrush", 0),
        BerthRowKind.Restricted => Tokens.Brush("RestrictedBrush"),
        _ => Tokens.Brush("HairlineStrongBrush")
    };

    public static Brush StateInk(BerthRowKind kind) => kind switch
    {
        BerthRowKind.Needs => Tokens.Brush("AlertInkBrush"),
        BerthRowKind.Restricted => Tokens.Brush("RestrictedBrush"),
        _ => Tokens.Brush("TextMutedBrush")
    };

    public static Brush NameInk(BerthRowKind kind) => Tokens.Brush(kind == BerthRowKind.Free ? "TextMutedBrush" : "InkBrush");

    public static Brush WhenInk(BerthRowKind kind) => Tokens.Brush(kind == BerthRowKind.Needs ? "AlertInkBrush" : "InkBrush");

    /// <summary>The detail's state and figure: alert ink for the vessel waiting on you, the restricted ink for a closed berth.</summary>
    public static Brush DetailInk(bool needsYou, bool restricted) =>
        Tokens.Brush(needsYou ? "AlertInkBrush" : restricted ? "RestrictedBrush" : "InkBrush");

    // xaml-lint: allow codebehind - the panels follow the store only while the page is on screen
    // (Live), like every other section; the page stays alive once visited
    private void Attach()
    {
        if (!_attached && ViewModel is { } vm)
        {
            _attached = true;
            this.RebuildWhenVisible(vm.State, vm.RefreshPanels);
            this.TickWhenVisible(vm.State, vm.RefreshPanels);
            this.RepaintWhenVisible(vm.State, vm.RepaintHover);
        }

        Bindings.Update();
    }
}
