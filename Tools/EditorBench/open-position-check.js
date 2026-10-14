// Opening a real document with a remembered caret and scroll offset, the way the host does, and
// then reading it with the wheel. What has to hold, in either mode:
//   - once the load has settled, the page is where the host asked for it to be;
//   - the last heading the page reported is the one at the top of the window — the outline may not
//     be left pointing at a place the page merely passed through;
//   - no state report during reading carries no current heading, which is what empties the outline.
//
//   node open-position-check.js --file doc.md [--scroll 6000] [--line 5] [--readonly 1] [--click "5.4.1"]
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const args = process.argv.slice(2);
const at = (f, d) => { const i = args.indexOf(f); return i >= 0 ? args[i + 1] : d; };
const file = at('--file'); const scrollTop = Number(at('--scroll', 0)); const line = Number(at('--line', 0));
const readOnly = at('--readonly', '1') === '1'; const clickAt = at('--click', null);
if (!file) { console.error('need --file'); process.exit(2); }
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const markdown = fs.readFileSync(file, 'utf8');

const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'], protocolTimeout: 180000 });
  const page = await browser.newPage(); await page.setViewport({ width: 1400, height: 900 });
  const cursor = { anchor: { line, ch: 0 }, focus: { line, ch: 0 } };
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly, markdown, basePath: '/tmp', cursor, scrollTop, loadId: 1 };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__prev={};window.__log=[];window.__nocur=0;window.__states=0;window.__t0=performance.now();
    const t=()=>Math.round(performance.now()-window.__t0);
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const apply=(m)=>{if(typeof m.args!=='string')return null;const o=window.__prev[m.name];const v=m.diff?o.slice(0,m.start)+m.args+o.slice(m.end):m.args;window.__prev[m.name]=v;try{return JSON.parse(v)}catch(e){return null}};
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    const st=()=>new Error().stack.split('\\n').slice(2,7).map(l=>l.trim().replace(/^at /,'').replace(/https?:[^ ]*\\//,'')).join(' < ');const w=(o,k,n)=>{const f=o[k];o[k]=function(){window.__log.push({t:t(),call:n+'('+Array.from(arguments).map(a=>typeof a==='object'&&a?JSON.stringify(a).slice(0,40):a).join(',')+')',stack:st()});return f.apply(this,arguments)}};w(window,'scrollTo','scrollTo');w(window,'scrollBy','scrollBy');w(Element.prototype,'scrollIntoView','scrollIntoView');w(HTMLElement.prototype,'focus','focus');
    let lastY=-1;window.__hmax=0;const tick=()=>{const y=Math.round(window.scrollY);const h=document.documentElement.scrollHeight;if(h>window.__hmax)window.__hmax=h;if(y!==lastY){lastY=y;window.__log.push({t:t(),y,h});}requestAnimationFrame(tick)};requestAnimationFrame(tick);
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;
      if(m.name==='OutlineCurrent'){const a=apply(m);if(a&&a.slug)window.__log.push({t:t(),slug:a.slug,where:a.where});}
      if(m.name==='StateChange'){const a=apply(m);if(a&&a.state){window.__states++;window.__log.push({t:t(),cur:a.state.cur?a.state.cur.slug:null});if(!a.state.cur)window.__nocur++;}}
      if(m.name==='FileLoaded'){window.__log.push({t:t(),loaded:true});}
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 120000 });
  await new Promise(r => setTimeout(r, 3000));

  await page.evaluate(() => { window.__topHead = () => {
    const heads = Array.from(document.querySelectorAll('#ag-editor-id > h1, #ag-editor-id > h2, #ag-editor-id > h3, #ag-editor-id > h4, #ag-editor-id > h5, #ag-editor-id > h6'));
    // Reached once it is where an outline jump puts it (16px below the edge), the same rule the follower uses.
    let found = -1; heads.forEach((h, i) => { if (h.getBoundingClientRect().top <= 17) found = i; });
    const h = heads[found >= 0 ? found : 0]; return h ? { id: h.id, text: h.textContent.trim().slice(0, 40) } : null;
  }; });
  const name = async (slug) => page.evaluate((s) => { const h = document.getElementById(s); return h ? h.textContent.trim().slice(0, 40) : s; }, slug);
  const show = async (log) => { for (const e of log) console.log('    ' + String(e.t).padStart(6) + 'ms  ' + (e.loaded ? 'FileLoaded' : e.slug ? `outline -> ${await name(e.slug)}  [scrollY ${e.where && e.where.y}]` : 'cur' in e ? `state report, cur ${e.cur ? await name(e.cur) : 'NONE'}` : e.call ? `${e.call}   ${e.stack}` : `page at ${e.y} (document ${e.h} tall)`)); };

  console.log(`open ${path.basename(file)} ${readOnly ? 'reading' : 'editing'}, remembered scroll ${scrollTop}, caret line ${line}`);
  const first = await page.evaluate(() => { const l = window.__log.splice(0); return { log: l, top: window.__topHead(), y: Math.round(window.scrollY), nocur: window.__nocur, shrunk: window.__hmax - document.documentElement.scrollHeight, max: document.documentElement.scrollHeight - window.innerHeight }; });
  await show(first.log);
  const lastSlug = (log) => { const s = log.filter(e => e.slug); return s.length ? s[s.length - 1].slug : null; };
  const afterOpen = { y: first.y, top: first.top, last: lastSlug(first.log), nocur: first.nocur };
  if (args.includes('--probe')) {
    const probe = await page.evaluate(async () => {
      const before = window.__log.length;
      window.dispatchEvent(new Event('scroll'));
      for (let i = 0; i < 10; i++) await new Promise(r => requestAnimationFrame(r));
      const heads = Array.from(document.querySelectorAll('#ag-editor-id > h1, #ag-editor-id > h2, #ag-editor-id > h3, #ag-editor-id > h4, #ag-editor-id > h5, #ag-editor-id > h6'));
      return { reported: window.__log.slice(before).filter(e => e.slug).map(e => e.slug), tops: heads.slice(10, 18).map(h => [h.textContent.trim().slice(0, 12), Math.round(h.getBoundingClientRect().top)]), y: window.scrollY };
    });
    console.log('  probe: synthetic scroll event ->', JSON.stringify(probe));
  }
  console.log(`  settled: page at ${afterOpen.y}, heading at top "${afterOpen.top && afterOpen.top.text}", outline last told "${afterOpen.last ? await name(afterOpen.last) : 'nothing'}"${first.shrunk ? ` (the document got ${first.shrunk}px shorter after landing — diagrams rendering — and the browser kept the content in view)` : ''}`);

  // A reader with a wheel, three notches at a time, down the whole document.
  await page.mouse.move(900, 500);
  for (let i = 0; i < 90; i++) { await page.mouse.wheel({ deltaY: 500 }); await new Promise(r => setTimeout(r, 60)); }
  await new Promise(r => setTimeout(r, 1500));
  const read = await page.evaluate(() => { const l = window.__log.splice(0); return { log: l, top: window.__topHead(), y: Math.round(window.scrollY), nocur: window.__nocur, states: window.__states }; });
  const slugs = read.log.filter(e => e.slug); const nocurNow = read.log.filter(e => 'cur' in e && !e.cur);
  console.log(`  wheeled to ${read.y}: ${slugs.length} outline reports, ${read.log.filter(e => 'cur' in e).length} state reports of which ${nocurNow.length} without a current heading`);
  console.log(`  heading at top "${read.top && read.top.text}", outline last told "${(lastSlug(read.log) || afterOpen.last) ? await name(lastSlug(read.log) || afterOpen.last) : 'nothing'}"`);
  if (nocurNow.length) await show(read.log.filter(e => 'cur' in e).slice(0, 5));

  let clicked = null;
  if (clickAt) {
    // Put the caret into the first table under that heading, the way a reader clicks a cell, and see
    // what the outline is told.
    clicked = await page.evaluate(async (needle) => {
      const heads = Array.from(document.querySelectorAll('#ag-editor-id > h1, #ag-editor-id > h2, #ag-editor-id > h3, #ag-editor-id > h4, #ag-editor-id > h5, #ag-editor-id > h6'));
      const h = heads.find(x => x.textContent.includes(needle)); if (!h) return { error: 'no such heading' };
      let el = h.nextElementSibling; while (el && !el.querySelector('table')) el = el.nextElementSibling;
      if (!el) return { error: 'no table under it' };
      const cell = el.querySelectorAll('td')[1] || el.querySelector('td'); cell.scrollIntoView({ block: 'center' });
      await new Promise(r => setTimeout(r, 300));
      const r = cell.getBoundingClientRect(); return { id: h.id, x: r.left + r.width / 2, y: r.top + r.height / 2 };
    }, clickAt);
    if (!clicked.error) {
      await page.evaluate(() => window.__log.splice(0));
      await page.mouse.click(clicked.x, clicked.y); await new Promise(r => setTimeout(r, 800));
      const after = await page.evaluate(() => window.__log.splice(0));
      const curs = after.filter(e => 'cur' in e); const last = curs.length ? curs[curs.length - 1].cur : undefined;
      clicked.ok = last === clicked.id;
      console.log(`  clicked a cell under "${clickAt}": ${curs.length} state reports, last says cur ${last === undefined ? '(none sent)' : last ? await name(last) : 'NONE'}`);
    } else console.log(`  click: ${clicked.error}`);
  }

  // Diagrams render after the page has landed, and the document above the window gets shorter; the browser
  // then keeps the same content in view, which moves scrollY by that much. That is not the page leaving.
  // An offset past the end of the document lands at the end.
  const settledRight = Math.abs(afterOpen.y - scrollTop) <= Math.max(2, first.shrunk) || (scrollTop === 0) || (scrollTop > first.max && Math.abs(afterOpen.y - first.max) <= 2);
  const opened = readOnly ? (afterOpen.last && afterOpen.top && afterOpen.last === afterOpen.top.id) : true;
  // A page that could not move (opened at the end) reports nothing while wheeling; what it said on opening still stands.
  const followed = readOnly ? ((lastSlug(read.log) || afterOpen.last) === (read.top && read.top.id)) : true;
  const quiet = afterOpen.nocur === 0 && nocurNow.length === 0;
  const clickOk = !clicked || clicked.error || clicked.ok;
  const ok = settledRight && opened && followed && quiet && clickOk;
  console.log(ok ? 'OK' : `FAIL: ${!settledRight ? `page settled at ${afterOpen.y}, not ${scrollTop}` : !opened ? 'after opening, the outline points elsewhere than the page' : !followed ? 'after wheeling, the outline points elsewhere than the page' : !quiet ? 'a state report had no current heading' : 'the caret in a table cell was not placed under its heading'}`);
  await browser.close(); server.close(); process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
