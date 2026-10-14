// Switching between documents over and over, the way a tab bar is actually used. One editor must be in the
// page and one only: a second one left behind is a second contenteditable root, which is why text could not
// be dragged and why it ended in a crash. Also checks the page goes quiet again afterwards.
//
//   node tab-stress-check.js [--rounds 12]
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const args = process.argv.slice(2);
const at = (f, d) => { const i = args.indexOf(f); return i >= 0 ? Number(args[i + 1]) : d; };
const rounds = at('--rounds', 12);
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');

const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});

const doc = (name) => [`# ${name}`, `Body of ${name}.`, `## ${name} one`, 'text', `## ${name} two`, 'text'].join('\n\n') + '\n';
const docs = ['Alpha', 'Beta', 'Gamma'].map(doc);

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'], protocolTimeout: 120000 });
  const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, markdown: docs[0], basePath: '/tmp' };
  const errors = [];
  page.on('pageerror', e => errors.push(String(e).slice(0, 200)));
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__count={};
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;window.__count[m.name]=(window.__count[m.name]||0)+1;
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
  await new Promise(r => setTimeout(r, 800));

  // The host hands back the text the editor reported, so collect each document's normalized form first.
  const texts = [];
  for (const d of docs) {
    await page.evaluate(async (text) => {
      window.__deliver('LoadFile', { text, basePath: '/tmp', cursor: null, scrollTop: 0, loadId: Date.now() });
      for (let i = 0; i < 60; i++) await new Promise(r => requestAnimationFrame(r));
    }, d);
    texts.push(await page.evaluate(() => window.__typedownMuya.getMarkdown()));
  }

  const dom = () => page.evaluate(() => ({
    editors: document.querySelectorAll('#editor').length,
    roots: document.querySelectorAll('#ag-editor-id').length,
    editable: document.querySelectorAll('[contenteditable="true"]').length,
    blocks: document.querySelector('#ag-editor-id')?.children.length ?? -1,
  }));

  let worst = null;
  for (let r = 0; r < rounds; r++) {
    const text = texts[r % texts.length];
    await page.evaluate(async (text) => {
      window.__deliver('LoadFile', { text, basePath: '/tmp', cursor: null, scrollTop: 0, loadId: Date.now() });
      for (let i = 0; i < 40; i++) await new Promise(r => requestAnimationFrame(r));
    }, text);
    const d = await dom();
    if (d.editors !== 1 || d.roots !== 1 || d.editable !== 1) { worst = { round: r + 1, ...d }; break; }
  }
  const after = worst || { round: rounds, ...(await dom()) };
  console.log(`  after ${after.round} switches: ${after.editors} editor element(s), ${after.roots} root(s), ${after.editable} editable, ${after.blocks} blocks`);

  // And it must settle: nothing sent, nothing rendered, once the switching stops.
  const idle = await page.evaluate(async () => {
    const before = JSON.parse(JSON.stringify(window.__count));
    await new Promise(r => setTimeout(r, 2500));
    const sent = {};
    for (const k of Object.keys(window.__count)) { const n = window.__count[k] - (before[k] || 0); if (n) sent[k] = n }
    return sent;
  });
  const noisy = Object.entries(idle).filter(([k]) => k !== 'OnScroll');
  console.log(`  then idle for 2.5s:        ${noisy.length ? noisy.map(([k, n]) => `${k}×${n}`).join(', ') : 'nothing sent'}`);
  if (errors.length) console.log(`  page errors:               ${errors.slice(0, 3).join(' | ')}`);

  let ok = !worst && noisy.length === 0 && errors.length === 0;
  // A key held down on the tab shortcut: two hundred loads in one burst, no time between them. The page
  // must end on the last one and must not have rendered them all — the host drops every report but the
  // newest anyway, and rendering each in turn kept the reader waiting a minute after the key came up.
  const burst = await page.evaluate(async () => {
    let fileLoaded = 0;
    const real = window.chrome.webview.postMessage;
    window.chrome.webview.postMessage = (raw) => { if (JSON.parse(raw).name === 'FileLoaded') fileLoaded++; return real(raw); };
    for (let i = 1; i <= 200; i++) window.__deliver('LoadFile', { text: `# burst ${i}\n\nparagraph ${i}\n`, basePath: '/tmp', cursor: null, scrollTop: 0, loadId: 100000 + i });
    await new Promise(r => setTimeout(r, 3000));
    const h1 = document.querySelector('#ag-editor-id h1');
    return { fileLoaded, shown: h1 ? h1.textContent.trim() : '(none)' };
  });
  const burstOk = burst.shown.includes('burst 200') && burst.fileLoaded <= 10;
  console.log(`  200 loads in one burst:    ${burst.fileLoaded} rendered, page shows "${burst.shown}"`);
  ok = ok && burstOk;
  console.log(ok ? 'OK: one editor in the page throughout, quiet afterwards' : burstOk ? 'FAIL: switching leaves the page in a state it should not be in' : 'FAIL: a burst of loads is rendered one by one');
  await browser.close(); server.close();
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
