// Drives the built Typedown editor (Dev/Typedown/Resources/Statics) in headless Chrome with a stubbed
// WebView2 host, loads a large document, types into it and reports per-keystroke cost.
//   node bench.js [staticsDir] [docPath] [label]
const puppeteer = require('puppeteer-core');
const http = require('http');
const fs = require('fs');
const path = require('path');

const statics = path.resolve(process.argv[2] || '../../Dev/Typedown/Resources/Statics');
const docPath = path.resolve(process.argv[3] || 'doc80k.md');
const label = process.argv[4] || 'run';
const markdown = fs.readFileSync(docPath, 'utf8');

const mime = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.woff': 'font/woff', '.woff2': 'font/woff2', '.json': 'application/json', '.svg': 'image/svg+xml', '.png': 'image/png' };
const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]);
  if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  res.setHeader('Content-Type', mime[path.extname(f)] || 'application/octet-stream');
  fs.createReadStream(f).pipe(res);
});

const settings = {
  focusMode: false, typewriter: false, sourceCode: false, fontSize: 16, lineHeight: 1.6,
  autoPairBracket: true, autoPairQuote: true, trimUnnecessaryCodeBlockEmptyLines: false, preferLooseListItem: true,
  autoPairMarkdownSyntax: true, editorAreaWidth: '1200px', fontFamily: '', textDirection: 'auto', tabSize: 4,
  markdown, basePath: 'C:\\tmp',
};
const theme = { theme: 'Light', accentColor: { r: 0, g: 120, b: 212, a: 1 }, background: { R: 249, G: 249, B: 249, A: 1 } };

// Host stub: installed before any page script runs.
const hostStub = `(() => {
  const listeners = [];
  const stats = { msgs: {}, bytes: {}, invokes: [] };
  window.__hostStats = stats;
  const deliver = (name, args) => listeners.forEach(l => l({ data: JSON.stringify({ name, args }) }));
  window.__hostDeliver = deliver;
  const responses = {
    GetSettings: ${JSON.stringify(settings)},
    GetCurrentTheme: ${JSON.stringify(theme)},
    ContentLoaded: '',
    GetStringResources: {},
  };
  window.chrome = { webview: {
    addEventListener: (_t, l) => listeners.push(l),
    postMessage: (raw) => {
      const msg = JSON.parse(raw);
      if (msg.type === 'invoke') {
        stats.invokes.push(msg.name);
        const data = msg.name in responses ? responses[msg.name] : null;
        setTimeout(() => deliver(msg.id, { code: 0, data }), 0);
        return;
      }
      stats.msgs[msg.name] = (stats.msgs[msg.name] || 0) + 1;
      stats.bytes[msg.name] = (stats.bytes[msg.name] || 0) + raw.length;
    },
  }};
})();`;

// Wraps hot functions on the Muya instance once it exists, accumulating self time per function.
const profilerStub = `(() => {
  window.__prof = { t: {}, n: {} };
  const wrap = (obj, name, key) => {
    const orig = obj[name];
    if (typeof orig !== 'function' || orig.__wrapped) return;
    const w = function (...a) { const s = performance.now(); try { return orig.apply(this, a); } finally { const d = performance.now() - s; window.__prof.t[key] = (window.__prof.t[key] || 0) + d; window.__prof.n[key] = (window.__prof.n[key] || 0) + 1; } };
    w.__wrapped = true; obj[name] = w;
  };
  window.__installProfiler = (muya) => {
    wrap(muya, 'getMarkdownAndCursor', 'muya.getMarkdownAndCursor(ExportMarkdown)');
    wrap(muya, 'getWordCount', 'muya.getWordCount');
    wrap(muya, 'getTOC', 'muya.getTOC');
    wrap(muya, 'dispatchChangeContentChange', 'muya.dispatchChangeContentChange(total)');
    wrap(muya, 'dispatchSelectionChange', 'muya.dispatchSelectionChange');
    wrap(muya, 'dispatchSelectionFormats', 'muya.dispatchSelectionFormats');
    wrap(muya.contentState, 'render', 'contentState.render');
    wrap(muya.contentState, 'partialRender', 'contentState.partialRender');
    wrap(muya.contentState, 'singleRender', 'contentState.singleRender');
    wrap(muya.contentState, 'inputHandler', 'contentState.inputHandler');
    wrap(muya.contentState, 'selectionChange', 'contentState.selectionChange');
    wrap(muya.contentState, 'selectionFormats', 'contentState.selectionFormats');
    wrap(muya.contentState, 'getTOC', 'contentState.getTOC');
    wrap(muya.contentState, 'getMarkdownAndCodeMirrorCursor', 'contentState.getMarkdownAndCodeMirrorCursor');
    wrap(JSON, 'stringify', 'JSON.stringify(all)');
  };
})();`;

(async () => {
  await new Promise(r => server.listen(0, r));
  const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox', '--disable-gpu', '--window-size=1400,1000'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1400, height: 1000 });
  page.on('pageerror', e => console.log('PAGEERROR', e.message.slice(0, 300)));
  page.on('console', m => { const t = m.text(); if (/error|Error/.test(t)) console.log('CONSOLE', t.slice(0, 200)); });
  await page.evaluateOnNewDocument(hostStub);
  await page.evaluateOnNewDocument(profilerStub);

  const t0 = Date.now();
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  for (let i = 0; i < 60; i++) { const st = await page.evaluate(() => ({ msgs: Object.keys(window.__hostStats.msgs), inv: window.__hostStats.invokes, blocks: document.querySelectorAll('#ag-editor-id > .ag-paragraph').length, ids: document.querySelectorAll('[data-id]').length, html: document.body.innerHTML.length })); if (st.msgs.includes('FileLoaded') && st.blocks > 50) break; if (i % 5 == 0) console.log('wait', JSON.stringify(st).slice(0, 300)); await new Promise(r => setTimeout(r, 1000)); }
  const loadMs = Date.now() - t0;
  const blocks = await page.evaluate(() => document.querySelectorAll('#ag-editor-id > .ag-paragraph').length);

  // the editor exposes its Muya instance for tooling
  const found = await page.evaluate(() => { window.__muya = window.__typedownMuya; return !!window.__muya; });
  if (!found) { console.log('could not locate Muya instance'); }
  else await page.evaluate(() => window.__installProfiler(window.__muya));

  // click into the middle paragraph, then type
  const target = await page.evaluateHandle(() => { const ps = [...document.querySelectorAll('#ag-editor-id > p.ag-paragraph')]; return ps[Math.floor(ps.length / 2)]; });
  await target.evaluate(el => el.scrollIntoView({ block: 'center' }));
  const box = await target.boundingBox();
  await page.mouse.click(box.x + 20, box.y + box.height / 2);
  await new Promise(r => setTimeout(r, 300));
  await page.evaluate(() => { window.__prof = { t: {}, n: {} }; for (const k in window.__hostStats.msgs) delete window.__hostStats.msgs[k]; for (const k in window.__hostStats.bytes) delete window.__hostStats.bytes[k]; });

  // Measure: type N chars, each followed by waiting for the frame to settle (event loop idle)
  const N = 40;
  const text = 'the quick brown fox 跳过 lazy dog '.repeat(2).slice(0, N);
  const times = [];
  const cdp = await page.createCDPSession();
  await cdp.send('Profiler.enable'); await cdp.send('Profiler.setSamplingInterval', { interval: 200 }); await cdp.send('Profiler.start');
  for (const ch of text) {
    const s = Date.now();
    await page.keyboard.type(ch);
    await page.evaluate(() => new Promise(r => setTimeout(r, 0)));   // flush the setTimeout(0) selection dispatch
    times.push(Date.now() - s);
  }
  const { profile } = await cdp.send('Profiler.stop'); fs.writeFileSync(`profile-${label}.cpuprofile`, JSON.stringify(profile));
  times.sort((a, b) => a - b);
  const avg = times.reduce((a, b) => a + b, 0) / times.length;
  const prof = await page.evaluate(() => window.__prof);
  const stats = await page.evaluate(() => window.__hostStats);

  console.log(`\n=== ${label}: doc ${markdown.length} chars, ${blocks} blocks, load ${loadMs} ms ===`);
  console.log(`keystroke latency (ms): avg ${avg.toFixed(1)}  p50 ${times[Math.floor(times.length * .5)]}  p90 ${times[Math.floor(times.length * .9)]}  max ${times[times.length - 1]}`);
  console.log('per-keystroke self time (ms, avg over %d keystrokes):', N);
  Object.entries(prof.t).sort((a, b) => b[1] - a[1]).forEach(([k, v]) => console.log(`  ${(v / N).toFixed(2).padStart(8)}  ${k}  (calls/keystroke ${(prof.n[k] / N).toFixed(1)})`));
  console.log('messages to host per keystroke:', Object.fromEntries(Object.entries(stats.msgs).map(([k, v]) => [k, (v / N).toFixed(1)])));
  console.log('bytes to host per keystroke:', Object.fromEntries(Object.entries(stats.bytes).map(([k, v]) => [k, Math.round(v / N)])));

  // paste test: insert a 5k chunk
  const chunk = markdown.slice(0, 5000);
  await page.evaluate(() => { window.__prof = { t: {}, n: {} }; });
  const ps = Date.now();
  await page.evaluate((c) => { const dt = new DataTransfer(); dt.setData('text/plain', c); document.activeElement.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true })); }, chunk);
  await page.evaluate(() => new Promise(r => setTimeout(r, 50)));
  console.log(`paste 5k chars: ${Date.now() - ps} ms`);
  const prof2 = await page.evaluate(() => window.__prof);
  Object.entries(prof2.t).sort((a, b) => b[1] - a[1]).slice(0, 6).forEach(([k, v]) => console.log(`  ${v.toFixed(1).padStart(8)}  ${k}`));

  await browser.close();
  server.close();
})().catch(e => { console.error(e); process.exit(1); });
