// ==text== is highlight. It has to render as <mark>, survive a round trip through the editor unchanged,
// export as <mark>, be stripped from the outline's heading text, and come back as ==text== when HTML with
// <mark> is imported. Three Store reviews asked for this; before the rule went in the markers were plain text.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const markdown = '# Title with ==light== word\n\nSome ==highlighted text== here, and ~~struck~~ and **bold ==inside==**.\n\nNot ==\n\nthis== across paragraphs.\n';
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
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__prev={};window.__state=null;window.__export=null;
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const apply=(m)=>{if(typeof m.args!=='string')return null;const o=window.__prev[m.name];const v=m.diff?o.slice(0,m.start)+m.args+o.slice(m.end):m.args;window.__prev[m.name]=v;try{return JSON.parse(v)}catch(e){return null}};
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;
      if(m.name==='StateChange'){const a=apply(m);if(a&&a.state)window.__state=a.state;}
      if(m.name==='FileLoaded'){const a=apply(m);if(a)window.__loaded=a.text;}
      if(m.name==='MarkdownChange'){const a=apply(m);if(a)window.__md=a.text;}
      if(m.type==='invoke'&&m.name==='ExportCallback'){window.__export=m.args;}
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded && window.__state, { timeout: 60000 });
  await new Promise(r => setTimeout(r, 500));

  const r = await page.evaluate(() => {
    const marks = Array.from(document.querySelectorAll('#ag-editor-id mark')).map(m => m.textContent);
    const raw = document.querySelector('#ag-editor-id').textContent;
    return { marks, rawHasMarkers: raw.includes('==highlighted text=='), loaded: window.__loaded, toc: window.__state.toc.map(t => t.content) };
  });
  await page.evaluate(() => window.__deliver('Export', { type: 'html', context: 1, basePath: '/tmp', title: 'T', options: {} }));
  await page.waitForFunction(() => window.__export, { timeout: 30000 });
  const html = await page.evaluate(() => typeof window.__export === 'string' ? JSON.parse(window.__export).html : window.__export.html);
  const exported = (html.match(/<mark[^>]*>/g) || []).length;
  // HTML with <mark> pasted in as a document.
  await page.evaluate(() => window.__deliver('ImportFile', { type: 'html', text: '<p>a <mark>lit</mark> word</p>' }));
  await new Promise(r => setTimeout(r, 800));
  const imported = await page.evaluate(() => window.__md);

  const rendered = r.marks.length === 3 && r.marks.includes('highlighted text') && r.marks.includes('inside') && r.marks.includes('light');
  const roundTrip = r.loaded === markdown;
  const heading = r.toc[0] === 'Title with light word';
  const importOk = typeof imported === 'string' && imported.includes('==lit==');
  console.log(`  rendered as <mark>: ${r.marks.length} (${r.marks.join(' | ')}); markers greyed not lost: ${r.rawHasMarkers}`);
  console.log(`  round trip unchanged: ${roundTrip}; outline heading "${r.toc[0]}"; export has ${exported} <mark>; imported HTML gives ${importOk ? '==lit==' : JSON.stringify(imported).slice(0, 60)}`);
  const ok = rendered && roundTrip && heading && exported === 3 && importOk;
  console.log(ok ? 'OK: ==text== is highlight everywhere it should be' : 'FAIL');
  await browser.close(); server.close(); process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
