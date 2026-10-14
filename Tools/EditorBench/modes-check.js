// Focus mode dims every block but the active one. Reading mode has no active block, so the two together used
// to grey out the whole document; reading mode must win.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const server = http.createServer((req, res) => { let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html'; const f = path.join(statics, p); if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); } fs.createReadStream(f).pipe(res); });
const markdown = '# Title\n\nFirst paragraph.\n\nSecond paragraph.\n';
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', readOnly: true, focusMode: true, typewriter: true, markdown, basePath: '/tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 30000 });
  await new Promise(r => setTimeout(r, 600));
  const read = () => page.evaluate(() => ({
    focusClass: window.__typedownMuya.container.classList.contains('ag-focus-mode'),
    dimmed: [...document.querySelectorAll('#ag-editor-id > *')].filter(e => parseFloat(getComputedStyle(e).opacity) < 0.5).length,
    blocks: document.querySelectorAll('#ag-editor-id > *').length,
    paddingTop: getComputedStyle(document.getElementById('editor')).paddingTop,
  }));
  const ro = await read();
  console.log('reading mode + focus + typewriter:', JSON.stringify(ro));
  await page.evaluate(() => { const m = window.__typedownMuya; m.options.readOnly = false; document.body.classList.remove('read-only'); m.container.setAttribute('contenteditable', 'true'); m.setFocusMode(true); m.contentState.render(true, true); });
  await new Promise(r => setTimeout(r, 400));
  const rw = await read();
  console.log('edit mode + focus + typewriter:   ', JSON.stringify(rw));
  const problems = [];
  if (ro.focusClass) problems.push('focus mode applied in reading mode');
  if (ro.dimmed) problems.push(`${ro.dimmed} of ${ro.blocks} blocks dimmed in reading mode`);
  if (ro.paddingTop.startsWith('calc') || parseFloat(ro.paddingTop) > 200) problems.push(`typewriter padding applied in reading mode (${ro.paddingTop})`);
  if (!rw.focusClass) problems.push('focus mode not applied in edit mode');
  console.log(problems.length ? 'FAIL: ' + problems.join('; ') : 'OK');
  await browser.close(); server.close();
  process.exit(problems.length ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
