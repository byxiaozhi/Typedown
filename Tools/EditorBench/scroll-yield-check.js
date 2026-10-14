// The hold that keeps a long document at the offset it was left at must let go the moment the reader moves
// the page themselves. It answers scroll events, so without this it pulls the page back from under the
// pointer — and dragging the scrollbar raises no wheel event, which is how that shipped unnoticed.
//
//   node scroll-yield-check.js
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');

const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});

const section = (i) => [`## Section ${i}`, `Paragraph ${i} long enough to wrap onto a second line in the editor.`, '- one\n- two'].join('\n\n');
let markdown = '', n = 0;
// Long enough to arm the hold, short enough that the load does not block past the protocol timeout.
while (markdown.length < 80000) markdown += section(++n) + '\n\n';

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'], protocolTimeout: 180000 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: true, keepSwitchedDocuments: false, markdown, basePath: '/tmp' };

  // A page of its own per gesture. Sharing one meant only the first gesture ever met a live hold — the
  // later ones ran after it had expired and "let go" was true of nothing, which is how this check passed
  // against a build with the guard deliberately removed.
  const fresh = async () => {
    const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
    await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
    await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
    await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 120000 });
    await new Promise(r => setTimeout(r, 800));
    return page;
  };

  const gesture = (page, how, round) => page.evaluate(async ([how, text]) => {
    window.__deliver('LoadFile', { text, basePath: '/tmp', cursor: null, scrollTop: 9000, loadId: Date.now() });
    // Wait for the restore to land, while the hold is still running.
    for (let i = 0; i < 60; i++) {
      await new Promise(r => requestAnimationFrame(r));
      if (Math.abs(window.scrollY - 9000) < 3) break;
    }
    const landed = Math.round(window.scrollY);
    if (how === 'wheel') window.dispatchEvent(new WheelEvent('wheel', { deltaY: 240, bubbles: true }));
    if (how === 'pointer') window.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true }));
    if (how === 'touch') window.dispatchEvent(new Event('touchstart', { bubbles: true }));
    if (how === 'nothing') { /* the hold should still be holding */ }
    window.scrollTo(0, 1200);          // the reader takes the page somewhere else
    for (let i = 0; i < 30; i++) await new Promise(r => requestAnimationFrame(r));
    return { landed, after: Math.round(window.scrollY), height: document.body.scrollHeight };
  }, [how, markdown + `\n<!-- ${round} -->\n`]);

  let ok = true;
  let round = 0;
  for (const how of ['wheel', 'pointer', 'touch']) {
    const page = await fresh();
    const r = await gesture(page, how, ++round);
    await page.close();
    // What matters is that it is no longer at the offset the hold was keeping. Where it ends up instead is
    // the document's business — a shorter document clamps the reader's own scroll to something else.
    // The hold has to have been live, or nothing was tested: it must have put the page at 9000 first.
    if (Math.abs(r.landed - 9000) > 50) { console.log(`  ${how}: the hold never took hold (landed ${r.landed}) — nothing tested`); ok = false; continue; }
    const yielded = Math.abs(r.after - 9000) > 50;
    console.log(`  after ${how.padEnd(8)} the page stays at ${String(r.after).padStart(5)} (held at ${r.landed}, asked for 1200, document ${r.height}px)  ${yielded ? 'let go' : 'PULLED BACK'}`);
    if (!yielded) ok = false;
  }
  console.log(ok ? 'OK: the hold lets go however the reader moves the page' : 'FAIL: the hold pulls the page back from under the reader');
  await browser.close(); server.close();
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
