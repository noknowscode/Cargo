using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class FleetView : Page
{
    public FleetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public FleetViewModel? ViewModel => DataContext as FleetViewModel;

    /// <summary>The vessel the crew channel is open to sits on a sunk ground.</summary>
    public static Brush RowGround(bool current) => current ? Tokens.Brush("SurfaceSunkAltBrush") : Tokens.Transparent;

    // The crew's messages sit on the left in a sunk bubble; dispatch's on the right in ink

    public static HorizontalAlignment BubbleSide(bool fromCrew) => fromCrew ? HorizontalAlignment.Left : HorizontalAlignment.Right;

    public static Brush BubbleGround(bool fromCrew) => Tokens.Brush(fromCrew ? "SurfaceSunkAltBrush" : "InkBrush");

    public static Brush BubbleInk(bool fromCrew) => Tokens.Brush(fromCrew ? "InkBrush" : "PaperBrush");

    // xaml-lint: allow codebehind - the river map is a code-built scene that takes the store directly
    private void Attach()
    {
        if (ViewModel is not { } vm || RiverHost.Content is not null)
        {
            return;
        }

        RiverHost.Content = new RiverMap(vm.State);
        Bindings.Update();
    }
}
