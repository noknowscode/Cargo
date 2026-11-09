// node cap.js <url> <outdir> <scheme> <WxH> <dpr> <steps.json>
// steps: [{click:[x,y]}, {move:[x,y]}, {wheel:[dx,dy], at:[x,y]}, {wait:ms}, {shot:"name", clip:{x,y,width,height}}, {scheme:"dark"}, {key:"Escape"}]
const { chromium } = require('playwright');
const fs = require('fs');
(async () => {
  const [url, out, scheme, size, dpr, stepsFile] = process.argv.slice(2);
  const steps = JSON.parse(fs.readFileSync(stepsFile, 'utf8'));
  const [w, h] = size.split('x').map(Number);
  const b = await chromium.launch({ args: ['--hide-scrollbars'] });
  const ctx = await b.newContext({ viewport: { width: w, height: h }, deviceScaleFactor: Number(dpr), colorScheme: scheme });
  const p = await ctx.newPage();
  const errors = [];
  p.on('pageerror', e => errors.push(String(e)));
  await p.goto(url, { waitUntil: 'load', timeout: 180000 });
  const t0 = Date.now();
  while (Date.now() - t0 < 300000) {
    let strip = null;
    try { strip = await p.screenshot({ clip: { x: 10, y: 8, width: 300, height: 40 }, scale: 'css', timeout: 120000 }); } catch (e) { console.log('probe timeout'); }
    if (strip && strip.length > 1500) break;
    await p.waitForTimeout(2000);
  }
  console.log('rendered after', Math.round((Date.now() - t0) / 1000), 's');
  await p.mouse.move(w - 5, h - 5);
  for (const s of steps) {
    if (s.click) { await p.mouse.move(...s.click); await p.mouse.down(); await p.waitForTimeout(100); await p.mouse.up(); }
    if (s.move) await p.mouse.move(...s.move);
    if (s.wheel) { await p.mouse.move(...(s.at || [w / 2, h / 2])); await p.mouse.wheel(...s.wheel); }
    if (s.key) await p.keyboard.press(s.key);
    if (s.scheme) await p.emulateMedia({ colorScheme: s.scheme });
    if (s.wait) await p.waitForTimeout(s.wait);
    if (s.shot) {
      const t = Date.now();
      await p.screenshot({ path: `${out}/${s.shot}.png`, clip: s.clip, animations: 'disabled', timeout: 180000 });
      console.log('shot', s.shot, (Date.now() - t) + 'ms');
    }
  }
  if (errors.length) console.log('errors', errors.slice(0, 5));
  await b.close();
})();
