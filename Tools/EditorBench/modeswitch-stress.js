// Toggling source mode back and forth (Ctrl+/) repeatedly: the content must survive every round trip and the
// page must not raise anything. Mirrors the host sending SettingsChanged { sourceCode } over and over.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const server = http.createServer((req, res) => { let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html'; const f = path.join(statics, p); if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); } fs.createReadStream(f).pipe(res); });
const markdown = '# Title\n\nSome **bold** text.\n\n| a | b |\n| --- | --- |\n| 1 | 2 |\n\n- [ ] task\n\nText with a note[^1].\n\n[^1]: the note\n';
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const problems = [];
  page.on('pageerror', e => problems.push('pageerror: ' + e.message.split('\n')[0]));
  page.on('console', m => { const t = m.text(); if (m.type() === 'error' && !t.includes('404')) problems.push('console: ' + t.slice(0, 160)); });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: true, markdown, basePath: '/tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));window.__deliver=deliver;const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 30000 });
  await new Promise(r => setTimeout(r, 400));

  // the editor normalizes on load (table padding); the baseline is what it holds before the first switch
  const baseline = await page.evaluate(() => window.__typedownMuya.getMarkdown());
  const rounds = Number(process.argv[2] || 15);
  for (let i = 1; i <= rounds; i++) {
    for (const on of [true, false]) {
      await page.evaluate((v) => window.__deliver('SettingsChanged', { sourceCode: v }), on);
      await new Promise(r => setTimeout(r, 180));
    }
    if (i % 5 === 0) {
      const text = await page.evaluate(() => window.__typedownMuya ? window.__typedownMuya.getMarkdown() : null);
      console.log(`round ${String(i).padStart(2)}: ${text === baseline ? 'content intact' : 'CONTENT DRIFTED -> ' + JSON.stringify(text)}`);
      if (text !== baseline) problems.push(`content drifted after ${i} rounds`);
    }
  }
  console.log(problems.length ? 'FAIL:\n  ' + problems.slice(0, 8).join('\n  ') : `OK — ${rounds} round trips, no page errors, content intact`);
  await browser.close(); server.close();
  process.exit(problems.length ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
