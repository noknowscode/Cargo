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

    public static Brush ConflictInk(bool conflicts) => Tokens.Brush(conflicts ? "ErrorBrush" : "OnSurfaceBrush");

    /// <summary>
    /// The row's ground: the needs row keeps its alert tint, a linked hover the hover ground. Alpha in the colour, not
    /// the brush: the ground fades through a BrushTransition.
    /// </summary>
    public static Brush RowGround(BerthRowKind kind, bool linked) =>
        kind == BerthRowKind.Needs ? Tokens.Tint("ErrorBrush", .05) : Tokens.Tint("OnSurfaceBrush", linked ? .06 : 0);

    /// <summary>Ink round the row being worked; the needs row takes a thin alert edge on a linked hover instead.</summary>
    public static Brush RowOutline(BerthRowKind kind, bool current, bool linked) =>
        current ? Tokens.Brush("OnSurfaceBrush") : linked && kind == BerthRowKind.Needs ? Tokens.Brush("ErrorBrush") : Tokens.Brush("OnSurfaceBrush", 0);

    /// <summary>The needs plate is the count badge: OnError on Error (5.4:1).</summary>
    public static Brush PlateGround(BerthRowKind kind) => Tokens.Brush(kind == BerthRowKind.Needs ? "ErrorBrush" : "SurfaceVariantBrush");

    public static Brush PlateInk(BerthRowKind kind) => kind switch
    {
        BerthRowKind.Needs => Tokens.Brush("OnErrorBrush"),
        BerthRowKind.Restricted => Tokens.Brush("ErrorBrush"),
        _ => Tokens.Brush("OnSurfaceBrush")
    };

    public static Brush PlateEdge(BerthRowKind kind) => kind switch
    {
        BerthRowKind.Needs => Tokens.Brush("OnSurfaceBrush", 0),
        BerthRowKind.Restricted => Tokens.Brush("ErrorBrush"),
        _ => Tokens.Brush("OutlineBrush")
    };

    public static Brush StateInk(BerthRowKind kind) => kind switch
    {
        BerthRowKind.Needs => Tokens.Brush("ErrorBrush"),
        BerthRowKind.Restricted => Tokens.Brush("ErrorBrush"),
        _ => Tokens.Brush("OnSurfaceVariantBrush")
    };

    public static Brush NameInk(BerthRowKind kind) => Tokens.Brush(kind == BerthRowKind.Free ? "OnSurfaceVariantBrush" : "OnSurfaceBrush");

    public static Brush WhenInk(BerthRowKind kind) => Tokens.Brush(kind == BerthRowKind.Needs ? "ErrorBrush" : "OnSurfaceBrush");

    /// <summary>The detail's state and figure: alert ink for the vessel waiting on you, the restricted ink for a closed berth.</summary>
    public static Brush DetailInk(bool needsYou, bool restricted) =>
        Tokens.Brush(needsYou ? "ErrorBrush" : restricted ? "ErrorBrush" : "OnSurfaceBrush");

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
