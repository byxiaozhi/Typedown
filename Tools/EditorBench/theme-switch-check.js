// How long the page takes to look different after the host says the theme changed. Switching theme with a
// large document open left the old colours on screen for a while, and this says where that time goes.
//
//   node theme-switch-check.js [--chars 300000]
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const args = process.argv.slice(2);
const at = (flag, fallback) => { const i = args.indexOf(flag); return i >= 0 ? Number(args[i + 1]) : fallback; };
const chars = at('--chars', 300000);
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');

const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});

const section = (i) => [`## Section ${i}`, `Paragraph ${i} with text long enough to wrap onto a second line in the editor.`, '- one\n- two', '```js\nconst a = ' + i + ';\n```', '| a | b |\n|---|---|\n| 1 | 2 |'].join('\n\n');
let markdown = '', n = 0;
while (markdown.length < chars) markdown += section(++n) + '\n\n';

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, markdown, basePath: '/tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 120000 });
  await new Promise(r => setTimeout(r, 1500));

  const before = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);

  const result = await page.evaluate(async () => {
    const start = performance.now();
    const was = getComputedStyle(document.body).backgroundColor;
    window.__deliver('ThemeChanged', { theme: 'Dark', accentColor: { r: 0, g: 120, b: 212, a: 1 }, background: { R: 32, G: 32, B: 32, A: 1 } });
    let changed = null, painted = null;
    for (let i = 0; i < 600; i++) {
      await new Promise(r => requestAnimationFrame(r));
      if (changed === null && getComputedStyle(document.body).backgroundColor !== was) changed = performance.now() - start;
      if (changed !== null) { painted = performance.now() - start; break; }
    }
    return { changed, painted, now: getComputedStyle(document.body).backgroundColor };
  });

  console.log(`${markdown.length} chars`);
  console.log(`  background before        ${before}`);
  console.log(`  background after         ${result.now}`);
  console.log(`  colours changed after    ${result.changed === null ? 'never (600 frames)' : Math.round(result.changed) + ' ms'}`);
  await browser.close(); server.close();
})().catch(e => { console.error(e); process.exit(1); });
