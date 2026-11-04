namespace Cargo.Presentation;

public sealed partial class SecurityView : Page
{
    public SecurityView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public SecurityViewModel? ViewModel => DataContext as SecurityViewModel;

    /// <summary>The tab shown is raised on the surface.</summary>
    public static Brush TabGround(bool current) => current ? Tokens.Brush("SurfaceBrush") : Tokens.Transparent;

    /// <summary>A container's livery is paint: the same in both themes.</summary>
    public static Windows.UI.Color Livery(string token) => Tokens.Paint(token);
}
