// The issues users reported against the original Typedown, checked against the current editor:
//   A  a code block typed and saved straight away loses its text
//   B  emptying a table cell leaves it unable to take input until the focus goes elsewhere
//   C  PageUp/PageDown get stuck within a range instead of scrolling on
//   D  raw HTML (with inline CSS) does not render
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const server = http.createServer((req, res) => { let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html'; const f = path.join(statics, p); if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); } fs.createReadStream(f).pipe(res); });

const filler = Array.from({ length: 40 }, (_, i) => [
  `## Section ${i + 1}`,
  `Paragraph ${i + 1} with enough text to make the document scroll a long way past one screen.`,
  '```js\nconst a = ' + i + ';\nconst b = a * 2;\nconsole.log(a, b);\n```',
  '| x | y |\n|---|---|\n| 1 | 2 |',
  '> a quote that takes a line',
].join('\n\n')).join('\n\n');
const markdown = `# Heading\n\n| a | b |\n|---|---|\n| one | two |\n\n<div style="color: rgb(220, 20, 60); border: 2px solid rgb(0, 128, 0);">html block</div>\n\nA paragraph with <span style="color: rgb(0, 0, 255);">inline html</span> in it.\n\n${filler}\n`;

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1200, height: 800 });
  page.on('pageerror', e => console.log('PAGEERROR:', e.message.split('\n')[0]));
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '1000px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, markdown, basePath: '/tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 30000 });
  await new Promise(r => setTimeout(r, 600));
  const wait = (ms) => new Promise(r => setTimeout(r, ms));
  const md = () => page.evaluate(() => window.__typedownMuya.getMarkdown());

  // ---- D: html rendering -------------------------------------------------------------------------------
  const html = await page.evaluate(() => {
    // the preview is the rendered copy of the block, shown while the cursor is elsewhere
    const preview = document.querySelector('#ag-editor-id div.ag-html-preview [style]');
    const inline = [...document.querySelectorAll('#ag-editor-id span')].find(e => e.textContent === 'inline html');
    return {
      previewFound: !!preview,
      previewColour: preview ? getComputedStyle(preview).color : null,
      previewBorder: preview ? getComputedStyle(preview).borderTopWidth : null,
      inlineColour: inline ? getComputedStyle(inline).color : null,
    };
  });
  console.log('D html rendering:', JSON.stringify(html));

  // ---- A: code block typed then saved without leaving it -----------------------------------------------
  await page.evaluate(() => { const last = document.querySelectorAll('#ag-editor-id p.ag-paragraph'); last[last.length - 1].scrollIntoView(); });
  await page.click('#ag-editor-id p.ag-paragraph:last-of-type');
  await page.keyboard.press('End');
  await page.keyboard.press('Enter');
  await page.keyboard.type('```js');
  await page.keyboard.press('Enter');
  await wait(400);
  await page.keyboard.type('const answer = 42;');
  await wait(400);
  const straightAway = await md();
  await page.keyboard.press('ArrowDown');           // the "other editing operation" the report mentions
  await wait(300);
  const afterMoving = await md();
  console.log('A code block:', JSON.stringify({
    inMarkdownStraightAway: straightAway.includes('const answer = 42;'),
    inMarkdownAfterMoving: afterMoving.includes('const answer = 42;'),
  }));

  // ---- B: empty a table cell, then type into it ---------------------------------------------------------
  await page.evaluate(() => document.querySelector('#ag-editor-id table td').scrollIntoView({ block: 'center' }));
  await wait(200);
  // the reported way in: select the text with the mouse, delete it, carry on typing
  const box = await (await page.$('#ag-editor-id table td')).boundingBox();
  await page.mouse.move(box.x + 4, box.y + box.height / 2);
  await page.mouse.down();
  await page.mouse.move(box.x + box.width - 4, box.y + box.height / 2, { steps: 8 });
  await page.mouse.up();
  await wait(300);
  await page.keyboard.press('Delete');
  await wait(300);
  const emptied = await page.evaluate(() => document.querySelector('#ag-editor-id table td').textContent);
  await page.keyboard.type('NEW');
  await wait(400);
  const cell = await page.evaluate(() => document.querySelector('#ag-editor-id table td').textContent);
  console.log('B empty table cell:', JSON.stringify({ afterDelete: emptied, afterTyping: cell, emptied: emptied === '', typingLanded: cell.includes('NEW') }));

  // ---- C: PageUp / PageDown ------------------------------------------------------------------------------
  await page.evaluate(() => { const c = document.querySelector('.editor-container') || document.scrollingElement; c.scrollTop = 0; });
  await page.click('#ag-editor-id h1');
  const tops = [];
  const top = () => page.evaluate(() => { const c = document.querySelector('.editor-container') || document.scrollingElement; return Math.round(c.scrollTop); });
  for (let i = 0; i < 6; i++) { await page.keyboard.press('PageDown'); await wait(350); tops.push(await top()); }
  const ups = [];
  for (let i = 0; i < 6; i++) { await page.keyboard.press('PageUp'); await wait(350); ups.push(await top()); }
  console.log('C paging: down', JSON.stringify(tops), 'up', JSON.stringify(ups));

  await browser.close(); server.close();
})().catch(e => { console.error(e); process.exit(1); });
