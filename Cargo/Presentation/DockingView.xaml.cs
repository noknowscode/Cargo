using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class DockingView : UserControl
{
    private bool _attached;

    public DockingView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public DockingViewModel? ViewModel => DataContext as DockingViewModel;

    // The model says what each element is; the theme's brushes and the artwork for it are picked here

    /// <summary>Now is lit (ink on paper) while the plan follows the live clock.</summary>
    public static Brush NowGround(bool live) => Tokens.Brush(live ? "SurfaceInverseBrush" : "SurfaceBrush");

    public static Brush NowInk(bool live) => Tokens.Brush(live ? "OnSurfaceInverseBrush" : "OnSurfaceBrush");

    public static Brush ConflictInk(bool conflicts) => Tokens.Brush(conflicts ? "ErrorBrush" : "SeaGreenInkBrush");

    public static ImageSource? Profile(string sprite) => string.IsNullOrEmpty(sprite) ? null : Sprites.Source(sprite);

    /// <summary>A berth that takes her has an accent edge and caption; the rest stay quiet and say why in words.</summary>
    public static Brush ChoiceEdge(bool fits) => Tokens.Brush(fits ? "PrimaryBrush" : "OutlineBrush");

    public static Brush ChoiceInk(bool fits) => Tokens.Brush(fits ? "PrimaryBrush" : "OnSurfaceVariantBrush");

    // xaml-lint: allow codebehind - the berth map is a code-built drag scene over the store; its hint
    // feeds the model, and the model's refreshes run only while the plan is on screen
    private void Attach()
    {
        if (!_attached && ViewModel is { } vm)
        {
            _attached = true;
            var map = new BerthMap(vm.State);
            map.HintChanged += (_, message) => vm.Hint = message;
            MapHost.Content = map;
            this.RebuildWhenVisible(vm.State, vm.Refresh);
            this.RebuildOnPlanHour(vm.State, vm.Scrubbed);
            this.TickWhenVisible(vm.State, vm.Tick);

            // Below 1000 px the vessel's panel follows the timeline: bring it on screen when a hull is picked
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DockingViewModel.HasSelection) && vm.HasSelection && Grid.GetRow(SelectedPanel) > 0)
                {
                    SelectedPanel.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = !Motion.Reduced });
                }
            };
        }

        Bindings.Update();
    }
}
