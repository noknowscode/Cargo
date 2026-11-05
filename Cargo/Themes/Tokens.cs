using Windows.UI;

namespace Cargo.Themes;

/// <summary>
/// The palette for code, so view models and scenes can hand brushes to the view without any colour literal
/// appearing in code. A key is a palette role of the Simple theme (<c>OnSurfaceBrush</c>, <c>PrimaryColor</c>: the
/// palette is Styles/ColorPaletteOverride.xaml) or one of the app's no-role colours in App.xaml (<c>NavyColor</c>,
/// <c>HarbourStageColor</c>). Tokens resolve for the window's theme (<see cref="Theme"/>), and when that changes
/// (<see cref="Apply"/>) every brush handed out here is recoloured in place, so a view that holds one follows the
/// theme with no work of its own. Code that keeps a colour rather than a brush (Skia paints, blends, gradients)
/// re-reads on <see cref="ThemeChanged"/>, through <c>Controls/Live.RebuildOnTheme</c> for anything on screen.
/// </summary>
public static class Tokens
{
    private static readonly Dictionary<string, Color> ColorCache = new();
    private static readonly Dictionary<(string, int), SolidColorBrush> BrushCache = new();
    private static readonly Dictionary<(string, int), SolidColorBrush> TintCache = new();

    /// <summary>The theme tokens resolve for: Light or Dark, never Default. Set by <see cref="Apply"/>.</summary>
    public static ElementTheme Theme { get; private set; } = ElementTheme.Light;

    /// <summary>Counts theme changes, so a view that was off screen can tell it missed one.</summary>
    public static int Version { get; private set; }

    /// <summary>Raised after <see cref="Apply"/> has recoloured the brushes, for code that keeps colours.</summary>
    public static event EventHandler? ThemeChanged;

    /// <summary>
    /// Switches the palette to <paramref name="theme"/> (Light or Dark; Default is ignored): clears the
    /// resolved colours, recolours every brush and tint handed out, then raises <see cref="ThemeChanged"/>.
    /// </summary>
    public static void Apply(ElementTheme theme)
    {
        if (theme == ElementTheme.Default || theme == Theme)
        {
            return;
        }

        Theme = theme;
        Version++;
        ColorCache.Clear();

        foreach (var ((key, bucket), brush) in BrushCache)
        {
            brush.Color = Color(key);
            brush.Opacity = bucket / 1000d;
        }

        foreach (var ((key, bucket), brush) in TintCache)
        {
            brush.Color = WithAlpha(Color(key), bucket / 1000d);
        }

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>A token's colour for the current theme. A brush token carries its transparency in Opacity, folded into the alpha here.</summary>
    public static Color Color(string key)
    {
        if (!ColorCache.TryGetValue(key, out var value))
        {
            value = Resolve(key, Theme);
            ColorCache[key] = value;
        }

        return value;
    }

    /// <summary>
    /// A token's daylight (Light) value whatever the theme: for paint, which does not change with the
    /// theme (a container's livery in the scanner, on a stage that is dark in both themes).
    /// </summary>
    public static Color Paint(string key) => Resolve(key, ElementTheme.Light);

    /// <summary>A token's value for one theme whatever the window's: for a part of the page that sets its own
    /// RequestedTheme (the scanner's stage is Dark in both themes).</summary>
    public static Color Color(string key, ElementTheme theme) => Resolve(key, theme);

    private static readonly Dictionary<(string, int, ElementTheme), SolidColorBrush> PinnedCache = new();

    /// <summary>
    /// Brush for a token in one theme whatever the window's (see <see cref="Color(string, ElementTheme)"/>).
    /// Cached and shared; it does not follow a theme change, because the element it paints does not either.
    /// </summary>
    public static SolidColorBrush Brush(string key, double opacity, ElementTheme theme)
    {
        var bucket = (int)Math.Round(opacity * 1000);
        if (!PinnedCache.TryGetValue((key, bucket, theme), out var brush))
        {
            brush = new SolidColorBrush(Resolve(key, theme)) { Opacity = bucket / 1000d };
            PinnedCache[(key, bucket, theme)] = brush;
        }

        return brush;
    }

    /// <summary>Brush for a palette key, optionally at reduced opacity. Instances are cached, shared and recoloured on a theme change.</summary>
    public static SolidColorBrush Brush(string key, double opacity = 1d)
    {
        var bucket = (int)Math.Round(opacity * 1000);
        if (BrushCache.TryGetValue((key, bucket), out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(Color(key)) { Opacity = bucket / 1000d };
        BrushCache[(key, bucket)] = brush;
        return brush;
    }

    /// <summary>
    /// A token at partial alpha with the alpha in the colour itself (brush Opacity 1). Use it for
    /// anything a <see cref="Microsoft.UI.Xaml.BrushTransition"/> animates: on Uno Skia the transition
    /// interpolates Color only, so an Opacity-based brush fades to and from solid colour.
    /// Cached, shared and recoloured on a theme change like <see cref="Brush"/>.
    /// </summary>
    public static SolidColorBrush Tint(string key, double alpha)
    {
        var bucket = (int)Math.Round(alpha * 1000);
        if (TintCache.TryGetValue((key, bucket), out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(WithAlpha(Color(key), bucket / 1000d));
        TintCache[(key, bucket)] = brush;
        return brush;
    }

    private static readonly Dictionary<(uint, int), SolidColorBrush> AdHoc = new();

    /// <summary>
    /// Brush for a colour that was resolved from a token but then blended or shaded. It does not follow
    /// the theme: the code that blended it rebuilds on <see cref="ThemeChanged"/>.
    /// </summary>
    public static SolidColorBrush Of(Color color, double opacity = 1d)
    {
        var bucket = (int)Math.Round(opacity * 1000);
        var packed = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (AdHoc.TryGetValue((packed, bucket), out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(color) { Opacity = bucket / 1000d };
        AdHoc[(packed, bucket)] = brush;
        return brush;
    }

    public static Color Shade(Color source, int delta)
    {
        static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);
        return Windows.UI.Color.FromArgb(source.A, Clamp(source.R + delta), Clamp(source.G + delta), Clamp(source.B + delta));
    }

    /// <summary>A number from App.xaml's theme dictionaries for the current theme (e.g. <c>HarbourLightLevel</c>).</summary>
    public static double Number(string key, double fallback) => Find(key, Theme) is double d ? d : fallback;

    public static readonly SolidColorBrush Transparent = new(Microsoft.UI.Colors.Transparent);

    private static Color WithAlpha(Color c, double alpha) =>
        Windows.UI.Color.FromArgb((byte)Math.Round(c.A * alpha), c.R, c.G, c.B);

    // A key names a colour or a brush; a no-role colour that has no brush resource ("NavyBrush") resolves through its
    // colour ("NavyColor")
    private static Color Resolve(string key, ElementTheme theme) => (Find(key, theme) ?? Find(key + "Color", theme)
        ?? (key.EndsWith("Brush", StringComparison.Ordinal) ? Find(key[..^5] + "Color", theme) : null)) switch
    {
        Color c => c,
        // A brush token carries its transparency in Opacity (the hairlines are ink at 4.5-14%); fold it
        // into the alpha, or every code-set hairline renders solid ink.
        SolidColorBrush b => WithAlpha(b.Color, b.Opacity),
        _ => default
    };

    /// <summary>
    /// Looks a key up for an explicit theme. Code has no {ThemeResource}, and a plain Application
    /// resource lookup answers with the application's theme, not the window's, so this walks the
    /// merged dictionaries' theme dictionaries itself, the theme's generated palette and role brushes
    /// included (the Dark palette is keyed "Default", the WinUI convention), then falls back to the
    /// invariant resources.
    /// </summary>
    private static object? Find(string key, ElementTheme theme)
    {
        if (Application.Current?.Resources is not { } resources)
        {
            return null;
        }

        string[] themeKeys = theme == ElementTheme.Dark ? ["Dark", "Default"] : ["Light"];
        return FindThemed(resources, key, themeKeys) ?? (resources.TryGetValue(key, out var direct) ? direct : null);
    }

    /// <summary>
    /// Depth-first through merged dictionaries, last first: a later dictionary wins in WinUI. Each dictionary answers
    /// with its own theme dictionary for the theme ("Dark", else "Default") before the dictionaries it merges.
    /// </summary>
    private static object? FindThemed(ResourceDictionary dictionary, string key, string[] themeKeys)
    {
        foreach (var themeKey in themeKeys)
        {
            if (dictionary.ThemeDictionaries.TryGetValue(themeKey, out var t) && t is ResourceDictionary themed
                && themed.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        for (var i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            if (FindThemed(dictionary.MergedDictionaries[i], key, themeKeys) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
