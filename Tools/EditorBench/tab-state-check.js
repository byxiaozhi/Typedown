// What the host is told after a tab switch. The outline, the word count and the caret all reach the host
// through StateChange; a switch that restores a document already built produces no change of its own, so
// the report has to be asked for — and asking for it at the wrong moment sends it to an editor that is no
// longer listening, which leaves the outline showing the previous document.
//
//   node tab-state-check.js
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');

const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});

const docA = ['# Alpha', 'text of the first document', '## Alpha one', 'more', '## Alpha two', 'more'].join('\n\n') + '\n';
const docB = ['# Beta', 'text of the second document', '## Beta one', 'more'].join('\n\n') + '\n';

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, markdown: docA, basePath: '/tmp' };
  // StateChange arrives as a diff against the previous payload, not as an object.
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__prev={};window.__state=null;window.__states=0;
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const apply=(m)=>{if(typeof m.args!=='string')return null;const o=window.__prev[m.name];const v=m.diff?o.slice(0,m.start)+m.args+o.slice(m.end):m.args;window.__prev[m.name]=v;try{return JSON.parse(v)}catch(e){return null}};
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;
      if(m.name==='StateChange'){const a=apply(m);if(a&&a.state){window.__state=a.state;window.__states++;window.__lastLoadId=(a.loadId===undefined?'(none)':a.loadId);}}
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
  await new Promise(r => setTimeout(r, 800));

  const normalized = () => page.evaluate(() => window.__typedownMuya.getMarkdown());
  const textA = await normalized();

  const go = async (text, label) => {
    const r = await page.evaluate(async (text) => {
      const before = window.__states;
      const id = Date.now();
      window.__deliver('LoadFile', { text, basePath: '/tmp', cursor: null, scrollTop: 0, loadId: id });
      for (let i = 0; i < 180; i++) await new Promise(r => requestAnimationFrame(r));
      const s = window.__state || {};
      return { reports: window.__states - before, toc: (s.toc || []).map(t => t.content), sent: id, tagged: window.__lastLoadId };
    }, text);
    // The host drops a report whose load id is not the one it is waiting for, so a report that arrives
    // untagged or tagged with anything else never reaches the outline at all.
    const matches = r.tagged === r.sent;
    console.log(`  ${label.padEnd(22)} ${r.reports} report(s), loadId ${matches ? 'matches' : `${r.tagged} != ${r.sent}`}, outline [${r.toc.join(', ')}]`);
    r.matches = matches;
    return r;
  };

  await go(docB, 'switch to B');
  const textB = await normalized();
  const backToA = await go(textA, 'back to A (restored)');
  const toB = await go(textB, 'to B again (restored)');

  const okA = backToA.toc.join('|') === 'Alpha|Alpha one|Alpha two' && backToA.matches;
  const okB = toB.toc.join('|') === 'Beta|Beta one' && toB.matches;
  console.log(okA && okB ? 'OK: the host is told the restored document\'s outline' : 'FAIL: the outline the host holds is not the document being shown');
  await browser.close(); server.close();
  process.exit(okA && okB ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
