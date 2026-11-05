using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using SkiaSharp;
using UnoOrbit.Helpers;
using UnoOrbit.Models;
using UnoOrbit.Rendering;
using UnoOrbit.State;

namespace UnoOrbit;

public sealed partial class MainPage : Page
{
    private readonly OrbitState _state = new();
    private DispatcherTimer? _timer;
    private bool _hintAnimationStarted;
    private Storyboard? _hintStoryboard;

    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Load font asynchronously (avoid blocking UI thread)
        var syneTypeface = await LoadSyneTypefaceAsync();

        // Initialize renderer
        OrbitCanvas.Initialize(_state, syneTypeface);

        // Wire up events
        ZoomControl.ZoomInClicked += OnZoomInClicked;
        ZoomControl.ZoomOutClicked += OnZoomOutClicked;
        Panel.HandleTapped += OnPanelHandleTapped;
        NavBar.PlanetSelected += OnNavPlanetSelected;

        // Start hint animation after 1.5s
        StartHintAnimation();

        // Focus for keyboard events
        this.Focus(FocusState.Programmatic);

        // Start render loop
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Stop timer
        _timer?.Stop();
        _timer = null;

        // Unsubscribe events
        ZoomControl.ZoomInClicked -= OnZoomInClicked;
        ZoomControl.ZoomOutClicked -= OnZoomOutClicked;
        Panel.HandleTapped -= OnPanelHandleTapped;
        NavBar.PlanetSelected -= OnNavPlanetSelected;

        // Dispose renderer resources
        OrbitCanvas.Dispose();

        // Stop hint
        _hintStoryboard?.Stop();
        _hintStoryboard = null;
    }

    private static async Task<SKTypeface?> LoadSyneTypefaceAsync()
    {
        try
        {
            // Use the static Regular weight for canvas text (variable fonts unsupported on Skia)
            var uri = new Uri("ms-appx:///Assets/Fonts/Syne-Regular.ttf");
            var file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(uri);
            using var stream = await file.OpenReadAsync();
            using var memStream = new System.IO.MemoryStream();
            await stream.AsStreamForRead().CopyToAsync(memStream);
            return SKTypeface.FromData(SKData.CreateCopy(memStream.ToArray()));
        }
        catch
        {
            return SKTypeface.Default;
        }
    }

    // --- Event handlers (named methods for proper unsubscribe) ---

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        _state.TargetZoom = Math.Clamp(_state.TargetZoom + 0.3f, OrbitState.MinZoom, OrbitState.MaxZoom);
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        _state.TargetZoom = Math.Clamp(_state.TargetZoom - 0.3f, OrbitState.MinZoom, OrbitState.MaxZoom);
    }

    private void OnPanelHandleTapped(object? sender, EventArgs e) => DeselectPlanet();

    private void OnNavPlanetSelected(object? sender, int index) => SelectPlanet(index);

    // --- Tick loop ---

    private void OnTick(object? sender, object e)
    {
        _state.Time += 0.008f;

        // Smooth interpolation with higher factors for snappier response
        _state.Zoom += (_state.TargetZoom - _state.Zoom) * 0.12f;
        _state.PanX += (_state.TargetPanX - _state.PanX) * 0.09f;
        _state.PanY += (_state.TargetPanY - _state.PanY) * 0.09f;
        _state.SelectionIntensity += (_state.TargetSelectionIntensity - _state.SelectionIntensity) * 0.08f;

        // Invalidate canvas
        OrbitCanvas.RequestInvalidate();

        // Update zoom display
        ZoomControl.UpdateZoomLevel(_state.Zoom);

        // Update atmosphere glow position
        UpdateAtmosphereGlow();

        // Update pan target for selected planet (it's orbiting)
        if (_state.SelectedPlanet is int sel)
        {
            var planet = Planets.All[sel];
            float baseRadius = MathF.Min((float)OrbitCanvas.ActualWidth, (float)OrbitCanvas.ActualHeight) * 0.42f;
            float orbitR = baseRadius * planet.OrbitRadius * OrbitState.SelectedZoom;
            float angle = _state.Time * planet.Speed * 0.5f;
            _state.TargetPanX = -(MathF.Cos(angle) * orbitR);
            _state.TargetPanY = -(MathF.Sin(angle) * orbitR * 0.55f);
        }
    }

    private void UpdateAtmosphereGlow()
    {
        if (_state.SelectedPlanet is int sel)
        {
            var planet = Planets.All[sel];
            float w = (float)OrbitCanvas.ActualWidth;
            float h = (float)OrbitCanvas.ActualHeight;
            float cx = w / 2f + _state.PanX;
            float cy = h / 2f + _state.PanY;
            float baseRadius = MathF.Min(w, h) * 0.42f;

            var (px, py, _) = OrbitState.GetPlanetPosition(planet, _state.Time, _state.Zoom, cx, cy, baseRadius);

            AtmosphereGlow.Margin = new Thickness(px - 250, py - 250, 0, 0);

            var color = ColorUtil.FromHex(planet.ColorHex);
            AtmosphereGlow.Background = new SolidColorBrush(
                Windows.UI.Color.FromArgb(77, color.Red, color.Green, color.Blue));
        }
    }

    // --- Input handlers ---

    private void OnCanvasPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(OrbitCanvas).Position;
        float pointerX = (float)pos.X;
        float pointerY = (float)pos.Y;

        float w = (float)OrbitCanvas.ActualWidth;
        float h = (float)OrbitCanvas.ActualHeight;
        float cx = w / 2f + _state.PanX;
        float cy = h / 2f + _state.PanY;
        float baseRadius = MathF.Min(w, h) * 0.42f;

        int? hit = OrbitState.HitTestPlanet(pointerX, pointerY, _state, cx, cy, baseRadius);
        _state.HoveredPlanet = hit;

        // Cursor change (use .Equals() for PointerDeviceType comparison per Uno best practice)
        try
        {
            var pt = e.GetCurrentPoint(OrbitCanvas);
            if (pt.PointerDevice.PointerDeviceType.Equals(Microsoft.UI.Input.PointerDeviceType.Mouse))
            {
                if (hit != null)
                    ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
                else
                    ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
            }
        }
        catch { /* Cursor API may not be available on all platforms */ }

        // Tooltip
        if (hit is int hovered && _state.SelectedPlanet == null)
        {
            TooltipText.Text = Planets.All[hovered].Name;
            Tooltip.Opacity = 1;
            TooltipTranslate.X = pointerX + 15;
            TooltipTranslate.Y = pointerY - 10;
        }
        else
        {
            Tooltip.Opacity = 0;
        }
    }

    private void OnCanvasPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(OrbitCanvas).Position;
        float pointerX = (float)pos.X;
        float pointerY = (float)pos.Y;

        float w = (float)OrbitCanvas.ActualWidth;
        float h = (float)OrbitCanvas.ActualHeight;
        float cx = w / 2f + _state.PanX;
        float cy = h / 2f + _state.PanY;
        float baseRadius = MathF.Min(w, h) * 0.42f;

        int? hit = OrbitState.HitTestPlanet(pointerX, pointerY, _state, cx, cy, baseRadius);

        if (hit is int index)
        {
            SelectPlanet(index);
        }
        else if (_state.SelectedPlanet != null)
        {
            DeselectPlanet();
        }

        this.Focus(FocusState.Programmatic);
    }

    private void OnCanvasWheel(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint(OrbitCanvas).Properties;
        int delta = props.MouseWheelDelta;
        float zoomDelta = delta > 0 ? 0.15f : -0.15f;
        _state.TargetZoom = Math.Clamp(_state.TargetZoom + zoomDelta, OrbitState.MinZoom, OrbitState.MaxZoom);
        e.Handled = true;
    }

    private void OnManipulationDelta(object sender, Microsoft.UI.Xaml.Input.ManipulationDeltaRoutedEventArgs e)
    {
        float scaleDelta = (float)(e.Delta.Scale - 1.0) * 0.5f;
        _state.TargetZoom = Math.Clamp(_state.TargetZoom + scaleDelta, OrbitState.MinZoom, OrbitState.MaxZoom);
    }

    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Escape:
                if (_state.SelectedPlanet != null)
                    DeselectPlanet();
                break;

            case Windows.System.VirtualKey.Left:
                if (_state.SelectedPlanet is int selL)
                    SelectPlanet((selL - 1 + 8) % 8);
                break;

            case Windows.System.VirtualKey.Right:
                if (_state.SelectedPlanet is int selR)
                    SelectPlanet((selR + 1) % 8);
                break;

            case Windows.System.VirtualKey.Add:
            case (Windows.System.VirtualKey)187: // = / + key
                _state.TargetZoom = Math.Clamp(_state.TargetZoom + 0.3f, OrbitState.MinZoom, OrbitState.MaxZoom);
                break;

            case Windows.System.VirtualKey.Subtract:
            case (Windows.System.VirtualKey)189: // - / _ key
                _state.TargetZoom = Math.Clamp(_state.TargetZoom - 0.3f, OrbitState.MinZoom, OrbitState.MaxZoom);
                break;
        }
    }

    // --- State transitions ---

    private void SelectPlanet(int index)
    {
        float w = (float)OrbitCanvas.ActualWidth;
        float h = (float)OrbitCanvas.ActualHeight;
        float baseRadius = MathF.Min(w, h) * 0.42f;
        float cx = w / 2f;
        float cy = h / 2f;

        bool isSwitch = _state.SelectedPlanet != null;

        _state.SelectPlanet(index, cx, cy, baseRadius);

        // Hide tooltip
        Tooltip.Opacity = 0;

        // Show/update panel
        if (isSwitch)
            Panel.SwitchPlanet(Planets.All[index]);
        else
            Panel.Open(Planets.All[index]);

        // Show nav and update active dot
        NavBar.UpdateActivePlanet(index);
        NavBar.Show();

        // Show atmosphere glow
        AnimateAtmosphereGlow(targetOpacity: 1);

        // Dismiss hint
        DismissHint();
    }

    private void DeselectPlanet()
    {
        _state.DeselectPlanet();

        Panel.Close();
        NavBar.Hide();
        AnimateAtmosphereGlow(targetOpacity: 0);
    }

    // --- Atmosphere glow ---

    private void AnimateAtmosphereGlow(double targetOpacity)
    {
        double duration = targetOpacity > 0 ? 600 : 400;
        Easings.CreateSlideAnimation(AtmosphereGlow, "Opacity", targetOpacity, duration, Easings.MaterialDecelerateSpline).Begin();
    }

    // --- Hint text ---

    private void StartHintAnimation()
    {
        if (_hintAnimationStarted) return;
        _hintAnimationStarted = true;

        var anim = new DoubleAnimationUsingKeyFrames
        {
            BeginTime = TimeSpan.FromMilliseconds(1500),
        };
        anim.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 0 });
        anim.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(750), Value = 0.4 });
        anim.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(4250), Value = 0.4 });
        anim.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(5000), Value = 0 });

        Storyboard.SetTarget(anim, HintText);
        Storyboard.SetTargetProperty(anim, "Opacity");

        _hintStoryboard = new Storyboard();
        _hintStoryboard.Children.Add(anim);
        _hintStoryboard.Begin();
    }

    private void DismissHint()
    {
        if (!_state.HintDismissed) return;

        _hintStoryboard?.Stop();
        HintText.Opacity = 0;
        HintText.Visibility = Visibility.Collapsed;
    }
}
