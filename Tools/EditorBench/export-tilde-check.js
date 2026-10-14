// A single ~ is not strikethrough — a range like 1~6 or 2~5 (common in Chinese) must survive export intact.
// Only ~~text~~ (two tildes) strikes through. The export lexer used to accept one tilde, so an exported PDF
// struck through the middle of "1~6和2~5". Checks both the editor's own render and the export HTML.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const markdown = '范围 1~6 和 2~5 之间。真的 ~~删除线~~ 保留。\n';
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
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: false, markdown, basePath: '/tmp', loadId: 1 };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__export=null;
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'&&m.name==='ExportCallback')window.__export=m.args;
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
  await new Promise(r => setTimeout(r, 600));
  const editorDels = await page.evaluate(() => Array.from(document.querySelectorAll('#ag-editor-id del')).map(d => d.textContent));
  await page.evaluate(() => window.__deliver('Export', { type: 'html', context: 1, basePath: '/tmp', title: 't', options: {} }));
  await page.waitForFunction(() => window.__export, { timeout: 30000 });
  const html = await page.evaluate(() => typeof window.__export === 'string' ? JSON.parse(window.__export).html : window.__export.html);
  const dels = (html.match(/<del>([\s\S]*?)<\/del>/g) || []).map(m => m.replace(/<\/?del>/g, ''));
  const rangesIntact = html.includes('1~6') && html.includes('2~5');
  const strikeKept = dels.some(d => d.includes('删除线'));
  const onlyRealStrike = dels.length === 1 && strikeKept;
  console.log(`  editor: ${editorDels.length} strike run(s) ${JSON.stringify(editorDels)}`);
  console.log(`  export: ${dels.length} <del> ${JSON.stringify(dels)}; ranges 1~6/2~5 intact ${rangesIntact}`);
  const ok = editorDels.length === 1 && rangesIntact && onlyRealStrike;
  console.log(ok ? 'OK: a single ~ is text, only ~~ strikes through, in the editor and the export' : 'FAIL');
  await browser.close(); server.close(); process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
