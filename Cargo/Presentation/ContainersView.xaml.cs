using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class ContainersView : UserControl
{
    private bool _attached;

    public ContainersView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public ContainersViewModel? ViewModel => DataContext as ContainersViewModel;

    // The model says what each chip, card and journey step is; the theme's brushes for each are picked here

    /// <summary>The filter on now fills with ink; the rest are surface pills with a strong hairline.</summary>
    public static Brush ChipGround(bool current) => Tokens.Brush(current ? "SurfaceInverseBrush" : "SurfaceBrush");

    public static Brush ChipEdge(bool current) => Tokens.Brush(current ? "SurfaceInverseBrush" : "OutlineBrush");

    public static Brush ChipInk(bool current) => Tokens.Brush(current ? "OnSurfaceInverseBrush" : "OnSurfaceBrush");

    /// <summary>The card's state dot: orange on a box that needs attention, the accent otherwise.</summary>
    public static Brush StateFill(bool warn) => Tokens.Brush(warn ? "OrangeColor" : "PrimaryColor");

    /// <summary>The state written out: the ink variant of its tone, which holds 4.5:1 as text.</summary>
    public static Brush StateInk(bool warn) => Tokens.Brush(warn ? "OrangeInkBrush" : "PrimaryBrush");

    /// <summary>A step's ring: ink where the box is (orange when warned), accent behind it, faint ahead.</summary>
    public static Brush StepEdge(StepState state) => state switch
    {
        StepState.Warned => Tokens.Brush("OrangeBrush"),
        StepState.Current => Tokens.Brush("OnSurfaceBrush"),
        StepState.Done => Tokens.Brush("PrimaryBrush"),
        _ => Tokens.Brush("OnSurfaceColor", 0.18)
    };

    /// <summary>The disc: filled with its ring's colour where the box is, an accent tint behind it.</summary>
    public static Brush StepGround(StepState state) => state switch
    {
        StepState.Warned or StepState.Current => StepEdge(state),
        StepState.Done => Tokens.Brush("PrimaryColor", 0.1),
        _ => Tokens.Brush("SurfaceBrush")
    };

    public static Brush StepIconInk(StepState state) => state switch
    {
        StepState.Warned or StepState.Current => Tokens.Brush("SurfaceBrush"),
        StepState.Done => Tokens.Brush("PrimaryBrush"),
        _ => Tokens.Brush("OnSurfaceVariantBrush")
    };

    public static Brush StepInk(StepState state) => state switch
    {
        StepState.Warned or StepState.Current => Tokens.Brush("OnSurfaceBrush"),
        StepState.Done => Tokens.Brush("PrimaryBrush"),
        _ => Tokens.Brush("OnSurfaceVariantBrush")
    };

    /// <summary>The line on to the next step: accent once the box has passed, faint ahead, none after the last.</summary>
    public static Brush StepLine(StepState state, bool last) =>
        last ? Tokens.Transparent : state == StepState.Done ? Tokens.Brush("PrimaryBrush") : Tokens.Brush("OnSurfaceColor", 0.12);

    // xaml-lint: allow codebehind - the model's refresh runs only while the explorer is on screen
    private void Attach()
    {
        if (!_attached && ViewModel is { } vm)
        {
            _attached = true;
            this.RebuildWhenVisible(vm.State, vm.Refresh);
        }

        Bindings.Update();
    }
}
