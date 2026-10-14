// Void HTML elements (<br>, <hr>, <img>, <wbr> ...) have no close tag. A stray close like the </br> in
// <br></br> must be left as literal text, not paired as the element's close: pairing it dropped a character
// as it was typed and, on the next render (a reading-mode round trip), destroyed the editor container.
// Each case is loaded, the markdown it reports must equal what went in, and the editor must survive a
// reading-mode toggle with #ag-editor-id intact.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
// Void elements paired with a stray close, self-closing forms, uppercase, attributes, repeats, and normal
// tags that must still work. Each must round-trip through the editor unchanged and survive a re-render.
const cases = [
  '<br></br>', 'a<br>b', 'a<br>b<br>c', '<br></br></br>', '<br/>', '<BR>', '<br class="x"></br>',
  'x<hr></hr>y', 'a<hr/>b', '<img src="p.png"></img>', '<img src="p.png"/>', 'a<wbr></wbr>b',
  'text <input> more', 'keep <span>text</span> here', 'nested <strong><em>x</em></strong> ok',
  'jump <a id="sec"></a> here', 'anchor <a name="s2"></a> point',
];
const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  let ok = true;
  for (const md of cases) {
    const page = await browser.newPage(); await page.setViewport({ width: 900, height: 600 });
    const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '800px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: false, markdown: md + '\n', basePath: '/tmp', loadId: 1 };
    await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__loaded=null;window.__prev={};
      window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
      const apply=(m)=>{if(typeof m.args!=='string')return null;const o=window.__prev[m.name]||'';const v=m.diff?o.slice(0,m.start)+m.args+o.slice(m.end):m.args;window.__prev[m.name]=v;try{return JSON.parse(v)}catch(e){return v}};
      const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
      window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.name==='FileLoaded'){const a=apply(m);if(a&&a.text!==undefined)window.__loaded=a.text;}
        if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
    await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
    await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
    await new Promise(r => setTimeout(r, 400));
    // The editor normalizes on load; the normalized text is the round-trip result. It must not lose a char.
    const loaded = (await page.evaluate(() => window.__loaded) || '').replace(/\n+$/, '');
    // Toggle reading mode and back; the container must survive.
    await page.evaluate(() => window.__deliver('SettingsChanged', { readOnly: true }));
    await new Promise(r => setTimeout(r, 300));
    await page.evaluate(() => window.__deliver('SettingsChanged', { readOnly: false }));
    await new Promise(r => setTimeout(r, 300));
    // Still in reading mode after the toggle sequence ended on readOnly=false; set it back to read to check
    // that a <br>'s literal source is not shown to a reader (width collapses to 0).
    let srcHidden = true;
    const hidesSource = md.includes('<br') || /<a (id|name)=/.test(md);
    if (hidesSource) {
      await page.evaluate(() => window.__deliver('SettingsChanged', { readOnly: true }));
      await new Promise(r => setTimeout(r, 300));
      // In reading mode the literal HTML source of a <br> or an empty anchor must not be visible.
      srcHidden = await page.evaluate(() => { const tags = document.querySelectorAll('#ag-editor-id .ag-html-tag'); for (const t of tags) if (Math.round(t.getBoundingClientRect().width) > 0) return false; return true; });
    }
    const survived = await page.evaluate(() => !!document.getElementById('ag-editor-id'));
    const kept = loaded === md;
    if (!kept || !survived || !srcHidden) ok = false;
    console.log(`  ${JSON.stringify(md)} -> loaded ${JSON.stringify(loaded)} ${kept ? '(kept)' : '(CHANGED)'}; survived ${survived}${hidesSource ? `; source hidden in reading ${srcHidden}` : ''}`);
    await page.close();
  }
  console.log(ok ? 'OK: void HTML round-trips, survives re-render, and hides its source in reading mode' : 'FAIL');
  await browser.close(); server.close(); process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
