// How far off the page height is while the blocks off screen have not been laid out. The scrollbar is drawn
// from that height, so an estimate three times too large makes the thumb tiny and the document race past the
// pointer when it is dragged.
//
//   node scroll-height-check.js [file.md] [--intrinsic 60]
//
// Reports the height the page reports at load, the height once everything has been laid out, and the error.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const args = process.argv.slice(2);
const file = args.filter((a, i) => !a.startsWith('--') && (i === 0 || !args[i - 1].startsWith('--')))[0];
const intrinsicAt = args.indexOf('--intrinsic');
const intrinsic = intrinsicAt >= 0 ? args[intrinsicAt + 1] : null;
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');

let markdown;
if (file) markdown = fs.readFileSync(file, 'utf8');
else {
  const section = (i) => [`## Section ${i}`, `Paragraph ${i} with text that wraps onto a second line in the editor.`, '- one\n- two', '```js\nconst a = ' + i + ';\n```', '> a quotation'].join('\n\n');
  markdown = ''; let n = 0;
  while (markdown.length < 300000) markdown += section(++n) + '\n\n';
}

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
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, markdown, basePath: '/tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  if (intrinsic) await page.evaluateOnNewDocument((px) => {
    document.addEventListener('DOMContentLoaded', () => {
      const style = document.createElement('style');
      style.textContent = `#editor.ag-long-document #ag-editor-id > * { contain-intrinsic-size: auto ${px}px; }`;
      document.head.appendChild(style);
    });
  }, intrinsic);

  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 120000 });
  await new Promise(r => setTimeout(r, 1500));

  const estimated = await page.evaluate(() => Math.round(document.documentElement.scrollHeight));
  const long = await page.evaluate(() => !!document.getElementById('editor')?.classList.contains('ag-long-document'));

  // Force everything to be laid out. Walking a screen at a time is too slow on a long document, so the rule
  // is simply turned off and the page measured again.
  await page.evaluate(() => {
    const style = document.createElement('style');
    style.textContent = '#editor.ag-long-document #ag-editor-id > * { content-visibility: visible !important; }';
    style.id = 'measure-real-height';
    document.head.appendChild(style);
    void document.documentElement.scrollHeight;
  });
  await new Promise(r => setTimeout(r, 1000));
  const real = await page.evaluate(() => Math.round(document.documentElement.scrollHeight));
  const blocks = await page.evaluate(() => document.querySelectorAll('#ag-editor-id > *').length);

  console.log(`${markdown.length} chars, ${blocks} top-level blocks, long-document rule ${long ? 'on' : 'off'}${intrinsic ? `, intrinsic ${intrinsic}px` : ''}`);
  console.log(`  height reported at load   ${estimated}`);
  console.log(`  height once laid out      ${real}`);
  console.log(`  error                     ${real ? Math.round((estimated - real) / real * 1000) / 10 : 0}%   (${Math.round(real / Math.max(1, blocks))} px per block once real)`);
  await browser.close(); server.close();
})().catch(e => { console.error(e); process.exit(1); });
