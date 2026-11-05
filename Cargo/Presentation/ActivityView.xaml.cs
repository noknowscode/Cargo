using Cargo.Controls;

namespace Cargo.Presentation;

public sealed partial class ActivityView : UserControl
{
    public ActivityView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public ActivityViewModel? ViewModel => DataContext as ActivityViewModel;

    public static TextWrapping Wrap(bool open) => open ? TextWrapping.Wrap : TextWrapping.NoWrap;

    /// <summary>An entry about an open decision wears the alert ink; the rest are muted.</summary>
    public static Brush Tone(bool needsAction) => Tokens.Brush(needsAction ? "ErrorBrush" : "OnSurfaceVariantBrush");

    /// <summary>The entry's icon: the Needs-you queue's for the decision it is about, else its own kind's.</summary>
    public static Geometry Icon(string data, Decision? about) =>
        (about is null ? null : QueueIcon(about.Kind)) ?? Geo.Path(data);

    /// <summary>
    /// The Needs-you queue's icon for the kind, read from its style in Themes/Icons.xaml. Uno keeps
    /// a Path.Data setter's value as the path string until the style is applied, so both forms count.
    /// </summary>
    private static Geometry? QueueIcon(DecisionKind kind) =>
        (Application.Current.Resources[NeedsYouPanel.IconKey(kind)] as Style)?.Setters
            .OfType<Setter>().FirstOrDefault(s => s.Property == Microsoft.UI.Xaml.Shapes.Path.DataProperty)?.Value switch
        {
            Geometry geometry => geometry,
            string data => Geo.Path(data),
            _ => null
        };
}
