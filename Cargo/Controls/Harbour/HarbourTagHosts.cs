using SkiaSharp;
using Windows.Foundation;

namespace Cargo.Controls.Harbour;

/// <summary>
/// What the frame leaves the tags this frame: the view's width and visible height, how far its top sits into the
/// rendered scene (<see cref="Offset"/>) and reaches up behind the overlay (<see cref="Inset"/>), and whether it is a
/// short frame (<see cref="Tight"/>) or a section's strip (<see cref="Compact"/>).
/// </summary>
public readonly record struct HarbourTagFrame(double Width, double Visible, double Offset, double Inset, bool Tight, bool Compact)
{
    /// <summary>A mast projected here is in frame; a vessel out of frame loses its tag rather than pinning it to an edge it is not near.</summary>
    public bool Holds(double x, double y) =>
        x > 0 && x < Width && y - Offset > Inset + (Tight ? 16 : 40) && y - Offset < Visible - (Tight ? 8 : 56);

    /// <summary>
    /// The frame's tag layout: the strip keeps its top-right corner clear for the Expand harbour button; tags stay below
    /// the overlay (the greeting and figures), clear of the top-right float, and above the HUD.
    /// </summary>
    public TagLayout Layout() =>
        new(Width - (Compact ? 150 : 10), (Tight ? 6 : 52) + Inset + Offset, Visible + Offset - (Tight ? 8 : 56));
}

/// <summary>
/// The data tags riding the vessels: a host per tag on the stage's tag layer, moved each frame to the spot
/// <see cref="TagLayout"/> gives it over the projection, with a leader from its mast. What the tags say is the view
/// model's (<see cref="HarbourViewModel"/>).
/// </summary>
public sealed class HarbourTagHosts(Canvas layer, DataTemplate template)
{
    private readonly Dictionary<HarbourTag, ContentControl> _hosts = new();

    /// <summary>A host per tag, replacing any built before.</summary>
    public void Build(IEnumerable<HarbourTag> tags)
    {
        foreach (var host in _hosts.Values)
        {
            layer.Children.Remove(host);
        }

        _hosts.Clear();
        foreach (var tag in tags)
        {
            var host = new ContentControl { Content = tag, ContentTemplate = template, IsTabStop = false, RenderTransform = new TranslateTransform() };
            layer.Children.Add(host);
            _hosts[tag] = host;
        }
    }

    /// <summary>
    /// Places the tags for one frame, in <paramref name="order"/>, each over its mast as <paramref name="project"/> puts
    /// it on screen; a tag out of frame or with no clear spot is hidden. Returns each placed tag's leader, mast to tag.
    /// </summary>
    // xaml-lint: allow responsive - the tags' own measured sizes, used to place them on their leaders; no breakpoint
    public Dictionary<string, (SKPoint, SKPoint)> Place(IEnumerable<HarbourTag> order, Func<string, (float X, float Y)> project,
        HarbourTagFrame frame)
    {
        var leaders = new Dictionary<string, (SKPoint, SKPoint)>();
        var layout = frame.Layout();
        foreach (var tag in order)
        {
            if (!_hosts.TryGetValue(tag, out var host))
            {
                continue;
            }

            var (x, y) = project(tag.Id);
            var size = new Size(host.ActualWidth > 0 ? host.ActualWidth : 160, host.ActualHeight > 0 ? host.ActualHeight : 36);
            var spot = frame.Holds(x, y) ? layout.Place(x, y, size.Width, size.Height) : null;
            // xaml-lint: allow codebehind - per-frame tag placement over the camera; there is no XAML surface for it
            host.Visibility = spot is null ? Visibility.Collapsed : Visibility.Visible;
            if (spot is not { } placed)
            {
                continue;
            }

            var transform = (TranslateTransform)host.RenderTransform;
            transform.X = Math.Round(placed.X);
            transform.Y = Math.Round(placed.Y);
            leaders[tag.Id] = (new SKPoint(x, y), new SKPoint((float)(placed.X + placed.Width / 2), (float)(placed.Y + placed.Height)));
        }

        return leaders;
    }
}
