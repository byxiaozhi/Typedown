// An editor left alone must go quiet. Every report costs the host a rebuild of the outline, and a stream of
// them with nobody typing shows up as an outline that flickers and collapses, a selection that cannot be
// dragged because it is destroyed on every render, and eventually a crash.
//
//   node idle-quiet-check.js [--seconds 4]
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const args = process.argv.slice(2);
const at = (f, d) => { const i = args.indexOf(f); return i >= 0 ? Number(args[i + 1]) : d; };
const seconds = at('--seconds', 4);
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');

const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});

const markdown = ['# A normal document', 'Some text.', '## First', 'More text.', '## Second', 'More text.'].join('\n\n') + '\n';

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'], protocolTimeout: 120000 });
  const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, markdown, basePath: '/tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__count={};window.__renders=0;
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;window.__count[m.name]=(window.__count[m.name]||0)+1;
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
  await new Promise(r => setTimeout(r, 1500));

  // Count what the editor sends, and how often it rebuilds its own document, while nothing touches it.
  const idle = await page.evaluate(async (seconds) => {
    const m = window.__typedownMuya, cs = m.contentState
    let renders = 0
    const render = cs.render.bind(cs); cs.render = (...a) => { renders++; return render(...a) }
    const partial = cs.partialRender.bind(cs); cs.partialRender = (...a) => { renders++; return partial(...a) }
    const before = JSON.parse(JSON.stringify(window.__count))
    await new Promise(r => setTimeout(r, seconds * 1000))
    const after = window.__count
    const sent = {}
    for (const k of Object.keys(after)) { const n = after[k] - (before[k] || 0); if (n) sent[k] = n }
    return { sent, renders }
  }, seconds);

  const noisy = Object.entries(idle.sent).filter(([k]) => k !== 'OnScroll');
  console.log(`  left alone for ${seconds}s with no input:`);
  console.log(`    messages to the host   ${noisy.length ? noisy.map(([k, n]) => `${k}×${n}`).join(', ') : 'none'}`);
  console.log(`    document re-renders    ${idle.renders}`);
  const ok = noisy.length === 0 && idle.renders === 0;
  console.log(ok ? 'OK: an editor left alone stays quiet' : 'FAIL: the editor keeps working with nobody touching it');
  await browser.close(); server.close();
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
