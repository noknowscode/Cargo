# Uno Orbit — Design Brief v1

**Parent document:** Uno Orbit PRD v1  
**Purpose:** Provide every visual and motion decision a developer needs to implement the UI without opening the prototype. This is the CSS/XAML translation layer.

---

## 1. Global Visual Rules

### 1.1 Background & Canvas

The entire app is one fixed viewport. No scrolling at the page level. Background color `#04060e` fills the window. All content layers are `position: fixed` equivalents (full-window overlays).

### 1.2 Font Loading

Two font families required. Both are Google Fonts.

| Font | Weights Used | Fallback |
|------|-------------|----------|
| **Syne** | 400, 500, 600, 700, 800 | `sans-serif` |
| **Space Mono** | 400, 700 | `monospace` |

Embed as app resources. Do not rely on web loading.

### 1.3 No Scrollbars

The planet panel is the only scrollable region. Its scrollbar must be hidden on all platforms (`scrollbar-width: none` / `::-webkit-scrollbar { display: none }`). In XAML: `ScrollViewer.VerticalScrollBarVisibility="Hidden"`.

### 1.4 No Text Selection

Entire app is non-selectable (`IsTextSelectionEnabled="False"` on all text). This is a visualization, not a document.

---

## 2. Layer-by-Layer Visual Specification

### 2.1 Layer 0: Starfield

**What it is:** 200 small circles scattered randomly across the full viewport.

**Per-star properties (randomized at startup, never change after):**

| Property | Range |
|----------|-------|
| X position | 0–100% of viewport width |
| Y position | 0–100% of viewport height |
| Diameter | 0.5–2.5 px |
| Base opacity | 0.1–0.7 |
| Twinkle period | 2–6 seconds |
| Twinkle delay | 0–3 seconds |

**Twinkle animation:** Each star alternates between opacity 0.1 and opacity 0.7, ease-in-out, looping infinitely. Stars are white (`#FFFFFF`). Stars are DOM elements, not canvas-drawn, so they sit behind the canvas and don't need redrawing each frame.

**XAML equivalent:** 200 `Ellipse` elements inside a `Canvas` that fills the window. Each gets an independent `DoubleAnimation` on `Opacity` with `AutoReverse="True"` and `RepeatBehavior="Forever"`.

---

### 2.2 Layer 0: Atmosphere Glow

**What it is:** A single 300×300 dp soft circle that follows the selected planet's screen position.

| Property | Value |
|----------|-------|
| Size | 300 × 300 dp |
| Shape | Circle (border-radius 50%) |
| Blur | 60 dp Gaussian blur |
| Color | Selected planet's `AtmosphereColor` with alpha boosted from ~0.1 to ~0.3 |
| Default state | Opacity 0, no position |
| Selected state | Opacity 1, centered on selected planet's screen coords |
| Transition | Opacity: 800ms ease; Background color: 800ms ease |
| Transform | `translate(-50%, -50%)` — anchor is center of element |

**Position update:** Every frame in the main loop, re-read the selected planet's current screen position and set `left` / `top` (or `Canvas.Left` / `Canvas.Top`). This makes the glow track the orbiting planet.

**XAML equivalent:** A `Border` with `CornerRadius="150"` and a `SolidColorBrush` fill, placed in a `Canvas`. Apply a `BlurEffect` or use `Compositor` blur. Animate opacity with `DoubleAnimation`.

---

### 2.3 Layer 1: Orbit Canvas (SkiaSharp)

This is the core rendering surface. Everything below is drawn per-frame on a single `SKCanvasView`.

#### 2.3.1 Canvas Setup

- Canvas size: `viewportWidth × devicePixelRatio` by `viewportHeight × devicePixelRatio`
- Display size: `viewportWidth × viewportHeight`
- Call `canvas.Scale(dpr, dpr)` at frame start, then draw in logical pixels
- Clear to transparent each frame (background handled by app background)

#### 2.3.2 Sun — Outer Glow

| Property | Value |
|----------|-------|
| Type | Filled circle with radial gradient |
| Center | `(centerX, centerY)` |
| Radius | `sunCoreSize × 3` where `sunCoreSize = 22 × zoom` |
| Gradient stop 0 (center) | `rgba(255, 209, 102, 0.25)` |
| Gradient stop 0.3 | `rgba(255, 180, 50, 0.08)` |
| Gradient stop 1.0 (edge) | `transparent` |

#### 2.3.3 Sun — Core

| Property | Value |
|----------|-------|
| Type | Filled circle with radial gradient |
| Center | `(centerX, centerY)` |
| Radius | `22 × zoom` |
| Gradient stop 0 (center) | `#fff8e7` |
| Gradient stop 0.5 | `#ffd166` |
| Gradient stop 1.0 (edge) | `#f4a261` |

#### 2.3.4 Orbit Paths (×8)

| Property | Default | Selected |
|----------|---------|----------|
| Shape | Ellipse | Ellipse |
| Center | `(centerX, centerY)` | same |
| Semi-major axis (horizontal) | `baseRadius × planet.OrbitRadius × zoom` | same |
| Semi-minor axis (vertical) | semi-major × 0.55 | same |
| Stroke color | `rgba(255,255,255,0.04)` | `rgba(255,255,255,0.12)` |
| Stroke width | 0.5 | 1.5 |
| Fill | none | none |

#### 2.3.5 Planet Hover/Select Glow

Only drawn when `hoveredPlanet === i || selectedPlanet === i`.

| Property | Value |
|----------|-------|
| Type | Filled circle with radial gradient |
| Center | planet screen position |
| Inner radius | planet rendered size (`ps`) |
| Outer radius | `ps × 3` |
| Gradient stop 0 | `{planet.Color}40` (25% alpha) |
| Gradient stop 1 | `transparent` |

#### 2.3.6 Planet Body (×8)

| Property | Value |
|----------|-------|
| Type | Filled circle with radial gradient (specular highlight) |
| Center | planet screen position |
| Radius | `planet.Size × zoom × 0.7` |
| Gradient center (light source) | offset `(-0.3 × radius, -0.3 × radius)` from planet center |
| Gradient inner radius | 0 |
| Gradient outer radius | planet radius |
| Gradient stop 0 (highlight) | `lighten(planet.Color, 40)` — add 40 to each RGB channel, clamp 255 |
| Gradient stop 1 (base) | `planet.Color` |

#### 2.3.7 Saturn Ring

Only drawn for the planet named "Saturn".

| Property | Value |
|----------|-------|
| Shape | Ellipse (stroked, not filled) |
| Center | Saturn's screen position |
| Semi-major axis | `saturnRadius × 2.2` |
| Semi-minor axis | `saturnRadius × 0.5` |
| Rotation | −0.2 radians (≈ −11.5°) |
| Stroke color | `{saturn.Color}80` (50% alpha hex) → `#e8d5a080` |
| Stroke width | `zoom > 1.5 ? 2.5 : 1.5` |

#### 2.3.8 Planet Labels

| Property | Value |
|----------|-------|
| Visibility | `zoom > 1.2` OR `hoveredPlanet === i` OR `selectedPlanet === i` |
| Font family | Syne |
| Font size | `max(9, 10 × min(zoom × 0.7, 1.2))` px |
| Color (hovered/selected) | `rgba(255,255,255,0.8)` |
| Color (default) | `rgba(255,255,255,0.35)` |
| Alignment | Centered horizontally on planet |
| Vertical position | planet center Y + planet radius + 14 px |

---

### 2.4 Layer 10: Top Bar

Layout: horizontal `SpaceBetween`, padding `24dp 32dp` (1.5rem 2rem at 16px base).

#### 2.4.1 Logo Text

| Property | Value |
|----------|-------|
| Content | `"UNO ORBIT"` |
| Font | Syne 600 |
| Size | 9.6 dp (0.6rem) |
| Letter spacing | 0.35em (3.36dp at this size) |
| Case | Uppercase |
| Color | `#e8e6f0` at 40% opacity |

#### 2.4.2 Zoom Indicator Pill

| Property | Value |
|----------|-------|
| Layout | Horizontal, gap 12dp, padding `6.4dp 16dp` |
| Corner radius | 100 (fully rounded) |
| Background | `rgba(255,255,255,0.04)` |
| Border | 1px `rgba(255,255,255,0.06)` |
| Backdrop blur | 20dp |

**Zoom button (×2, identical except label):**

| Property | Value |
|----------|-------|
| Size | 28 × 28 dp |
| Corner radius | 8 dp |
| Background (rest) | `rgba(255,255,255,0.06)` |
| Background (hover) | `rgba(255,255,255,0.12)` |
| Border | 1px `rgba(255,255,255,0.1)` |
| Label | `"+"` or `"−"` (not ASCII minus, use proper minus `−` U+2212) |
| Label font | Syne 300, 1rem (16dp) |
| Label color | `#e8e6f0` |
| Hover transition | 200ms ease |

**Zoom level text:**

| Property | Value |
|----------|-------|
| Font | Space Mono 400 |
| Size | 10.4dp (0.65rem) |
| Color | `#e8e6f0` at 50% opacity |
| Min width | 30dp |
| Alignment | Center |
| Format | `"{zoom:F1}×"` e.g. `"1.0×"`, `"2.5×"` |
| Update | Every frame from interpolated zoom value |

---

### 2.5 Layer 15: Hint Text

| Property | Value |
|----------|-------|
| Content | `"Click a planet to explore · Scroll to zoom"` |
| Font | Syne 400, 9.6dp (0.6rem) |
| Letter spacing | 0.2em |
| Case | Uppercase |
| Position | Fixed bottom 3rem (48dp), horizontally centered |
| Animation | Keyframe: 0%→opacity 0, 15%→0.4, 85%→0.4, 100%→0. Duration 5s, delay 1.5s, plays once |
| Removal | Set `display: none` (or `Visibility.Collapsed`) on first planet selection. Never shown again. |

---

### 2.6 Layer 20: Planet Panel (Bottom Sheet)

#### 2.6.1 Panel Container

| Property | Closed | Open |
|----------|--------|------|
| Position | Fixed, bottom 0, full width | same |
| Transform | `translateY(100%)` | `translateY(0)` |
| Transition | 600ms `cubic-bezier(0.4, 0, 0.2, 1)` | same |

#### 2.6.2 Panel Backdrop

| Property | Value |
|----------|-------|
| Position | Absolute fill of panel |
| Background | `linear-gradient(to top, rgba(4,6,14,0.97) 60%, transparent)` |
| Blur | 30dp backdrop blur |

In XAML: Use a `Border` with a `LinearGradientBrush` (start point bottom, end point top) and an `AcrylicBrush` or `Compositor` blur. The 60% stop means: at 60% from the bottom the gradient is fully `rgba(4,6,14,0.97)`, above that it fades to transparent.

#### 2.6.3 Panel Handle

| Property | Value |
|----------|-------|
| Container | Centered, padding 16dp top/bottom |
| Bar | 36 × 3 dp, corner radius 2dp, `rgba(255,255,255,0.2)` |
| Tap action | `deselectPlanet()` (close panel, return to System View) |

#### 2.6.4 Panel Content Area

| Property | Value |
|----------|-------|
| Padding | 0 32dp 40dp (0 2rem 2.5rem) |
| Max height | 55% of viewport height |
| Scroll | Vertical, scrollbar hidden |

#### 2.6.5 Planet Name

| Property | Value |
|----------|-------|
| Font | Syne 800 |
| Size | `clamp(32dp, 6vw, 51.2dp)` |
| Color | `planet.Color` (dynamic per planet) |
| Letter spacing | −0.03em |
| Line height | 1.0 |
| Bottom margin | 4.8dp (0.3rem) |

#### 2.6.6 Planet Type Line

| Property | Value |
|----------|-------|
| Content | `"{planet.Type} · {planet.Atmosphere}"` |
| Font | Syne 500, 10.4dp (0.65rem) |
| Letter spacing | 0.2em |
| Case | Uppercase |
| Color | `#e8e6f0` at 35% opacity |
| Bottom margin | 24dp (1.5rem) |

#### 2.6.7 Stats Grid

| Property | Value |
|----------|-------|
| Layout | 3 columns, equal width |
| Gap | 12dp (0.75rem) |
| Bottom margin | 24dp |

#### 2.6.8 Stat Card (×6)

| Property | Rest (panel closed) | Animated (panel open) |
|----------|--------------------|-----------------------|
| Padding | 16dp | 16dp |
| Corner radius | 16dp | 16dp |
| Background | `rgba(255,255,255,0.03)` | same |
| Border | 1px `rgba(255,255,255,0.05)` | same |
| Text align | Center | Center |
| Transform | `translateY(15dp)` | `translateY(0)` |
| Opacity | 0 | 1 |
| Transition | 500ms `cubic-bezier(0.34, 1.56, 0.64, 1)` | same |

**Stagger delays per card position:**

| Card # | Delay |
|--------|-------|
| 1 (Diameter) | 100ms |
| 2 (Mass) | 150ms |
| 3 (Distance) | 200ms |
| 4 (Day Length) | 250ms |
| 5 (Gravity) | 300ms |
| 6 (Temperature) | 350ms |

**Stat value text:**

| Property | Value |
|----------|-------|
| Font | Space Mono 700 |
| Size | `clamp(16dp, 2.5vw, 20.8dp)` |
| Color | `planet.Color` (dynamic) |
| Bottom margin | 4.8dp |

**Stat label text:**

| Property | Value |
|----------|-------|
| Font | Syne 500, 8.8dp (0.55rem) |
| Letter spacing | 0.15em |
| Case | Uppercase |
| Color | `#e8e6f0` at 35% opacity |

#### 2.6.9 Fun Fact Card

| Property | Rest | Animated (panel open) |
|----------|------|-----------------------|
| Padding | `16dp 19.2dp` | same |
| Corner radius | 14dp | same |
| Background | `rgba(255,255,255,0.03)` | same |
| Border | 1px `rgba(255,255,255,0.05)` | same |
| Layout | Horizontal, gap 12.8dp, top-aligned | same |
| Transform | `translateY(15dp)` | `translateY(0)` |
| Opacity | 0 | 1 |
| Transition | 500ms `cubic-bezier(0.34, 1.56, 0.64, 1)`, delay 400ms | same |

**Fact icon:** 19.2dp font size, flex-shrink 0, top margin 1.6dp.  
**Fact text:** Syne 400, 12.8dp (0.8rem), line-height 1.5, color `#e8e6f0` at 70% opacity.

---

### 2.7 Layer 25: Planet Nav

| Property | Hidden | Visible |
|----------|--------|---------|
| Position | Fixed bottom 0, full width, centered | same |
| Layout | Horizontal, gap 6.4dp, padding `12.8dp 16dp` | same |
| Opacity | 0 | 1 |
| Pointer events | None | Auto |
| Transition | 400ms ease | same |

**Nav dot (×8):**

| Property | Inactive | Active | Hover |
|----------|----------|--------|-------|
| Size | 8 × 8 dp | 8 × 8 dp | 8 × 8 dp |
| Corner radius | 50% (circle) | 50% | 50% |
| Border | 1.5px, color = `planet.Color` | 1.5px, white | same as state |
| Background | transparent | `planet.Color` | no change |
| Transform | scale(1) | scale(1.4) | scale(1.3) |
| Transition | 300ms `cubic-bezier(0.34, 1.56, 0.64, 1)` | same | same |
| Cursor | Pointer | Pointer | Pointer |

---

### 2.8 Layer 30: Tooltip

| Property | Hidden | Visible |
|----------|--------|---------|
| Position | Fixed, tracks mouse at `(mouseX+15, mouseY−10)` | same |
| Opacity | 0 | 1 |
| Transition | 200ms ease | same |
| Corner radius | 8dp | same |
| Background | `rgba(20,20,30,0.9)` | same |
| Border | 1px `rgba(255,255,255,0.1)` | same |
| Backdrop blur | 10dp | same |
| Padding | `5.6dp 12dp` | same |
| Font | Syne 500, 11.2dp (0.7rem) | same |
| Color | `#e8e6f0` | same |
| White-space | No wrap | same |
| Pointer events | None (always) | None |

**Visibility rules:**
- Show when: `hoveredPlanet !== null` AND `selectedPlanet === null`
- Content: `planet.Name`
- Hide immediately when any planet is selected

---

## 3. Easing Curve Reference

All easing curves used in this app, named for easy reuse:

| Name | Cubic Bezier | Usage |
|------|-------------|-------|
| `MaterialDecelerate` | `(0.4, 0, 0.2, 1)` | Panel slide, stat bar fill |
| `SpringOvershoot` | `(0.34, 1.56, 0.64, 1)` | Stat cards, fun fact, nav dots, zoom buttons |
| `StandardEase` | `(0.25, 0.1, 0.25, 1)` | Standard CSS `ease` — tooltip, hint, nav container, glow |
| `Linear` | `(0, 0, 1, 1)` | Frame-based lerp (zoom, pan) — not CSS, done in code |

---

## 4. State Visual Summary

### 4.1 System View (no planet selected)

- Full solar system visible at zoom ~1.0×
- All orbit paths at low opacity (0.04)
- Planets orbit continuously
- Tooltip appears on hover
- Hint text visible for first ~5 seconds
- Panel hidden (translateY 100%)
- Nav dots hidden (opacity 0)
- Atmosphere glow hidden (opacity 0)
- Cursor: `grab` on canvas, `pointer` over planets

### 4.2 Planet Detail (planet selected)

- Zoom interpolating toward 2.5×
- Pan interpolating to center selected planet
- Selected orbit path brightened (opacity 0.12, width 1.5)
- Selected planet has glow ring
- Panel visible (translateY 0), content populated for selected planet
- Stat cards staggered in
- Nav dots visible, active dot filled
- Atmosphere glow visible, tracking planet position
- Hint permanently gone
- Tooltip hidden (regardless of hover)
- Cursor: `pointer` over planets, `grab` elsewhere

### 4.3 Transition: System View → Planet Detail

1. `selectedPlanet` set
2. `targetZoom` = 2.5
3. `targetPanX/Y` calculated to center planet
4. Panel content populated with planet data
5. Panel `open` class added (triggers slide + stagger)
6. Nav rendered with active dot
7. Nav container faded in
8. Atmosphere glow color set, opacity → 1
9. Hint hidden permanently
10. Zoom/pan interpolate over ~30–50 frames (lerp, not tween)

### 4.4 Transition: Planet Detail → System View

1. `selectedPlanet` = null
2. `targetZoom` = 1.0
3. `targetPanX/Y` = 0, 0
4. Panel `open` class removed (triggers slide down)
5. Nav container faded out
6. Atmosphere glow opacity → 0
7. Zoom/pan interpolate back over ~30–50 frames

### 4.5 Transition: Planet Detail → Different Planet

1. `selectedPlanet` updated
2. `targetZoom` remains 2.5 (or re-set to 2.5 if user had manually zoomed)
3. `targetPanX/Y` re-calculated for new planet
4. Panel content replaced (innerHTML swap)
5. Panel `open` class removed then re-added next frame to re-trigger stagger
6. Nav re-rendered with new active dot
7. Atmosphere glow color updated, position begins tracking new planet

---

## 5. Responsive Behavior

The prototype has no breakpoints. It scales naturally:

- `baseRadius = min(viewportWidth, viewportHeight) × 0.42` — orbits fit any aspect ratio
- Planet name: `clamp(32dp, 6vw, 51.2dp)` — scales with viewport width
- Stat values: `clamp(16dp, 2.5vw, 20.8dp)` — same
- Planet labels on canvas: font size formula adapts to zoom
- Stats grid: always 3 columns (may be tight on very narrow screens but prototype doesn't handle that — match as-is)

---

## 6. Accessibility Notes (as-built)

The prototype has zero accessibility features. This is a visual demo. If accessibility is added later, it is out of scope for this design brief. Build what exists.

---

*End of Design Brief v1*
