# Cargo (Westhaven Port Authority) gallery banners

Banners for the Uno Platform Studio samples gallery, made from real captures of the Cargo sample ("Westhaven Dispatch",
display name **Westhaven Port Authority**) running as a Release WebAssembly build in headless Chromium. Nothing in them is
redrawn: the UI, data and copy are the app's seeded state at the design clock (Wednesday 2 September, about 21:00).
Styling is only around and on top of the captures: device frames drawn in CSS, crop, perspective, falloff masks, light
and backdrop.

Open `index.html` for the contact sheet.

## Finals

All in `finals/`: every banner at 1x (1200 × 630) and 2x (2400 × 1260), and two heroes at 1920 × 1080.

| Banner | Family | What it shows | Source captures (route · theme · viewport · DPR) |
|---|---|---|---|
| `showcase-desk` | showcase | Desktop window with the Overview (Light) between a Berths phone (Dark) and a Cargo phone (Light), on harbour mist | `overview` · Light · 1440×900 · 3 (FA2) ; `berths` · Dark · 390×844 · 3 (PD) ; `cargo` · Light · 390×844 · 3 (FB) |
| `showcase-phones` | showcase | Three phones: Security (Light), Overview (Dark, centre, raised), Fleet & waterways (Light), on sea-mid blue | `security`, `fleet` · Light · 390×844 · 3 (PL) ; `overview` · Dark · 390×844 · 3 (POD) |
| `showcase-daynight` | showcase | Berths (Light) in front of the Overview (Dark), on a dusk gradient from harbour mist to night | `berths` · Light · 1440×900 · 3 (L) ; `overview` · Dark · 1440×900 · 3 (FA) |
| `focus-harbour` (+ hero) | focused | The SkiaSharp 3D harbour at night, Overview with the stage expanded: Baltic Crown departing B06, Nordic Star on her dashed approach to B07; laid back 10°, edges falling into the night Background | `overview` (Expand) · Dark · 1440×900 · 3 (D), crop 875×438 CSS px |
| `focus-timeline` | focused | Overview's "Today at North Quay" timeline: vessels, tide, crane moves on the NOW line; the Needs-you card blurred behind | `overview` scrolled 700 px · Light · 1440×900 · 3 (L), both cards cropped inside their borders |
| `focus-river` | focused | Macro crop of the live Alder River map, km 80 to km 0: Nordbank, Alder Trader, Lock 2, Westhaven Spirit, Lock 1, Westhaven Pride, Tug Kestrel, gauges | `fleet` · Dark · 1440×900 · 3 (D), crop 718×187 CSS px |
| `focus-cargo` | focused | The cargo classes: nine containers (standard, high cube, 20 ft, reefer, tank, hazmat) with route and status, laid back from a 1:1 bottom edge | `cargo` scrolled 300 px · Light · 1440×900 · 2 (XL), crop 1000×524 CSS px |
| `stylized-split` (+ hero) | stylized | One Overview, Light and Dark, cut on one diagonal running through backdrop and window | `overview` · Light then Dark, same session, 14 s apart · 1440×900 · 3 (FA2), top 692 CSS px |
| `stylized-stack` | stylized | Isometric stack of the five sections: Security, Waterways (Dark), Cargo, Berths (Light), Overview (Dark) on top | `security`, `fleet` · Dark (D) ; `cargo`, `berths` · Light (FA2, L) ; `overview` · Dark (FA) · all 1440×900 · 3 |
| `stylized-mosaic` | stylized | Every section at phone width, Light and Dark alternating, as a tilted wall of 18 devices | `overview`, `berths`, `cargo`, `fleet`, `security` · Light and Dark · 390×844 · 3 (POL, POD, PL, PD, FB) |
| `focus-harbour-titled` | focused, titled (**optional**) | `focus-harbour` with the display name in Bricolage Grotesque SemiBold (the app's display face, used for its wordmark and greeting) | as `focus-harbour` |

Strongest: `focus-harbour` and `stylized-split` (both have heroes).

The letters in brackets name the capture session (see `src/tools/cap.js`): L, D, PL, PD from the first published build;
FA, FA2, FB, POL, POD, XL from the build with the capsule-pill fix (`5568b29`, "pills are capsules again, not ovals").
Every capture that shows the Cargo filter pills or the masthead's shift bar comes from the fixed build.

## Palette

Backdrops and glows use the app's own colours: `Cargo/Styles/ColorPaletteOverride.xaml` (Primary #1C6E9E Light and
#6CB4E4 Dark, Secondary/Navy #0F4A6E, PrimaryContainer #E0EAEF, Background #F3F3F1 Light and #0F1315 Dark, Surface
#181D20 Dark) and the harbour data colours in `Cargo/App.xaml` (HarbourSeaFar #C9DDE4, SeaMid #B9CDDB, Water #5598C4,
SeaGreen #1E8F7A for the river's glow). They are listed in `src/frames.css`.

## Rules kept

- **Real pixels.** Every screen is a full-page or element-box capture; nothing is retouched.
- **No enlarging.** Captures are taken at DPR 3 (DPR 2 for the cargo grid), stored at 2x or 3x, and every composition
  displays them at or below native pixels in the 2x export, including the perspective's nearer edge (the cargo grid and
  the 1:1 compositions pivot on their bottom edge so nothing comes closer than 1:1).
- **Clean states.** Seeded data, animations settled (40 s or more after first render, then 7 to 25 s after each step),
  pointer parked on the header, Chromium's scrollbars hidden. One capture with the app's overlay scroll indicator (Berths
  after a scroll) was replaced by a clean one.
- **Generic frames.** Window title bars and phone bezels are plain CSS: no notch, camera or logo.
- **Safe area.** Key content sits inside the central 90%.

## Rebuild

`src/*.html` are the compositions (one per banner, the heroes as their own files), `src/cap/` the captures they use,
`src/frames.css` the shared frames and palette, `src/fonts/` the display font. Render a composition at its exact size
with `node src/tools/render.js <html> <out.png> <W> <H> <scale>` (Playwright; `NODE_PATH` must reach `playwright`).
`src/tools/cap.js` is the capture driver (load, wait for the wordmark, then click/scroll/theme steps from a JSON list).

## Uncertain

- The clock differs by a minute or two between captures (21:02 to 21:04): the app's clock runs from the design time.
  Where two captures sit side by side (`stylized-split`, `showcase-daynight`), it shows 21:04 and 21:03.
- `stylized-mosaic` repeats some sections (five sections, 18 devices): it reads as a wall, not an index.
- The Berths, Security and Waterways phone captures and the harbour, timeline, river, Waterways and Security desktop
  captures come from the build before the pill fix; none of them shows the affected pills or the shift bar.
- The titled variant uses Bricolage Grotesque, the app's wordmark face, rather than Archivo (the UI text face).
