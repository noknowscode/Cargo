namespace Cargo.Presentation;

/// <summary>
/// The full-window container inspection. It lives in the shell rather than inside the
/// Cargo page so it covers the whole app the way the design's fixed overlay does.
/// </summary>
public sealed partial class ScannerOverlay : UserControl
{
    public ScannerOverlay()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            // xaml-lint: allow codebehind - the 3D scanner is a code-built panel that reads the store directly
            if (ViewModel is { } vm)
            {
                Scanner3D.State = vm.State;
            }

            Bindings.Update();
        };

        // xaml-lint: allow codebehind - keyboard focus has no binding: an opened inspection takes it, so Tab does not
        // wander into the dimmed page behind
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) =>
        {
            if (Visibility == Visibility.Visible)
            {
                DispatcherQueue.TryEnqueue(() => CloseButton.Focus(FocusState.Programmatic));
            }
        });
    }

    public static readonly DependencyProperty IsShortLandscapeProperty = DependencyProperty.Register(
        nameof(IsShortLandscape), typeof(bool), typeof(ScannerOverlay), new PropertyMetadata(false));

    /// <summary>
    /// The window is 600 to 999 wide (where the result sits under the stage) but under 560 high, a phone on its side:
    /// the stage cannot sit over the scan result, so the Short state puts the result beside it. The size classes are
    /// widths, so the height is read here. From 1000 the result is beside the stage already.
    /// </summary>
    public bool IsShortLandscape
    {
        get => (bool)GetValue(IsShortLandscapeProperty);
        private set => SetValue(IsShortLandscapeProperty, value);
    }

    /// <summary>Below this height a stacked scanner leaves the 3D stage no room.</summary>
    public const double ShortHeight = 560;

    // xaml-lint: allow responsive - the window's height decides the Short state; the size classes are widths only
    // xaml-lint: allow codebehind - SizeChanged has no command surface; it only sets the state's trigger
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        IsShortLandscape = e.NewSize.Height < ShortHeight && e.NewSize.Width is >= 600 and < 1000;

    // xaml-lint: allow codebehind - Escape closes the inspection, as its × does
    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && ViewModel?.CloseCommand.CanExecute(null) == true)
        {
            ViewModel.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    public ScannerViewModel? ViewModel => DataContext as ScannerViewModel;

    // The scanner's stage is dark in both themes, so its chrome takes the invariant on-dark tokens

    public static Brush XrayGround(bool xray) => xray ? Tokens.Brush("AccentBrightColor", 0.2) : Tokens.Transparent;

    public static Brush XrayEdge(bool xray) => Tokens.Brush(xray ? "AccentBrightInvariantBrush" : "HairlineOnDarkBrush");

    public static Brush XrayInk(bool xray) => Tokens.Brush(xray ? "AccentPaleInvariantBrush" : "TextOnDarkBrush");

    /// <summary>The preset the box is turned to is lit.</summary>
    public static Brush PresetGround(bool current) => current ? Tokens.Brush("DeckWhiteColor", 0.16) : Tokens.Transparent;

    public static Brush FlagInk(bool flagged) => Tokens.Brush(flagged ? "ScanAnomalyColor" : "SeaGreenColor");
}
