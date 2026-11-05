namespace Cargo.Presentation;

/// <summary>
/// The Needs-you queue, over its view model (<see cref="NeedsYouPanelViewModel"/>). The row visuals that depend on a
/// decision (icon, button, late ink) are picked by the static functions below through x:Bind, so the decision itself
/// carries no brushes.
/// </summary>
public sealed partial class NeedsYouPanel : UserControl
{
    private NeedsYouPanelViewModel? _model;

    public NeedsYouPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    public NeedsYouPanelViewModel? ViewModel => DataContext as NeedsYouPanelViewModel;

    private void Attach()
    {
        if (_model is not null)
        {
            _model.RevealRequested -= OnRevealRequested;
        }

        _model = ViewModel;
        if (_model is not null)
        {
            _model.RevealRequested += OnRevealRequested;
        }

        Bindings.Update();
    }

    // xaml-lint: allow codebehind - "Needs you" in the masthead scrolls this section into view and
    // focuses its heading; bringing an element into view has no XAML surface
    private void OnRevealRequested(object? sender, EventArgs e)
    {
        Section.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = !Motion.Reduced, VerticalOffset = 24 });
        Heading.Focus(FocusState.Programmatic);
    }

    public static Brush Ground(int count) => count > 0 ? Tokens.Brush("ErrorBrush", .05) : Tokens.Transparent;

    public static Visibility Shown(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Empty(int count) => count > 0 ? Visibility.Collapsed : Visibility.Visible;

    public static string Count(int count) => count.ToString();

    public static string Spoken(int count) => count switch
    {
        0 => "Needs you, nothing open",
        1 => "Needs you, 1 item",
        _ => $"Needs you, {count} items"
    };

    public static Style Icon(DecisionKind kind) => (Style)Application.Current.Resources[IconKey(kind)];

    /// <summary>The icon style for a decision kind in Themes/Icons.xaml; the shift log uses it too.</summary>
    public static string IconKey(DecisionKind kind) => kind switch
    {
        DecisionKind.InspectionHold => "LockIcon",
        DecisionKind.DriverCheck => "IdCardIcon",
        DecisionKind.LateOrders => "ClockIcon",
        _ => "AnchorIcon"
    };

    public static Style Action(bool primary) =>
        (Style)Application.Current.Resources[primary ? "QueuePrimaryButton" : "QueueSecondaryButton"];

    /// <summary>A row's refusal shows only once there is one.</summary>
    public static Visibility Said(string text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The " · " before the live part, only when there is one.</summary>
    public static string Separator(string when) => string.IsNullOrEmpty(when) ? string.Empty : " · ";

    public static Brush WhenInk(bool late) => Tokens.Brush(late ? "ErrorBrush" : "OnSurfaceVariantBrush");

    public static FontFamily WhenFace(bool late) =>
        (FontFamily)Application.Current.Resources[late ? "BodyStrongFont" : "BodyFont"];
}
