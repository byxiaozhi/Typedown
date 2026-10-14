// A link to a place in the same document. Ctrl+click while editing, plain click in reading mode: the heading
// the fragment names has to land at the top of the window. Fragments are slugs of the heading text, the way
// the export names them (lower case, spaces to dashes, punctuation dropped, duplicates numbered), and may be
// percent-encoded. Five Store reviews said these links did nothing.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const filler = Array.from({ length: 12 }, (_, i) => `Paragraph ${i} of filler text so that the document scrolls.`).join('\n\n');
const markdown = `# Top\n\n[to section two](#section-two) · [to 中文](#中文标题) · [to second same](#same-1) · [encoded](#%E4%B8%AD%E6%96%87%E6%A0%87%E9%A2%98) · [dotted](#1.1-研究背景与意义)\n\n${filler}\n\n## Section Two\n\n${filler}\n\n## 中文标题\n\n${filler}\n\n## Same\n\n${filler}\n\n## Same\n\n${filler}\n\n## 1.1 研究背景与意义\n\n${filler}\n`;
const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});
const run = async (browser, port, readOnly) => {
  const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 600 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly, markdown, basePath: '/tmp', loadId: 1 };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__opened=[];
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.name==='OpenNewWindow')window.__opened.push(m.args);
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
  await new Promise(r => setTimeout(r, 800));
  const results = [];
  for (const [text, heading] of [['to section two', 'Section Two'], ['to 中文', '中文标题'], ['to second same', 'Same'], ['encoded', '中文标题'], ['dotted', '1.1 研究背景与意义']]) {
    await page.evaluate(() => window.scrollTo(0, 0));
    const box = await page.evaluate((t) => { const a = Array.from(document.querySelectorAll('#ag-editor-id a')).find(a => a.textContent.trim() === t); const r = a.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 }; }, text);
    if (!readOnly) await page.keyboard.down('Control');
    await page.mouse.click(box.x, box.y);
    if (!readOnly) await page.keyboard.up('Control');
    await new Promise(r => setTimeout(r, 400));
    const r = await page.evaluate((h, nth) => {
      const heads = Array.from(document.querySelectorAll('#ag-editor-id h2')).filter(x => x.textContent.replace(/^H₂#+\s*/, '').trim() === h);
      const el = heads[nth] || heads[0];
      const atEnd = Math.ceil(window.scrollY + window.innerHeight) >= document.documentElement.scrollHeight - 1;
      return { top: el ? Math.round(el.getBoundingClientRect().top) : null, y: Math.round(window.scrollY), atEnd };
    }, heading, text === 'to second same' ? 1 : 0);
    // A heading near the end lands as high as the page can go.
    const ok = r.top !== null && r.top >= 0 && (r.top <= 40 || r.atEnd) && r.y > 0;
    results.push(ok);
    console.log(`  ${readOnly ? 'reading' : 'editing'}: "${text}" -> "${heading}" ${ok ? `at the top (${r.top}px)` : `MISSED (heading at ${r.top}px, page at ${r.y})`}`);
  }
  const opened = await page.evaluate(() => window.__opened.length);
  if (opened) console.log(`  ${opened} link(s) were sent to the host to open externally — an anchor must never be`);
  await page.close();
  return results.every(Boolean) && opened === 0;
};
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const a = await run(browser, port, false); const b = await run(browser, port, true);
  const ok = a && b;
  console.log(ok ? 'OK: anchor links land on their heading, editing and reading' : 'FAIL');
  await browser.close(); server.close(); process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
