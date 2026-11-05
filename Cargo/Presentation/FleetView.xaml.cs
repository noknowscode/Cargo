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
    public static Brush RowGround(bool current) => current ? Tokens.Brush("SurfaceVariantBrush") : Tokens.Transparent;

    // The crew's messages sit on the left in a sunk bubble; dispatch's on the right in ink

    public static HorizontalAlignment BubbleSide(bool fromCrew) => fromCrew ? HorizontalAlignment.Left : HorizontalAlignment.Right;

    public static Brush BubbleGround(bool fromCrew) => Tokens.Brush(fromCrew ? "SurfaceVariantBrush" : "SurfaceInverseBrush");

    public static Brush BubbleInk(bool fromCrew) => Tokens.Brush(fromCrew ? "OnSurfaceBrush" : "OnSurfaceInverseBrush");

    // xaml-lint: allow codebehind - the river map is a code-built scene that takes the store directly
    private void Attach()
    {
        if (ViewModel is not { } vm || RiverHost.Content is not null)
        {
            return;
        }

        RiverHost.Content = new RiverMap(vm.State);
        vm.Thread.CollectionChanged += (_, _) => ShowNewest();
        Bindings.Update();
    }

    // xaml-lint: allow codebehind - a sent message lands below the 220 px thread's fold; bringing the newest into view
    // has no XAML surface
    private void ShowNewest() =>
        DispatcherQueue.TryEnqueue(() => ThreadScroll.ChangeView(null, ThreadScroll.ScrollableHeight, null, Motion.Reduced));
}
