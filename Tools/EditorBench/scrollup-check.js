const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.argv[2] || '../../Dev/Typedown/Resources/Statics'); const md = fs.readFileSync(process.argv[3] || 'big.md', 'utf8');
const server = http.createServer((req, res) => { let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html'; const f = path.join(statics, p); if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); } fs.createReadStream(f).pipe(res); });
(async () => { await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] }); const page = await browser.newPage(); await page.setViewport({ width: 1200, height: 800 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '1200px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', typewriter: false, markdown: md, basePath: 'C:\\tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' }); await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
  const target = await page.evaluateHandle(() => document.querySelectorAll('#ag-editor-id > p.ag-paragraph')[200]);
  await target.evaluate(el => el.scrollIntoView({ block: 'center' })); const box = await target.boundingBox(); await page.mouse.click(box.x + 10, box.y + 5);
  await new Promise(r => setTimeout(r, 200));
  await page.evaluate(() => window.scrollBy(0, 500)); await new Promise(r => setTimeout(r, 200));   // caret now ~100px above the viewport
  const before = await page.evaluate(() => window.scrollY);
  await page.keyboard.press('ArrowUp'); await new Promise(r => setTimeout(r, 300));
  const after = await page.evaluate(() => ({ scrollY: window.scrollY, caretY: window.__typedownMuya.getSelection().cursorCoords.y }));
  console.log('scrollY before ArrowUp:', before, 'after:', after.scrollY, 'caret y in viewport:', Math.round(after.caretY), '->', after.scrollY < before && after.caretY >= 0 ? 'PASS' : 'FAIL');
  await browser.close(); server.close(); })().catch(e => { console.error(e); process.exit(1); });
