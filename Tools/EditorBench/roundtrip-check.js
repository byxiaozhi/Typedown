const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const server = http.createServer((req, res) => { let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html'; const f = path.join(statics, p); if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); } fs.createReadStream(f).pipe(res); });
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '1200px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, markdown: 'x\n', basePath: 'C:\\tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 30000 });
  const inputs = process.argv.slice(2).length ? process.argv.slice(2).map(s => JSON.parse(s)) : ['abc', 'abc\n', 'abc\n\n', 'abc\n\n\n', 'abc\n\n\n\n', '- a\n- b\n\n\n', '# h\n\n\n'];
  for (const md of inputs) {
    const r = await page.evaluate((md) => { const cs = window.__typedownMuya.contentState; const blocks = cs.markdownToState(md); const summary = blocks.map(b => `${b.type}[${b.children.map(c => JSON.stringify(c.text)).join(',')}]`).join(' '); cs.blocks = blocks; return { summary, out: window.__typedownMuya.getMarkdown() }; }, md);
    console.log(JSON.stringify(md).padEnd(16), '->', r.summary.padEnd(28), '-> export', JSON.stringify(r.out));
  }
  await browser.close(); server.close();
})().catch(e => { console.error(e); process.exit(1); });
