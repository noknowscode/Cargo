# UnoOrbit

A single-screen interactive solar system built with Uno Platform. Eight planets orbit the sun in real time; click a planet to zoom to it and open a data panel. It is a C#/XAML rebuild of an HTML prototype, specified by the PRD and briefs in this folder.

## What's in it

- **Orbit view** drawn every frame with SkiaSharp (`Rendering/OrbitRenderer.cs`): sun with glow, corona and rays, orbit rings and trails, textured planets (rocky, cloudy, Earth, gas and ice giants, Saturn's ring), plus a starfield, nebula fog, particles and a dust band.
- **Planet detail**: selecting a planet animates zoom and pan to center it, shows an atmosphere glow behind it, and slides up a bottom panel (`Components/PlanetPanel`) with type, stats grid and a fun fact. Tap the panel handle or press Escape to return to the system view.
- **Planet nav** dots (`Components/PlanetNav`) to step between planets while one is selected.
- **Zoom** with mouse wheel, pinch, `+`/`-` keys, or the zoom buttons (`Components/ZoomIndicator`).
- **Keyboard**: Left/Right to change planet, Escape to deselect.
- Hover tooltip with the planet name, and an orbit hint that fades out.
- Planet data for Mercury to Neptune is hardcoded in `Models/PlanetData.cs`.

## Tech

- Uno Platform single project, Uno.Sdk 6.5.31, .NET 10.
- Targets: `net10.0-desktop`, `net10.0-browserwasm`, `net10.0-android`, `net10.0-ios`. Skia renderer.
- Rendering on an `SKCanvasElement` subclass; UI overlays are XAML user controls.
- No MVVM/MVUX or DI: page code-behind drives a plain `State/OrbitState.cs` class. Easing curves in `Helpers/EasingCurves.cs`.
- Fonts: Syne and Space Mono (`Assets/Fonts/`).

## Run it

From the repo root:

```bash
dotnet run --project UnoOrbit/UnoOrbit/UnoOrbit.csproj -f net10.0-desktop
dotnet run --project UnoOrbit/UnoOrbit/UnoOrbit.csproj -f net10.0-browserwasm
```

## Docs

- `uno-orbit-prd-v1.md` - behavior, visual layers, planet data.
- `uno-orbit-design-brief-v1.md` - visual design.
- `uno-orbit-architecture-brief-v1.md` - project structure and rendering plan.

## Status

Prototype. The single screen and its interactions are implemented in one initial commit; there are no tests.
