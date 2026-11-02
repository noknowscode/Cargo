# HANDOFF — Cargo: production readiness, Phase B2 (responsive, states, accessibility, evidence)
Updated: 2026-11-02

## After B2: the WinAppSDK head, fictional carriers (branch `feat/production-readiness`)
- **WinAppSDK build** (CI run 41499042343 failed there only; fixed from its log, Windows itself not run here):
  - 239 MVVMTK0045: every `[ObservableProperty]` field is a partial property now (`[ObservableProperty] public
    partial T Name { get; set; }`, CommunityToolkit.Mvvm 8.4.2 from the Uno.Sdk, C# 14 `field`). PortState's DEBUG
    start hooks wrote backing fields to skip the change hooks; they are property initializers now (an initializer
    sets the field without the setter), through `PortState.Hook`, which returns null in Release. Check on Linux:
    build the desktop head with `-p:_MvvmToolkitIsUsingWindowsRuntimePack=true -p:CsWinRTAotOptimizerEnabled=true`,
    which switches the toolkit's WinRT analyzers on (478 MVVMTK0045 lines before, 0 after).
  - CS9035 / CS8852 in WinUI's `XamlTypeInfo.g.cs`: `HarbourTag` takes its values through a constructor (no
    `required`); Liveline's `LivelinePoint`/`LivelineMarker` properties are settable instead of init-only.
  - CsWinRT1032: the free-berth facts are an array, not a collection expression typed `IReadOnlyList<Fact>`. The
    other collection expressions in Cargo and Liveline are empty or target arrays.
  - Re-read the WinUI trap list: no `{Binding}` anywhere (bool to Visibility goes through `x:Bind`), font URIs carry
    `#Family`, Uno-only C# is behind `#if __DESKTOP__`, CI's XAML passes reported nothing.
- **Fictional carriers** (owner decision): MSC -> Avren, Hapag-Lloyd -> Ferrand-Lowe, ONE -> Tomoe, Maersk -> Ravnby,
  CMA CGM -> Morvane, COSCO -> Lanhai; lessors Textainer -> Stackwell, Seaco -> Saltco, GE Seaco -> Ridgebox,
  Triton -> Nereid; MSC Aurora -> Avren Aurora. Owner codes AVRU RVBU FLWU MRVU TMOU SKWU LNHU SLTU RDBU NRDU (check
  digits recomputed, valid ISO 6346), seal prefixes to match, livery tokens renamed (`HullAvrenColor`, ...).
- **Artwork:** `aurora-side.png` (was `msc-side.png`) letters "AVREN AURORA" at the bow and "AVREN" midships, below
  the side profile's bay chips; `ship-side.png`, the hull every other vessel shares, had "MSC AURORA" too and now
  carries no name; `aurora-plan.png` (was `msc-plan.png`) is only renamed. Canvas sizes and alpha unchanged.

## Phase B2 (branch `feat/production-readiness`)
- **Size classes** (owner decision 6): App.xaml's `DefaultResponsiveLayout` (Narrowest 0, Narrow 600, Normal 1000,
  Wide 1280, Widest 1600), used by every `{utu:Responsive}`; the comment says why the Toolkit defaults do not fit.
  Categories: Narrow = phone (<600) + tablet (600-999), Medium = 1000-1279, Wide = 1280-1599, Widest = 1600+.
  No page has a minimum width and the section region no longer scrolls sideways; pages cap at 1520 (Overview 1440)
  and centre from 1600. New: `Controls/FlowPanel.cs` (wrapping rows), `LayoutMath` (its and `TileGridPanel`'s
  arithmetic, tested), responsive `TileGridPanel` properties, `TouchTarget` (44) and page-padding tokens.
- **Per section** (phone / tablet / 1000 / 1280 / 1600+): header tabs on their own 44 px row below 1000 (13 px, all
  five fit at 360); Overview timeline figures above a full-width chart below 600, table scrolls in its card below
  1280; Berths status beside detail from 1000, needs-you bar beside the title from 1280, plan controls on one line
  from 1280, vessel panel beside map from 1000 and under the timeline below, vessel band 3 columns from 1280 /
  2 + voyage below from 1000 / 1 below; Cargo explorer and yard panels beside from 1000; Waterways row of four from
  1280, 2x2 from 600; Security side columns beside from 1000, timeline and inspection diagram scroll sideways on a
  phone; scanner a sheet below 1000 (full window on a phone), the 3D box scaled to its stage.
- **States and feedback:** drag-to-assign labels each berth Fits / Too shallow / Taken / Closed while dragging; every
  drop says what happened; `BerthPlan.Check`/`Refusal` name the rule (a restricted berth used to be reported as
  "taken"); the optimiser reports what it moved and who nothing fits (it used to claim success always). Empty
  states: container filter with no match (with "Show all"), crew channel with no messages; Needs you and Next 6 h
  already had theirs. Loading and error states do not exist to show: every figure is an in-memory constant.
- **Accessibility:** a "Move to berth" list in the docking panel (keyboard and touch path for the drag, same
  refusals); the yard map takes focus and the arrows step through stacks (`Domain/YardGrid.cs`); the inspection
  takes focus, cycles Tab and closes on Escape; names on cards, chips, tabs, presets and icon+label buttons; 44 px
  targets below 1000; timeline bars carry shape (outline, dashed edge, ink edge) as well as colour.
- **Contrast:** Light TextFaint #6B7377 -> #636B6F (4.89:1 on Paper); status colours set as text use their ink
  variants (sea green 3.9, alert 4.2, orange 2.8:1 on Light before); timeline bar labels on opaque fills (>= 5.2:1).
- **Every control works:** Assign inspector and Request re-weigh had no command; each now logs and stands done.
- **Evidence:** `docs/evidence/b2-*.jpg`, Debug desktop under Xvfb, 640 high, 360-1920, Light and Dark. The stale
  screenshots that named the real port (two root PNGs, `shots/`) are deleted; README points at the evidence.

## Phase B1
- **Westhaven, not Montreal.** The title, publisher, description, wordmark, Overview subtitle, quay sign, river map,
  routes, river boats and staff badges named the real Montreal Port Authority (and a CBSA badge); all now the
  fictional Westhaven (owner decision 4). The application id was already `com.westhaven.cargo`. Settings now live
  under the new publisher folder, so a saved reduced-motion choice starts over once. Carrier names stayed in B1
  (renamed after B2, above).
- **Cargo.Tests** (NUnit, 54 tests) on the app's plain `net10.0` build: berth rules and optimiser (`Domain/BerthPlan.cs`),
  timeline maths (`Domain/ShiftTimeline.cs`), container filters (`PortData.ContainersFor`), bay views, harbour
  geometry. The pure parts moved out of `PortState`, `TimelineViewModel` and `ContainersViewModel` unchanged in
  behaviour. CI has a `test` job.
- **Dark theme.** `Themes/Tokens.xaml` holds Light (the design, unchanged) and Dark ("Default") theme dictionaries;
  themed brushes lost the `Invariant` suffix (mapping in the file header). XAML uses `{ThemeResource}`.
  `Domain/Tokens.cs` is the code palette: resolves per window theme, recolours the brushes it handed out, raises
  `ThemeChanged`; scenes that keep colours rebuild through `Live.RebuildOnTheme`; the harbour reloads its palette
  and rebuilds the world (night: theme ground/water/sky, invariant paint under `HarbourLightLevel`). Liveline gained
  `GridLineBrush`, `LabelBrush`, `BadgeTextBrush`. Invariant on purpose: the scanner stage and its text, cargo
  classes, container and model paint, white paint, cast shadows.
- **Theme choice:** System (default) / Light / Dark radio buttons in the account flyout, applied and saved by
  Uno.Extensions' theme service (already in the graph through the Toolkit navigation package). `App.StartTheme()`
  reads the saved choice ("CurrentTheme") so the palette and the desktop window ground start right.
- **WinUI trap fixed:** `ActivityView`'s acrylic used the Uno-only `AcrylicBrushExtensions.UseCompositionEffectBrush`
  (WMC0010 on WinAppSDK); it is now behind `not_win:`, with a `win:` twin.

## Phase A
- **Liveline vendored** into `Liveline/` (copied from noknowscode/uno-lab at `1289007`, then moved to Uno 7 and
  SkiaSharp 4 in separate commits). A fresh clone builds with nothing next to it.
- **Uno.Sdk 7.0.0-dev.115** (SkiaSharp 4.151.1). Android host on `CreateHost()`; Hot Design and App MCP off in
  Debug (`NO_UNO_STUDIO`) until the dev SDK drops `Uno.UI.Lottie`. SkiaSharp 4 paths through `SKPathBuilder`.
- **Five heads**, with `-p:CargoTargetFrameworks=<tfm>` for single-head builds (Liveline follows it). WinAppSDK:
  x64/ARM64, `WindowsSdkPackageVersion` 10.0.19041.87, packaged.
- **Per head:** window sizing only on desktop/WinAppSDK; wheel zoom compiled out on Android/iOS (pinch remains);
  `GridPaper` on `PathGeometry` (LineGeometry is Uno0001 on Android/WASM); timeline lanes get a transparent
  background (Uno 7 `SKCanvasElement` is a `Grid`); every `CARGO_*` hook is Debug-only.
- **Build gate** (`Directory.Build.props`): warnings and Uno0001 fail the build; code style enforced; NuGet audit
  and trim analysis stay warnings. **CI** (`.github/workflows/ci.yml`): desktop x3, WinAppSDK, wasm, android, ios.
- **Next:** the code review (after B2).

## Where we were (2026-10-01): Overview one-timeline built (spec steps 0-9) + uno-audit
`SPEC-OVERVIEW-TIMELINE.md` is built end to end. App-wide: the design palette (teal → accent; reserved/arriving
accent, needs-you alert, vessel/happened navy), the type ramp (11/12/13/14/15/18/20/24/30/40, `Type*` + role
styles), fonts renamed for Android, a light header with text tabs, needs-you dots and an account flyout.
Overview: masthead figures + duty ID card; a 340/491 harbour band with the design's HUD floats and tags; the
`Today at North Quay` timeline (Skia lanes + the extended Liveline tide lane, Chart/Table); the Needs-you queue
(`PortState.Decisions`); Next 6 h with to-scale ship glyphs; Activity in two columns with NEEDS ACTION chips;
responsive bands at 1280/1040; linked hover from chart dots and Next 6 h rows; card entrance/hover motion.
Container volume / Cargo flow moved to Cargo, Sea & weather to Waterways. Then an uno-audit pass.
Follow-up: Expand/Collapse no longer resizes the scene (it renders once at 491; the band clips it and slides
it 110 px via negative margins), the duty ID card sits level (entrance is fade + drop, hover lifts 2 px), and a
dead-code pass removed 47 usings, 7 members, 12 motion tokens, a font and 10 unused sprites.

## Last verified state
- 2026-11-02 (Linux, after B2): Release, 0 warnings / 0 errors for `net10.0-desktop`, `net10.0-android` and
  `net10.0-browserwasm`; `Cargo.Tests` 89/89 pass. Desktop with the toolkit's WinRT analyzers forced on (see above):
  0 MVVMTK0045. Debug desktop under Xvfb, 1440x900: Overview (tags, timeline, tide lane, needs-you, activity) in
  Light and Dark; Berths in Dark: picked berth 02 (the facts array) and Kaida Maru, then Baltic Crown in the vessel
  band (unnamed generic hull) and Avren Aurora's side profile in Light (name clear of the bay chips), berth plan and
  timeline; Cargo: picking MRVU 918204 1 opens the scanner with the new seal; Security zones, Inspection (Assign
  inspector logs and stands done) and Access; Light switched to Dark from the account flyout. Not run: the
  WinAppSDK head (CI only; it has never passed CI yet, so the steps after compiling are unexercised), iOS,
  WebAssembly and Android at runtime.
- 2026-11-02 (Linux, B2): Release, 0 warnings / 0 errors for `net10.0-desktop`, `net10.0-android` and
  `net10.0-browserwasm`; `Cargo.Tests` 89/89 pass (35 new: berth checks and refusals, the optimiser's report,
  `LayoutMath`, `YardGrid`). Evidence: Debug desktop under Xvfb, one launch per view and theme live-resized through
  360, 599, 600, 999, 1000, 1280, 1600, 1920 at 640 high (12 views: Overview top and scrolled, Berths top and plan,
  Cargo top and scrolled, Waterways top and scrolled, Security zones, access and inspection, the scanner), Light and
  Dark, contact sheets in `docs/evidence/b2-*.jpg`; also tall captures of every section at 360/600/1000/1280/1920
  while building it. Keyboard checked under Xvfb: focus visuals on header tabs and scanner presets, Tab held inside
  the scanner. Not run: WebAssembly and Android at runtime, WinAppSDK/iOS (CI only); a screen reader; real touch.
- 2026-11-02 (Linux, B1): Release, 0 warnings / 0 errors for `net10.0-desktop`, `net10.0-android` and
  `net10.0-browserwasm`; `Cargo.Tests` 54/54 pass. Desktop (Debug, Xvfb, 1440×900) in Dark: Overview top and
  scrolled, Berths (status, plan, timeline, vessels), Cargo (explorer, yard), Waterways (river, levels, chat, sea &
  weather), Security (zones, access, inspection), the scanner overlay, the account flyout, the harbour Key and camera
  menus, the timeline table; in Light: Overview, Berths timeline, Cargo, Waterways, Security, scanner. Light matches
  the pre-change capture except animated pixels. Theme switched at runtime both ways (CARGO_THEME after launch, and
  clicking System/Light/Dark in the flyout). Not run: WebAssembly and Android at runtime, WinAppSDK/iOS (CI only).
- 2026-11-01 (Linux): Release, 0 warnings / 0 errors for `net10.0-desktop`, `net10.0-android` and
  `net10.0-browserwasm`; the WinAppSDK and iOS heads are built by CI only. No test project. Desktop runtime under
  Xvfb (every section, timeline dot hover, the scanner overlay) matches the 6.7.30 baseline; WebAssembly renders
  in headless Chromium. The entries below are from 2026-10-01 on Windows.
- Build: pass, 0 warnings / 0 errors, `net10.0-desktop` (full `--no-incremental`) and `net10.0-android`.
- Runtime (uno-app MCP, 1680×1020 / 1100×900 / tall captures): every section; Overview top to bottom; Expand
  340↔491; camera MenuFlyout (Placement Top works); layer pill; Chart↔Table; Confirm berth from the queue
  (row leaves, count 4→3, Berths dot clears, tag turns light); account flyout + reduced-motion switch; 1100 band.
  Overview idle CPU 17-25% of one core (Debug; baseline was 48-53%).
- Git: `main` at `cff9944`, 2 commits ahead of origin (not pushed). Liveline was then `C:\Users\Platform006\Uno-Builds-net10`,
  branch `liveline-window` at `15ecf9f`; it is now vendored under `Liveline/`.
- Lint: CARD 0 · HEX 0 · TOKENTHEME 0 · BACKBAR 0 · CODEBEHIND 0 · OVERLAY 0 · RESPONSIVE 0 · BUILTIN 0 · ICON 0 · WORKAROUND 1 (gotcha recorded).

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| skiasharp-uno: snapshot state, reuse paints, bake alpha | Controls/TimelineLanes.cs:234; Controls/CanvasFonts.cs:20 |
| xaml-design-polish: reduced motion jumps to the end | Controls/SegmentIndicator.cs:106; Presentation/TimelineViewModel.cs:199 |
| gotcha G60: FillBehavior Stop over local values | Controls/Harbour/HarbourView.xaml.cs:210; Controls/IdCardMotion.cs:97 |
| uno-toolkit / gotcha G36: ShadowContainer shadows inline | Presentation/OverviewMasthead.xaml:81 |
| uno-scaffolding Breakpoints: `{utu:Responsive}` incl. structure | Presentation/OverviewView.xaml:35 |
| gotcha BrushTransition: alpha in the colour (`Tokens.Tint`) | Presentation/NextSixHoursViewModel.cs:56 |
| capability-coverage: icons from one keyed file, sources named | Themes/Icons.xaml:43; Presentation/NeedsYouPanel.xaml:44 |
| navigation-xaml: `uen:Navigation.Request` tabs | Presentation/MainPage.xaml:50 |
| spec: Liveline by ProjectReference | Cargo.csproj:39 |
| workaround protocol (new gotcha) | Presentation/MainPage.xaml:122; docs/evidence/2026-10-01-shadowcontainer-offset-paint.md |
Read, not applied: none.

## Next actions (in order)
1. Done 2026-11-01: Liveline is vendored, so a fresh clone builds.
2. Add `Assets/Fonts/IBMPlexMono_SemiBold.ttf` (download was blocked in-session); point `CountBadgeText`,
   the NOW-row values and the moves rate at it.
3. Motion the spec lists but left instant: tab hover (160 ms), Chart→Table fade (200 ms), Expand icon
   cross-fade, the departing ship's wake loop (3.6 s; budget its CPU first).
4. Accessibility: keyboard focus for the timeline's vessel dots (the table is the alternative today); the
   masthead "Needs you" hover ring on the queue.
5. Queued from before: `GLCanvasElement` harbour trial (uno-build-options), MainPage gold-standard pass.

## Open questions
- **Images that still show the old carrier names:** the B2 evidence sheets (`docs/evidence/b2-*.jpg`, thumbnails of
  the app before the rename) and the design's own export, `docs/design/overview-timeline/overview-timeline.png`
  ("MSC Aurora", "CMAU", "MAEU", "CSQU", "COSCO"). Re-shoot the evidence and retouch or drop the export?
- **IMO numbers** (9839272, 9776171, 9741401, 9632179, 9705523; the first is painted on the Aurora's stern) are valid
  IMO numbers from the design and were not checked against real ships. The invented owner codes (AVRU, ...) were not
  checked against the BIC register either (no network access to it here).
- Dark keeps the design's rendered sprites (hulls, containers, cranes, buoys) as they are: daylight renders on a
  night ground. They read fine; a night set would need new artwork.
- On a dark launch the X11 window is white until Uno's first frame (measured under Xvfb, 2026-11-02: about 1.5 s of
  white, one dark frame, white again while the startup resize repaints, then dark). `WindowHelper.SetBackground`
  is already called right after the window exists; the X11 host creates its window on `XWhitePixel`/`XBlackPixel`
  before app code runs, so it is not reachable from the app. Needs an Uno host option (or a smaller dev change).
- Maps and scenes (berth map, yard, river, zone map) render as scaled overviews on a phone; their facts are in the
  lists beside them (berth status, Move to berth, Underway, records). Only the security timeline and the inspection
  diagram scroll sideways. A pan-and-zoom phone map would be a design decision.
- The Vessels key-figure cards now sit under the side profile at every width (floated over the bow they covered
  bays 01-03 up to the 1520 cap): a design change, owner to confirm.
- `SegmentIndicator` is custom (Toolkit 9.1.3 has no Simple/Material SegmentedStyle). Adopt WCT 8 `Segmented` (new package)?
- Camera menu radio items show circles under SimpleTheme; the design shows a check mark.
- Header uses a Flyout, not the spec's MenuFlyout (disabled MenuFlyout items were unreadable).
- Cargo lane scale fitted to the data (to 170/h); the design's 0.66 assumed ~50.
- Activity shows 12 entries (the data), the design 7; chips derive from open decisions (2 today).
- Skia `SKPaint`s in `TimelineLanes`/`ShipGlyph` are left to finalizers (disposing on Unloaded breaks re-parenting, G63).
- Security zone sub-labels spill below 34 px zones (`SecurityScenes.cs:43`); canvas scenes still pass literal text sizes.
- Scanner overlay restyle (`CloseOnDarkButton`): opened from Scan container and closed with Close on Linux desktop
  (2026-11-01); `CARGO_SCANNER=1` opens it at launch in Debug.
- uno-app MCP cannot select a solution outside the session root (Liveline demo verified by exe + PrintWindow).

## Relaunch
Linux (as verified 2026-11-02):
```
dotnet build Cargo/Cargo.csproj -f net10.0-desktop -c Release -p:CargoTargetFrameworks=net10.0-desktop
APP_NO_HOTDESIGN=1 dotnet Cargo/bin/Release/net10.0-desktop/Cargo.dll
dotnet test Cargo.Tests/Cargo.Tests.csproj -c Release -p:CargoTargetFrameworks=net10.0
```
Debug hooks need a Debug build: `CARGO_THEME=dark CARGO_START_SECTION=berths CARGO_WINDOW=1440x900 dotnet
Cargo/bin/Debug/net10.0-desktop/Cargo.dll` (the theme hook saves the choice, as the account menu does).
Windows (2026-10-01):
```
cd C:\Users\Platform006\Cargo\Cargo
dotnet build -f net10.0-desktop -p:CargoTargetFrameworks=net10.0-desktop
$env:APP_NO_HOTDESIGN='1'; .\bin\Debug\net10.0-desktop\Cargo.exe
```
With the App MCP (off while `NO_UNO_STUDIO` is defined): `uno_app_start`, wait for the first frame before resizing
(a resize during startup once killed the app). `taskkill /IM Cargo.exe /F` first; the running app locks `Cargo.exe`
and `Liveline.dll`.
