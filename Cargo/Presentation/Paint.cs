using Cargo.Controls;

namespace Cargo.Presentation;

/// <summary>
/// The views' door to the palette, for x:Bind functions. View models hold states (current, late, warned) and the
/// tones the data names (a palette key such as a vessel's accent, a cargo class or a notice's status); the views turn
/// them into the theme's brushes and geometry here or in their own mapping functions. Every brush comes from
/// <see cref="Tokens"/>, so a brush handed out follows a theme change in place.
/// </summary>
public static class Paint
{
    /// <summary>A data tone (a palette key) as its brush.</summary>
    public static Brush Tone(string key) => Tokens.Brush(key);

    /// <summary>A legend swatch: its tone at its strength.</summary>
    public static Brush Swatch(string key, double strength) => Tokens.Brush(key, strength);

    /// <summary>A data tone as text ink: ink when the data names none.</summary>
    public static Brush Ink(string? key) => Tokens.Brush(key ?? "InkBrush");

    /// <summary>
    /// The text beside a status fill: the darker ink variant of the fill's colour (the status fills are too light to
    /// hold 4.5:1 as text).
    /// </summary>
    public static Brush TextInk(string fill) => Tokens.Brush(fill switch
    {
        var t when t.StartsWith("Orange", StringComparison.Ordinal) => "OrangeInkBrush",
        var t when t.StartsWith("Amber", StringComparison.Ordinal) => "AmberInkBrush",
        var t when t.StartsWith("SeaGreen", StringComparison.Ordinal) => "SeaGreenInkBrush",
        var t when t.StartsWith("Accent", StringComparison.Ordinal) => "AccentDarkBrush",
        var t when t.StartsWith("Alert", StringComparison.Ordinal) => "AlertInkBrush",
        _ => "InkBrush"
    });

    /// <summary>A status as its fill (a dot, a swatch).</summary>
    public static Brush StatusFill(Standing standing) => Tokens.Brush(standing switch
    {
        Standing.Done => "TextFaintBrush",
        Standing.Late => "OrangeBrush",
        Standing.Blocked => "AlertBrush",
        _ => "SeaGreenBrush"
    });

    /// <summary>A status written out, in the ink variant of its fill.</summary>
    public static Brush StatusInk(Standing standing) => Tokens.Brush(standing switch
    {
        Standing.Done => "TextMutedBrush",
        Standing.Late => "OrangeInkBrush",
        Standing.Blocked => "AlertBrush",
        _ => "SeaGreenInkBrush"
    });

    /// <summary>A trace through points, as geometry.</summary>
    public static Geometry Polyline(IReadOnlyList<Windows.Foundation.Point> points) => Geo.Polyline(points.ToList());

    /// <summary>A closed shape through points (the area under a trace), as geometry.</summary>
    public static Geometry Polygon(IReadOnlyList<Windows.Foundation.Point> points) => Geo.Polyline(points.ToList(), close: true);

    /// <summary>The inverse of x:Bind's bool to Visibility: collapsed while the state holds.</summary>
    public static Visibility Unless(bool state) => state ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>A sprite from Assets/Sprites by name; none for an empty name.</summary>
    public static ImageSource? Sprite(string name) => string.IsNullOrEmpty(name) ? null : Sprites.Source(name);

    /// <summary>An icon's SVG path data as geometry (the design's 24-unit stroke icons).</summary>
    public static Geometry Icon(string data) => Geo.Path(data);

    /// <summary>Ink on the current item, muted on the rest (header tabs, in-page tabs).</summary>
    public static Brush CurrentInk(bool current) => Tokens.Brush(current ? "InkBrush" : "TextMutedBrush");

    /// <summary>A tab's underline or a selection mark: shown on the current item only.</summary>
    public static double Shown(bool current) => current ? 1 : 0;

    /// <summary>A card's edge: ink when it is the one selected, a hairline otherwise.</summary>
    public static Brush SelectedEdge(bool selected) => Tokens.Brush(selected ? "InkBrush" : "HairlineBrush");

    /// <summary>A status fill: orange when it runs late or warns, sea green when on time.</summary>
    public static Brush LateFill(bool late) => Tokens.Brush(late ? "OrangeBrush" : "SeaGreenBrush");

    /// <summary>The ink beside a <see cref="LateFill"/> (the fills are too light for text).</summary>
    public static Brush LateInk(bool late) => Tokens.Brush(late ? "OrangeInkBrush" : "SeaGreenInkBrush");
}

/// <summary>Where something stands against its plan, for the views' status colours: done, on time, late, blocked.</summary>
public enum Standing { OnTime, Done, Late, Blocked }
