# Completeness audit: Cargo / Westhaven Dispatch (2026-11-04, Uno.Sdk 7.0.0-dev.115, heads: desktop, WinAppSDK, wasm, android, ios)

Audited head: `feat/production-readiness` at a6d1ce4. Report mode, no code changed. Runtime proof: Debug `net10.0-desktop`
under Xvfb, driven with xdotool and screenshots (the App MCP is off in this SDK, `NO_UNO_STUDIO`). The previous file at
this path did not exist; `docs/audit-2026-09-29.md` is a UI audit, not a completeness audit.

Verdict: **Complete with gaps**
Safe to show: every section; confirm and undo the berth (bar and Overview queue); drag and Move to berth with refusals;
the optimiser and the 36 h scrub; filter, scanner, X-ray, inspection record, Assign inspector, Request re-weigh; crew
chat; harbour layers, presets and tag > vessel profile; theme and reduce-motion choices across a relaunch.
Keep off screen: the harbour tags and vessel status lines after a drag or the optimiser (they keep the data's berth);
the scanner on a landscape phone; the header tooltips; trying to clear the hold, driver or late-orders rows.

By design (not findings): every figure is in-memory and frozen at 20:58 (the clock runs, the vessels' statuses stay
the design's 20:58 picture; README "The evening does not play out"), so nothing but the theme and reduce-motion choice
persists across launches and Loading/Error states have nothing to show. The carriers are fictional by owner decision.

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
