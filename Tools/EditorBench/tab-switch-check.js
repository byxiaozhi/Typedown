// How long a tab switch takes. The host keeps one editor and re-loads the document on every switch, so
// switching between a huge file and a small one pays a full load each way. This measures both directions.
//
//   node tab-switch-check.js [--big 300000] [--small 20000] [--rounds 3]
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const args = process.argv.slice(2);
const at = (flag, fallback) => { const i = args.indexOf(flag); return i >= 0 ? Number(args[i + 1]) : fallback; };
const bigChars = at('--big', 300000), smallChars = at('--small', 20000), rounds = at('--rounds', 3);
// --keep 0 turns off keeping switched-away documents in memory, which is the setting's off position.
const keep = at('--keep', 1) !== 0;
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');

const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});

const section = (i) => [`## Section ${i}`, `Paragraph ${i} with text long enough to wrap onto a second line in the editor.`, '- one\n- two', '```js\nconst a = ' + i + ';\n```', '| a | b |\n|---|---|\n| 1 | 2 |'].join('\n\n');
const doc = (chars) => { let m = '', n = 0; while (m.length < chars) m += section(++n) + '\n\n'; return m; };
const big = doc(bigChars), small = doc(smallChars);

const median = (a) => { const s = [...a].sort((x, y) => x - y); return s[Math.floor(s.length / 2)]; };

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  // --expose-gc so the heap can be read after a collection: without it the figure is mostly garbage that
  // has not been collected yet, and switching with the keeping turned *off* reads higher than with it on.
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox', '--js-flags=--expose-gc'], protocolTimeout: 180000 });
  const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, keepSwitchedDocuments: keep, markdown: big, basePath: '/tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__loaded=0;window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.name==='FileLoaded')window.__loaded++;if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 180000 });
  await new Promise(r => setTimeout(r, 1500));

  // One switch: deliver LoadFile, wait until the editor has the new text laid out, then keep watching for a
  // while. What a reader feels is not when the content appears but how long the window stops answering, so
  // the blocked time is the number that matters — work that lands after the content does still counts.
  const switchTo = (text, blocks) => page.evaluate(async ([text, blocks]) => {
    let blocked = 0, longest = 0;
    const po = new PerformanceObserver(l => { for (const e of l.getEntries()) { blocked += e.duration; longest = Math.max(longest, e.duration) } });
    po.observe({ entryTypes: ['longtask'] });
    const start = performance.now();
    window.__deliver('LoadFile', { text, basePath: '/tmp', cursor: null, scrollTop: 0, loadId: Date.now() });
    const root = () => document.querySelector('#ag-editor-id');
    let shown = null;
    for (let i = 0; i < 2000; i++) {
      await new Promise(r => requestAnimationFrame(r));
      if (shown === null && root() && root().children.length === blocks) shown = performance.now() - start;
      if (shown !== null && performance.now() - start > shown + 2000) break;
    }
    po.disconnect();
    return { shown: Math.round(shown ?? -1), blocked: Math.round(blocked), longest: Math.round(longest) };
  }, [text, blocks]);

  const count = async () => page.evaluate(() => document.querySelector('#ag-editor-id').children.length);
  // The editor normalizes the markdown it is given and the host stores what comes back, so a tab switch
  // hands over the normalized text, not the file as written. Switching with anything else is a load of a
  // different document — which is what the host would do too, and would miss a document held in memory.
  const normalized = async () => page.evaluate(() => window.__typedownMuya.getMarkdown());
  const bigBlocks = await count();
  const bigText = await normalized();
  await switchTo(small, 0).catch(() => 0); // prime; block count unknown yet
  const smallBlocks = await count();
  const smallText = await normalized();

  const toSmall = [], toBig = [];
  for (let r = 0; r < rounds; r++) {
    toBig.push(await switchTo(bigText, bigBlocks));
    toSmall.push(await switchTo(smallText, smallBlocks));
  }
  const heap = await page.evaluate(async () => {
    if (typeof window.gc === 'function') { window.gc(); await new Promise(r => setTimeout(r, 300)); window.gc() }
    return performance.memory ? Math.round(performance.memory.usedJSHeapSize / 1048576) : -1;
  });
  const line = (name, rows) => console.log(`  ${name.padEnd(26)}${String(median(rows.map(r => r.shown))).padStart(5)} ms${String(median(rows.map(r => r.blocked))).padStart(7)} ms${String(median(rows.map(r => r.longest))).padStart(7)} ms`);
  console.log(`big ${big.length} chars / ${bigBlocks} blocks, small ${small.length} chars / ${smallBlocks} blocks, keeping ${keep ? 'on' : 'off'}`);
  console.log(`  ${''.padEnd(26)}shown  blocked  longest frame`);
  line('switch to the big one', toBig);
  line('switch back to the small', toSmall);
  console.log(`  JS heap (after a gc)      ${heap} MB`);
  await browser.close(); server.close();
})().catch(e => { console.error(e); process.exit(1); });
