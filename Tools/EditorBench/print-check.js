// Printing prints the editor page itself, in its own theme. Under print media: none of the editing chrome
// (block icons, tool bars, drag handles, syntax markers) is visible; every block is laid out even in a long
// document (the off-screen rule would print blanks); the page is white; and a code block with a long line
// wraps instead of printing a scrollbar and losing its end (a Store review from 2022, still true in 2026).
//
//   node print-check.js
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const longLine = 'const veryLongIdentifier = ' + Array.from({ length: 30 }, (_, i) => `argumentNumber${i}`).join(' + ') + ';';
const section = (i) => [`## Section ${i}`, `Paragraph ${i} with text long enough to wrap onto a second line in the editor.`, '- one\n- two'].join('\n\n');
let markdown = `# Print\n\nSome text.\n\n> a quotation with a bar\n\n\`\`\`js\n${longLine}\nshort();\n\`\`\`\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n`;
for (let i = 1; i <= 2100; i++) markdown += section(i) + '\n\n';   // long enough for the off-screen rule
const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'], protocolTimeout: 180000 });
  const page = await browser.newPage(); await page.setViewport({ width: 794, height: 1123 });
  const dark = fs.readFileSync(path.join(statics, 'theme/editor/dark.theme.css'), 'utf8');
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: false, markdown, basePath: '/tmp', loadId: 1, themeCss: dark };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Dark',accentColor:{r:0,g:120,b:212,a:1},background:{R:40,G:40,B:40,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 120000 });
  await new Promise(r => setTimeout(r, 1500));
  // Click into a paragraph so a block is active, the way a reader who prints mid-edit has one.
  await page.click('#ag-editor-id p');
  await new Promise(r => setTimeout(r, 300));
  const screen = await page.evaluate(() => ({ chrome: Array.from(document.querySelectorAll('.ag-front-icon, .ag-tool-bar, .ag-drag-handler')).filter(e => getComputedStyle(e).display !== 'none').length, longDoc: document.getElementById('editor').classList.contains('ag-long-document') }));
  await page.evaluate(() => { (document.activeElement || {}).blur?.(); });
  await page.emulateMediaType('print');
  await new Promise(r => setTimeout(r, 500));
  const r = await page.evaluate(() => {
    const root = document.getElementById('ag-editor-id');
    const visible = (sel) => Array.from(document.querySelectorAll(sel)).filter(e => { const s = getComputedStyle(e); return s.display !== 'none' && s.visibility !== 'hidden'; }).length;
    const blocks = Array.from(root.children);
    const last = blocks[blocks.length - 3];
    const bg = getComputedStyle(document.body).backgroundColor;
    const pre = root.querySelector('pre');
    const px = (v) => parseFloat(v) || 0;
    const opaque = (c) => c && c !== 'transparent' && !/rgba\([^)]*,\s*0\)/.test(c);
    const quote = root.querySelector('blockquote');
    const td = root.querySelector('td');
    const th = root.querySelector('th');
    const preStyle = pre ? getComputedStyle(pre) : null;
    return {
      chrome: visible('.ag-front-icon, .ag-tool-bar, .ag-drag-handler'),
      lastBlockHeight: last ? last.getBoundingClientRect().height : 0,
      cv: last ? getComputedStyle(last).contentVisibility : null,
      bg,
      codeOverflow: pre ? pre.scrollWidth - pre.clientWidth : null,
      blocks: blocks.length,
      // The block visuals a reader expects on paper: a code box, a quote bar, table borders.
      codeBox: preStyle ? (opaque(preStyle.backgroundColor) && px(preStyle.borderTopWidth) > 0) : false,
      quoteBar: quote ? (px(getComputedStyle(quote).borderLeftWidth) > 0 || opaque(getComputedStyle(quote, '::before').backgroundColor)) : false,
      tableBorder: td ? (px(getComputedStyle(td).borderTopWidth) > 0 || px(getComputedStyle(td, '::before').borderTopWidth) > 0) : false,
      headerFill: th ? opaque(getComputedStyle(th).backgroundColor) : false,
    };
  });
  console.log(`  on screen: ${screen.chrome} editing controls visible, long-document rule ${screen.longDoc ? 'on' : 'off'}`);
  console.log(`  in print media: ${r.chrome} editing controls visible, body ${r.bg}, block ${r.blocks - 2} of ${r.blocks} is ${Math.round(r.lastBlockHeight)}px tall (content-visibility ${r.cv}), code block ${r.codeOverflow > 0 ? `${r.codeOverflow}px wider than its box` : 'fits'}`);
  console.log(`  block visuals: code box ${r.codeBox ? 'kept' : 'GONE'}, quote bar ${r.quoteBar ? 'kept' : 'GONE'}, table borders ${r.tableBorder ? 'kept' : 'GONE'}, header fill ${r.headerFill ? 'kept' : 'GONE'}`);
  const ok = r.chrome === 0 && r.lastBlockHeight > 0 && /255, 255, 255/.test(r.bg) && r.codeOverflow <= 0 && screen.longDoc && r.codeBox && r.quoteBar && r.tableBorder && r.headerFill;
  console.log(ok ? 'OK: the page prints as the reader sees it — chrome gone, blocks laid out, code/quote/table visible' : 'FAIL');
  await browser.close(); server.close(); process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
