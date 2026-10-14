const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const server = http.createServer((req, res) => { let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html'; const f = path.join(statics, p); if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); } fs.createReadStream(f).pipe(res); });
// a test-case table: one long description cell per row, several short columns — like the user's document
// one long cell in a column is enough: every other cell in that column is padded to the same width
const rows = Array.from({ length: 40 }, (_, i) =>
  `| TC-${String(i + 1).padStart(3, '0')} | 功能 ${i} | ${i === 7 ? '验证在网络异常时客户端能够正确重试并给出提示，'.repeat(30) : '正常流程'} | 通过 |`).join('\n');
const markdown = `# 测试用例\n\n| 编号 | 模块 | 描述 | 结果 |\n|---|---|---|---|\n${rows}\n`;
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  for (const align of [true, false]) {
    const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: align, markdown, basePath: '/tmp' };
    await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
    await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
    await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 30000 });
    await new Promise(r => setTimeout(r, 400));
    const out = await page.evaluate(() => window.__typedownMuya.getMarkdown());
    console.log(`tableAlignColumns=${String(align).padEnd(5)} input ${markdown.length} chars -> editor ${out.length} chars  (x${(out.length / markdown.length).toFixed(1)})`);
  }
  await browser.close(); server.close();
})().catch(e => { console.error(e); process.exit(1); });
