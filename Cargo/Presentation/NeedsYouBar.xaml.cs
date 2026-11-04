namespace Cargo.Presentation;

/// <summary>The needs-you bar; Berths shows it in the masthead, Overview in its context panel.</summary>
public sealed partial class NeedsYouBar : UserControl
{
    public NeedsYouBar()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public NeedsYouViewModel? ViewModel => DataContext as NeedsYouViewModel;

    // The model says which state the bar is in; the theme's brushes for each are picked here

    /// <summary>Amber while a berth waits on you, the surface once nothing does.</summary>
    public static Brush Ground(bool done) => done ? Tokens.Brush("SurfaceBrush") : Tokens.Brush("AlertBrush", .05);

    public static Brush Edge(bool done) => done ? Tokens.Brush("HairlineStrongBrush") : Tokens.Brush("InkBrush", 0);

    /// <summary>A refused confirm reads in alert ink.</summary>
    public static Brush MetaInk(bool refused) => Tokens.Brush(refused ? "AlertInkBrush" : "TextMutedBrush");
}
