using SkiaSharp;
using UnoOrbit.Models;
using UnoOrbit.State;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace UnoOrbit.Rendering;

public class OrbitRenderer : SKCanvasElement, IDisposable
{
    private OrbitState? _state;
    private OrbitDrawer? _drawer;

    public OrbitRenderer()
    {
        if (!IsSupportedOnCurrentPlatform())
        {
            throw new PlatformNotSupportedException("SKCanvasElement only supported on Skia platforms.");
        }
    }

    public void Initialize(OrbitState state, SKTypeface? syneTypeface)
    {
        _state = state;
        _drawer = new OrbitDrawer(syneTypeface);
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (_state == null || _drawer == null) return;
        _drawer.Draw(canvas, (float)area.Width, (float)area.Height, _state);
    }

    public void RequestInvalidate() => Invalidate();

    public new void Dispose()
    {
        _drawer?.Dispose();
        _drawer = null;
        base.Dispose();
    }
}

/// <summary>
/// Pure rendering logic, separated from the control.
/// All SKPaint objects are cached and reused.
/// </summary>
public sealed class OrbitDrawer : IDisposable
{
    private readonly StarField _starField = new();
    private readonly NebulaField _nebulaField = new();
    private readonly ParticleField _particleField = new();

    // Cached paints
    private readonly SKPaint _starPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _nebulaPaint = new() { IsAntialias = true };
    private readonly SKPaint _particlePaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cosmicDustPaint = new() { IsAntialias = true };
    private readonly SKPaint _orbitPaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _trailPaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _sunGlowPaint = new() { IsAntialias = true };
    private readonly SKPaint _sunCoronaPaint = new() { IsAntialias = true };
    private readonly SKPaint _sunRayPaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _sunCorePaint = new() { IsAntialias = true };
    private readonly SKPaint _planetPaint = new() { IsAntialias = true };
    private readonly SKPaint _planetDetailPaint = new() { IsAntialias = true };
    private readonly SKPaint _terminatorPaint = new() { IsAntialias = true };
    private readonly SKPaint _glowPaint = new() { IsAntialias = true };
    private readonly SKPaint _saturnRingPaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _saturnRingPaint2 = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _labelPaint = new() { IsAntialias = true };
    private readonly SKFont _labelFont;

    private float _lastCachedZoom = -1f;
    private float _lastCx, _lastCy;

    public OrbitDrawer(SKTypeface? syneTypeface)
    {
        _labelFont = new SKFont(syneTypeface ?? SKTypeface.Default);
    }

    public void Draw(SKCanvas canvas, float w, float h, OrbitState state)
    {
        canvas.Clear(new SKColor(4, 6, 14)); // #04060e

        float cx = w / 2f + state.PanX;
        float cy = h / 2f + state.PanY;
        float baseRadius = MathF.Min(w, h) * 0.42f;
        float zoom = state.Zoom;
        float time = state.Time;

        bool zoomChanged = MathF.Abs(zoom - _lastCachedZoom) > 0.05f
            || MathF.Abs(cx - _lastCx) > 1f
            || MathF.Abs(cy - _lastCy) > 1f;

        // 0. Cosmic dust band (faint Milky Way sweep)
        DrawCosmicDust(canvas, w, h, time);

        // 0b. Nebula fog (behind everything)
        DrawNebulae(canvas, w, h, time);

        // 1. Starfield
        DrawStarField(canvas, w, h, time);

        // 2. Floating particles
        DrawParticles(canvas, w, h, time);

        // 3. Sun outer corona (pulsating haze)
        DrawSunCorona(canvas, cx, cy, zoom, time);

        // 3b. Sun outer glow
        DrawSunGlow(canvas, cx, cy, zoom, zoomChanged);

        // 4. Sun core
        DrawSunCore(canvas, cx, cy, zoom, zoomChanged);

        // 4b. Sun rays (subtle light spikes)
        DrawSunRays(canvas, cx, cy, zoom, time);

        if (zoomChanged)
        {
            _lastCachedZoom = zoom;
            _lastCx = cx;
            _lastCy = cy;
        }

        float selI = state.SelectionIntensity;

        // Collect planet positions for occlusion testing
        Span<float> planetXs = stackalloc float[Planets.All.Length];
        Span<float> planetYs = stackalloc float[Planets.All.Length];
        Span<float> planetRs = stackalloc float[Planets.All.Length];

        for (int i = 0; i < Planets.All.Length; i++)
        {
            var (px, py, pr) = OrbitState.GetPlanetPosition(Planets.All[i], time, zoom, cx, cy, baseRadius);
            planetXs[i] = px;
            planetYs[i] = py;
            planetRs[i] = pr;
        }

        // 5. Per-planet
        for (int i = 0; i < Planets.All.Length; i++)
        {
            var planet = Planets.All[i];
            float orbitR = baseRadius * planet.OrbitRadius * zoom;

            // 5a. Orbit path
            bool isSelectedOrbit = state.SelectedPlanet == i;
            byte baseAlpha = (byte)(10 + selI * 18);
            byte orbitAlpha = isSelectedOrbit ? (byte)(31 + selI * 30) : baseAlpha;
            float strokeW = isSelectedOrbit ? (1.0f + selI * 0.8f) : (0.5f + selI * 0.3f);
            _orbitPaint.Color = new SKColor(255, 255, 255, orbitAlpha);
            _orbitPaint.StrokeWidth = strokeW;
            canvas.DrawOval(cx, cy, orbitR, orbitR * 0.55f, _orbitPaint);

            // 5b. Orbital trail
            DrawOrbitalTrail(canvas, planet, time, zoom, cx, cy, baseRadius, i, state);

            float px = planetXs[i], py = planetYs[i], pr = planetRs[i];

            // 5c. Hover/select glow
            if (state.HoveredPlanet == i || state.SelectedPlanet == i)
                DrawPlanetGlow(canvas, px, py, pr, planet, isSelectedOrbit, selI);

            // 5d. Planet body with realistic texturing
            DrawPlanetBody(canvas, px, py, pr, planet, time);

            // 5e. Saturn rings
            if (planet.Name == "Saturn")
                DrawSaturnRing(canvas, px, py, pr, planet, zoom);

            // 5f. Label - only show when zoomed in enough or hovered/selected,
            //     and hide if another planet's body occludes the label position
            bool showLabel = state.HoveredPlanet == i || state.SelectedPlanet == i
                || (zoom > 2.0f);

            if (showLabel && !IsLabelOccluded(i, px, py + pr + 14f, planetXs, planetYs, planetRs))
                DrawPlanetLabel(canvas, px, py, pr, planet, state, i);
        }
    }

    // --- Nebula fog ---

    private void DrawNebulae(SKCanvas canvas, float w, float h, float time)
    {
        for (int i = 0; i < _nebulaField.Clouds.Length; i++)
        {
            ref var cloud = ref _nebulaField.Clouds[i];

            // Slow drift
            float nx = (cloud.X + cloud.DriftX * time) % 1f;
            float ny = (cloud.Y + cloud.DriftY * time) % 1f;
            if (nx < 0) nx += 1f;
            if (ny < 0) ny += 1f;

            float px = nx * w;
            float py = ny * h;
            float radius = cloud.Radius * MathF.Max(w, h);

            // Breathing opacity
            float breath = 1f + 0.15f * MathF.Sin(time * 0.3f + i * 1.7f);
            byte alpha = (byte)(cloud.Alpha * breath * 255f);

            var color = new SKColor(
                (byte)(cloud.R * 255f),
                (byte)(cloud.G * 255f),
                (byte)(cloud.B * 255f),
                alpha);

            _nebulaPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(px, py), radius,
                [color, color.WithAlpha((byte)(alpha / 3)), SKColors.Transparent],
                [0f, 0.5f, 1f],
                SKShaderTileMode.Clamp
            );
            canvas.DrawCircle(px, py, radius, _nebulaPaint);
        }
    }

    // --- Starfield ---

    private void DrawStarField(SKCanvas canvas, float w, float h, float time)
    {
        for (int i = 0; i < _starField.Stars.Length; i++)
        {
            ref var star = ref _starField.Stars[i];
            float t = (time * 0.125f) / star.Period * MathF.PI * 2f + star.Phase;
            float opacity = star.Brightness * (0.15f + 0.35f * (1f + MathF.Sin(t)));
            byte alpha = (byte)(opacity * 255);
            _starPaint.Color = new SKColor(star.R, star.G, star.B, alpha);

            float radius = star.Size * 0.5f;
            float sx = star.X * w;
            float sy = star.Y * h;
            canvas.DrawCircle(sx, sy, radius, _starPaint);

            // Bright stars get a soft halo for extra sparkle
            if (star.Size > 2f && opacity > 0.5f)
            {
                byte haloAlpha = (byte)(alpha * 0.2f);
                _starPaint.Color = new SKColor(star.R, star.G, star.B, haloAlpha);
                canvas.DrawCircle(sx, sy, radius * 2.5f, _starPaint);
            }
        }
    }

    // --- Floating particles ---

    private void DrawParticles(SKCanvas canvas, float w, float h, float time)
    {
        for (int i = 0; i < _particleField.Particles.Length; i++)
        {
            ref var p = ref _particleField.Particles[i];

            // Drift with wrap-around
            float px = (p.X + p.SpeedX * time) % 1f;
            float py = (p.Y + p.SpeedY * time) % 1f;
            if (px < 0) px += 1f;
            if (py < 0) py += 1f;

            // Fade in/out
            float t = (time * 0.2f) / p.Period * MathF.PI * 2f + p.Phase;
            float alpha = 0.05f + 0.12f * (1f + MathF.Sin(t));

            _particlePaint.Color = new SKColor(p.R, p.G, p.B, (byte)(alpha * 255));
            canvas.DrawCircle(px * w, py * h, p.Size * 0.4f, _particlePaint);
        }
    }

    // --- Cosmic dust band (faint Milky Way) ---

    private void DrawCosmicDust(SKCanvas canvas, float w, float h, float time)
    {
        // A faint diagonal band of cosmic dust sweeping across the canvas
        canvas.Save();
        canvas.RotateRadians(-0.35f, w * 0.5f, h * 0.5f);

        float bandCenterY = h * 0.45f;
        float bandHeight = h * 0.35f;

        // Slow vertical drift for a living feel
        float drift = MathF.Sin(time * 0.08f) * h * 0.02f;
        bandCenterY += drift;

        _cosmicDustPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, bandCenterY - bandHeight),
            new SKPoint(0, bandCenterY + bandHeight),
            [
                SKColors.Transparent,
                new SKColor(60, 50, 90, 8),
                new SKColor(80, 60, 110, 14),
                new SKColor(50, 70, 100, 12),
                new SKColor(70, 50, 90, 8),
                SKColors.Transparent,
            ],
            [0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f],
            SKShaderTileMode.Clamp
        );

        // Draw an oversized rect to cover rotated area
        canvas.DrawRect(-w * 0.3f, bandCenterY - bandHeight, w * 1.6f, bandHeight * 2f, _cosmicDustPaint);
        canvas.Restore();
    }

    // --- Sun corona (pulsating outer haze) ---

    private void DrawSunCorona(SKCanvas canvas, float cx, float cy, float zoom, float time)
    {
        float sunSize = 22f * zoom;
        // Pulsating corona - breathes slowly
        float pulse = 1f + 0.12f * MathF.Sin(time * 0.6f) + 0.06f * MathF.Sin(time * 1.3f);
        float coronaRadius = sunSize * 5.5f * pulse;

        _sunCoronaPaint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(cx, cy), coronaRadius,
            [
                new SKColor(255, 220, 130, 22),
                new SKColor(255, 180, 80, 12),
                new SKColor(200, 140, 60, 5),
                SKColors.Transparent,
            ],
            [0f, 0.2f, 0.5f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(cx, cy, coronaRadius, _sunCoronaPaint);
    }

    // --- Sun rays (subtle light spikes) ---

    private void DrawSunRays(SKCanvas canvas, float cx, float cy, float zoom, float time)
    {
        float sunSize = 22f * zoom;
        // 6 rays at different angles, slowly rotating
        float baseAngle = time * 0.04f;

        for (int i = 0; i < 6; i++)
        {
            float angle = baseAngle + i * MathF.PI / 3f;

            // Each ray has a slightly different length that pulses
            float pulseOffset = i * 1.1f;
            float rayLength = sunSize * (2.2f + 0.6f * MathF.Sin(time * 0.5f + pulseOffset));
            byte alpha = (byte)(18 + 8 * MathF.Sin(time * 0.7f + pulseOffset));

            float endX = cx + MathF.Cos(angle) * rayLength;
            float endY = cy + MathF.Sin(angle) * rayLength;

            _sunRayPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx, cy),
                new SKPoint(endX, endY),
                [new SKColor(255, 230, 150, alpha), SKColors.Transparent],
                [0f, 1f],
                SKShaderTileMode.Clamp
            );
            _sunRayPaint.StrokeWidth = 1.5f * zoom;
            canvas.DrawLine(cx, cy, endX, endY, _sunRayPaint);
        }
        _sunRayPaint.Shader = null;
    }

    // --- Orbital trail (arc behind the planet) ---

    private void DrawOrbitalTrail(SKCanvas canvas, PlanetData planet, float time, float zoom,
        float cx, float cy, float baseRadius, int index, OrbitState state)
    {
        float orbitR = baseRadius * planet.OrbitRadius * zoom;
        float orbitRy = orbitR * 0.55f;
        float currentAngle = time * planet.Speed * 0.5f;

        // Trail length: ~90 degrees for selected, ~50 degrees for unselected
        bool isSelected = state.SelectedPlanet == index;
        float trailAngle = isSelected ? (MathF.PI / 2f) : (MathF.PI * 5f / 18f);
        int segments = 30;
        float step = trailAngle / segments;

        var color = ColorUtil.FromHex(planet.ColorHex);

        using var path = new SKPath();
        bool first = true;

        for (int s = 0; s <= segments; s++)
        {
            float angle = currentAngle - s * step;
            float x = cx + MathF.Cos(angle) * orbitR;
            float y = cy + MathF.Sin(angle) * orbitRy;

            if (first)
            {
                path.MoveTo(x, y);
                first = false;
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        // Much more visible trails
        byte trailAlpha = isSelected ? (byte)140 : (byte)80;
        float trailWidth = isSelected ? 3.5f : 2f;

        _trailPaint.StrokeWidth = trailWidth;
        _trailPaint.StrokeCap = SKStrokeCap.Round;

        // Gradient trail: bright at planet, fading behind
        float startX = cx + MathF.Cos(currentAngle) * orbitR;
        float startY = cy + MathF.Sin(currentAngle) * orbitRy;
        float endX = cx + MathF.Cos(currentAngle - trailAngle) * orbitR;
        float endY = cy + MathF.Sin(currentAngle - trailAngle) * orbitRy;

        _trailPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(startX, startY),
            new SKPoint(endX, endY),
            [color.WithAlpha(trailAlpha), color.WithAlpha((byte)(trailAlpha / 3)), SKColors.Transparent],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp
        );

        canvas.DrawPath(path, _trailPaint);
        _trailPaint.Shader = null;
    }

    // --- Sun ---

    private void DrawSunGlow(SKCanvas canvas, float cx, float cy, float zoom, bool zoomChanged)
    {
        float sunSize = 22f * zoom;
        float glowRadius = sunSize * 3f;

        if (zoomChanged || _sunGlowPaint.Shader == null)
        {
            _sunGlowPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), glowRadius,
                [
                    new SKColor(255, 209, 102, 64),
                    new SKColor(255, 180, 50, 20),
                    SKColors.Transparent
                ],
                [0f, 0.3f, 1f],
                SKShaderTileMode.Clamp
            );
        }
        canvas.DrawCircle(cx, cy, glowRadius, _sunGlowPaint);
    }

    private void DrawSunCore(SKCanvas canvas, float cx, float cy, float zoom, bool zoomChanged)
    {
        float sunSize = 22f * zoom;

        if (zoomChanged || _sunCorePaint.Shader == null)
        {
            _sunCorePaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), sunSize,
                [
                    SKColor.Parse("#fff8e7"),
                    SKColor.Parse("#ffd166"),
                    SKColor.Parse("#f4a261"),
                ],
                [0f, 0.5f, 1f],
                SKShaderTileMode.Clamp
            );
        }
        canvas.DrawCircle(cx, cy, sunSize, _sunCorePaint);
    }

    // --- Planet glow ---

    private void DrawPlanetGlow(SKCanvas canvas, float px, float py, float pr, PlanetData planet, bool isSelected, float selectionIntensity)
    {
        var color = ColorUtil.FromHex(planet.ColorHex);
        float glowMultiplier = isSelected ? (3f + selectionIntensity * 3f) : 3f;
        float glowR = pr * glowMultiplier;
        byte innerAlpha = isSelected ? (byte)(64 + selectionIntensity * 40) : (byte)64;

        _glowPaint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(px, py), glowR,
            [color.WithAlpha(innerAlpha), color.WithAlpha((byte)(innerAlpha / 4)), SKColors.Transparent],
            [0f, 0.4f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, glowR, _glowPaint);
    }

    // --- Planet body with realistic texturing ---

    private void DrawPlanetBody(SKCanvas canvas, float px, float py, float pr, PlanetData planet, float time)
    {
        var baseColor = ColorUtil.FromHex(planet.ColorHex);

        // Directional light from sun (upper-left)
        float lightOffX = -0.35f * pr;
        float lightOffY = -0.35f * pr;

        switch (planet.Name)
        {
            case "Mercury":
                DrawRockyPlanet(canvas, px, py, pr, baseColor, lightOffX, lightOffY,
                    darkenAmount: 50, highlightAmount: 25);
                break;

            case "Venus":
                DrawCloudyPlanet(canvas, px, py, pr, baseColor, lightOffX, lightOffY,
                    new SKColor(210, 185, 120), new SKColor(190, 165, 100), time);
                break;

            case "Earth":
                DrawEarth(canvas, px, py, pr, baseColor, lightOffX, lightOffY, time);
                break;

            case "Mars":
                DrawRockyPlanet(canvas, px, py, pr, baseColor, lightOffX, lightOffY,
                    darkenAmount: 45, highlightAmount: 35);
                DrawPolarCap(canvas, px, py, pr, topCap: true);
                break;

            case "Jupiter":
                DrawGasGiant(canvas, px, py, pr, baseColor, lightOffX, lightOffY,
                    bandColors: [
                        new SKColor(195, 155, 100),
                        new SKColor(220, 180, 130),
                        new SKColor(180, 140, 90),
                        new SKColor(210, 170, 120),
                        new SKColor(165, 125, 80),
                    ], time);
                break;

            case "Saturn":
                DrawGasGiant(canvas, px, py, pr, baseColor, lightOffX, lightOffY,
                    bandColors: [
                        new SKColor(215, 195, 145),
                        new SKColor(235, 215, 165),
                        new SKColor(200, 180, 130),
                        new SKColor(225, 205, 155),
                        new SKColor(190, 170, 120),
                    ], time);
                break;

            case "Uranus":
                DrawIceGiant(canvas, px, py, pr, baseColor, lightOffX, lightOffY,
                    new SKColor(100, 180, 180), new SKColor(140, 210, 210));
                break;

            case "Neptune":
                DrawIceGiant(canvas, px, py, pr, baseColor, lightOffX, lightOffY,
                    new SKColor(50, 80, 190), new SKColor(80, 120, 230));
                break;

            default:
                DrawSimplePlanet(canvas, px, py, pr, baseColor, lightOffX, lightOffY);
                break;
        }

        // Terminator shadow (dark side) - all planets get this
        DrawTerminator(canvas, px, py, pr);
    }

    private void DrawSimplePlanet(SKCanvas canvas, float px, float py, float pr,
        SKColor baseColor, float lightOffX, float lightOffY)
    {
        var highlight = ColorUtil.LightenColor(baseColor, 40);
        _planetPaint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(px + lightOffX, py + lightOffY), pr,
            [highlight, baseColor],
            [0f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, pr, _planetPaint);
    }

    private void DrawRockyPlanet(SKCanvas canvas, float px, float py, float pr,
        SKColor baseColor, float lightOffX, float lightOffY, int darkenAmount, int highlightAmount)
    {
        var highlight = ColorUtil.LightenColor(baseColor, highlightAmount);
        var shadow = new SKColor(
            (byte)Math.Max(0, baseColor.Red - darkenAmount),
            (byte)Math.Max(0, baseColor.Green - darkenAmount),
            (byte)Math.Max(0, baseColor.Blue - darkenAmount));

        // 3-stop gradient: highlight -> base -> shadow
        _planetPaint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(px + lightOffX, py + lightOffY), pr * 1.3f,
            [highlight, baseColor, shadow],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, pr, _planetPaint);
    }

    private void DrawCloudyPlanet(SKCanvas canvas, float px, float py, float pr,
        SKColor baseColor, float lightOffX, float lightOffY,
        SKColor cloudLight, SKColor cloudDark, float time)
    {
        // Base sphere
        var highlight = ColorUtil.LightenColor(baseColor, 30);
        _planetPaint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(px + lightOffX, py + lightOffY), pr * 1.3f,
            [highlight, baseColor, new SKColor(
                (byte)Math.Max(0, baseColor.Red - 40),
                (byte)Math.Max(0, baseColor.Green - 40),
                (byte)Math.Max(0, baseColor.Blue - 40))],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, pr, _planetPaint);

        // Cloud bands - horizontal sweep gradient that shifts over time
        canvas.Save();
        canvas.ClipRect(new SKRect(px - pr, py - pr, px + pr, py + pr));

        float cloudShift = time * 0.5f;
        _planetDetailPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(px - pr, py),
            new SKPoint(px + pr, py),
            [cloudLight.WithAlpha(110), cloudDark.WithAlpha(70), cloudLight.WithAlpha(110), cloudDark.WithAlpha(70)],
            [0f, 0.3f + 0.1f * MathF.Sin(cloudShift), 0.6f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, pr, _planetDetailPaint);
        canvas.Restore();
    }

    private void DrawEarth(SKCanvas canvas, float px, float py, float pr,
        SKColor baseColor, float lightOffX, float lightOffY, float time)
    {
        // Ocean base
        var oceanLight = new SKColor(60, 140, 220);
        var oceanDark = new SKColor(20, 60, 120);

        _planetPaint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(px + lightOffX, py + lightOffY), pr * 1.3f,
            [oceanLight, baseColor, oceanDark],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, pr, _planetPaint);

        // Continent-like patches (sweep gradient)
        canvas.Save();
        canvas.ClipRect(new SKRect(px - pr, py - pr, px + pr, py + pr));

        var landColor = new SKColor(80, 160, 80, 120);
        var landDark = new SKColor(50, 120, 50, 80);
        _planetDetailPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(px - pr, py - pr * 0.3f),
            new SKPoint(px + pr, py + pr * 0.3f),
            [SKColors.Transparent, landColor, landDark, SKColors.Transparent, landColor, SKColors.Transparent],
            [0f, 0.15f, 0.35f, 0.5f, 0.7f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, pr * 0.95f, _planetDetailPaint);
        canvas.Restore();

        // Subtle polar caps
        DrawPolarCap(canvas, px, py, pr, topCap: true);
        DrawPolarCap(canvas, px, py, pr, topCap: false);
    }

    private void DrawGasGiant(SKCanvas canvas, float px, float py, float pr,
        SKColor baseColor, float lightOffX, float lightOffY,
        SKColor[] bandColors, float time)
    {
        // Base sphere
        var highlight = ColorUtil.LightenColor(baseColor, 30);
        var shadow = new SKColor(
            (byte)Math.Max(0, baseColor.Red - 50),
            (byte)Math.Max(0, baseColor.Green - 50),
            (byte)Math.Max(0, baseColor.Blue - 50));

        _planetPaint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(px + lightOffX, py + lightOffY), pr * 1.3f,
            [highlight, baseColor, shadow],
            [0f, 0.45f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, pr, _planetPaint);

        // Horizontal bands
        canvas.Save();
        canvas.ClipRect(new SKRect(px - pr, py - pr, px + pr, py + pr));

        // Build band color array with transparency
        var bandColorsAlpha = new SKColor[bandColors.Length];
        var bandPositions = new float[bandColors.Length];
        for (int b = 0; b < bandColors.Length; b++)
        {
            bandColorsAlpha[b] = bandColors[b].WithAlpha(120);
            bandPositions[b] = (float)b / (bandColors.Length - 1);
        }

        _planetDetailPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(px, py - pr),
            new SKPoint(px, py + pr),
            bandColorsAlpha,
            bandPositions,
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, pr, _planetDetailPaint);
        canvas.Restore();
    }

    private void DrawIceGiant(SKCanvas canvas, float px, float py, float pr,
        SKColor baseColor, float lightOffX, float lightOffY,
        SKColor deepColor, SKColor surfaceColor)
    {
        // Smooth gradient from surface to deep
        _planetPaint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(px + lightOffX, py + lightOffY), pr * 1.4f,
            [surfaceColor, baseColor, deepColor],
            [0f, 0.4f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, pr, _planetPaint);

        // Subtle haze band
        canvas.Save();
        canvas.ClipRect(new SKRect(px - pr, py - pr, px + pr, py + pr));

        _planetDetailPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(px, py - pr),
            new SKPoint(px, py + pr),
            [SKColors.Transparent, surfaceColor.WithAlpha(80), SKColors.Transparent,
             surfaceColor.WithAlpha(55), SKColors.Transparent],
            [0f, 0.25f, 0.5f, 0.75f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, pr * 0.95f, _planetDetailPaint);
        canvas.Restore();
    }

    private void DrawPolarCap(SKCanvas canvas, float px, float py, float pr, bool topCap)
    {
        float capY = topCap ? py - pr * 0.75f : py + pr * 0.75f;
        float capR = pr * 0.35f;

        canvas.Save();
        // Clip to planet circle
        using var clipPath = new SKPath();
        clipPath.AddCircle(px, py, pr);
        canvas.ClipPath(clipPath);

        _planetDetailPaint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(px, capY), capR,
            [new SKColor(255, 255, 255, 110), SKColors.Transparent],
            [0f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, capY, capR, _planetDetailPaint);
        canvas.Restore();
    }

    private void DrawTerminator(SKCanvas canvas, float px, float py, float pr)
    {
        // Shadow on the right side (away from sun)
        _terminatorPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(px - pr * 0.2f, py),
            new SKPoint(px + pr, py),
            [SKColors.Transparent, new SKColor(0, 0, 0, 120)],
            [0f, 1f],
            SKShaderTileMode.Clamp
        );
        canvas.DrawCircle(px, py, pr, _terminatorPaint);
    }

    // --- Saturn ring ---

    private void DrawSaturnRing(SKCanvas canvas, float px, float py, float pr, PlanetData planet, float zoom)
    {
        var color = ColorUtil.FromHex(planet.ColorHex);

        canvas.Save();
        canvas.RotateRadians(-0.2f, px, py);

        // Outer ring
        _saturnRingPaint.Color = color.WithAlpha(90);
        _saturnRingPaint.StrokeWidth = zoom > 1.5f ? 3f : 1.8f;
        canvas.DrawOval(px, py, pr * 2.4f, pr * 0.55f, _saturnRingPaint);

        // Inner ring (slightly different shade)
        _saturnRingPaint2.Color = color.WithAlpha(60);
        _saturnRingPaint2.StrokeWidth = zoom > 1.5f ? 2f : 1.2f;
        canvas.DrawOval(px, py, pr * 1.8f, pr * 0.4f, _saturnRingPaint2);

        canvas.Restore();
    }

    // --- Label occlusion ---

    private static bool IsLabelOccluded(int planetIndex, float labelX, float labelY,
        Span<float> planetXs, Span<float> planetYs, Span<float> planetRs)
    {
        // Check if the label position overlaps any other planet's body
        for (int j = 0; j < planetXs.Length; j++)
        {
            if (j == planetIndex) continue;
            float dx = labelX - planetXs[j];
            float dy = labelY - planetYs[j];
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            // Occluded if label center is inside another planet's radius (with margin)
            if (dist < planetRs[j] + 4f)
                return true;
        }
        return false;
    }

    // --- Label ---

    private void DrawPlanetLabel(SKCanvas canvas, float px, float py, float pr, PlanetData planet, OrbitState state, int index)
    {
        float fontSize = MathF.Max(10.4f, 11.5f * MathF.Min(state.Zoom * 0.7f, 1.2f));
        _labelFont.Size = fontSize;
        _labelPaint.Color = (state.HoveredPlanet == index || state.SelectedPlanet == index)
            ? new SKColor(255, 255, 255, 204)
            : new SKColor(255, 255, 255, 89);

        canvas.DrawText(planet.Name, px, py + pr + 14f, SKTextAlign.Center, _labelFont, _labelPaint);
    }

    public void Dispose()
    {
        _starPaint.Dispose();
        _nebulaPaint.Dispose();
        _particlePaint.Dispose();
        _cosmicDustPaint.Dispose();
        _orbitPaint.Dispose();
        _trailPaint.Dispose();
        _sunGlowPaint.Dispose();
        _sunCoronaPaint.Dispose();
        _sunRayPaint.Dispose();
        _sunCorePaint.Dispose();
        _planetPaint.Dispose();
        _planetDetailPaint.Dispose();
        _terminatorPaint.Dispose();
        _glowPaint.Dispose();
        _saturnRingPaint.Dispose();
        _saturnRingPaint2.Dispose();
        _labelPaint.Dispose();
        _labelFont.Dispose();
    }
}
