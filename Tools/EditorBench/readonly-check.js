// Reading mode must show the rendered document only: no Markdown markers, no active block, none of the
// editing affordances (table drag bars/tool bar, paragraph front icon) and no way to change the content.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const server = http.createServer((req, res) => { let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html'; const f = path.join(statics, p); if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); } fs.createReadStream(f).pipe(res); });
const markdown = '# Title\n\nSome **bold** and `code` and a [link](https://example.com).\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n- [ ] task\n';
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  page.on('pageerror', e => console.log('PAGEERROR:', e.message.split('\n')[0]));
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '1200px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: true, markdown, basePath: '/tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 30000 });
  await new Promise(r => setTimeout(r, 500));

  const click = async (sel) => { const el = await page.$(sel); if (el) { await el.click(); await new Promise(r => setTimeout(r, 250)); } };
  const measure = () => page.evaluate(() => ({
    editable: window.__typedownMuya.container.getAttribute('contenteditable'),
    gray: document.querySelectorAll('#ag-editor-id .ag-gray').length,
    active: document.querySelectorAll('#ag-editor-id .ag-active').length,
    dragBars: [...document.querySelectorAll('.ag-drag-handler')].filter(e => getComputedStyle(e).display !== 'none').length,
    toolBar: [...document.querySelectorAll('.ag-tool-bar')].filter(e => getComputedStyle(e).display !== 'none').length,
    frontIcon: [...document.querySelectorAll('.ag-front-icon')].filter(e => getComputedStyle(e).display !== 'none').length,
    markdown: window.__typedownMuya.getMarkdown(),
  }));

  // the exported form of the untouched document (the table delimiter row is normalized on export)
  const baseline = (await measure()).markdown;

  const probe = async (label) => {
    await click('strong');
    const inline = await measure();
    await click('table td');
    const table = await measure();
    await page.keyboard.type('XYZ');
    await new Promise(r => setTimeout(r, 300));
    const after = await measure();
    const state = { editable: inline.editable, grayOnInline: inline.gray, activeOnInline: inline.active,
      frontIcon: inline.frontIcon, dragBars: table.dragBars, toolBar: table.toolBar, changed: after.markdown !== baseline };
    console.log(label, JSON.stringify(state));
    return state;
  };

  // reading mode must still allow selecting text with the mouse and copying it
  const selectByDrag = async () => {
    const el = await page.$('p.ag-paragraph');
    if (!el) {
      console.log('DEBUG editor html:', (await page.evaluate(() => document.querySelector('#ag-editor-id')?.innerHTML || 'no editor')).slice(0, 400));
      return '';
    }
    const box = await el.boundingBox();
    await page.mouse.move(box.x + 4, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width - 10, box.y + box.height / 2, { steps: 10 });
    await page.mouse.up();
    await new Promise(r => setTimeout(r, 400));
    return page.evaluate(() => window.getSelection().toString());
  };

  const ro = await probe('reading mode :');
  const selected = await selectByDrag();
  console.log('reading mode : selection after drag', JSON.stringify(selected));
  const problems = [];
  if (!selected) problems.push('text cannot be selected with the mouse');
  if (ro.editable !== 'false') problems.push('container still editable');
  if (ro.grayOnInline) problems.push(`${ro.grayOnInline} source markers visible`);
  if (ro.activeOnInline) problems.push(`${ro.activeOnInline} active blocks`);
  if (ro.dragBars) problems.push(`${ro.dragBars} table drag handles`);
  if (ro.toolBar) problems.push(`${ro.toolBar} table tool bars`);
  if (ro.frontIcon) problems.push(`${ro.frontIcon} paragraph front icons`);
  if (ro.changed) problems.push('content changed');

  // switching reading mode off must bring editing back
  await page.evaluate(() => {
    const m = window.__typedownMuya;
    m.options.readOnly = false;
    document.body.classList.remove('read-only');
    m.container.setAttribute('contenteditable', 'true');
    m.contentState.render(true, true);
  });
  await new Promise(r => setTimeout(r, 300));
  const rw = await probe('edit mode    :');
  if (rw.editable !== 'true') problems.push('edit mode: container not editable');
  if (!rw.changed) problems.push('edit mode: typing had no effect');
  if (!rw.grayOnInline) problems.push('edit mode: no source markers around the caret');
  if (!rw.dragBars) problems.push('edit mode: no table drag handles');

  console.log(problems.length ? 'FAIL: ' + problems.join('; ') : 'OK');
  await browser.close(); server.close();
  process.exit(problems.length ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
