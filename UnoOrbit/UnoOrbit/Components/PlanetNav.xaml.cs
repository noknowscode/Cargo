using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using UnoOrbit.Helpers;
using UnoOrbit.Models;
using UnoOrbit.Rendering;

namespace UnoOrbit.Components;

public sealed partial class PlanetNav : UserControl
{
    public event EventHandler<int>? PlanetSelected;
    private readonly Ellipse[] _dots = new Ellipse[8];
    private readonly ScaleTransform[] _dotScales = new ScaleTransform[8];
    private int? _currentActive;

    public PlanetNav()
    {
        this.InitializeComponent();
        CreateDots();
    }

    private void CreateDots()
    {
        for (int i = 0; i < Planets.All.Length; i++)
        {
            var planet = Planets.All[i];
            var color = ColorUtil.FromHex(planet.ColorHex);
            var winColor = Windows.UI.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);

            var scale = new ScaleTransform { ScaleX = 1, ScaleY = 1, CenterX = 4, CenterY = 4 };
            _dotScales[i] = scale;

            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                StrokeThickness = 1.5,
                Stroke = new SolidColorBrush(winColor),
                Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                RenderTransform = scale,
            };

            int index = i;
            dot.PointerPressed += (s, e) =>
            {
                e.Handled = true;
                PlanetSelected?.Invoke(this, index);
            };
            dot.PointerEntered += (s, e) =>
            {
                AnimateDotScale(index, 1.3, 150);
            };
            dot.PointerExited += (s, e) =>
            {
                bool isActive = _currentActive == index;
                AnimateDotScale(index, isActive ? 1.4 : 1.0, 200);
            };

            _dots[i] = dot;
            DotsPanel.Children.Add(dot);
        }
    }

    public void UpdateActivePlanet(int? selectedIndex)
    {
        _currentActive = selectedIndex;
        for (int i = 0; i < _dots.Length; i++)
        {
            UpdateDotState(i, selectedIndex);
        }
    }

    private void UpdateDotState(int index, int? selectedIndex = null)
    {
        var dot = _dots[index];
        var planet = Planets.All[index];
        var color = ColorUtil.FromHex(planet.ColorHex);
        var winColor = Windows.UI.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
        bool isActive = selectedIndex == index;

        dot.Fill = isActive
            ? new SolidColorBrush(winColor)
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        dot.Stroke = isActive
            ? new SolidColorBrush(Microsoft.UI.Colors.White)
            : new SolidColorBrush(winColor);

        // Animate scale transition instead of snapping
        AnimateDotScale(index, isActive ? 1.4 : 1.0, 250);
    }

    private void AnimateDotScale(int index, double targetScale, double durationMs)
    {
        var transform = _dotScales[index];
        Easings.CreateSlideAnimation(transform, "ScaleX", targetScale, durationMs, Easings.SpringApproxSpline).Begin();
        Easings.CreateSlideAnimation(transform, "ScaleY", targetScale, durationMs, Easings.SpringApproxSpline).Begin();
    }

    public void Show()
    {
        Easings.CreateSlideAnimation(this, "Opacity", 1, 350, Easings.MaterialDecelerateSpline).Begin();
    }

    public void Hide()
    {
        Easings.CreateSlideAnimation(this, "Opacity", 0, 300, Easings.MaterialDecelerateSpline).Begin();
    }
}
