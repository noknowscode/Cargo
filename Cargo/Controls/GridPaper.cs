using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Cargo.Controls;

/// <summary>
/// The faint 48 px survey grid behind the container scanner's inspection stage, plus a teal
/// wash in the top-left corner. Drawn as lines rather than a tiled bitmap so it stays crisp at any DPI,
/// and as one Path so a window resize rebuilds a geometry, not a hundred elements.
/// </summary>
public sealed partial class GridPaper : UserControl
{
    private const double Pitch = 48;

    private readonly Path _lines = new() { StrokeThickness = 1 };

    public GridPaper()
    {
        IsHitTestVisible = false;
        // The scanner stage is the Dark theme in both themes: its grid is the Dark page ink, its wash the accent's paint
        _lines.Stroke = Tokens.Brush("BackgroundColor", 0.045, ElementTheme.Dark);

        var glow = new Rectangle
        {
            Fill = new RadialGradientBrush
            {
                Center = new Windows.Foundation.Point(0.2, 0),
                RadiusX = 0.7,
                RadiusY = 0.9,
                GradientStops =
                {
                    new GradientStop { Offset = 0, Color = Tokens.Paint("PrimaryColor") },
                    new GradientStop { Offset = 1, Color = Microsoft.UI.Colors.Transparent }
                }
            },
            Opacity = 0.10
        };

        Content = new Grid { Children = { glow, _lines } };
        SizeChanged += (_, e) => Rebuild(e.NewSize.Width, e.NewSize.Height);
    }

    // One open figure per line. LineGeometry would read better, but Uno reports its points as not
    // implemented on Android, iOS and WebAssembly (Uno0001); PathGeometry is implemented on every head.
    private void Rebuild(double width, double height)
    {
        var grid = new PathGeometry();
        for (var x = Pitch; x < width; x += Pitch)
        {
            grid.Figures.Add(Line(new(x, 0), new(x, height)));
        }

        for (var y = Pitch; y < height; y += Pitch)
        {
            grid.Figures.Add(Line(new(0, y), new(width, y)));
        }

        _lines.Data = grid;
    }

    private static PathFigure Line(Windows.Foundation.Point from, Windows.Foundation.Point to) =>
        new() { StartPoint = from, Segments = { new LineSegment { Point = to } } };
}
