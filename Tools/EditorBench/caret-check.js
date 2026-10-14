// After a document arrives, edit mode has to have a caret: the editor focused, a selection inside it, and a
// key typed landing in the text. Checked for the first load and for a load that arrives as a tab switch.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: false, markdown: '# First\n\nhello first\n', basePath: '/tmp', loadId: 1, cursor: { anchor: { line: 2, ch: 5 }, focus: { line: 2, ch: 5 } } };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__last={};
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=(window.__marks[m.name]||0)+1;
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
  await new Promise(r => setTimeout(r, 800));
  const probe = async (label, typed) => {
    const before = await page.evaluate(() => document.querySelector('#ag-editor-id').textContent);
    await page.keyboard.type(typed);
    await new Promise(r => setTimeout(r, 300));
    const r = await page.evaluate((typed) => {
      const root = document.querySelector('#ag-editor-id');
      const sel = window.getSelection();
      const inside = sel.rangeCount > 0 && root.contains(sel.anchorNode);
      const active = document.activeElement;
      return { focused: active === root || root.contains(active) || (active && active.id === 'editor'), activeTag: active && (active.id || active.tagName), inside, typedLanded: root.textContent.includes(typed), activeBlock: !!root.querySelector('.ag-active') };
    }, typed);
    console.log(`  ${label}: editor focused ${r.focused} (active: ${r.activeTag}), selection inside ${r.inside}, active block ${r.activeBlock}, typed text landed ${r.typedLanded}`);
    return r.focused && r.inside && r.typedLanded;
  };
  const first = await probe('first load', 'XYZ1');
  await page.evaluate(() => window.__deliver('LoadFile', { text: '# Second\n\nhello second\n', basePath: '/tmp', cursor: { anchor: { line: 2, ch: 5 }, focus: { line: 2, ch: 5 } }, scrollTop: 0, loadId: 2 }));
  await new Promise(r => setTimeout(r, 1200));
  const second = await probe('after a tab switch', 'XYZ2');
  const ok = first && second;
  console.log(ok ? 'OK: a caret is there after every load' : 'FAIL: no caret after a load');
  await browser.close(); server.close(); process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
