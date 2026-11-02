# Westhaven Dispatch (Cargo)

An Uno Platform implementation of the **Quay Port v4** design
([Claude Design project](https://claude.ai/design/p/254ce48f-20f1-482a-bfa8-c813390c0840)) — a port
operations dashboard for the fictional port of Westhaven.

The design's eight sections are merged into five, switched from the header. Every section
sits under the same harbour stage: full height on Overview, a 210 px strip everywhere else.

| Route | Section | What it does |
|---|---|---|
| `overview` | Overview | Orbitable 3D harbour with the greeting and headline figures laid over it; vessel-movement ribbon, next-up list, throughput blocks, cargo flow, sea & tide, the day's activity |
| `berths` | Berths | Berth map with drag-to-assign, 36-hour timeline scrubber, conflict detection and a berth optimiser; vessel side profile with a clickable bay grid, live crane scene, voyage and clearances |
| `cargo` | Cargo | Filterable container explorer and journey tracker; live yard block map with stack tiers; the 3D inspection scanner with X-ray |
| `fleet` | Fleet & waterways | 120 km river map with chainage, buoys, gauges, locks and notices; lockings, water levels, crew chat |
| `security` | Security | Tabs `zones`, `access` and `inspection`: zone map and event timeline, access control per badge, the cargo-inspection sheet |

## Running it

```
dotnet build Cargo/Cargo.csproj -f net10.0-desktop -p:CargoTargetFrameworks=net10.0-desktop
dotnet run   --project Cargo/Cargo.csproj -f net10.0-desktop -p:CargoTargetFrameworks=net10.0-desktop
dotnet test  Cargo.Tests/Cargo.Tests.csproj -p:CargoTargetFrameworks=net10.0
```

`CargoTargetFrameworks` narrows the build to one head, so a machine without the Android, iOS or
wasm-tools workloads can still restore it; leave it out to restore every head.

Five heads: `net10.0-desktop` (Skia on Windows, macOS and Linux), `net10.0-windows10.0.19041`
(WinAppSDK, x64 or ARM64), `net10.0-browserwasm`, `net10.0-android` and `net10.0-ios`. Desktop,
WebAssembly and Android build with 0 warnings on Linux; the Windows and iOS heads build on their own
OS, in CI (`.github/workflows/ci.yml`). Warnings fail the build (`Directory.Build.props`).

`Cargo.Tests` (NUnit) covers the logic with no UI in it: the berth rules, the reasons a berth refuses a
vessel and the optimiser's report (`Domain/BerthPlan.cs`), the timeline maths (`Domain/ShiftTimeline.cs`),
the container filters, bay views, the yard map's keyboard steps (`Domain/YardGrid.cs`), the wrap and tile
arithmetic of the responsive panels (`Controls/LayoutMath.cs`) and the harbour's geometry helpers. It references the app's plain `net10.0` build,
which never starts the Uno runtime, and runs in CI's `test` job.

## Architecture

- **Uno.Sdk 7.0.0-dev.115** (Uno 7 dev build, SkiaSharp 4), single project, Skia renderer,
  `SimpleTheme` + Uno Toolkit, Light and Dark. Hot Design and the App MCP are off in Debug while this dev SDK's
  clients still reference `Uno.UI.Lottie` (see `Cargo.csproj`).
- **MVVM (`CommunityToolkit.Mvvm`)**, not MVUX. Every figure in this app is an in-memory
  constant; there is no async or reactive data flow for feeds to model. What the app
  actually has is a lot of imperative selection state — which berth is hovered, which bay
  is open, where a dragged hull currently is — which is what MVVM is good at.
  `Domain/PortState.cs` is the single observable store, and it raises four separate signals
  so that work is proportional to what actually changed: `StructureChanged` for selections
  and assignments, `Ticked` for the clock, `HoverChanged` for the pointer, and `YardChanged`
  for the yard's own live moves. Hover and yard moves fire orders of magnitude more often
  than anything else and are handled as repaints of the affected element, not rebuilds — a
  berth hover used to tear down and rebuild the entire harbour.
- **Navigation** is Uno Navigation. The route tree (`App.xaml.cs` `RegisterRoutes`) is
  Shell > Main > one view-less route per section, and Security's tabs nest under `security`.
  `MainPage` declares the sections as `x:Load` panes in its Visibility region; the header
  navigates with `uen:Navigation.Request`, and jumps that start in the store (open a vessel,
  "Open inspection") set `PortState.Section` for `MainViewModel` to follow. Every page and
  embedded panel has its own view model.
- **Size classes** come from one `utu:ResponsiveLayout`, App.xaml's `DefaultResponsiveLayout`, which every
  `{utu:Responsive}` uses: phone below 600, tablet 600, laptop 1000 (side panels beside their map or plan),
  desktop 1280 (the design's layout), wide 1600 (pages capped at 1520 and centred). Every section reflows to
  the width it is given and the page never scrolls sideways; below 1000 every control is a 44 px target.
  `Controls/FlowPanel.cs` wraps figures, legends, chips and button rows; `TileGridPanel` takes responsive
  column widths.
- **Design tokens** live in `Themes/Tokens.xaml`; `Domain/Tokens.cs` resolves them for code
  so no colour literal appears outside that one file.
- **Themes.** Light is the design's palette; Dark is the same desk at night (a blue-black
  page, cool off-white ink, the three blues re-spaced, status inks re-picked, every text colour
  at 4.5:1 or better). Every themed colour and brush sits in Light and Dark theme dictionaries
  and the XAML takes them through `{ThemeResource}`. Code asks `Domain/Tokens.cs`, which resolves
  for the window's theme, recolours every brush it handed out when the theme changes, and raises
  `ThemeChanged` so canvas scenes that keep colours (blends, gradients, Skia paints) redraw
  (`Controls/Live.RebuildOnTheme`). Paint stays invariant: cargo classes, container liveries,
  white paint, the 3D model's paint and the dark scanner stage. The account menu (the avatar)
  offers System, Light and Dark; System is the default and follows the device, and
  Uno.Extensions' theme service applies and saves the choice.
- **The harbour** (`Controls/Harbour/`) is a true-scale 3D scene on `SKCanvasElement`
  (1 unit = 3.9 m) with its own perspective projection and painter's sort. The still scene is
  recorded once; when the camera settles it is baked into one GPU image with its tilt-shift,
  so an idle frame is one image draw plus the cranes and trucks. It runs at 30 fps while the
  camera moves and 15 fps at rest. In Dark it is the same model at night: the ground, water and
  sky tint come from the theme and the paint is lit lower (`HarbourLightLevel`). Build record: `SPEC.md`.
- **Other scenes** (river, yard, berth map, charts, vessel profile) are drawn into a
  fixed design-space `Canvas` hosted by `Controls/SceneHost.cs`, which scales it to the
  available width — the WinUI equivalent of an SVG `viewBox`. The source design is SVG
  throughout, so this keeps every coordinate in the file identical to the design's.

### Notable pieces

- `Controls/ContainerScanner.cs` — WinUI has no CSS-style 3D transforms, so the inspection
  box is projected by hand: eight corners rotated about Y then X, divided through by a
  1500-unit perspective, faces painted back to front. Drag rotates it.
- `Controls/BerthMap.cs` — pointer-driven berth assignment. A drop is validated against
  draft versus berth depth and against the other vessels' occupancy windows, so a berth
  that cannot take the ship lights red and says why (Too shallow, Taken, Closed). The selected
  vessel's "Move to berth" list is the keyboard and touch path to the same rules.
- `Controls/VesselProfile.cs` — the bay grid is laid over the hull render, positioned from
  the deck rect measured inside the sprite, and each bay column is its own hit target.
- `Liveline/` — the tide lane's chart control, a copy of Liveline from
  [noknowscode/uno-lab](https://github.com/noknowscode/uno-lab) (taken at `1289007`) kept in this
  repository so it builds on its own. Changes made here are not synced back.
- `Presentation/MainPage.xaml.cs` realizes one section pane per idle turn after the window
  shows, so all five exist about 1.5 s after launch (30 to 370 ms each). A jump before that
  realizes its pane on demand, because paying a section's construction cost on the click is
  what made moving to a new section feel like a stall.

## Artwork

The 2D scenes use the design's own rendered artwork, in `Cargo/Assets/Sprites/`
(the harbour is the exception: it is built from `PortData` in 3D): crane bases, plan and side views of the hulls, the container
renders, the quay crane and the river buoys. Every one is placed with the rect the design
uses, in the same coordinate space, so a berth and the ship alongside it keep their exact
relationship at any window size (`Controls/Sprites.cs` holds the placement helpers, the
intrinsic sizes and the vessel-to-artwork mapping).

Where a hull sprite is overlaid with interactive geometry — the bay grid on the vessel
profile — the deck rect is measured from inside the sprite (`Sprites.Hull`) and everything
scales off that, so the columns land on the containers the artwork actually shows.

A handful of things stay vector because the design draws them that way too: the yard block
map, the yard tractor, the security zone plan and the chart furniture.

## Deviations from the design, and why

**Fonts.** The design specifies Instrument Sans / IBM Plex Mono / Bricolage Grotesque.
Plex Mono and Bricolage were available locally; Instrument Sans was not, and the sandbox
blocks the font download. **Archivo** stands in for it — same neo-grotesque lineage, and the
three weights the design uses (400/500/600) all exist as static instances. Static instances
are deliberate: variable-font axes do not resolve on the Uno Skia text stack.

**Letterspacing.** The design tracks its display type at −0.035em. `CharacterSpacing` is a
silent no-op on the Skia text stack, so the display sizes are set without it rather than
faked with per-character layout.

**Motion.** The design's CSS keyframe loops (crane hoist, yard RTGs, tractors, dash marching)
are driven by `DispatcherTimer` rather than `RepeatBehavior="Forever"` storyboards, which pin
the Skia compositor at display refresh and cost roughly a third of a core each, even off
screen. The timers stop on `Unloaded`, and they run at 10 fps: an RTG covers 330 px in nine
seconds and a tractor 1180 px in twenty-two, so the step is a few pixels and the motion reads
the same, while the scene behind it is re-composited half as often. The harbour has its own
frame timer (30 fps moving, 15 fps at rest), stopped on `Unloaded` the same way.

## Verification

`tools/Capture-Window.ps1` captures the window with `PrintWindow` (occlusion-proof, with a
screen-copy fallback when the GL surface hands back a blank bitmap). On Linux the evidence is
captured under Xvfb: `docs/evidence/b2-*.jpg` are contact sheets of every section and the scanner at
360, 599, 600, 999, 1000, 1280, 1600 and 1920 px wide (640 high), Light and Dark (Phase B2).

Because Windows will not let a background process drive the foreground window, panels that
only exist after an interaction are reachable through DEBUG-only environment hooks so a
headless run can photograph them:

| Variable | Effect |
|---|---|
| `APP_NO_HOTDESIGN=1` | Skips `UseStudio()`, which otherwise blocks window creation with no DevServer (compiled out anyway while `NO_UNO_STUDIO` is defined) |
| `CARGO_THEME` | `light`, `dark` or `system`, set through the theme service as the account menu does (so it is saved) |
| `CARGO_WINDOW` | Opens the window at a size, e.g. `390x844` (desktop head) |
| `CARGO_START_SECTION` | Opens straight onto a section (`overview`, `berths`, `cargo`, `fleet`, `security`) |
| `CARGO_SECURITY_TAB` | `zones`, `access` or `inspection` |
| `CARGO_DOCK_SELECT` | Vessel id to preselect in the docking planner |
| `CARGO_STACK` | Yard slot key, e.g. `A-1-2` |
| `CARGO_SCANNER=1`, `CARGO_XRAY=1` | Opens the container inspection, optionally in X-ray |
| `CARGO_LAYER` | Harbour overlay: `port`, `yard`, `security`, `traffic` |
| `CARGO_OPTIMIZE=1` | Runs the berth optimiser at startup |
| `CARGO_SCROLL` | Scrolls the section body by N pixels, to photograph a panel below the fold |
| `CARGO_HARBOUR_VIEW` | Harbour camera preset, no intro swing: `overview`, `sea`, `land`, `plan` |
| `CARGO_ORBIT=1` | Spins the harbour camera without stopping, to measure drag-frame cost |
| `CARGO_HOVER_BERTH` | Berth index 0–7 to show hovered, for the harbour highlight and its label |

Startup failures are written to `startup.log` beside the executable — a desktop Uno app is a
windowed process, so redirected stdout captures nothing.
