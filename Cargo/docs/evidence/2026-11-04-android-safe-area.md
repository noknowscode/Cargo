# Safe area on Android, and the timeline's figures read once (2026-11-04)

The Android emulator job (API 34 x86_64, Pixel 6 profile, 1080 x 2400 at 2.625 px/dp) reads the app's accessibility
tree with `uiautomator dump` after launch. Run 21 (job 126701803671, at 2e3cb44) showed two defects; run 22 (job
126722827591, at ad78135) is after the fixes (a245b3e, ad78135). Bounds are physical pixels.

## Safe area

| Node | Run 21 (before) | Run 22 (after) |
|---|---|---|
| Wordmark "Westhaven Port Authority" | [42,43][666,103] | [42,171][666,231] |
| Account button | [938,16][1054,132] | [938,144][1054,260] |
| Section tabs | [11,147][1054,263] | [11,275][1054,391] |
| Harbour | [0,266][1080,1642] | [0,394][1080,1770] |
| Section scroll | [0,1642][1080,2400] | [0,1770][1080,2400] |

Every header node moved down 128 px (48.8 dp), the status bar's inset: the topmost, the account button, now starts at
y 144. The header's surface still starts at y 0 (SafeArea's Padding mode), and the scroll still runs to the window's
bottom edge. The bottom inset is padding at the end of the section's content: what sits at the viewport's bottom edge
before you scroll is still under the gesture bar (edge to edge), and the last content can now scroll clear of it.
The launch dump shows only the top of the content, so it cannot show that end; it was checked on desktop instead.

**Desktop simulation** (temporary code, not committed): the Toolkit's internal `SafeArea.SafeAreaOverride` set on the
header and the section region by reflection to 0, 48.8, 0, 24 (a status bar and a gesture bar), Debug desktop at
390 x 844 under Xvfb. SafeArea applied padding 0,49,0,0 to the header and 0,0,0,24 to the sections.
`2026-11-04-safe-area-simulated.jpg`: left, Overview scrolled to its end with no insets (the activity card ends 33 px
from the bottom); middle, the same with the simulated insets (the header's surface fills the 49 px band, the card
ends 57 px from the bottom, 24 px of page ground below it); right, the top in Dark with the insets.

**Desktop and the browser are unchanged.** Debug desktop, Overview at 1440 x 900, 999 x 800 and 390 x 844, Light and Dark,
plus 390 scrolled to the end and the inspection overlay at 390 (Light) and 1440 (Dark), each captured before and
after the change: the only differing pixels are the header clock's seconds and the harbour's animation (cranes, the
arriving ship's track). Release WebAssembly in headless Chromium at the same sizes and themes: the header and
greeting bands at 390 match the desktop frames to 24-47 px of anti-aliasing out of 39,000 and 93,600.

## The timeline's figures

Raw on the tile panel (phones) and on the summary column (600 and up) hid only the panel: their texts stayed in the
tree, so the figures were read there and again in the lanes' name (`TimelineViewModel.Summary`). Each of those texts
is Raw now. Run 21's dump listed the tiles' texts ("VESSELS", "3 of 7 done", "1 departing · 3 to come", "TIDE",
"1.5 m", ..., "Net −34 / h"); run 22's lists none of them, and the lanes' name, which reads them once, is below the
first screen at launch, so it is not in either dump.
