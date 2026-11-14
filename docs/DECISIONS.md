# Decisions: Cargo (Westhaven Dispatch)

The owner's settled decisions, then the calls made during the production-readiness passes. A **proposed** entry is the
smallest reasonable call the pass needed; it stands until the owner answers it.

## Settled (owner)

| # | Decision |
|---|---|
| O-1 | MVVM with CommunityToolkit.Mvvm partial properties (`[ObservableProperty] public partial T X { get; set; }`), not MVUX; `x:Bind` everywhere, no `{Binding}`. |
| O-2 | Uno Simple theme plus the Toolkit; Light and Dark through `ThemeResource`. |
| O-3 | The app's own breakpoints: 600 / 1000 / 1280 / 1600 (App.xaml `DefaultResponsiveLayout`). |
| O-4 | Every figure is in-memory and the vessels' statuses are the design's picture of 20:58 (the clock runs, the evening does not play out). |
| O-5 | Fictional port (Westhaven) and fictional carriers, lessors and IMO numbers. |
| O-6 | Uno.Sdk 7.0.0-dev.115 (Uno 7, SkiaSharp 4). |
| O-7 | Refactors approved for the A pass: split `PortState` (plan in `docs/UNO-AUDIT.md`); view models hold states and the views pick the brushes; page-model tests. |
| O-8 | Functional gaps: build small, cut large. |

## Made in the A pass (2026-11-04)

### D-C1 · The hold's row settles when the inspector is assigned · proposed
Decision: Assign inspector (Security, Inspection) settles the "Hold on MRVU 918204 1" row in the needs-you queue and
drops its NEEDS ACTION chip from the log. The hold itself stays (the Cargo masthead, the Security eyebrow and the
tooltip still count 1 hold) until the 21:15 inspection, which the frozen demo never reaches.
Reason: the row asks the dispatcher to get the inspection staffed; once an inspector is assigned nothing waits on them.
Tradeoff: a dispatcher cannot release the hold from the app (that would be a new inspection-result feature).

### D-C2 · The driver check settles at Access control · proposed
Decision: the truck awaiting driver ID (TRK 8834) shows a Verify driver action on its card in Access control. It
settles the "Driver ID pending" row and the card reads "Authorized · driver ID verified".
Reason: Verify driver already sent the dispatcher there, where nothing could be done (completeness Minor).
Tradeoff: there is no ID data to check against; the action stands for the gate officer's check, like Assign inspector.

### D-C3 · Late orders are handled on Waterways, and their dot moves there · proposed
Decision: Confirm new ETA (a quick message on Waterways) to a late crew handles her late order: the row counts down
("2 orders late", "1 order late", then gone). The section dot for late orders moves from Cargo to Waterways, where View
orders lands and where they are handled.
Reason: the spec put the dot on Cargo, but nothing on Cargo can act on a river order; a dot must lead to the action.
Tradeoff: departs from SPEC-BERTHS-NEEDS-YOU's dot placement.

### D-C4 · Status lines and notes follow the berth plan · proposed
Decision: a vessel's status line, operations note, harbour tag and screen-reader name read the berth the plan gives
her (`PortState.StatusLine`, `OpsNote`); an arrival's status line counts down with the clock ("Arriving in 41 min"),
where the data had a frozen "42 min". The 3D ships stay at the data's berths.
Reason: after a drag or the optimiser the text contradicted the plan (completeness Major).
Tradeoff: the vessels' statuses (Docked, Arriving, ...) stay frozen at 20:58 (O-4); only the berth and the countdown
move.

### D-C5 · The scanner's short-window state reads the window's height · proposed
Decision: from 600 to 999 wide and under 560 high (a phone on its side) the scanner puts its result beside the stage
(`ScannerOverlay.IsShortLandscape`, a VisualState). The height is read in code-behind, the one height-driven layout in
the app; the size classes stay widths.
Reason: at 844 x 390 the stacked scanner gave the 3D stage no height (completeness Major).
Tradeoff: one layout rule outside the width ladder.

### D-C6 · A harbour tag with no clear spot gives way · proposed
Decision: when a tag cannot sit above, beside or below the tags already placed (a 390 px band with vessels close
together), it is hidden for that frame rather than drawn over another's countdown.
Reason: at 390 Baltic Crown's tag covered Nordic Star's countdown (completeness Minor).
Tradeoff: on the narrowest band one vessel can be untagged; her facts stay in Next 6 h, the Berths list and her hull.

## Made in A pass 2 (2026-11-04)

### D-C7 · A scan is logged where it is taken; the inspection record opens for the held box only · proposed
Decision: on any box but the held one, the scanner's action reads "Log scan". It adds "Scan logged · <box> · optical
or X-ray · <verdict>" to Security's inspection log at the time now, once per box, then reads "Scan logged" (disabled),
and the scanner stays open. The held box (MRVU 918204 1) keeps "Open inspection →". The container explorer offers
"Open inspection →" on the held box only; it showed "Inspection record →" on every box.
Reason: "Log scan →" logged nothing and opened MRVU 918204 1's inspection for any box, and so did "Inspection record →"
(the A-pass review's completeness Major). Security keeps an inspection record for the held box only.
Tradeoff: no inspection record is built for the other nine boxes, which are clear or under documentary review. The
log is in memory for the session, like every figure (O-4).

## Made for the showcase move (2026-11-04)

### D-C8 · A container-ship icon in place of the Uno template's · proposed
Decision: the launcher, splash and gallery icon is a container ship from the side (the bridge aft, two tiers of
boxes, the hull, a wave) in white on the accent, `AccentColor` #1C6E9E (`Themes/Tokens.xaml`): `Assets/Icons/icon.svg`
is the ground, `icon_foreground.svg` the glyph, `Assets/Splash/splash_screen.svg` the same glyph in the accent on the
splash's white. The template's viewBoxes stay; the glyph keeps about 14% margins, so it fills 72% of the icon on
desktop, WebAssembly and Windows (foreground scale 1.0), 58% on iOS (0.8) and 47% on Android (0.65, inside the
adaptive icon's safe zone).
Reason: the owner approved a simple custom icon for the apps that still shipped the template's (info file brief).
Tradeoff: flat and generic; no wordmark, so it does not say "Westhaven".

### D-C9 · The info file's icon and screenshot URLs point at the planned public home · proposed
Decision: `.studiolive/sample.json` names its icon and screenshots by
`https://raw.githubusercontent.com/unoplatform/Uno.Samples/master/studio/cargo/.studiolive/...`, as the format guide's
example does, so they resolve only once the sample moves to Uno.Samples.
Reason: the guide asks for absolute URLs; whether relative paths may be used instead is question 1 to the Studio team
(uno-private#2444).
Tradeoff: until the move, the gallery card's links are dead; the files themselves are in `.studiolive/`.

## Still open (owner questions, unchanged)

- Should dragging the waiting arrival off her reserved berth settle the decision, or need a confirm on the new berth?
  (Today the drag settles it.)
- The harbour's idle frame policy (15 fps ambient loop; options in `docs/UNO-AUDIT.md`).
- Accept the token snaps (odd gaps up one step, the swatch radius 3 to 2, the 9 radius to 10).
- Should the 3D ships follow the plan too (tags and text do since D-C4)?
