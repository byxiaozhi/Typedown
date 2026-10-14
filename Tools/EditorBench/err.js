const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve('../../Dev/Typedown/Resources/Statics');
const server = http.createServer((req, res) => { let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html'; const f = path.join(statics, p); if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); } fs.createReadStream(f).pipe(res); });
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  page.on('pageerror', e => console.log('PAGEERROR:', e.message.split('\n').slice(0, 3).join(' | ')));
  page.on('console', async m => { if (m.type() === 'error') { const args = await Promise.all(m.args().map(a => a.evaluate(x => (x && x.stack) || String(x)).catch(() => '?'))); console.log('CONSOLE:', args.join(' | ').split('\n').slice(0, 4).join(' | ')); } });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', readOnly: true, markdown: '# T\n\nSome **bold** text.\n\n| a | b |\n|---|---|\n| 1 | 2 |\n', basePath: '/tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await new Promise(r => setTimeout(r, 2500));
  console.log('paragraphs:', await page.evaluate(() => document.querySelectorAll('p.ag-paragraph').length), 'editor html len:', await page.evaluate(() => (document.querySelector('#ag-editor-id') || {}).innerHTML?.length || 0));
  await browser.close(); server.close();
})().catch(e => { console.error(e); process.exit(1); });
