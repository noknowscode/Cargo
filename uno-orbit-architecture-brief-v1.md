# Uno Orbit — Architecture Brief v1

**Parent documents:** Uno Orbit PRD v1, Design Brief v1  
**Purpose:** Define the project structure, class hierarchy, rendering pipeline, state management, animation approach, and platform-specific implementation decisions. A developer reads this and knows *where every file goes* and *how every system connects* before writing code.

---

## 1. Project Setup

### 1.1 Template & Target Frameworks

```
dotnet new unoapp -o UnoOrbit --preset=blank
```

**Targets:** Windows (net9.0-windows10.0.22621), WebAssembly, iOS, Android.  
**Minimum Uno.WinUI version:** 6.x (for SkiaSharp integration and Composition API support).

### 1.2 NuGet Dependencies

| Package | Purpose |
|---------|---------|
| `Uno.WinUI` | Core framework (comes with template) |
| `SkiaSharp.Views.WinUI` | `SKXamlCanvas` for per-frame orbit rendering |
| `SkiaSharp` | Drawing primitives (gradients, paths, text) |

No other packages. No MVVM framework, no DI container, no navigation library. This is a single-page app with trivial state.

### 1.3 Font Assets

Bundle both fonts as app resources:

```
Assets/
  Fonts/
    Syne-VariableFont_wght.ttf
    SpaceMono-Regular.ttf
    SpaceMono-Bold.ttf
```

Register in `App.xaml` or platform font configuration. Reference in XAML as `"ms-appx:///Assets/Fonts/Syne-VariableFont_wght.ttf#Syne"` (exact format depends on platform — test on all four targets).

For SkiaSharp canvas text, load an `SKTypeface` from the same TTF bytes at startup and cache it as a static field.

---

## 2. Solution Structure

```
UnoOrbit/
├── App.xaml / App.xaml.cs
├── MainPage.xaml / MainPage.xaml.cs
├── Models/
│   └── PlanetData.cs              // Planet record + static array of 8
├── Rendering/
│   ├── OrbitRenderer.cs           // All SkiaSharp draw calls
│   └── ColorUtil.cs               // lightenColor() helper
├── State/
│   └── OrbitState.cs              // Mutable state object (zoom, pan, selection, time)
├── Components/
│   ├── StarfieldControl.xaml/.cs  // 200-star background (XAML, not canvas)
│   ├── PlanetPanel.xaml/.cs       // Bottom sheet with stats
│   ├── PlanetNav.xaml/.cs         // Dot navigation bar
│   └── ZoomIndicator.xaml/.cs     // Top-right pill with +/− buttons
├── Helpers/
│   └── EasingCurves.cs            // Named cubic-bezier definitions
└── Assets/
    └── Fonts/
        ├── Syne-VariableFont_wght.ttf
        ├── SpaceMono-Regular.ttf
        └── SpaceMono-Bold.ttf
```

Total: ~12 files of substance. No ViewModels folder — state is simple enough for a single mutable object observed directly.

---

## 3. Data Layer

### 3.1 PlanetData.cs

```csharp
public record PlanetData(
    string Name,
    string Type,
    string ColorHex,       // e.g. "#4da6ff"
    float OrbitRadius,     // 0–1 normalized
    float Size,            // base pixels at zoom=1
    float Speed,           // angular velocity multiplier
    string Diameter,
    string Mass,
    string Distance,
    string DayLength,
    string Gravity,
    string Temperature,
    string Atmosphere,
    string AtmosphereColorRgba,  // e.g. "rgba(77,166,255,0.12)"
    string Fact,
    string FactIcon        // emoji string
);
```

### 3.2 Static Data

```csharp
public static class Planets
{
    public static readonly PlanetData[] All = new PlanetData[]
    {
        new("Mercury", "Terrestrial Planet", "#b0a090", 0.08f, 4f, 4.15f,
            "4,879 km", "3.30 × 10²³ kg", "57.9M km", "58.6 days",
            "3.7 m/s²", "167°C avg", "Virtually none",
            "rgba(176,160,144,0.15)", 
            "A year on Mercury is just 88 Earth days, but a single day lasts 59 Earth days.",
            "🌡️"),
        // ... 7 more, copied verbatim from prototype lines 401–465
    };
}
```

All string values are display-only. No parsing, no units, no calculations on stat strings. They render as-is.

---

## 4. State Management

### 4.1 OrbitState.cs

Single mutable object. No INotifyPropertyChanged — the render loop reads fields directly each frame. XAML-bound UI reads a small subset via manual updates.

```csharp
public class OrbitState
{
    // Zoom
    public float Zoom = 1.0f;
    public float TargetZoom = 1.0f;
    public const float MinZoom = 0.6f;
    public const float MaxZoom = 4.5f;

    // Pan
    public float PanX = 0f;
    public float PanY = 0f;
    public float TargetPanX = 0f;
    public float TargetPanY = 0f;

    // Selection
    public int? SelectedPlanet = null;   // null or 0–7
    public int? HoveredPlanet = null;    // null or 0–7

    // Time
    public float Time = 0f;

    // Hint
    public bool HintDismissed = false;
}
```

### 4.2 State Mutation Rules

State is only mutated from:

1. **Main loop tick** — updates `Time`, interpolates `Zoom`/`PanX`/`PanY` toward targets.
2. **Input handlers** — set `TargetZoom`, `TargetPanX/Y`, `SelectedPlanet`, `HoveredPlanet`.
3. **SelectPlanet(int index)** — sets `SelectedPlanet`, calculates `TargetPanX/Y`, sets `TargetZoom = 2.5`, triggers panel open, nav show, glow show, hint dismiss.
4. **DeselectPlanet()** — sets `SelectedPlanet = null`, `TargetZoom = 1`, `TargetPanX/Y = 0`, triggers panel close, nav hide, glow hide.

No async. No Commands. No message bus. Direct method calls from input handlers.

### 4.3 Computed Values (pure functions, not stored)

```csharp
public static (float X, float Y, float R) GetPlanetPosition(
    PlanetData planet, float time, float zoom, 
    float centerX, float centerY, float baseRadius)
{
    float r = baseRadius * planet.OrbitRadius * zoom;
    float angle = time * planet.Speed * 0.5f;
    return (
        centerX + MathF.Cos(angle) * r,
        centerY + MathF.Sin(angle) * r * 0.55f,
        planet.Size * zoom * 0.7f
    );
}
```

This function is called per-planet per-frame for rendering, and ad-hoc for hit testing and pan calculations.

---

## 5. Rendering Pipeline (SkiaSharp)

### 5.1 Component: SKXamlCanvas

In `MainPage.xaml`, the orbit canvas is an `SKXamlCanvas` that fills the viewport:

```xml
<skia:SKXamlCanvas x:Name="OrbitCanvas"
                   PaintSurface="OnPaintSurface"
                   PointerMoved="OnCanvasPointerMoved"
                   PointerPressed="OnCanvasPointerPressed"
                   PointerReleased="OnCanvasPointerReleased" />
```

### 5.2 Render Loop

Use `DispatcherTimer` at ~16ms interval (≈60fps) or `CompositionTarget.Rendering` if available:

```csharp
private void OnTick(object sender, object e)
{
    _state.Time += 0.008f;

    // Interpolate zoom
    _state.Zoom += (_state.TargetZoom - _state.Zoom) * 0.08f;
    _state.PanX += (_state.TargetPanX - _state.PanX) * 0.06f;
    _state.PanY += (_state.TargetPanY - _state.PanY) * 0.06f;

    // Invalidate canvas (triggers PaintSurface)
    OrbitCanvas.Invalidate();

    // Update XAML-bound UI
    UpdateZoomDisplay();
    UpdateAtmosphereGlowPosition();
}
```

### 5.3 OrbitRenderer.cs — Draw Order

`OrbitRenderer` is a static class (or instance with no state) that takes the canvas, state, and planet data:

```csharp
public static void Draw(SKCanvas canvas, SKImageInfo info, OrbitState state)
{
    float dpr = info.Width / (float)logicalWidth;  // or use actual DPI
    canvas.Scale(dpr, dpr);
    canvas.Clear(SKColors.Transparent);

    float cx = logicalWidth / 2f + state.PanX;
    float cy = logicalHeight / 2f + state.PanY;
    float baseRadius = MathF.Min(logicalWidth, logicalHeight) * 0.42f;

    // 1. Sun outer glow
    DrawSunGlow(canvas, cx, cy, state.Zoom);

    // 2. Sun core
    DrawSunCore(canvas, cx, cy, state.Zoom);

    // 3. For each planet (in order Mercury→Neptune):
    for (int i = 0; i < Planets.All.Length; i++)
    {
        var planet = Planets.All[i];
        float orbitR = baseRadius * planet.OrbitRadius * state.Zoom;

        // 3a. Orbit path
        DrawOrbitPath(canvas, cx, cy, orbitR, isSelected: state.SelectedPlanet == i);

        // 3b. Planet position
        var pos = GetPlanetPosition(planet, state.Time, state.Zoom, cx, cy, baseRadius);

        // 3c. Hover/select glow
        if (state.HoveredPlanet == i || state.SelectedPlanet == i)
            DrawPlanetGlow(canvas, pos, planet);

        // 3d. Planet body
        DrawPlanetBody(canvas, pos, planet);

        // 3e. Saturn ring
        if (planet.Name == "Saturn")
            DrawSaturnRing(canvas, pos, planet, state.Zoom);

        // 3f. Label
        if (state.Zoom > 1.2f || state.HoveredPlanet == i || state.SelectedPlanet == i)
            DrawPlanetLabel(canvas, pos, planet, state, i);
    }
}
```

### 5.4 SKPaint Caching

Create paints once and reuse. Do NOT allocate per frame:

```csharp
// Cache at class level, dispose on unload
private static readonly SKPaint _orbitPaintDefault = new()
{
    Style = SKPaintStyle.Stroke,
    StrokeWidth = 0.5f,
    Color = new SKColor(255, 255, 255, 10),  // rgba(255,255,255,0.04)
    IsAntialias = true,
};
```

Gradient paints (sun, planet bodies) must be recreated when zoom changes their radii, but reuse the `SKPaint` object and just swap the `Shader`.

### 5.5 Ellipse Drawing

SkiaSharp has no `drawEllipse` with rotation. For orbit paths (axis-aligned), use `canvas.DrawOval(cx, cy, rx, ry, paint)`. For Saturn's ring (rotated −0.2 rad):

```csharp
canvas.Save();
canvas.RotateRadians(-0.2f, pos.X, pos.Y);
canvas.DrawOval(pos.X, pos.Y, pos.R * 2.2f, pos.R * 0.5f, saturnRingPaint);
canvas.Restore();
```

### 5.6 Text on Canvas

Use `SKPaint` with `Typeface` set to the cached Syne `SKTypeface`:

```csharp
var labelPaint = new SKPaint
{
    Typeface = _syneTypeface,
    TextSize = MathF.Max(9f, 10f * MathF.Min(state.Zoom * 0.7f, 1.2f)),
    Color = (state.HoveredPlanet == i || state.SelectedPlanet == i)
        ? new SKColor(255, 255, 255, 204)   // 0.8 alpha
        : new SKColor(255, 255, 255, 89),    // 0.35 alpha
    TextAlign = SKTextAlign.Center,
    IsAntialias = true,
};
canvas.DrawText(planet.Name, pos.X, pos.Y + pos.R + 14f, labelPaint);
```

---

## 6. Hit Testing

### 6.1 Algorithm

On `PointerMoved` and `PointerPressed`, convert pointer position to logical coordinates, then:

```csharp
public static int? HitTestPlanet(
    float pointerX, float pointerY, 
    OrbitState state, float centerX, float centerY, float baseRadius)
{
    // Reverse iterate: last planet (Neptune) tested first for z-order
    for (int i = Planets.All.Length - 1; i >= 0; i--)
    {
        var pos = GetPlanetPosition(Planets.All[i], state.Time, state.Zoom,
                                     centerX, centerY, baseRadius);
        float hitR = MathF.Max(pos.R + 8f, 15f);
        float dx = pointerX - pos.X;
        float dy = pointerY - pos.Y;
        if (dx * dx + dy * dy < hitR * hitR)
            return i;
    }
    return null;
}
```

### 6.2 Pointer Events Mapping

| Prototype Event | Uno Equivalent | Handler |
|----------------|----------------|---------|
| `mousemove` | `PointerMoved` on `SKXamlCanvas` | Update `HoveredPlanet`, tooltip position, cursor |
| `click` | `PointerPressed` + `PointerReleased` (check no drag) on canvas | Select/deselect planet |
| `wheel` | `PointerWheelChanged` on canvas | Adjust `TargetZoom` |
| `touchstart` (1 finger) | `PointerPressed` with `PointerDeviceType.Touch` | Hit test → select |
| `touchstart/move` (2 finger) | `ManipulationDelta` with `Scale` | Pinch zoom |
| `keydown` | `KeyDown` on `MainPage` (focus required) | Escape, arrows, +/− |

### 6.3 Cursor Changes

```csharp
// In PointerMoved handler:
if (hitResult != null)
    ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
else
    ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
    // Note: no "grab" cursor in WinUI. SizeAll is closest. 
    // Alternatively use a custom cursor or just use Arrow.
```

---

## 7. XAML Component Architecture

### 7.1 MainPage.xaml — Visual Tree

```xml
<Page Background="#04060e">
  <Grid>
    <!-- Layer 0: Starfield (behind canvas) -->
    <local:StarfieldControl />

    <!-- Layer 0: Atmosphere glow -->
    <Border x:Name="AtmosphereGlow"
            Width="300" Height="300"
            CornerRadius="150"
            Opacity="0"
            HorizontalAlignment="Left" VerticalAlignment="Top"
            IsHitTestVisible="False">
      <!-- Blur applied via Compositor or custom effect -->
    </Border>

    <!-- Layer 1: Orbit canvas -->
    <skia:SKXamlCanvas x:Name="OrbitCanvas"
                       PaintSurface="OnPaintSurface"
                       PointerMoved="OnCanvasPointerMoved"
                       PointerPressed="OnCanvasPointerPressed"
                       PointerWheelChanged="OnCanvasWheel" />

    <!-- Layer 10: Top bar -->
    <Grid VerticalAlignment="Top" 
          Padding="32,24,32,24"
          IsHitTestVisible="False">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>
      <TextBlock Text="UNO ORBIT" 
                 Style="{StaticResource LogoStyle}"
                 Grid.Column="0"/>
      <local:ZoomIndicator x:Name="ZoomControl"
                           Grid.Column="1"
                           IsHitTestVisible="True"/>
    </Grid>

    <!-- Layer 15: Hint -->
    <TextBlock x:Name="HintText"
               Text="Click a planet to explore · Scroll to zoom"
               Style="{StaticResource HintStyle}"
               VerticalAlignment="Bottom"
               HorizontalAlignment="Center"
               Margin="0,0,0,48"
               IsHitTestVisible="False"/>

    <!-- Layer 20: Planet panel -->
    <local:PlanetPanel x:Name="Panel"
                       VerticalAlignment="Bottom"/>

    <!-- Layer 25: Planet nav -->
    <local:PlanetNav x:Name="NavBar"
                     VerticalAlignment="Bottom"
                     HorizontalAlignment="Center"/>

    <!-- Layer 30: Tooltip -->
    <Border x:Name="Tooltip"
            Style="{StaticResource TooltipStyle}"
            Opacity="0"
            IsHitTestVisible="False">
      <TextBlock x:Name="TooltipText"/>
    </Border>
  </Grid>
</Page>
```

All layers stack via Grid implicit z-ordering (later children = higher z). No explicit `Canvas.ZIndex` needed.

### 7.2 StarfieldControl

Generates 200 `Ellipse` children in code-behind at `Loaded`. Each gets a `Storyboard` with a `DoubleAnimation` targeting `Opacity`:

```csharp
for (int i = 0; i < 200; i++)
{
    var star = new Ellipse
    {
        Width = 0.5 + _rng.NextDouble() * 2.0,
        Fill = new SolidColorBrush(Colors.White),
    };
    star.Height = star.Width;

    Canvas.SetLeft(star, _rng.NextDouble() * ActualWidth);
    Canvas.SetTop(star, _rng.NextDouble() * ActualHeight);
    star.Opacity = 0.1 + _rng.NextDouble() * 0.6;

    StarCanvas.Children.Add(star);

    var anim = new DoubleAnimation
    {
        From = 0.1,
        To = 0.7,
        Duration = TimeSpan.FromSeconds(2 + _rng.NextDouble() * 4),
        BeginTime = TimeSpan.FromSeconds(_rng.NextDouble() * 3),
        AutoReverse = true,
        RepeatBehavior = RepeatBehavior.Forever,
        EasingFunction = new SineEase(),
    };
    Storyboard.SetTarget(anim, star);
    Storyboard.SetTargetProperty(anim, "Opacity");
    
    var sb = new Storyboard();
    sb.Children.Add(anim);
    sb.Begin();
}
```

### 7.3 PlanetPanel

A `Border` with `RenderTransform` = `TranslateTransform`. Open/close is animated:

```csharp
public void Open(PlanetData planet)
{
    PopulateContent(planet);
    // Animate TranslateTransform.Y from panel height → 0
    var anim = new DoubleAnimation
    {
        To = 0,
        Duration = TimeSpan.FromMilliseconds(600),
        EasingFunction = new CubicBezierEase(0.4, 0, 0.2, 1),
    };
    _translateTransform.BeginAnimation(TranslateTransform.YProperty, anim);
    // Then trigger staggered stat card animations
}

public void Close()
{
    var anim = new DoubleAnimation
    {
        To = ActualHeight,  // or a large value
        Duration = TimeSpan.FromMilliseconds(600),
        EasingFunction = new CubicBezierEase(0.4, 0, 0.2, 1),
    };
    _translateTransform.BeginAnimation(TranslateTransform.YProperty, anim);
}
```

**Stat card stagger:** After populating the 6 stat card elements, run 6 `DoubleAnimation`s on each card's `Opacity` (0→1) and `TranslateTransform.Y` (15→0) with delays of 100, 150, 200, 250, 300, 350ms. Easing: spring overshoot `(0.34, 1.56, 0.64, 1)`.

**Fun fact:** Same animation, delay 400ms.

**Planet switching:** Call `Close()`, wait ~100ms for content to not be visible, swap content, call `Open()` to re-trigger stagger. Alternatively, just swap content and re-run stagger animations without closing (prototype does innerHTML swap + re-add `open` class next frame).

### 7.4 PlanetNav

An `ItemsControl` (or manual `StackPanel`) with 8 `Ellipse` children. Bound to planet data. Active state updates `Fill`, `BorderBrush`, and `RenderTransform` (scale).

### 7.5 ZoomIndicator

A horizontal `StackPanel` inside a `Border`. Two `Button`s for +/−, one `TextBlock` for the zoom level. The `TextBlock.Text` is updated from the main loop every frame.

---

## 8. Animation Strategy Summary

| Animation | Mechanism | Why |
|-----------|-----------|-----|
| Orbit motion (planets, sun glow) | SkiaSharp per-frame redraw | Must be per-frame for smooth 60fps planetary motion |
| Zoom & pan interpolation | Code-based lerp in tick handler | Exponential ease-out, not duration-based — can't use Storyboard |
| Starfield twinkle | XAML `DoubleAnimation` with `RepeatBehavior.Forever` | Set-and-forget, no per-frame cost |
| Panel slide up/down | XAML `DoubleAnimation` on `TranslateTransform.Y` | Duration-based, standard UI transition |
| Stat card stagger | XAML `DoubleAnimation` on `Opacity` + `TranslateTransform.Y` with `BeginTime` delays | Duration-based with stagger, natural for Storyboard |
| Fun fact entrance | Same as stat cards | Same |
| Nav dot scale/opacity | XAML `DoubleAnimation` or `ImplicitAnimations` | Simple property changes |
| Tooltip opacity | XAML `DoubleAnimation` or direct property set | Fast 200ms fade |
| Atmosphere glow opacity | XAML `DoubleAnimation` | 800ms fade |
| Atmosphere glow position | Direct `Canvas.SetLeft/Top` in tick handler | Must track orbiting planet per-frame |
| Hint text | XAML `DoubleAnimation` keyframe sequence | One-shot, fire-and-forget |

**Key principle:** Canvas-drawn elements (planets, orbits, sun) animate via the tick → invalidate loop. XAML overlay elements (panel, nav, tooltip, glow) animate via Storyboard/DoubleAnimation. Never mix — don't try to animate XAML elements in the tick handler or draw UI chrome on the canvas.

---

## 9. Custom Easing Functions

WinUI's built-in easing functions don't include arbitrary cubic-bezier. Options:

### Option A: SplineDoubleKeyFrame (preferred for XAML animations)

```xml
<DoubleAnimationUsingKeyFrames>
  <SplineDoubleKeyFrame KeyTime="0:0:0.6"
                        Value="0"
                        KeySpline="0.4,0,0.2,1"/>  <!-- MaterialDecelerate -->
</DoubleAnimationUsingKeyFrames>
```

### Option B: Custom IEasingFunction (for code-behind animations)

```csharp
public class CubicBezierEase : EasingFunctionBase
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }

    public CubicBezierEase(double x1, double y1, double x2, double y2)
    {
        X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
    }

    protected override double EaseInCore(double t)
    {
        // Newton-Raphson cubic bezier solver
        // (standard implementation — many reference implementations available)
    }
}
```

Define the three named curves as static instances:

```csharp
public static class Easings
{
    public static readonly CubicBezierEase MaterialDecelerate = new(0.4, 0, 0.2, 1);
    public static readonly CubicBezierEase SpringOvershoot = new(0.34, 1.56, 0.64, 1);
    // StandardEase (0.25, 0.1, 0.25, 1) is not needed — it's close to built-in QuadraticEase
}
```

---

## 10. Platform-Specific Considerations

### 10.1 SkiaSharp on WASM

`SKXamlCanvas` on WebAssembly uses a `<canvas>` element internally. Performance is good for this workload (8 circles + 8 ellipses + 1 gradient per frame). No special handling needed.

Verify: `devicePixelRatio` scaling. On WASM, use `SKXamlCanvas`'s built-in DPI handling rather than manual `canvas.Scale()`. Test if `CanvasSize` already accounts for DPI.

### 10.2 Backdrop Blur

The prototype uses `backdrop-filter: blur()` on the panel and tooltip. In Uno:

- **Windows:** Use `AcrylicBrush` or `Compositor` blur.
- **Non-Windows:** Uno's `AcrylicBrush` support varies. Fallback: use a semi-opaque solid background (the panel backdrop gradient at 97% opacity is already nearly opaque — blur adds subtle polish but isn't critical).

If blur is not achievable cross-platform, use the gradient alone: `linear-gradient(to top, rgba(4,6,14,0.97) 60%, rgba(4,6,14,0.80))`. This matches the prototype's visual effect to ~95%.

### 10.3 Touch vs. Pointer

Uno unifies touch/mouse/pen as `Pointer` events. For pinch zoom:

- Use `ManipulationMode="Scale"` on the canvas or a parent element.
- In `ManipulationDelta`, read `e.Delta.Scale` and apply to `TargetZoom`.

Single-touch tap is handled via `PointerPressed`/`PointerReleased` with a check that `ManipulationMode` didn't consume it as a pinch start.

### 10.4 Keyboard Focus

`MainPage` must have focus to receive `KeyDown`. Call `this.Focus(FocusState.Programmatic)` on `Loaded`. On WASM, keyboard events may require the page to have focus — test and handle.

### 10.5 Cursor Styling

WinUI cursor API (`ProtectedCursor`) works on Windows. On other platforms, Uno support may be limited. Acceptable fallback: no cursor changes (default arrow everywhere). This is a polish item, not a behavioral requirement.

---

## 11. Performance Budget

| Metric | Target | Why |
|--------|--------|-----|
| Frame time | < 16ms | 60fps required for smooth orbital animation |
| SKPaint allocations per frame | 0 | Reuse cached paints, only swap shaders |
| GC pressure | Minimal | Avoid LINQ, closures, and string allocations in render path |
| Star animations | Fire-and-forget | No per-frame cost after setup |
| Panel animations | Duration-based | Only run during transitions, zero cost at rest |

**Profiling priority:** The `OnPaintSurface` handler. It runs 60× per second. Everything else is event-driven or one-shot.

### 11.1 Render Path Optimization

The draw loop draws 8 planets with gradients. SkiaSharp gradient shader creation is the main cost. Mitigation:

- Cache `SKShader` objects per planet body. Only recreate when zoom changes enough to matter (e.g., when `abs(zoom - lastCachedZoom) > 0.05`).
- Sun glow/core shaders: recreate when zoom changes (same threshold).
- Orbit path paints: two cached paints (default + selected), swap `Color` and `StrokeWidth` only.
- Text paints: one cached paint, update `TextSize` per-frame (cheap).

---

## 12. Data Flow Diagram

```
┌─────────────┐     ┌──────────────┐     ┌─────────────────┐
│ Input Events │────▶│ OrbitState    │────▶│ OrbitRenderer   │
│ (pointer,    │     │ (zoom, pan,  │     │ (SkiaSharp      │
│  keyboard,   │     │  selection,  │     │  draw calls)    │
│  wheel)      │     │  hover, time)│     └────────┬────────┘
└─────────────┘     └──────┬───────┘              │
                           │                       ▼
                           │              ┌────────────────┐
                           │              │ SKXamlCanvas   │
                           │              │ (PaintSurface) │
                           │              └────────────────┘
                           │
                           ▼
                    ┌──────────────┐
                    │ XAML UI      │
                    │ (Panel, Nav, │
                    │  Tooltip,    │
                    │  ZoomDisplay,│
                    │  Glow)       │
                    └──────────────┘

Tick (16ms) ──▶ Mutate OrbitState (time, lerp zoom/pan)
             ──▶ Invalidate SKXamlCanvas
             ──▶ Update zoom display text
             ──▶ Update glow position
```

No events, no bindings, no reactive framework. The tick handler is the single orchestrator. Input handlers write to state. The tick handler reads state, updates interpolated values, and pushes to both the canvas and XAML UI.

---

## 13. File Size Estimates

| Component | Estimated Lines |
|-----------|----------------|
| `PlanetData.cs` + static data | ~100 |
| `OrbitState.cs` | ~30 |
| `OrbitRenderer.cs` | ~200 |
| `ColorUtil.cs` | ~15 |
| `MainPage.xaml` | ~80 |
| `MainPage.xaml.cs` (input handlers, tick, wiring) | ~250 |
| `StarfieldControl.xaml/.cs` | ~60 |
| `PlanetPanel.xaml/.cs` | ~150 |
| `PlanetNav.xaml/.cs` | ~60 |
| `ZoomIndicator.xaml/.cs` | ~40 |
| `EasingCurves.cs` | ~40 |
| **Total** | **~1,025 lines** |

This is a tight, single-purpose app. No abstraction beyond what's needed.

---

*End of Architecture Brief v1*
