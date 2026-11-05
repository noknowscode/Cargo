using Windows.Foundation;

namespace Cargo.Controls;

/// <summary>
/// Auto-filling tile grid: as many equal columns as fit at <see cref="MinTileWidth"/>. Both properties are
/// dependency properties, so a page can give them {utu:Responsive} values.
/// </summary>
public sealed partial class TileGridPanel : Panel
{
    public static readonly DependencyProperty MinTileWidthProperty = DependencyProperty.Register(
        nameof(MinTileWidth), typeof(double), typeof(TileGridPanel),
        new PropertyMetadata(240d, (d, _) => ((TileGridPanel)d).InvalidateMeasure()));

    public double MinTileWidth
    {
        get => (double)GetValue(MinTileWidthProperty);
        set => SetValue(MinTileWidthProperty, value);
    }

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(TileGridPanel),
        new PropertyMetadata(14d, (d, _) => ((TileGridPanel)d).InvalidateMeasure()));

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    private double _cellWidth;
    private int _columns = 1;
    private readonly List<double> _rowHeights = new();

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinTileWidth : availableSize.Width;
        _columns = LayoutMath.TileColumns(width, MinTileWidth, Gap);
        _cellWidth = (width - Gap * (_columns - 1)) / _columns;

        _rowHeights.Clear();
        var rowHeight = 0d;

        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(new Size(_cellWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);

            if (i % _columns == _columns - 1 || i == Children.Count - 1)
            {
                _rowHeights.Add(rowHeight);
                rowHeight = 0;
            }
        }

        var height = _rowHeights.Sum() + Math.Max(0, _rowHeights.Count - 1) * Gap;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0d;

        for (var i = 0; i < Children.Count; i++)
        {
            var row = i / _columns;
            var column = i % _columns;

            if (column == 0 && row > 0)
            {
                y += _rowHeights[row - 1] + Gap;
            }

            Children[i].Arrange(new Rect(column * (_cellWidth + Gap), y, _cellWidth, _rowHeights[row]));
        }

        return finalSize;
    }
}

/// <summary>Equal-width columns in a single row — the container journey tracker.</summary>
public sealed partial class EvenRowPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
        {
            return new Size(0, 0);
        }

        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        var cell = width / Children.Count;
        var height = 0d;

        foreach (var child in Children)
        {
            child.Measure(new Size(cell, double.PositiveInfinity));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0)
        {
            return finalSize;
        }

        var cell = finalSize.Width / Children.Count;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(new Rect(i * cell, 0, cell, finalSize.Height));
        }

        return finalSize;
    }
}
