// Types the same document into two editor builds and reports what each costs, so "it feels faster" can be
// checked rather than argued about.
//
//   node compare-editors.js [pathToOtherBundle]
//
// Ours is driven through the WebView2 stub the other checks here use. The other build (the WinUI 3 fork's
// CodeMirror editor) falls back to its own fake host in a plain browser, so it is driven through that.
// Both are measured the same way: a keydown listener records when the key arrived and again two frames
// later, which is roughly when the user sees the result.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');

const ours = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const theirs = path.resolve(process.argv[2] || '/root/repos/typedown-awarson/Dev/Typedown.WinUI/Resources/Statics');

// One document, used by both: headings, paragraphs, lists, code, tables — about 80k characters.
const markdown = Array.from({ length: 120 }, (_, i) => [
  `## Section ${i + 1}`,
  `Paragraph ${i + 1} with **bold**, *italic* and \`code\`, long enough to wrap onto a second line in any reasonable editor width.`,
  '- one\n- two\n- three',
  '```js\nconst a = ' + i + ';\nconsole.log(a * 2);\n```',
  '| a | b |\n|---|---|\n| 1 | 2 |',
  '> a quotation',
].join('\n\n')).join('\n\n') + '\n';

const serve = (dir) => {
  const server = http.createServer((req, res) => {
    let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
    const f = path.join(dir, p);
    if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
    // A module script served without a JavaScript media type is refused by the browser, which is how one of
    // these bundles silently never started.
    const type = { '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.html': 'text/html',
      '.json': 'application/json', '.woff': 'font/woff', '.woff2': 'font/woff2', '.ttf': 'font/ttf',
      '.svg': 'image/svg+xml', '.png': 'image/png', '.map': 'application/json' }[path.extname(f).toLowerCase()];
    if (type) res.setHeader('Content-Type', type);
    fs.createReadStream(f).pipe(res);
  });
  return server;
};

const probe = () => {
  window.__keys = [];
  window.addEventListener('keydown', () => {
    const at = performance.now();
    requestAnimationFrame(() => requestAnimationFrame(() => window.__keys.push(performance.now() - at)));
  }, true);
};

const stats = (xs) => {
  if (!xs.length) return { n: 0 };
  const s = [...xs].sort((a, b) => a - b);
  const at = q => s[Math.min(s.length - 1, Math.floor(q * s.length))];
  return { n: s.length, median: +at(0.5).toFixed(1), p90: +at(0.9).toFixed(1), worst: +s[s.length - 1].toFixed(1) };
};

async function measure(browser, dir, which) {
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

  const t0 = Date.now();
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });

  let loaded;
  if (which === 'ours') {
    await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
    loaded = Date.now() - t0;
  } else {
    try {
      await page.waitForFunction(() => !!window.__typedownFakeHost, { timeout: 20000 });
    } catch {
      console.log(`  the other page never came up; errors: ${JSON.stringify(errors.slice(0, 3))}`);
      await page.close(); server.close();
      return { loaded: -1, dom: { elements: 0, chars: 0 }, keys: { n: 0 }, memory: null, errors };
    }
    const start = Date.now();
    await page.evaluate((text) => window.__typedownFakeHost.command('doc.load', { version: 2, text, basePath: '' }), markdown);
    await page.waitForFunction(() => document.querySelectorAll('.cm-line').length > 5, { timeout: 60000 });
    loaded = (Date.now() - t0) + 0; // page load plus the document load below
    loaded = Date.now() - t0;
    void start;
  }
  await new Promise(r => setTimeout(r, 1200)); // let the first render settle

  const dom = await page.evaluate(() => {
    const root = document.querySelector('#ag-editor-id') || document.querySelector('.cm-content') || document.body;
    return { elements: root.querySelectorAll('*').length, chars: (root.textContent || '').length };
  });

  // Type into the document: click the first line, go to its end, then type.
  const target = which === 'ours' ? '#ag-editor-id p.ag-paragraph' : '.cm-line';
  await page.click(target);
  await page.keyboard.press('End');
  await new Promise(r => setTimeout(r, 300));
  await page.evaluate(() => { window.__keys.length = 0; });
  for (const ch of 'the quick brown fox jumps over the lazy dog again') {
    await page.keyboard.type(ch);
    await new Promise(r => setTimeout(r, 35));
  }
  await new Promise(r => setTimeout(r, 400));
  const keys = await page.evaluate(() => window.__keys);

  const memory = await page.evaluate(() => (performance.memory ? Math.round(performance.memory.usedJSHeapSize / 1048576) : null));
  await page.close(); server.close();
  return { loaded, dom, keys: stats(keys), memory, errors: errors.slice(0, 3) };
}

(async () => {
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox', '--enable-precise-memory-info'] });
  console.log(`document: ${markdown.length} characters\n`);
  for (const [which, dir] of [['ours', ours], ['theirs', theirs]]) {
    const r = await measure(browser, dir, which);
    console.log(`${which} (${dir})`);
    console.log(`  load to rendered   ${r.loaded} ms`);
    console.log(`  elements in editor ${r.dom.elements}   text ${r.dom.chars} chars`);
    console.log(`  keystroke to paint median ${r.keys.median} ms, p90 ${r.keys.p90} ms, worst ${r.keys.worst} ms  (${r.keys.n} keys)`);
    console.log(`  JS heap            ${r.memory} MB`);
    if (r.errors.length) console.log(`  page errors        ${JSON.stringify(r.errors)}`);
    console.log();
  }
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
