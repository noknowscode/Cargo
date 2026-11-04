# Completeness audit: Cargo / Westhaven Dispatch (2026-11-04, Uno.Sdk 7.0.0-dev.115, heads: desktop, WinAppSDK, wasm, android, ios)

**Updated after the A pass** (`feat/production-readiness`, from ac065f1; the commits are listed under Fixed) and the
Android emulator job's two findings (Fixed after the A pass). The
first audit, at a6d1ce4, follows for the record; the Fixed table maps each of its findings to its fix. Runtime proof:
Debug `net10.0-desktop` under Xvfb (CPU renderer), driven with xdotool and screenshots, and a Release
`net10.0-browserwasm` build in headless Chromium (Playwright).

Verdict: **Complete with gaps** (0 Blockers, 0 functional Majors, 3 head Majors, 4 Minors)
Safe to show: every section; confirm and undo the berth (bar and Overview queue, desktop and WebAssembly); drag and
Move to berth with refusals, and the tags, docking card and Berths rows naming the new berth; the optimiser and the
36 h scrub; filter, scanner (also on a phone on its side), X-ray, inspection record, Assign inspector (settles the
hold's row), Request re-weigh; Verify driver at Access control; Confirm new ETA to a late crew (counts the late orders
down); crew chat; harbour layers, presets and tag > vessel profile; header tooltips; theme and reduce-motion choices
across a relaunch.
Keep off screen: nothing in the app. Under Xvfb, use the CPU renderer (`CARGO_X11_RENDERER=software`): the Mesa
llvmpipe OpenGL path can draw a black frame after a live resize across 600 (renderer issue, below).

By design (not findings): every figure is in-memory and the vessels' statuses are frozen at 20:58 (the clock runs;
README "The evening does not play out"), so nothing but the theme and reduce-motion choice persists across launches
and Loading/Error states have nothing to show. The carriers are fictional by owner decision. The 3D ships stay at the
data's berths (open owner question); the text about them follows the plan (D-C4).

## Fixed in the A pass

| Finding (first audit) | Severity | Fix | Commit | Proof |
|---|---|---|---|---|
| Harbour tags and status lines ignore the plan (`HarbourView.xaml.cs:494,520-523`, `HomeBerth`, docking card, Berths rows) | Major (Logic) | `PortState.StatusLine`/`OpsNote` read the plan; tags, their screen-reader names, docking card, status rows, vessel detail and vessel list follow them; an arrival's line counts down live | a89b9fc | R: drag Nordic Star to 08 > tag "B08 · arriving 21:40", card "Arriving in 42 min · assigned Berth 08"; optimiser > row 05 "Levant Express · Assigned to Berth 05 · pilots and mooring gang notified"; `PlanFollowTests` (6) |
| Scanner stage collapses at 844 x 390 (`ScannerOverlay.xaml`) | Major (Responsive) | From 600 to 999 wide and under 560 high the Short state puts the result beside the stage | cbb516e | R: 844x390 shows the box, presets, zoom and the result; 390x844 and 1000x480 unchanged |
| Header tooltips are hard-coded (`MainViewModel.cs:197-204`) | Major (Interactions) | `MainViewModel.SectionSummary` reads the countdowns (Ceiling, as the tags), the plan, the yard, the queue and the data, once a minute and on every settled decision; Cargo masthead and Security eyebrow read the data | 1f7435c | `HeaderTooltipTests` (6), `MainViewModelTests.The_tooltips_follow_the_clock_once_a_minute`; R: tooltips at runtime |
| Three of four queue rows cannot be cleared | Minor (Interactions) | Assign inspector settles the hold (D-C1); Verify driver on the waiting truck settles the driver check (D-C2); Confirm new ETA to each late crew counts the late orders down (D-C3) | 084e41d | R: Assign inspector > Needs you 3, log entry; Verify driver by keyboard > TRK 8834 "Authorized · driver ID verified"; Confirm new ETA > "1 order late"; `NeedsYouActionsTests` (6) |
| Late-orders dot on Cargo, View orders lands on Waterways | Minor (Navigation) | The dot moves to Waterways (D-C3) | 084e41d | R: the Waterways tab carries the dot; Cargo's is gone |
| Container detail keeps a box the filter hides | Minor (Data) | The detail moves to the first box the filter shows | 235ba64 | `StoreTests.A_filter_that_hides_the_open_box_moves_the_detail_to_its_first` |
| Crew thread does not scroll to the newest message | Minor (States) | The thread scrolls to the newest on every change | 235ba64 | R: after a reply and Confirm new ETA the newest bubble shows at the bottom of the 220 px thread |
| Harbour tags overlap at 390 | Minor (Responsive) | Tags try above, right, left and below; the pending arrival and the selected vessel are placed first; a tag with no clear spot gives way (D-C6) | e81054d | R: 390x844, Nordic Star's dark tag and countdown clear, Avren Aurora's beside it |
| Overview Table clips NET / H at 1680 | Minor (Responsive) | From 1280 the table takes the card's width and the vessels column trims | e81054d | R: 1680x1050 Table view, all seven columns inside the card |
| WebAssembly not run in this audit | Major (head) | Release wasm run in headless Chromium | ab8b4c0 | `docs/evidence/2026-11-04-wasm.md`: berth confirmed from Overview, Needs you 4 > 3, Berths dot gone |
| The black frame after a live resize to 599 (HANDOFF; not chased) | Not checked | Re-tested: reproduces on the X11 OpenGL path (llvmpipe) only, never on the CPU renderer or when opened at 599 | b7a638a | `docs/evidence/2026-11-04-black-frame-599.md` (rates, bisection, images) |

Also in the pass, not findings of the first audit: `PortState` split into its parts (7561a4e); view models hold
states, the views pick brushes, geometry and images (e2503c0); page-model tests (c852534, 211 tests in all);
automation names 11.0 > 31.9 per 1,000 XAML lines and no invisible tab stops, keyboard walked at runtime on every
section (bc72e6e).

## Fixed after the A pass (Android emulator job)

The emulator job's window dump of the first screen (run 21, job 126701803671) found two defects; run 22 (job
126722827591) is after the fixes. Bounds are physical pixels at 2.625 px/dp. Method and numbers:
`docs/evidence/2026-11-04-android-safe-area.md`.

| Finding | Severity | Fix | Commit | Proof |
|---|---|---|---|---|
| No safe area on Android: the header started at y 0 (wordmark y 43-103, account button y 16-132, in the status bar's band) and the section scroll ran to y 2400 under the gesture bar | Major (Platform) | `utu:SafeArea` in Padding mode, so the surfaces still run edge to edge: the header through a wrapper Border (Left, Top, Right; its own responsive padding stays its own), the section region inside the scroll (Left, Right, Bottom: padding at the content's end), the inspection panel (VisibleBounds) | a245b3e | R (CI run 22 dump): every header node 128 px lower, wordmark [42,171][666,231], account button [938,144][1054,260], tabs y 275-391; the scroll still runs to y 2400. The content's end is below the launch dump; a desktop simulation of the insets padded the sections 24 at their end (`2026-11-04-safe-area-simulated.jpg`). Desktop 1440/999/390 Light and Dark and the inspection overlay: unchanged but for the clock and the harbour's animation; WebAssembly: header and greeting match desktop |
| The phone figure tiles (and the summary column from 600) were read twice: Raw on their panel hid only the panel, so the texts stayed in the tree beside the lanes' name | Minor (Accessibility) | Each text in the tiles and the column is Raw; the lanes' name (the timeline summary) is the one reading | ad78135 | R (CI run 22 dump): none of run 21's tile texts ("3 of 7 done", "1.5 m", "2,447 today", "Net −34 / h", their labels and sub-lines) is in the tree; the lanes' node is below the first screen |

## Fixed in A pass 2

The A-pass code review (2026-11-04) found one functional Major the audit had missed.

| Finding | Severity | Fix | Commit | Proof |
|---|---|---|---|---|
| "Log scan →" (9 of 10 boxes) logged nothing: it closed the scanner and opened Security's inspection, which always shows MRVU 918204 1; "Inspection record →" in the container explorer did the same for every box (`ScannerViewModel.cs:143`, `SecurityView.xaml:329`, `ContainersView.xaml:194`) | Major (Interactions) | "Log scan" adds the scan to the inspection log (`Domain/InspectionLog.cs`, held by the store), once per box, and then reads "Scan logged"; "Open inspection →" is offered on the held box only, whose record Security shows (D-C7) | ITEM13 | `CargoViewModelTests` (3 new) |

## Current findings

### Blockers
- None.

### Majors (functional)
- None.

### Majors (head)
- [Testing] WinAppSDK never run (CI builds it) - X
- [Testing] Android: the CI emulator job installs and launches the Release APK and reads its first screen (marker,
  frame, no crash; run 22), but no workflow is driven there - R (launch only)
- [Testing] iOS never run (CI builds it) - X

### Minors
- [Logic] Confirm's busy state is a fixed `Task.Delay` on an in-memory write (`NeedsYouViewModel.ConfirmLatency`, spec'd 600 ms) - C
- [States] The empty crew-thread state is unreachable: every river vessel has a seeded thread - C
- [Performance] Idle CPU on Overview is the designed 15 fps harbour loop (49% of a core on the CPU backend under Xvfb, 4.9% with reduce motion); its idle policy is an owner decision - R
- [Accessibility] Screen reader never run (names on every list, card, chart and canvas target; keyboard walked with `CARGO_FOCUS_LOG=1`) - X

### Environment (not an app defect)
- Under Xvfb, Mesa llvmpipe's OpenGL path draws a black frame after a live resize across 600 on scrolled Waterways
  with the river map in view; the CPU renderer draws the same tree correctly. Evidence and method:
  `docs/evidence/2026-11-04-black-frame-599.md`.

## Critical workflows by head (A pass)

| Workflow | desktop | WinAppSDK | android | ios | wasm |
|---|---|---|---|---|---|
| 1. Confirm and undo the berth | R Complete (bar: "Berth 07 confirmed for Nordic Star", Berths dot gone; Undo restores the bar and the dot) | X | X | X | R Complete (Overview queue: Needs you 4 > 3, row gone, dot gone, tag light) |
| 2. Drag or move to a berth | R Complete (drag to 08: hint, plan, card, tag "B08 · arriving 21:40" all follow) | X | X | X | X |
| 3. Optimise and scrub the plan | R Complete (optimiser: row 05 and Levant's lines name Berth 05) | X | X | X | X |
| 4. Scan and inspect a container | R Complete (scanner at 1440 and at 844x390; Assign inspector logs and settles the hold row) | X | X | X | X |
| 5. Work the needs-you queue | R Complete (hold, driver, one late order and the berth settled where each row leads) | X | X | X | X |
| 6. Message a river crew | R Complete (typed reply sent with Enter, box cleared, thread shows the newest) | X | X | X | X |
| 7. Choice survives relaunch | R Complete (Dark saved, relaunch opens Dark, `VERIFY theme=Dark actual=Dark palette=Dark`) | X | X | X | X |

Keyboard (A pass): Tab walks on Overview, Berths, Cargo, Waterways and Security (Access) reach every interactive
area: tabs, account menu, harbour and its tags, camera, key, zoom, layers, Expand, Chart/Table, every queue action,
Next 6 h and log rows, Berths rows, plan controls, Move to berth, vessel list and profile, filters and cards, yard
map, river vessels, fleet rows, quick messages, chat box and Send, Security tabs, badges and Verify driver.

---

# First audit (a6d1ce4)

## Inventory

Screens: 12 intended, 12 present, 0 unreachable, 0 missing (source: README route table + specs; overlays derived).
Five sections (Overview, Berths, Cargo, Waterways, Security with Zones / Access / Inspection tabs), the scanner overlay,
the account flyout, the harbour camera menu, Key flyout and vessel facts card. 10 of 12 opened at runtime (not: Zones, Key).
Routes: 10 registered (`App.xaml.cs` RegisterRoutes), 8 XAML triggers (`uen:Navigation.Request`, header and Security
tabs) + 7 store jumps (OpenVessel, OpenDocking, OpenInspection, OpenAccess, Go, RevealNeedsYou, OpenSecurity), 0 dead
route names, 1 dead command (`OpenSecurity`, `Domain/PortState.cs:244`, bound nowhere).
Interactions: about 70 (63 XAML controls + 7 canvas gestures: harbour orbit, berth drag, plan scrub, scanner rotate, bays,
yard stacks, timeline dots), 0 dead by construction, 1 simulated (header tooltips). About 30 driven at runtime.
Critical workflows (derived; confirm):
1. Confirm and undo the waiting berth (Berths bar, Overview queue)
2. Drag or move a vessel to a berth, with refusals
3. Optimise and scrub the berth plan
4. Find a container, scan it, open the inspection record, act on it
5. Work the needs-you queue from Overview
6. Message a river crew
7. Theme and reduce-motion choice survives a relaunch

## Completeness by area

| # | Area | Status | Evidence | Blockers | Majors | Minors | Note |
|---|---|---|---|---|---|---|---|
| 1 | Screens | Complete | R | 0 | 0 | 0 | 10 of 12 opened; Zones, Key from code |
| 2 | Navigation | Partial | R | 0 | 0 | 1 | Tabs, store jumps, keyboard Tab+Enter work; late-orders dot on the wrong tab |
| 3 | Interactions | Simulated | R | 0 | 1 | 1 | Header tooltips hard-coded; 3 queue rows cannot be resolved |
| 4 | Logic | Broken | R | 0 | 1 | 1 | Harbour tags and status lines contradict the plan after a drag |
| 5 | Data | Complete | R | 0 | 0 | 1 | In-memory by design; theme/motion persist; filter keeps a hidden container |
| 6 | States | Partial | R | 0 | 0 | 2 | Busy/done/refused/empty proven; chat does not scroll to new; empty thread unreachable |
| 7 | Forms | Complete | R | 0 | 0 | 0 | Chat: Enter sends, empty send ignored, box clears |
| 8 | Authentication | N/A | C | 0 | 0 | 0 | Fixed duty operator from data; product reason to confirm |
| 9 | Responsive/adaptive UI | Broken | R | 0 | 1 | 2 | Scanner stage 0 px at 844x390; tags overlap at 390; table column clipped |
| 10 | Platform behavior | Partial | C | 0 | 0 | 0 | Desktop window sizing R; mobile back/rotation X |
| 11 | Failure handling | Complete | C | 0 | 0 | 0 | No external dependency; startup.log + unhandled-exception traces |
| 12 | Accessibility | Partial | R | 0 | 0 | 1 | Keyboard proven on header; screen reader never run |
| 13 | Performance | Partial | R | 0 | 0 | 1 | Idle 24% of a core on Overview (live harbour), 7.5% on Waterways |
| 14 | Security/privacy | Complete | C | 0 | 0 | 0 | No secrets; every CARGO_* hook Debug-only |
| 15 | Visual quality | Partial | R | 0 | 0 | 0 | Residue counted under Responsive |
| 16 | Testing | Partial | R | 0 | 4 | 0 | 125/125 unit tests; four heads never run |

## Critical workflows by head

| Workflow | desktop | WinAppSDK | android | ios | wasm |
|---|---|---|---|---|---|
| 1. Confirm and undo the berth | R Complete (bar: Confirming > "Berth 07 confirmed for Nordic Star", Berths dot gone, row 07 "berth confirmed", tag light; Undo restores all; queue Confirm: count 4 > 3) | X (CI build only) | X (no device) | X (no Mac) | X (not run) |
| 2. Drag or move to a berth | R Partial (drop: "assigned to Berth 08", plan, timeline, dot follow; 03 refused "restricted and takes no vessel"; back to 07 re-raises the decision; tag still "B07") | X | X | X | X |
| 3. Optimise and scrub the plan | R Complete ("Optimized: Levant Express to Berth 05"; scrub to Thu 06:30 redraws the map; Now returns to 20:58) | X | X | X | X |
| 4. Scan and inspect a container | R Complete (Inspection filter 10 > 2 cards; MRVU 918204 1 opens scanner; X-ray, drag rotates; Open inspection > Security; Assign inspector and Request re-weigh add log rows and stand done) | X | X | X | X |
| 5. Work the needs-you queue | R Partial (Open inspection, Verify driver, View orders navigate; only the berth row can be cleared) | X | X | X | X |
| 6. Message a river crew | R Complete (Enter sends "Dispatch 21:01", empty Send ignored, thread kept across vessel switches; session only by design) | X | X | X | X |
| 7. Choice survives relaunch | R Complete (Dark + Reduce motion written to Local.dat; relaunch opens Dark with the toggle on) | X | X | X | X |

## Findings

### Blockers
- None.

### Majors (functional)
- [Logic] Berth facts outside the plan do not follow it - `Controls/Harbour/HarbourView.xaml.cs:494`, `:520-523` (`HomeBerth`),
  vessel `StatusLine` in the docking card and Berth status rows (`Domain/PortData.cs`) - R - after a drag to 08 the tag reads
  "Nordic Star B07 · arriving 21:40" and the card reads "assigned Berth 07" above "Berth 08"; after the optimiser row 05
  reads "Levant Express · Waiting for Berth 06 to clear". The 3D ships stay at the data's berths (HANDOFF open question);
  the text is the cheap part - owner: mvux/uno-platform-agent (store-derived text)
- [Responsive] Scanner stage collapses at landscape phone height - `Presentation/ScannerOverlay.xaml` (result pane 200 px under
  the stage below 1000 wide) - R - at 844x390 the box, its stage and the zoom/Reset row are gone; the preset row sits on top
  of "Scan result". 390x844 and 1000x480 are fine - owner: uno-toolkit
- [Interactions] Header tooltips are hard-coded figures - `Presentation/MainViewModel.cs:197-204` - R - at 20:58:43 the Berths
  tooltip said "Nordic Star in 41 min" while her tag and row said 42 (Floor versus `PortState.MinutesUntil` Ceiling); it says
  "Berth 06 frees 21:30" and names Nordic Star after she is confirmed or moved; "2,146 moves today" while Overview shows
  2,447; "1 hold", "0 incidents" literals; rebuilt only on a section or decision change, so the minutes go stale - owner: mvux

### Majors (head)
- [Testing] WinAppSDK never run (CI builds it) - X
- [Testing] Android never run (Release build only) - X
- [Testing] iOS never run (CI builds it) - X
- [Testing] WebAssembly not run in this audit (a headless Chromium render on 2026-11-01 predates the B2/round-2 work) - X

### Minors
- [Interactions] Three of four queue rows cannot be cleared anywhere: Verify driver lands on Access control, which has no verify
  action; late orders and the hold stay after Assign inspector (spec: those actions navigate only) - R
- [Navigation] Late-orders dot sits on Cargo while View orders lands on Waterways, which has no dot - `Domain/PortState.cs`
  `SectionNeedsYou` (LateOrders > cargo) vs `GoCommand "fleet"` - R
- [Logic] Confirm's busy state is a fixed `Task.Delay` on an in-memory write (`Presentation/NeedsYouViewModel.cs:74`, spec'd 600 ms) - C
- [Data] The container detail keeps a container the active filter hides (Inspection filter, AVRU 482193 4 stays) - R
- [States] Crew thread does not scroll to the newest message: a quick reply lands below the 220 px viewer's fold
  (`Presentation/FleetView.xaml:283`) - R
- [States] The empty crew-thread state is unreachable: every river vessel has a seeded thread - C
- [Responsive] Harbour tags overlap at 390 px (Baltic Crown's covers Nordic Star's countdown) - R
- [Responsive] Overview Table clips its NET / H column at the card edge at 1680 (scrolls sideways; README says only below 1280) - R
- [Performance] Idle CPU, Debug under Xvfb: Overview 24.1% of one core (live harbour at 15 fps by design), Waterways 7.5%; RSS 764 MB - R
- [Accessibility] Screen reader never run; the 3D harbour and timeline dots are pointer-only by spec (tags, Table, Next 6 h carry them) - X

## Not checked (X)
- WinAppSDK, iOS, Android, WebAssembly at runtime - no device, no Mac, no Windows here - run workflows 1-7 per head.
- Wheel scrolling under Xvfb - two attempts (instant and held button-5 events) did not scroll; `CARGO_SCROLL` used instead.
- The black frame after a live resize to 599 on Waterways scrolled (HANDOFF) - tried once (600 > 599 at 640 high): it
  rendered normally. Not chased.
- Release build and Release start time - Debug only here (HANDOFF: Release 0 warnings on desktop, android, wasm).
- Security Zones tab, the harbour Key flyout, Activity row expand - read in code only.
- Text scaling and display density - not available under Xvfb.

## Noted for other skills
- uno-audit: dead `OpenSecurity` command (`Domain/PortState.cs:244`); the `CARGO_THEME` hook prints `palette=Dark` right
  after switching to Light (the palette follows a moment later).
- gold-standard-pass: tag collisions on the narrow harbour band; camera radio items show circles, not the design's check.

## Unresolved Questions
- Should dragging the waiting arrival off her reserved berth settle the decision, or need a confirm on the new berth?
  (open owner question; today the drag settles it, proven at runtime).
- Should the harbour follow the plan (tags and status text at least, ships later), or stay the data's 20:58 picture?
- Authentication N/A: a single fixed duty operator from `PortData.Operator`, no sign-in. Confirm that is the product.
- Loading and Error N/A: every figure is an in-memory constant. Confirm.
- Should Access control be able to verify a driver, and should the hold and late-orders rows be clearable, or is the queue
  read-only apart from the berth?
- Late orders: dot on Cargo (spec) or on Waterways (where View orders goes)?
- The seven critical workflows are derived from the README and specs; confirm them.
- Every X on a supported head above (WinAppSDK, Android, iOS, WebAssembly).
