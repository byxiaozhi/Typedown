// Switching tabs restores a document with a remembered scroll offset. This watches where the page actually
// is over the frames that follow, to catch the top of the document being shown for a moment before the
// offset is applied.
//
//   node scroll-flash-check.js [--chars 300000] [--scroll 40000]
//
// Prints the scroll position sampled every frame for the first half second. A restore that works shows the
// target from the first sample; a flash shows zero (or something else) first and the target later.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const args = process.argv.slice(2);
const at = (flag, fallback) => { const i = args.indexOf(flag); return i >= 0 ? Number(args[i + 1]) : fallback; };
const chars = at('--chars', 300000);
const scrollTarget = at('--scroll', 40000);
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');

const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});

const section = (i) => [`## Section ${i}`, `Paragraph ${i} with some text to make it wrap onto a second line in the editor.`, '- one\n- two', '> a quotation'].join('\n\n');
let markdown = '', n = 0;
while (markdown.length < chars) markdown += section(++n) + '\n\n';

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, markdown: '# start\n', basePath: '/tmp' };
  // Record who moves the page: every scroll call with its stack, so a jump can be traced to its caller.
  // Off by default: watching thousands of nodes distorts the very timings being measured.
  const trace = args.includes('--trace');
  if (trace) await page.evaluateOnNewDocument(() => {
    window.__scrolls = [];
    const note = (what, y) => {
      const stack = (new Error().stack || '').split('\n').slice(2, 6).join(' | ');
      window.__scrolls.push([Math.round(performance.now()), what, Math.round(y), stack]);
    };
    const realScrollTo = window.scrollTo.bind(window);
    window.scrollTo = function (...a) {
      const y = typeof a[0] === 'object' ? (a[0] && a[0].top) : a[1];
      note('scrollTo', y ?? -1); return realScrollTo(...a);
    };
    const realIntoView = Element.prototype.scrollIntoView;
    Element.prototype.scrollIntoView = function (...a) { note('scrollIntoView<' + this.tagName + '>', window.scrollY); return realIntoView.apply(this, a); };
    const desc = Object.getOwnPropertyDescriptor(Element.prototype, 'scrollTop');
    Object.defineProperty(Element.prototype, 'scrollTop', {
      get() { return desc.get.call(this); },
      set(v) { note('el.scrollTop<' + this.tagName + '>', v); return desc.set.call(this, v); },
    });
  });
  // Watch the editor root: a full re-render replaces its children, and a scroll container whose content is
  // replaced goes back to the top without anyone calling scrollTo.
  if (trace) await page.evaluateOnNewDocument(() => {
    window.__dom = [];
    const start = () => {
      const root = document.querySelector('#ag-editor-id');
      if (!root) return setTimeout(start, 50);
      new MutationObserver(records => {
        let added = 0, removed = 0;
        for (const r of records) { added += r.addedNodes.length; removed += r.removedNodes.length; }
        window.__dom.push([Math.round(performance.now()), added, removed, Math.round(window.scrollY)]);
      }).observe(root, { childList: true });
    };
    document.addEventListener('DOMContentLoaded', start);
  });
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__msgs=[];const prev={};window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);if(m.type==='diffmsg'){const full=m.diff?prev[m.name].slice(0,m.start)+m.args+prev[m.name].slice(m.end):m.args;prev[m.name]=full;m.args=JSON.parse(full);}window.__msgs.push(m);if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__msgs.some(m => m.name === 'FileLoaded'), { timeout: 30000 });
  await new Promise(r => setTimeout(r, 500));

  // What a tab switch does: hand the document over with the offset the tab was left at.
  await page.evaluate((text, scrollTop) => {
    window.__samples = [];
    const start = performance.now();
    const tick = () => {
      window.__samples.push([Math.round(performance.now() - start), Math.round(window.scrollY), Math.round(document.documentElement.scrollHeight)]);
      if (performance.now() - start < 6000) requestAnimationFrame(tick);
    };
    window.__msgs.length = 0;
    // With the caret the host remembers for the tab, at the top of the document — where it usually is,
    // and where the page was seen to end up a moment after landing at the offset.
    window.__deliver('LoadFile', { text, basePath: '/tmp', scrollTop, loadId: 2, cursor: { anchor: { line: 0, ch: 0 }, focus: { line: 0, ch: 0 } } });
    requestAnimationFrame(tick);
  }, markdown, scrollTarget);

  await new Promise(r => setTimeout(r, 7000));
  const samples = await page.evaluate(() => window.__samples);
  const final = await page.evaluate(() => Math.round(window.scrollY));

  console.log(`document ${markdown.length} chars, restoring to ${scrollTarget}`);
  console.log('  ms   scrollY   page height');
  for (const [ms, y, h] of samples.filter((s, i) => i < 8 || i % 10 === 0 || (i > 0 && Math.abs(s[1] - samples[i-1][1]) > 100))) console.log(String(ms).padStart(4), String(y).padStart(9), String(h).padStart(13));
  console.log(`final scrollY ${final}`);
  const scrolls = trace ? await page.evaluate(() => window.__scrolls) : [];
  const dom = trace ? await page.evaluate(() => window.__dom) : [];
  console.log('--- editor root children replaced (ms, added, removed, scrollY at that moment) ---');
  for (const row of dom.slice(-8)) console.log(row.join('  '));
  console.log('--- who moved the page ---');
  for (const [ms, what, y, stack] of scrolls.slice(-14)) console.log(String(ms).padStart(6), what.padEnd(26), String(y).padStart(8), stack.slice(0, 150));
  // Only frames where the document is long enough to hold the offset count: before it is laid out the page
  // is one viewport tall and sitting at zero, which is not the position being lost — there is nothing there
  // yet. Counting those made a clean restore read as a flash.
  const settled = samples.filter(([, , h]) => h > scrollTarget + 700);
  const firstFrame = settled.length ? settled[0][1] : null;
  const flashed = settled.some(([, y]) => Math.abs(y - scrollTarget) > 200) && Math.abs(final - scrollTarget) <= 200;
  // Ending somewhere else is worse than flashing: that is the tab switch that lands, then leaves.
  const lost = Math.abs(final - scrollTarget) > 200;
  console.log(lost ? `LOST: the page ended at ${final}, not ${scrollTarget}` : flashed ? `FLASH: the page was somewhere else before settling (first laid-out frame at ${firstFrame})` : `no flash: the restore held from the first laid-out frame (${settled.length} frames sampled)`);
  await browser.close(); server.close();
  process.exit(lost || flashed ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
