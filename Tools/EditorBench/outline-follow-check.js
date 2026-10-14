// Reading mode has no caret, so the outline follows the page instead. Two things have to hold: the heading
// it reports has to move with the page, and reporting it must not move the page — the first attempt sent
// the heading through StateChange, which is what the host scrolls from, and the page twitched by itself.
//
//   node outline-follow-check.js [--chars 120000]
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const args = process.argv.slice(2);
const at = (f, d) => { const i = args.indexOf(f); return i >= 0 ? Number(args[i + 1]) : d; };
const chars = at('--chars', 120000);
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');

const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});

const section = (i) => [`## Section ${i}`, `Paragraph ${i} with text long enough to wrap onto a second line.`, '- one\n- two'].join('\n\n');
let markdown = '', n = 0;
while (markdown.length < chars) markdown += section(++n) + '\n\n';

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'], protocolTimeout: 180000 });
  const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: true, markdown, basePath: '/tmp' };
  // A host that answers the way the real one does — and, where it used to be wrong, worse: this one
  // scrolls to whatever `cur` a state report carries. If the follower ever routes a scroll-derived heading
  // through that report again, this host will start the loop and the check will see the page moving.
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__prev={};window.__slugs=[];window.__jumps=0;
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const apply=(m)=>{if(typeof m.args!=='string')return null;const o=window.__prev[m.name];const v=m.diff?o.slice(0,m.start)+m.args+o.slice(m.end):m.args;window.__prev[m.name]=v;try{return JSON.parse(v)}catch(e){return null}};
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;
      if(m.name==='OutlineCurrent'){const a=apply(m);if(a&&a.slug)window.__slugs.push(a.slug);}
      if(m.name==='StateChange'){const a=apply(m);if(a&&a.state&&a.state.cur){window.__jumps++;window.__deliver('ScrollTo',{slug:a.state.cur.slug});}}
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 120000 });
  await new Promise(r => setTimeout(r, 1200));

  const seen = [];
  for (const y of [0, 4000, 12000, 30000, 0]) {
    const r = await page.evaluate(async (y) => {
      window.scrollTo(0, y);
      for (let i = 0; i < 20; i++) await new Promise(r => requestAnimationFrame(r));
      return { slug: window.__slugs[window.__slugs.length - 1], at: Math.round(window.scrollY) };
    }, y);
    seen.push(r);
    console.log(`  scrollY ${String(y).padStart(6)}  ->  heading ${r.slug ?? 'none'}, page at ${r.at}`);
  }

  // The same again, but reported while the page is still moving — a reader holds Page Down and the
  // answer has to be right during the movement, not only once it stops. Asking the browser where each
  // heading is one at a time was wrong exactly here: every question saw a different scroll position.
  const during = await page.evaluate(async () => {
    const heads = Array.from(document.querySelectorAll('#ag-editor-id > h1, #ag-editor-id > h2, #ag-editor-id > h3, #ag-editor-id > h4, #ag-editor-id > h5, #ag-editor-id > h6'));
    const order = new Map(heads.map((h, i) => [h.id, i]));
    window.scrollTo(0, 0);
    for (let i = 0; i < 10; i++) await new Promise(r => requestAnimationFrame(r));
    const start = window.__slugs.length;
    // Step down the document a screen at a time, sampling on every frame in between.
    for (let step = 0; step < 40; step++) {
      window.scrollBy(0, 700);
      await new Promise(r => requestAnimationFrame(r));
    }
    for (let i = 0; i < 20; i++) await new Promise(r => requestAnimationFrame(r));
    const reported = window.__slugs.slice(start).map(s => order.get(s)).filter(i => i !== undefined);
    let backwards = 0;
    for (let i = 1; i < reported.length; i++) if (reported[i] < reported[i - 1]) backwards++;
    return { count: reported.length, backwards, first: reported[0], last: reported[reported.length - 1], headings: heads.length };
  });
  console.log(`  while scrolling: ${during.count} headings reported, ${during.backwards} of them going backwards (${during.first} -> ${during.last} of ${during.headings})`);

  // Left alone, the page must stay exactly where the reader put it.
  const still = await page.evaluate(async () => {
    const before = Math.round(window.scrollY);
    await new Promise(r => setTimeout(r, 1500));
    return { before, after: Math.round(window.scrollY), jumps: window.__jumps };
  });
  console.log(`  left alone: page ${still.before} -> ${still.after}, scrolls asked for by the host: ${still.jumps}`);

  const moved = new Set(seen.map(s => s.slug).filter(Boolean)).size > 1;
  const cameBack = seen[seen.length - 1].slug === seen[0].slug;
  const steady = still.before === still.after;
  // Scrolling one way must never report a heading from further back.
  const forwards = during.backwards === 0 && during.count > 3;
  const ok = moved && cameBack && steady && forwards;
  console.log(ok ? 'OK: the outline follows the page, and following it does not move the page'
                 : `FAIL: ${!moved ? 'the heading does not follow' : !cameBack ? 'it does not come back' : !steady ? 'the page moved on its own' : `it went backwards ${during.backwards} times while scrolling down`}`);
  await browser.close(); server.close();
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
