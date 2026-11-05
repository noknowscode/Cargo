# Uno Orbit — Product Requirements Document v1

**Purpose:** Faithful rebuild of the Uno Orbit HTML prototype as an Uno Platform (C# + XAML) application.  
**Reference artifact:** `uno-orbit-prototype.html` (845 lines, self-contained HTML/CSS/JS).  
**Fidelity target:** Pixel-match visual output, interaction-match behavior. No creative additions.

---

## 1. Product Summary

Uno Orbit is a single-screen interactive solar system visualization. Eight planets orbit a central sun in real time. Users explore by clicking planets (which triggers an animated zoom + pan to center the planet and reveals a data panel from the bottom), scrolling to zoom, and navigating between planets via dot indicators or keyboard arrows. The app has exactly two states: **System View** (all planets visible, no panel) and **Planet Detail** (zoomed on one planet, panel open).

---

## 2. Visual Layers (z-order, bottom to top)

| z-index | Layer | Rendering | Notes |
|---------|-------|-----------|-------|
| 0 | Starfield | 200 static DOM elements (circles) with twinkle animation | Not on canvas; separate container |
| 0 | Atmosphere Glow | Single 300×300 blurred circle, positioned at selected planet | `filter: blur(60px)`, opacity 0→1 on select |
| 1 | Orbit Canvas | Full-viewport `<canvas>` at `devicePixelRatio` | All sun, orbits, planets, labels drawn here |
| 10 | UI Overlay | XAML layer, `IsHitTestVisible=false` except children | Contains top bar |
| 15 | Orbit Hint | Text, auto-fades after 5s | One-shot animation |
| 20 | Planet Panel | Bottom sheet, slides up | Translucent gradient + blur backdrop |
| 25 | Planet Nav | Dot row, bottom-center | Only visible when planet selected |
| 30 | Tooltip | Positioned label following cursor | Only visible on hover, no planet selected |

---

## 3. Data Model

### 3.1 Planet Record (8 items, baked in)

| Field | Type | Example (Earth) |
|-------|------|-----------------|
| `Name` | string | `"Earth"` |
| `Type` | string | `"Terrestrial Planet"` |
| `Color` | hex string | `"#4da6ff"` |
| `OrbitRadius` | float (0–1 normalized) | `0.20` |
| `Size` | float (px at zoom=1) | `8` |
| `Speed` | float (angular velocity multiplier) | `1.0` |
| `Diameter` | string | `"12,742 km"` |
| `Mass` | string | `"5.97 × 10²⁴ kg"` |
| `Distance` | string | `"149.6M km"` |
| `DayLength` | string | `"24 hours"` |
| `Gravity` | string | `"9.8 m/s²"` |
| `Temperature` | string | `"15°C avg"` |
| `Atmosphere` | string | `"78% N₂, 21% O₂"` |
| `AtmosphereColor` | rgba string | `"rgba(77,166,255,0.12)"` |
| `Fact` | string | `"Earth is the only planet not named after a Greek or Roman god."` |
| `FactIcon` | string (emoji) | `"🌍"` |

Complete data for all 8 planets (Mercury through Neptune) in section 3.2 below.

### 3.2 Complete Planet Table

```
Mercury:  #b0a090, orbit=0.08, size=4,  speed=4.15,  type="Terrestrial Planet"
Venus:    #e6c88a, orbit=0.14, size=7,  speed=1.62,  type="Terrestrial Planet"
Earth:    #4da6ff, orbit=0.20, size=8,  speed=1.0,   type="Terrestrial Planet"
Mars:     #e07040, orbit=0.27, size=6,  speed=0.53,  type="Terrestrial Planet"
Jupiter:  #d4a574, orbit=0.38, size=18, speed=0.084, type="Gas Giant"
Saturn:   #e8d5a0, orbit=0.50, size=15, speed=0.034, type="Gas Giant"
Uranus:   #7ec8c8, orbit=0.63, size=11, speed=0.012, type="Ice Giant"
Neptune:  #4466dd, orbit=0.76, size=10, speed=0.006, type="Ice Giant"
```

Full stat strings, facts, and atmosphere data are in the prototype source lines 401–465. Copy verbatim.

---

## 4. Application State

```
State {
  zoom: float           // current interpolated zoom (init: 1.0)
  targetZoom: float     // target zoom being interpolated toward (init: 1.0)
  minZoom: 0.6
  maxZoom: 4.5
  panX: float           // current interpolated X offset (init: 0)
  panY: float           // current interpolated Y offset (init: 0)
  targetPanX: float     // target pan X (init: 0)
  targetPanY: float     // target pan Y (init: 0)
  selectedPlanet: int?  // null = System View, 0–7 = Planet Detail
  hoveredPlanet: int?   // null = nothing hovered, 0–7 = hovered
  time: float           // monotonically increasing (init: 0, +0.008 per frame)
}
```

**State transitions (exhaustive):**

| From | Trigger | To | Side Effects |
|------|---------|-----|-------------|
| System View | Click planet | Planet Detail | `targetZoom=2.5`, pan to center planet, show panel, show nav, show atmosphere glow, hide hint |
| System View | Scroll wheel / pinch | System View | `targetZoom` ± 0.15 per wheel tick, clamped [0.6, 4.5] |
| System View | +/− buttons | System View | `targetZoom` ± 0.3, clamped |
| System View | Hover planet | System View | `hoveredPlanet` set, cursor→pointer, show tooltip |
| System View | Hover empty | System View | `hoveredPlanet` null, cursor→grab, hide tooltip |
| Planet Detail | Click empty space | System View | `targetZoom=1`, pan to 0,0, hide panel, hide nav, hide glow |
| Planet Detail | Press Escape | System View | Same as click empty |
| Planet Detail | Click panel handle | System View | Same as click empty |
| Planet Detail | Click nav dot[i] | Planet Detail (planet i) | Re-target zoom+pan, refresh panel content |
| Planet Detail | Arrow ← | Planet Detail (prev) | Wrap: index −1 mod 8 |
| Planet Detail | Arrow → | Planet Detail (next) | Wrap: index +1 mod 8 |
| Planet Detail | Click another planet on canvas | Planet Detail (that planet) | Re-target zoom+pan, refresh panel |
| Any | Keyboard +/= | Zoom in | `targetZoom` + 0.3 |
| Any | Keyboard −/_ | Zoom out | `targetZoom` − 0.3 |

---

## 5. Rendering Specifications

### 5.1 Coordinate System

- **Center:** `(viewportWidth/2 + panX, viewportHeight/2 + panY)`
- **Base radius:** `min(viewportWidth, viewportHeight) × 0.42`
- **Per-planet orbit radius:** `baseRadius × planet.OrbitRadius × zoom`
- **Planet screen position:**
  ```
  angle = time × planet.Speed × 0.5
  x = centerX + cos(angle) × orbitRadius
  y = centerY + sin(angle) × orbitRadius × 0.55   // elliptical flattening
  ```
- **Planet rendered size:** `planet.Size × zoom × 0.7` pixels

### 5.2 Sun

- **Core:** radial gradient from `#fff8e7` (center) → `#ffd166` (50%) → `#f4a261` (edge)
- **Core radius:** `22 × zoom` pixels
- **Glow:** radial gradient from `rgba(255,209,102,0.25)` → `rgba(255,180,50,0.08)` at 30% → transparent
- **Glow radius:** `sunSize × 3`
- Positioned at center point

### 5.3 Orbit Paths

- Ellipses: `(centerX, centerY, orbitRadius, orbitRadius × 0.55)`
- Default: `rgba(255,255,255,0.04)`, stroke width 0.5
- Selected planet orbit: `rgba(255,255,255,0.12)`, stroke width 1.5

### 5.4 Planet Bodies

- Radial gradient with specular highlight: light source at `(−0.3 × size, −0.3 × size)` relative to center
- Highlight color: planet.Color + 40 brightness per channel (clamped at 255)
- Base color: planet.Color
- **Saturn special case:** additional ellipse ring at `(2.2 × size, 0.5 × size)`, rotation −0.2 radians, stroke color `planet.Color + "80"`, width `zoom > 1.5 ? 2.5 : 1.5`

### 5.5 Planet Hover/Select Glow

- Radial gradient from `planet.Color + "40"` at planet radius → transparent at `3 × planet radius`
- Only drawn when `hoveredPlanet === i` or `selectedPlanet === i`

### 5.6 Planet Labels

- Visible when: `zoom > 1.2` OR planet is hovered OR planet is selected
- Font: Syne, size = `max(9, 10 × min(zoom × 0.7, 1.2))` px
- Color: `rgba(255,255,255,0.8)` if hovered/selected, `rgba(255,255,255,0.35)` otherwise
- Position: centered below planet, offset = `planetSize + 14` px down

### 5.7 Hit Testing

- Iterate planets in reverse order (Neptune first → Mercury last, so visually-topmost gets priority)
- Hit radius: `max(planetRenderedSize + 8, 15)` px
- Test: `(mouseX − planetX)² + (mouseY − planetY)² < hitRadius²`

---

## 6. Animation Specifications

### 6.1 Main Loop (per frame, ~60fps)

```
time += 0.008
zoom += (targetZoom − zoom) × 0.08      // lerp factor 0.08
panX += (targetPanX − panX) × 0.06      // lerp factor 0.06
panY += (targetPanY − panY) × 0.06
```

These are exponential ease-out interpolations running every frame. Not tween-based — they asymptotically approach the target.

### 6.2 Panel Slide

- Transition: `transform: translateY(100%) → translateY(0)`
- Duration: 600ms
- Easing: `cubic-bezier(0.4, 0, 0.2, 1)` (Material decelerate)

### 6.3 Stat Card Stagger

- Each of 6 stat cards: `translateY(15px), opacity 0 → translateY(0), opacity 1`
- Duration: 500ms
- Easing: `cubic-bezier(0.34, 1.56, 0.64, 1)` (spring overshoot)
- Stagger delays: 100ms, 150ms, 200ms, 250ms, 300ms, 350ms

### 6.4 Fun Fact Entrance

- Same as stat cards but delay = 400ms

### 6.5 Atmosphere Glow

- Opacity: 0 → 1 over 800ms ease
- Follows selected planet position each frame (set via left/top in main loop)

### 6.6 Planet Nav Dots

- Container: opacity 0 → 1 over 400ms ease
- Active dot: `scale(1.4)`, border-color white, spring easing
- Hover dot: `scale(1.3)`, spring easing

### 6.7 Tooltip

- Opacity: 0 → 1 over 200ms ease
- Positioned at `(mouseX + 15, mouseY − 10)`
- Hidden when any planet is selected

### 6.8 Hint Text

- Keyframe: 0% opacity 0 → 15% opacity 0.4 → 85% opacity 0.4 → 100% opacity 0
- Total duration: 5s, starts after 1.5s delay
- One-shot (never replays)
- Permanently hidden once a planet is selected

### 6.9 Starfield Twinkle

- Per star: alternate between opacity 0.1 and 0.7
- Duration: random 2–6s per star
- Easing: ease-in-out
- Delay: random 0–3s per star
- Loops infinitely

---

## 7. UI Components (XAML overlay)

### 7.1 Top Bar

- Left: "UNO ORBIT" — Syne 600, 0.6rem, letter-spacing 0.35em, uppercase, opacity 0.4
- Right: Zoom indicator pill — border-radius 100px, `rgba(255,255,255,0.04)` bg, 1px `rgba(255,255,255,0.06)` border, blur(20px) backdrop
  - "+" button: 28×28, radius 8, `rgba(255,255,255,0.06)` bg, `rgba(255,255,255,0.1)` border
  - Zoom level: Space Mono 400, 0.65rem, opacity 0.5, format `{zoom:F1}×`
  - "−" button: same as +

### 7.2 Planet Panel (Bottom Sheet)

- Backdrop: `linear-gradient(to top, rgba(4,6,14,0.97) 60%, transparent)` + blur(30px)
- Handle: 36×3px, radius 2px, `rgba(255,255,255,0.2)`, centered, 1rem padding top/bottom
- Max height: 55vh, scrollable (hidden scrollbar)
- Content padding: 0 2rem 2.5rem

**Panel contents (per planet):**
- Planet name: Syne 800, clamp(2rem, 6vw, 3.2rem), color = planet.Color, letter-spacing −0.03em
- Type line: Syne 500, 0.65rem, letter-spacing 0.2em, uppercase, opacity 0.35. Format: `"{Type} · {Atmosphere}"`
- Stats grid: 3 columns, 0.75rem gap
  - 6 stat cards: 1rem padding, radius 16px, `rgba(255,255,255,0.03)` bg, `rgba(255,255,255,0.05)` border
    - Value: Space Mono 700, clamp(1rem, 2.5vw, 1.3rem), color = planet.Color
    - Label: Syne 500, 0.55rem, letter-spacing 0.15em, uppercase, opacity 0.35
  - Cards in order: Diameter, Mass, Distance from Sun, Day Length, Gravity, Avg Temperature
- Fun fact: radius 14px, same card style, flex row with 0.8rem gap
  - Icon: 1.2rem font size
  - Text: 0.8rem, line-height 1.5, weight 400, opacity 0.7

### 7.3 Planet Nav Dots

- 8 dots, bottom-center, 0.4rem gap, 0.8rem 1rem padding
- Each: 8×8px, radius 50%, 1.5px border
- Inactive: transparent bg, border color = planet.Color
- Active: bg = planet.Color, border-color = white
- Container hidden (opacity 0) in System View, visible (opacity 1) in Planet Detail

### 7.4 Tooltip

- Radius 8px, `rgba(20,20,30,0.9)` bg, 1px `rgba(255,255,255,0.1)` border, blur(10px)
- Syne 500, 0.7rem
- No pointer events

---

## 8. Typography

| Usage | Family | Weight | Size |
|-------|--------|--------|------|
| Body / UI | Syne | 400–800 | varies |
| Zoom level, stat values | Space Mono | 400, 700 | 0.65rem, clamp(1rem–1.3rem) |
| Planet labels (canvas) | Syne | 400 | dynamic (see 5.6) |

---

## 9. Color Tokens

| Token | Value |
|-------|-------|
| `--bg` | `#04060e` |
| `--surface` | `rgba(255,255,255,0.04)` |
| `--surface-border` | `rgba(255,255,255,0.06)` |
| `--text` | `#e8e6f0` |
| `--text-dim` | `rgba(232,230,240,0.35)` |
| `--sun-color` | `#ffd166` |
| `--sun-glow` | `rgba(255,209,102,0.15)` |

---

## 10. Input Handling

| Input | Platform | Behavior |
|-------|----------|----------|
| Mouse move | Desktop | Hit-test planets → update hoveredPlanet, cursor style, tooltip position |
| Mouse click on planet | Desktop | `selectPlanetByIndex(i)` |
| Mouse click on empty | Desktop | `deselectPlanet()` (only if in Planet Detail) |
| Scroll wheel | Desktop | `targetZoom ± 0.15` per delta unit, clamped |
| Touch tap on planet | Mobile | `selectPlanetByIndex(i)` |
| Pinch (2-finger) | Mobile | Scale delta × 0.008 added to `targetZoom`, clamped |
| Keyboard Escape | All | `deselectPlanet()` |
| Keyboard ←/→ | All | Cycle selected planet (wrapping) |
| Keyboard +/− | All | `targetZoom ± 0.3`, clamped |

---

## 11. Platform Targets

Must run on: Windows (WinAppSDK), WebAssembly, iOS, Android.  
Canvas rendering must use SkiaSharp `SKCanvasView` (Uno's cross-platform canvas).

---

## 12. Acceptance Criteria

1. **8 planets orbit the sun** at correct relative speeds with elliptical paths (Y flattening = 0.55×).
2. **Sun renders** with multi-stop radial gradient core + outer glow, both scaling with zoom.
3. **Saturn has a visible ring** rendered as a tilted ellipse stroke.
4. **Hover** shows tooltip and planet glow. Cursor changes to pointer over planets, grab elsewhere.
5. **Click planet** smoothly zooms to 2.5× and pans to center the planet over ~1 second (lerp-based, not duration-based).
6. **Panel slides up** from bottom with 600ms Material decelerate easing.
7. **6 stat cards** stagger in with 50ms increments starting at 100ms, using spring overshoot easing.
8. **Fun fact** appears 400ms after panel opens with same spring easing.
9. **Planet nav dots** appear at bottom, active dot filled with planet color, clicking switches planet.
10. **Arrow keys ←/→** cycle planets with wrapping.
11. **Escape** or clicking empty space returns to System View: zoom→1, pan→0,0, panel closes, nav hides, glow fades.
12. **Scroll wheel** zooms smoothly (lerp-based).
13. **+/− buttons** zoom in/out with zoom level displayed as `{n}×`.
14. **200 stars** twinkle independently in the background.
15. **Hint text** fades in after 1.5s, holds for ~3.5s, fades out. Never replays after planet selection.
16. **All typography** uses Syne (UI) and Space Mono (data values) exactly as specified.
17. **No content, layout, or interaction** exists beyond what is described in this document.

---

*End of PRD v1*
