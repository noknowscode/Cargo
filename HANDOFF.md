# HANDOFF — Cargo: production readiness, Phase A (Uno 7 foundation)
Updated: 2026-11-01

## Phase A (branch `feat/production-readiness`)
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
- **Next (Phase B, UI hardening):** Dark theme (the app is light-only: 370 `StaticResource` brush/colour uses,
  0 `ThemeResource`, 310 `Tokens.Brush` lookups in code resolved once), the desktop-first bands in Narrow and
  Medium, then the code review.

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
- `SegmentIndicator` is custom (Toolkit 9.1.3 has no Simple/Material SegmentedStyle). Adopt WCT 8 `Segmented` (new package)?
- Camera menu radio items show circles under SimpleTheme; the design shows a check mark.
- Header uses a Flyout, not the spec's MenuFlyout (disabled MenuFlyout items were unreadable).
- Cargo lane scale fitted to the data (to 170/h); the design's 0.66 assumed ~50.
- Activity shows 12 entries (the data), the design 7; chips derive from open decisions (2 today).
- Skia `SKPaint`s in `TimelineLanes`/`ShipGlyph` are left to finalizers (disposing on Unloaded breaks re-parenting, G63).
- Security zone sub-labels spill below 34 px zones (`SecurityScenes.cs:43`); canvas scenes still pass literal text sizes.
- Vessels key-figure cards cover bays 01-03 below ~1300 px panel width (pre-existing layout intent).
- Berths clips its right column below ~1200 px (pre-existing, fixed columns).
- Scanner overlay restyle (`CloseOnDarkButton`): opened from Scan container and closed with Close on Linux desktop
  (2026-11-01); `CARGO_SCANNER=1` opens it at launch in Debug.
- uno-app MCP cannot select a solution outside the session root (Liveline demo verified by exe + PrintWindow).

## Relaunch
Linux (as verified 2026-11-01):
```
dotnet build Cargo/Cargo.csproj -f net10.0-desktop -c Release -p:CargoTargetFrameworks=net10.0-desktop
APP_NO_HOTDESIGN=1 dotnet Cargo/bin/Release/net10.0-desktop/Cargo.dll
```
Windows (2026-10-01):
```
cd C:\Users\Platform006\Cargo\Cargo
dotnet build -f net10.0-desktop -p:CargoTargetFrameworks=net10.0-desktop
$env:APP_NO_HOTDESIGN='1'; .\bin\Debug\net10.0-desktop\Cargo.exe
```
With the App MCP (off while `NO_UNO_STUDIO` is defined): `uno_app_start`, wait for the first frame before resizing
(a resize during startup once killed the app). `taskkill /IM Cargo.exe /F` first; the running app locks `Cargo.exe`
and `Liveline.dll`.
