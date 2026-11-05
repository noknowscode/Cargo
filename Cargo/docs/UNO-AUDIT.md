# uno-audit: Cargo / Westhaven Port Authority (2026-11-04)

Branch `feat/production-readiness`, from cc60ef2. Uno.Sdk 7.0.0-dev.115 (owner decision), Simple theme + Toolkit,
CommunityToolkit.Mvvm view models with partial properties (owner decision; the MVUX-only rules do not apply, their
MVVM equivalents do: commands, `x:Bind`, no logic in code-behind). Breakpoints 600/1000/1280/1600 in App.xaml's
`DefaultResponsiveLayout` (owner decision 6); layout tokens in `Themes/Layout.xaml`. Scope: hygiene, pattern
conformance, the semantic lint gate, performance, the build gate and a runtime re-check. Functional gaps from
`docs/COMPLETENESS.md` stay for the next pass. `Liveline/` is vendored and not audited inside.

## Skills applied

| Skill | Pattern it governs here | Applied at |
|---|---|---|
| uno-audit | The pass, the lint gate, the workaround protocol | this file; the lint table below |
| uno-platform-agent | Uno.Sdk single project, UnoFeatures, platform TFMs before `net10.0`, CommunityToolkit.Mvvm (`[ObservableProperty]` partial properties, `[RelayCommand]`), `x:Bind` on MVVM pages, `__DESKTOP__`/`WINDOWS` symbols | Cargo.csproj:8; Domain/PortState.cs; Platforms/Desktop/Program.cs:15 |
| uno-scaffolding + uno-runtime-gotchas | Code-behind budget, breakpoints only in `{utu:Responsive}`, semantic-first tokens, workaround protocol; gotchas checked: no held `CompositionTarget.Rendering` (Liveline detaches when settled, Reveal and the band step one-shot with guard timers), no Forever storyboard, ShadowContainer `Shadows` inline and solid backgrounds (G36, G37), `SKCanvasElement` alpha baked (G38), DispatcherTimer singleton built on the UI thread before the host, view-model DependencyObjects deferred through `IDispatcher`, route sync from `e.Route.Path` segments, ShadowContainer offset paint (workaround re-measured) | App.xaml.cs:43; Presentation/MainViewModel.cs:38; Presentation/MainPage.xaml:148 |
| uno-toolkit | `{utu:Responsive}` (262 uses), ShadowContainer, CommandExtensions, Toolkit navigation, CardContentControl lightweight keys | App.xaml:37; Presentation/FleetView.xaml |
| uno-navigation | Region navigation: header and Security tabs by `uen:Navigation.Request`, nested `RouteMap`s with `IsDefault`, store moves by `./` relative routes | App.xaml.cs:190; Presentation/MainViewModel.cs:179 |
| winui-xaml | `x:Bind` everywhere (0 `{Binding}`), virtualization (no list over 25 rows: ItemsControl is fine), async and threading, event cleanup (`Live` drops static events while hidden) | Controls/Live.cs:30 |
| dotnet-csharp | Per-frame allocations, `async void`, no blocking waits (0 `.Result`/`.Wait()`) | Controls/Harbour/HarbourRenderer.cs:118 |
| skiasharp-uno | `SKCanvasElement` scenes, paints/effects cached, baked still scene, nothing built per frame | Controls/Harbour/HarbourRenderer.cs:28-60 |

Not invoked: `mvux` (MVVM by owner decision), `uno-material` (Simple theme; the shared role brushes are used).

## Semantic lint gate

`xaml-semantic-lint.ps1 -Path Cargo` (119 files). Exit 0 before and after.

| Rule | Before | After | Note |
|---|---|---|---|
| CARD | 0 | 0 | |
| HEX | 0 | 0 | |
| TOKENTHEME | 0 | 0 | |
| BACKBAR | 0 | 0 | |
| CODEBEHIND | 0 | 0 | every allowed site carries `xaml-lint: allow codebehind - reason` |
| OVERLAY | 0 | 0 | |
| RESPONSIVE | 0 | 0 | |
| BUILTIN | 0 | 0 | |
| ICON | 0 | 0 | XAML only; the icons kept as path strings in C# are listed under Remaining |
| TOKEN (info) | 301 | 301 | 38 app brush keys |
| ADAPTIVE (info) | 0 | 0 | `{utu:Responsive}`: 262 |
| TEMPLATE (info) | 3 | 3 | PrimaryButton, BareButton, NavTabButton now each say why lightweight styling cannot do it (`Themes/Styles.xaml:431, 502, 543`) |
| WORKAROUND (info) | 1 | 1 | `Presentation/MainPage.xaml:148`, reproduced with versions and steps (below) |

### The WORKAROUND marker

`WORKAROUND(ShadowContainer keeps its paint when only its offset changes)`, the harbour's layer switcher. Its record
was one Windows measurement on Uno.Sdk 6.7.30, not reduced. Re-measured on Uno.Sdk 7.0.0-dev.115 (Uno.WinUI
7.0.0-dev.2509, Toolkit 11.0.0-dev.157), Debug desktop, Xvfb, CPU backend: with the responsive alignment and margin
moved back onto the `ShadowContainer`, a resize from 1440 to 1100 (across 1280) leaves the surface and shadow at the
old centred offset while the segments lay out at x = 40; with the wrapper the switcher repaints whole. Steps, versions
and the A/B image: `docs/evidence/2026-10-01-shadowcontainer-offset-paint.md`,
`docs/evidence/2026-11-04-shadowcontainer-offset-paint.png`. The other floats whose position moves with the width
(Expand and the camera menu at 600, the duty card's row span at 1280) repaint correctly, so the trigger measured is a
change of the container's own `HorizontalAlignment`. Proposed amendment to the gotcha entry (the skill's
`uno-runtime-gotchas.md`, Toolkit & Material controls): "Reproduced 2026-11-04 on Uno.Sdk 7.0.0-dev.115 / Toolkit
11.0.0-dev.157, Linux X11, CPU backend; the trigger measured is the container's own HorizontalAlignment changing
(Center to Left) at the same size; margin-only and row-span moves repaint. Repro: Cargo
docs/evidence/2026-10-01-shadowcontainer-offset-paint.md."

## Findings and fixes

### Dead code (fixed)
- `Domain/PortState.cs:244` `OpenSecurity` command, bound nowhere (completeness audit). Removed. f01ec9f
- `Domain/PortModels.cs:222` `NavSection.Icon` and the five path strings in `Domain/PortData.cs:22-26`, read nowhere
  since the header became text tabs. Removed. f01ec9f
- `Themes/Styles.xaml:451` PrimaryButton's `ScaleTransform x:Name="Squish"`, which no state animated. Removed. f57b314
- Theme resource keys: none unreferenced (the three keys with no app reference are Toolkit lightweight keys).
- Public members: this scan missed two, which the A-pass code review found: `PortState.NotifyStructureChanged` and
  `CrewChat.SentTo`, referenced nowhere. Removed in A pass 2 (b534043). The same review found an unreachable branch:
  Next 6 h added " · pilot aboard" when a status line contained "pilot", which none does. The data now says which
  arrival has her pilot (`Vessel.PilotAboard`), and the row reads it (0a2f544).

### Unused usings and duplicates (fixed)
- IDE0005 (one-off build with the rule raised): `Controls/TimelineLanes.cs:1`, `Presentation/BerthStatusViewModel.cs:1`,
  `FleetViewModel.cs:1`, `Items.cs:1`, `MainViewModel.cs:2`, `SecurityViewModel.cs:1`, `YardViewModel.cs:1`. 47d7fd7
- `GlobalUsings.cs`: six namespaces `ImplicitUsings` already imports on every head (System, Collections.Generic, Linq,
  Threading.Tasks, Microsoft.UI.Xaml, Microsoft.UI.Xaml.Controls). 47d7fd7
- Five DEBUG hooks read the environment themselves (`App.xaml.cs:107,132`, `Presentation/MainPage.xaml.cs:170`,
  `Controls/Harbour/HarbourView.xaml.cs:834,849`, `HarbourScene.cs:22`) beside PortState's private `Hook`: all go
  through `Domain/DebugHooks.cs` now, which reads nothing in Release. d35a5f2

### Narration comments (fixed)
History-phrased justifications rewritten as present constraints: `Presentation/Items.cs:8`,
`Controls/OverviewCharts.cs:8,64`, `Domain/PortState.cs:70`, `Presentation/MainViewModel.cs:190`,
`Presentation/MainPage.xaml.cs:52,137`, `Controls/Harbour/HarbourRenderer.cs:52,137`; PortState's hook comment lost its
"before the properties became partial" clause. fd1c199, d35a5f2, d3fac30

### Pattern conformance
- Literal font sizes outside the theme (fixed, f57b314): `Presentation/BerthsView.xaml:124` and `NeedsYouBar.xaml:34`
  (`FontIcon FontSize="11"`, now `GlyphSizeSmall` in `Themes/Layout.xaml`); `Controls/ProportionalPanel.cs:63` (font
  family and size in code, now the `TypeMedium11` style).
- Kept: `Controls/ContainerScanner.cs:263` `FontSize = 9`, a label inside the scanner's scaled scene, in the scene's
  design units like the scene's other geometry.
- Templates: each of the three states its reason (above). f57b314
- `{Binding}`: 0. Region navigation in XAML; the store's own moves navigate in MainViewModel with `./` routes (no
  code-behind navigation).
- Debug verification: `CARGO_X11_RENDERER=software` added (Debug, desktop), as the other apps on this box have; the
  `CARGO_THEME` VERIFY line now waits for the palette to follow the window (it printed the old palette, noted by the
  completeness audit). d35a5f2
- Reviewed, kept: two `async void` font loads (`Controls/TimelineLanes.cs:145`, `Controls/Harbour/HarbourScene.cs:476`)
  await `CanvasFonts.Load`, which catches and returns null, so nothing can throw out of them. The tide lane's load is
  a `Task` now (`OverviewView.LoadTideFacesAsync`). The one other `async void` is the framework's `App.OnLaunched`
  override, which logs a launch failure to `startup.log` before rethrowing.

### Performance

Measured on the Debug desktop head under Xvfb (:456), 1440x900, idle 15-20 s windows after a 25-30 s settle, % of one
core from `/proc`. GL = the default X11 path (Mesa llvmpipe); CPU = `CARGO_X11_RENDERER=software`.

| State | GL | CPU backend | Breakdown (CPU backend) |
|---|---|---|---|
| Overview, motion on (before) | 130-175% | 49% | render thread 39%, UI thread 5% |
| Overview, motion on (after) | | 53% | render thread 47%, UI thread 5% (load noise) |
| Overview, reduce motion on | 27% | 4.9% | mostly tiered JIT |
| Overview scrolled (180 px mini band) | | 34% | render thread 23% |
| Berths (harbour strip) | | 38% | render thread 28% |
| Waterways | 9.2% | 0.9% | |

What ticks while idle (temporary render probe, not committed): the harbour's ambient timer 14.6/s with 13.5-14.7
scene renders/s, the store clock 1/s, the timeline lanes 1/s, the Liveline tide lane 1/s, ship glyphs 0. No held
`Rendering` subscription and no Forever storyboard. On llvmpipe each harbour frame is rasterised in software GL
(124% of a core in llvmpipe's threads); the completeness audit's 24% is not reproducible on this box today.

Fixed (d3fac30), the allocations on the frame path: the still scene's key was an interpolated string (pose, size,
inset, layer) built every frame, now a record struct compared by value; the atmosphere pass's rect array, now a
`stackalloc` span; `HarbourFrameState`, now a readonly record struct; the tide lane's two arrays, reassigned each
second, now only when NOW crosses a 15-minute sample. Allocation on idle Overview 12.4 MB per 5 s before, 12.2 after:
the rest is the live layer's geometry (cranes and trucks extruded per frame, by design). CPU is unchanged within noise:
the idle cost is the designed 15 fps ambient loop's rasterisation, not app code.

Not fixed, an owner decision: the ambient loop's idle policy. Options, each a few lines in `HarbourScene.Tick`: idle at
10 fps (a third less), hold still after some seconds without pointer input over the harbour, or animate only while the
window is active. Measure on a Windows GPU first: llvmpipe overstates every frame.

### Stack currency (reported, not changed)

| Package | In use | Newest stable | Newest | Note |
|---|---|---|---|---|
| Uno.Sdk | 7.0.0-dev.115 | 6.7.30 | 7.0.0-dev.132 | owner decision |
| Uno.WinUI | 7.0.0-dev.2509 | 6.7.135 | | SDK-pinned |
| Uno.Toolkit.WinUI | 11.0.0-dev.157 | 9.1.3 | 11.0.0-dev.157 | SDK-pinned |
| Uno.Extensions (Navigation, Hosting) | 8.0.0-dev.150 | 7.3.6 | 8.0.0-dev.250 | SDK-pinned |
| Uno.Themes.WinUI | 9.0.0-dev.61 | 7.1.1 | 9.0.0-dev.61 | SDK-pinned |
| Uno.Resizetizer | 2.0.0-dev.51 | 1.12.7 | 2.0.0-dev.71 | SDK-pinned |
| SkiaSharp | 4.151.1 | 4.154.0 | 4.156.0-preview.1 | SDK-pinned; `SkiaSharpVersion` could lift it |
| CommunityToolkit.Mvvm | 8.4.2 | 8.4.2 | | current |
| Microsoft.Extensions.Hosting | 10.0.12 | 10.0.14 | | transitive |
| Uno.Fonts.Fluent | 2.9.4 | 2.10.1 | | transitive |
| Microsoft.NET.Test.Sdk | 18.10.1 | 18.11.0 | | Directory.Packages.props |
| NUnit | 4.6.1 | 5.0.0 | | major version: a decision |
| NUnit3TestAdapter | 6.3.0 | 6.3.1 | | Directory.Packages.props |

.NET 10 TFMs on every head; the build used SDK 10.0.113.

## Build, tests, runtime

- Release, 0 warnings / 0 errors on the final code: `net10.0-desktop` (`--no-incremental`), `net10.0-browserwasm`,
  `net10.0-android`. Debug desktop 0 warnings after every change.
- `Cargo.Tests`: 125/125 (Release, `net10.0`), before and after the changes.
- Runtime, Debug desktop under Xvfb, CPU backend unless noted: Overview Light 1440 and 390x844, Dark 1440 (palette
  line `VERIFY theme=Dark actual=Dark palette=Dark`); harbour zoom in, the bake after the camera settles; the tide lane
  before and after NOW crossed 21:00 (the past series grows to the new sample); Overview scrolled; Berths Light (row
  chevrons at `GlyphSizeSmall`), Berths Dark confirm (the bar's done check), the berth timeline's segment labels
  (`TypeMedium11`); the layer switcher A/B across 1280; Expand and the camera menu across 600 both ways; Waterways.

## Done in the A pass (2026-11-04)

- `PortState` split as planned below: `NavigationState`, `StageState`, `ScannerState`, `YardState`, `CrewChat`,
  `PortSignals` and the static `ShiftClock`; `OpenVessel`, `OpenDocking` and `PickHullVessel` stay with the berth
  desk, since they pick a vessel before they move (7561a4e). `PortState` 856 > 582 lines (it gained the plan-derived status lines and the needs-you actions).
- Brushes, visibilities, geometry and images out of the view models (e2503c0, c852534 for the timeline's tide marks):
  rows carry states and the data's palette keys; views map them (`Presentation/Paint.cs`, x:Bind functions).
- View-model tests for every model that holds logic (c852534); 211 tests.
- Header tooltips from the store (1f7435c).

## Done in A pass 2 (2026-11-04)

- The needs-you queue and its settle actions left `PortState` for `NeedsYouQueue`, and the panel acts through
  `NeedsYouPanelViewModel` (0cd7053). The vessel copy moved to `VesselCopy`, written from the data rather than parsed
  back from display text (0a2f544). `PortState` 582 > 362 lines: the clock and the berth desk.
- The harbour's tags, facts card and screen-reader line moved to a tested `HarbourViewModel` (8745ada).
  `HarbourView.xaml.cs` 934 > 488 lines: the camera, the input, the band's motion and the per-frame tag placement.
- View state left `Domain/`: stage heights, toggle labels, Security's title and the scanner camera (b63c866).
  `Decision` carries no `ICommand` (0cd7053).
- One berth-number lookup (`PortData.BerthIndex`, `PortData.Berth`) and one stamp-to-clock helper
  (`ShiftClock.Display`) (a678e30, 685dae3).
- Dead members removed (b534043); the "nordic" special case in the berth map replaced by the pending arrival
  (255397e); the masthead's figures read the data (a503cdf).

## Remaining (next pass)

| Item | Where | Size |
|---|---|---|
| Icons kept as path strings in C# (18): the layer switcher (`MainViewModel.cs:298-301`), the cargo journey (`Domain/PortData.cs:31-35`), the activity kinds (`PortData.cs:519-523`), the vessel cards (`VesselsViewModel.cs:307-313`). Move them into `Themes/Icons.xaml` with the rest. First run the WinAppSDK head: `Geo.Path` caches one Geometry per path string and hands the same instance to every Path that uses it (`Controls/Geo.cs:12`), which Skia accepts and WinUI may not. | 3 files | 0.5 day + a Windows run |
| The harbour's idle policy (above) | Controls/Harbour/HarbourScene.cs:244 | owner decision, then 1-2 h |
| Pool the live layer's per-frame geometry (about 2.4 MB/s of allocation) | Controls/Harbour/HarbourRenderer.cs:682 | 0.5-1 day; low value unless GC pauses show |
| Test package bumps; NUnit 5 is a major version | Directory.Packages.props | 1 h + a decision |
| WinAppSDK and iOS not run at runtime (CI builds them); Android launches in CI's emulator job, but no workflow is driven there | | per head |
