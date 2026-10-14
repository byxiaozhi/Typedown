const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = require('path').resolve(process.argv[2] || '../../Dev/Typedown/Resources/Statics');
const server = http.createServer((req, res) => { let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html'; const f = path.join(statics, p); if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); } fs.createReadStream(f).pipe(res); });
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '1200px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', markdown: 'abc\n', basePath: 'C:\\tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__last={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));window.__deliver=deliver;const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    const prev={};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);return;}
      if(m.type==='diffmsg'){let full=m.args;if(m.diff){const old=prev[m.name]||'';full=old.slice(0,m.start)+m.args+old.slice(m.end);}prev[m.name]=full;window.__last[m.name]=JSON.parse(full);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 30000 });
  const show = s => JSON.stringify(s);
  // put cursor at end of 'abc' and press Enter to create an empty trailing paragraph
  await page.click('#ag-editor-id p'); await page.keyboard.press('End'); await page.keyboard.press('Enter');
  await new Promise(r => setTimeout(r, 300));
  let md = await page.evaluate(() => window.__typedownMuya.getMarkdown());
  let host = await page.evaluate(() => ({ md: window.__last.MarkdownChange && window.__last.MarkdownChange.text, cursor: window.__last.CursorChange && window.__last.CursorChange.cursor }));
  console.log('after Enter: muya.getMarkdown =', show(md), ' host MarkdownChange =', show(host.md), ' cursor =', show(host.cursor));
  // switch to source code mode and back, like the host does
  await page.evaluate(() => window.__deliver('SettingsChanged', { sourceCode: true }));
  await new Promise(r => setTimeout(r, 500));
  let cm = await page.evaluate(() => document.querySelector('.CodeMirror') && document.querySelector('.CodeMirror').CodeMirror.getValue());
  console.log('CodeMirror value:', show(cm));
  host = await page.evaluate(() => ({ md: window.__last.MarkdownChange.text }));
  console.log('host MarkdownChange after switch to source:', show(host.md));
  await page.evaluate(() => window.__deliver('SettingsChanged', { sourceCode: false }));
  await new Promise(r => setTimeout(r, 800));
  md = await page.evaluate(() => window.__typedownMuya.getMarkdown());
  host = await page.evaluate(() => ({ md: window.__last.MarkdownChange.text }));
  console.log('back to muya: getMarkdown =', show(md), ' host =', show(host.md));
  await page.evaluate(() => window.__deliver('SettingsChanged', { sourceCode: true }));
  await new Promise(r => setTimeout(r, 500));
  cm = await page.evaluate(() => document.querySelector('.CodeMirror').CodeMirror.getValue());
  console.log('CodeMirror value 2nd time:', show(cm));
  await browser.close(); server.close();
})().catch(e => { console.error(e); process.exit(1); });
