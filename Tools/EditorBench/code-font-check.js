// Code has to follow the font size setting and the tab size setting, in a code block, in inline code and in
// source mode. "The normal font can be enlarged but the text in code boxes stays as it was" and "pasted
// code is not indented by four" are Store reviews; a tab in a code block was drawn at the browser's default
// of eight columns whatever the setting said.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const markdown = 'Text with `inline code` here.\n\n```js\nfunction f() {\n\treturn 1;\n}\n```\n';
const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});
const open = async (browser, port, sourceCode) => {
  const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: false, sourceCode, markdown, basePath: '/tmp', loadId: 1 };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
  await new Promise(r => setTimeout(r, 600));
  return page;
};
const measure = (sourceCode) => {
  const px = (el, prop) => el ? parseFloat(getComputedStyle(el)[prop]) : null;
  if (sourceCode) {
    const cm = document.querySelector('.CodeMirror');
    const line = document.querySelector('.CodeMirror-line') || cm;
    // CodeMirror 5 draws tabs by its own option, not by CSS tab-size.
    return { code: px(line, 'fontSize'), tab: cm && cm.CodeMirror ? cm.CodeMirror.getOption('tabSize') : null, inline: null };
  }
  const root = document.getElementById('ag-editor-id');
  const inline = root.querySelector('p code');
  const block = root.querySelector('pre .CodeMirror-line') || root.querySelector('pre code') || root.querySelector('pre');
  return { code: px(block, 'fontSize'), tab: px(block, 'tabSize'), inline: px(inline, 'fontSize') };
};
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  let ok = true;
  for (const sourceCode of [false, true]) {
    const page = await open(browser, port, sourceCode);
    const before = await page.evaluate(measure, sourceCode);
    await page.evaluate(() => window.__deliver('SettingsChanged', { fontSize: 24, tabSize: 2 }));
    await new Promise(r => setTimeout(r, 800));
    const after = await page.evaluate(measure, sourceCode);
    const grew = before.code && after.code && after.code / before.code > 1.4;
    const inlineGrew = sourceCode || (before.inline && after.inline && after.inline / before.inline > 1.4);
    const tabs = before.tab === 4 && after.tab === 2;
    const good = grew && inlineGrew && tabs;
    ok = ok && good;
    console.log(`  ${sourceCode ? 'source mode' : 'rich mode  '}: code ${before.code}px -> ${after.code}px${grew ? '' : '  <- did not follow'}${sourceCode ? '' : `; inline ${before.inline}px -> ${after.inline}px${inlineGrew ? '' : '  <- did not follow'}`}; tab-size ${before.tab} -> ${after.tab}${tabs ? '' : '  <- not the setting'}`);
    await page.close();
  }
  console.log(ok ? 'OK: code follows the font size and the tab size' : 'FAIL');
  await browser.close(); server.close(); process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
