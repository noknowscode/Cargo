using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using UnoOrbit.Helpers;
using UnoOrbit.Models;
using UnoOrbit.Rendering;

namespace UnoOrbit.Components;

public sealed partial class PlanetPanel : UserControl
{
    public event EventHandler? HandleTapped;
    private readonly Border[] _statCards = new Border[6];
    private readonly TranslateTransform[] _statTransforms = new TranslateTransform[6];

    private static readonly string[] StatLabels = ["Diameter", "Mass", "Distance from Sun", "Day Length", "Gravity", "Avg Temperature"];

    private DispatcherTimer? _factRotationTimer;
    private PlanetData? _currentPlanet;
    private int _currentFactIndex;

    public PlanetPanel()
    {
        this.InitializeComponent();
        CreateStatCards();
    }

    private void CreateStatCards()
    {
        for (int i = 0; i < 6; i++)
        {
            int row = i / 3;
            int col = i % 3;

            var translate = new TranslateTransform { Y = 15 };
            _statTransforms[i] = translate;

            var valueText = new TextBlock
            {
                FontFamily = (FontFamily)Application.Current.Resources["SpaceMonoBoldFont"],
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 24,
                Margin = new Thickness(0, 0, 0, 4.8),
                HorizontalTextAlignment = TextAlignment.Center,
                IsTextSelectionEnabled = false,
            };

            var labelText = new TextBlock
            {
                Text = StatLabels[i].ToUpperInvariant(),
                FontFamily = (FontFamily)Application.Current.Resources["SyneFont"],
                FontWeight = Microsoft.UI.Text.FontWeights.Medium,
                FontSize = 13.3,
                CharacterSpacing = 150,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 232, 230, 240)),
                Opacity = 0.35,
                HorizontalTextAlignment = TextAlignment.Center,
                IsTextSelectionEnabled = false,
            };

            var cardContent = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Children = { valueText, labelText }
            };

            var card = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(8, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(13, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Child = cardContent,
                Opacity = 0,
                RenderTransform = translate,
                Margin = new Thickness(col > 0 ? 6 : 0, 0, col < 2 ? 6 : 0, 12),
            };

            _statCards[i] = card;
            Grid.SetRow(card, row);
            Grid.SetColumn(card, col);
            StatsGrid.Children.Add(card);
        }
    }

    public void Open(PlanetData planet)
    {
        PopulateContent(planet);

        // Animate panel slide up with material decelerate curve
        Easings.CreateSlideAnimation(PanelTranslate, "Y", 0, 500, Easings.MaterialDecelerateSpline).Begin();

        // Stagger stat card animations with tighter delays for snappier feel
        for (int i = 0; i < 6; i++)
        {
            AnimateStatCardIn(i, 80 + i * 40);
        }
        AnimateFunFactIn(320);
    }

    public void Close()
    {
        StopFactRotation();
        Easings.CreateSlideAnimation(PanelTranslate, "Y", 800, 400, Easings.MaterialDecelerateSpline).Begin();
    }

    public void SwitchPlanet(PlanetData planet)
    {
        StopFactRotation();

        // Fade out current content quickly, then fade in new content
        for (int i = 0; i < 6; i++)
        {
            Easings.CreateSlideAnimation(_statCards[i], "Opacity", 0, 150, Easings.MaterialDecelerateSpline).Begin();
            Easings.CreateSlideAnimation(_statTransforms[i], "Y", 8, 150, Easings.MaterialDecelerateSpline).Begin();
        }
        Easings.CreateSlideAnimation(FunFactCard, "Opacity", 0, 150, Easings.MaterialDecelerateSpline).Begin();
        Easings.CreateSlideAnimation(FunFactTranslate, "Y", 8, 150, Easings.MaterialDecelerateSpline).Begin();

        // After fade-out completes, swap content and fade in
        var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(170) };
        delayTimer.Tick += (s, e) =>
        {
            delayTimer.Stop();
            PopulateContent(planet);

            for (int i = 0; i < 6; i++)
            {
                AnimateStatCardIn(i, i * 35);
            }
            AnimateFunFactIn(220);
        };
        delayTimer.Start();
    }

    private void PopulateContent(PlanetData planet)
    {
        _currentPlanet = planet;
        _currentFactIndex = 0;

        var color = ColorUtil.FromHex(planet.ColorHex);
        var winColor = Windows.UI.Color.FromArgb(255, color.Red, color.Green, color.Blue);

        PlanetNameText.Text = planet.Name;
        PlanetNameText.Foreground = new SolidColorBrush(winColor);

        TypeLineText.Text = $"{planet.Type} \u00b7 {planet.Atmosphere}".ToUpperInvariant();

        string[] values = [planet.Diameter, planet.Mass, planet.Distance, planet.DayLength, planet.Gravity, planet.Temperature];
        for (int i = 0; i < 6; i++)
        {
            var card = _statCards[i];
            if (card.Child is StackPanel sp && sp.Children[0] is TextBlock valueText)
            {
                valueText.Text = values[i];
                valueText.Foreground = new SolidColorBrush(winColor);
            }
        }

        ShowFact(0);
        StartFactRotation();
    }

    private void ShowFact(int index)
    {
        if (_currentPlanet == null) return;
        FactIconText.Text = _currentPlanet.FactIcons[index];
        FactText.Text = _currentPlanet.Facts[index];
    }

    private void StartFactRotation()
    {
        StopFactRotation();
        if (_currentPlanet == null || _currentPlanet.Facts.Length <= 1) return;

        _factRotationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _factRotationTimer.Tick += OnFactRotationTick;
        _factRotationTimer.Start();
    }

    private void StopFactRotation()
    {
        _factRotationTimer?.Stop();
        _factRotationTimer = null;
    }

    private void OnFactRotationTick(object? sender, object e)
    {
        if (_currentPlanet == null) return;

        // Fade out current fact
        Easings.CreateSlideAnimation(FunFactCard, "Opacity", 0, 200, Easings.MaterialDecelerateSpline).Begin();
        Easings.CreateSlideAnimation(FunFactTranslate, "Y", 6, 200, Easings.MaterialDecelerateSpline).Begin();

        var swapTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        swapTimer.Tick += (s, e2) =>
        {
            swapTimer.Stop();
            _currentFactIndex = (_currentFactIndex + 1) % _currentPlanet.Facts.Length;
            ShowFact(_currentFactIndex);

            // Fade in new fact
            Easings.CreateSlideAnimation(FunFactCard, "Opacity", 1, 300, Easings.MaterialDecelerateSpline).Begin();
            Easings.CreateSlideAnimation(FunFactTranslate, "Y", 0, 300, Easings.MaterialDecelerateSpline).Begin();
        };
        swapTimer.Start();
    }

    private void AnimateStatCardIn(int index, int delayMs)
    {
        var card = _statCards[index];
        var transform = _statTransforms[index];

        // Use material decelerate for both opacity and translate
        Easings.CreateSlideAnimation(card, "Opacity", 1, 400, Easings.MaterialDecelerateSpline, delayMs).Begin();
        Easings.CreateSlideAnimation(transform, "Y", 0, 400, Easings.MaterialDecelerateSpline, delayMs).Begin();
    }

    private void AnimateFunFactIn(int delayMs)
    {
        Easings.CreateSlideAnimation(FunFactCard, "Opacity", 1, 400, Easings.MaterialDecelerateSpline, delayMs).Begin();
        Easings.CreateSlideAnimation(FunFactTranslate, "Y", 0, 400, Easings.MaterialDecelerateSpline, delayMs).Begin();
    }

    private void OnHandleTapped(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        e.Handled = true;
        HandleTapped?.Invoke(this, EventArgs.Empty);
    }
}
