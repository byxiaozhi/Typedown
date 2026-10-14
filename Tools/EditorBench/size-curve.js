// How the cost grows with document length: the same measurements as compare-editors.js, repeated at several
// sizes, so "when does it start to hurt" has an answer.
//
//   node size-curve.js                 # ours, at 30k / 100k / 300k characters
//   node size-curve.js --sizes 30,100  # in thousands of characters
//   node size-curve.js --other DIR     # also measure another bundle (its page must bring its own fake host)
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');

const args = process.argv.slice(2);
const at = (flag, fallback) => { const i = args.indexOf(flag); return i >= 0 ? args[i + 1] : fallback; };
const ours = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const other = at('--other', null);
const sizes = at('--sizes', '30,100,300').split(',').map(n => Number(n) * 1000);
const extraCss = at('--css', null);

const section = (i) => [
  `## Section ${i}`,
  `Paragraph ${i} with **bold**, *italic* and \`code\`, long enough to wrap onto a second line in any reasonable editor width.`,
  '- one\n- two\n- three',
  '```js\nconst a = ' + i + ';\nconsole.log(a * 2);\n```',
  '| a | b |\n|---|---|\n| 1 | 2 |',
  '> a quotation',
].join('\n\n');

const document_ = (chars) => {
  let out = '', i = 0;
  while (out.length < chars) out += section(++i) + '\n\n';
  return out;
};

const serve = (dir) => http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(dir, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  const type = { '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.html': 'text/html', '.json': 'application/json', '.woff': 'font/woff', '.woff2': 'font/woff2', '.ttf': 'font/ttf', '.svg': 'image/svg+xml', '.png': 'image/png', '.map': 'application/json' }[path.extname(f).toLowerCase()];
  if (type) res.setHeader('Content-Type', type);
  fs.createReadStream(f).pipe(res);
});

const probe = () => {
  window.__keys = [];
  window.addEventListener('keydown', () => {
    const start = performance.now();
    requestAnimationFrame(() => requestAnimationFrame(() => window.__keys.push(performance.now() - start)));
  }, true);
};

const stats = (xs) => {
  if (!xs.length) return { median: null, p90: null };
  const s = [...xs].sort((a, b) => a - b);
  const q = (p) => s[Math.min(s.length - 1, Math.floor(p * s.length))];
  return { median: +q(0.5).toFixed(1), p90: +q(0.9).toFixed(1) };
};

async function measure(browser, dir, which, markdown) {
  const server = serve(dir);
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const page = await browser.newPage();
  await page.setViewport({ width: 1200, height: 900 });
  const errors = [];
  page.on('pageerror', e => errors.push(e.message.split('\n')[0]));

  if (which === 'ours') {
    const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, markdown, basePath: '/tmp' };
    await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  }
  await page.evaluateOnNewDocument(probe);
  // An experiment hook: extra CSS applied before the document is laid out, so ideas like content-visibility
  // can be measured before anyone changes the editor's stylesheet.
  if (extraCss) await page.evaluateOnNewDocument((css) => {
    document.addEventListener('DOMContentLoaded', () => {
      const style = document.createElement('style');
      style.textContent = css;
      document.head.appendChild(style);
    });
  }, extraCss);

  const started = Date.now();
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  let loaded = null;
  try {
    if (which === 'ours') {
      await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 120000 });
    } else {
      await page.waitForFunction(() => !!window.__typedownFakeHost, { timeout: 30000 });
      await page.evaluate((text) => window.__typedownFakeHost.command('doc.load', { version: 2, text, basePath: '' }), markdown);
      await page.waitForFunction(() => document.querySelectorAll('.cm-line').length > 5, { timeout: 120000 });
    }
    loaded = Date.now() - started;
  } catch {
    await page.close(); server.close();
    return { loaded: null, elements: null, keys: { median: null, p90: null }, memory: null, errors };
  }
  await new Promise(r => setTimeout(r, 1500));

  const elements = await page.evaluate(() => {
    const root = document.querySelector('#ag-editor-id') || document.querySelector('.cm-content') || document.body;
    return root.querySelectorAll('*').length;
  });

  await page.click(which === 'ours' ? '#ag-editor-id p.ag-paragraph' : '.cm-line');
  await page.keyboard.press('End');
  await new Promise(r => setTimeout(r, 300));
  await page.evaluate(() => { window.__keys.length = 0; });
  for (const ch of 'the quick brown fox jumps over') {
    await page.keyboard.type(ch);
    await new Promise(r => setTimeout(r, 40));
  }
  await new Promise(r => setTimeout(r, 500));
  const keys = stats(await page.evaluate(() => window.__keys));
  const memory = await page.evaluate(() => (performance.memory ? Math.round(performance.memory.usedJSHeapSize / 1048576) : null));
  await page.close(); server.close();
  return { loaded, elements, keys, memory, errors: errors.slice(0, 2) };
}

(async () => {
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox', '--enable-precise-memory-info'] });
  const targets = [['ours', ours]];
  if (other) targets.push(['theirs', path.resolve(other)]);
  console.log('chars     build    load ms   elements   key median   key p90   heap MB');
  for (const chars of sizes) {
    const markdown = document_(chars);
    for (const [which, dir] of targets) {
      const r = await measure(browser, dir, which, markdown);
      console.log(
        String(markdown.length).padEnd(9),
        which.padEnd(8),
        String(r.loaded ?? 'failed').padEnd(9),
        String(r.elements ?? '-').padEnd(10),
        String(r.keys.median ?? '-').padEnd(12),
        String(r.keys.p90 ?? '-').padEnd(9),
        String(r.memory ?? '-'),
        r.errors && r.errors.length ? ' ' + JSON.stringify(r.errors) : '');
    }
  }
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
