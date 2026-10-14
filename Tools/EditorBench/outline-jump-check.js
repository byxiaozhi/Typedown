// Clicking a heading in the outline. The page has to land with that heading at the top of the window,
// and the outline has to end up marking the heading that was clicked — not the one before it, which
// is what happens when the heading is put a third of the way down and the follower then reads the top.
//
//   node outline-jump-check.js [--file doc.md]
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const args = process.argv.slice(2);
const at = (f, d) => { const i = args.indexOf(f); return i >= 0 ? args[i + 1] : d; };
const file = at('--file', null);
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const section = (i) => [`## Section ${i}`, `Paragraph ${i} with text long enough to wrap onto a second line.`, '- one\n- two'].join('\n\n');
let markdown = '';
if (file) markdown = fs.readFileSync(file, 'utf8'); else for (let i = 1; i <= 40; i++) markdown += section(i) + '\n\n';

const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'], protocolTimeout: 180000 });
  const page = await browser.newPage(); await page.setViewport({ width: 1200, height: 800 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: true, markdown, basePath: '/tmp', loadId: 1 };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__prev={};window.__slugs=[];
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const apply=(m)=>{if(typeof m.args!=='string')return null;const o=window.__prev[m.name];const v=m.diff?o.slice(0,m.start)+m.args+o.slice(m.end):m.args;window.__prev[m.name]=v;try{return JSON.parse(v)}catch(e){return null}};
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;
      if(m.name==='OutlineCurrent'){const a=apply(m);if(a&&a.slug)window.__slugs.push(a.slug);}
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 120000 });
  await new Promise(r => setTimeout(r, 4000));

  const results = await page.evaluate(async () => {
    const heads = Array.from(document.querySelectorAll('#ag-editor-id > h1, #ag-editor-id > h2, #ag-editor-id > h3, #ag-editor-id > h4, #ag-editor-id > h5, #ag-editor-id > h6'));
    const out = [];
    for (const h of heads) {
      window.__deliver('ScrollTo', { slug: h.id });
      for (let i = 0; i < 12; i++) await new Promise(r => requestAnimationFrame(r));
      const top = Math.round(h.getBoundingClientRect().top);
      const atEnd = Math.ceil(window.scrollY + window.innerHeight) >= document.documentElement.scrollHeight - 1;
      out.push({ id: h.id, text: h.textContent.trim().slice(0, 30), top, atEnd, told: window.__slugs[window.__slugs.length - 1] });
    }
    return out;
  });
  let placed = 0, marked = 0;
  for (const r of results) {
    const okTop = (r.top >= 0 && r.top <= 40) || r.atEnd; const okMark = r.told === r.id;
    if (okTop) placed++; if (okMark) marked++;
    if (!okTop || !okMark) console.log(`  ${r.text.padEnd(30)} landed ${String(r.top).padStart(5)}px from the top${r.atEnd ? ' (end of document)' : ''}, outline told ${okMark ? 'it' : r.told ? 'a different heading' : 'nothing'}`);
  }
  console.log(`${results.length} headings clicked: ${placed} landed at the top, ${marked} marked in the outline`);
  const ok = placed === results.length && marked === results.length;
  console.log(ok ? 'OK: clicking a heading puts it at the top and the outline marks it' : 'FAIL');
  await browser.close(); server.close(); process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
