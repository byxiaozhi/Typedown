// Rapid Mermaid edits used to start overlapping renders against one shared queue. The older render could clear
// the latest work or overwrite it, leaving "Loading..." until the user changed editor mode.
const assert = require('assert/strict');
const puppeteer = require('puppeteer-core');
const http = require('http');
const fs = require('fs');
const path = require('path');

const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const server = http.createServer((req, res) => {
  let requestPath = decodeURIComponent(req.url.split('?')[0]);
  if (requestPath === '/') requestPath = '/index.html';
  const file = path.join(statics, requestPath);
  if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(file).pipe(res);
});

(async () => {
  await new Promise(resolve => server.listen(0, resolve));
  const browser = await puppeteer.launch({
    executablePath: process.env.CHROME || '/opt/google/chrome/chrome',
    headless: 'new',
    args: ['--no-sandbox']
  });
  const page = await browser.newPage();
  const markdown = 'Before\n\n~~~mermaid\nflowchart LR\n  A[Initial] --> B[End]\n~~~\n';
  const settings = {
    fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4,
    textDirection: 'auto', preferLooseListItem: true, listIndentation: '1',
    tableAlignColumns: false, readOnly: false, markdown, basePath: '/tmp', loadId: 1
  };
  await page.evaluateOnNewDocument(settings => {
    const listeners = [];
    const deliver = (name, args) => listeners.forEach(listener => listener({ data: JSON.stringify({ name, args }) }));
    const replies = {
      GetSettings: settings,
      GetCurrentTheme: { theme: 'Light', accentColor: { r: 0, g: 120, b: 212, a: 1 }, background: { R: 249, G: 249, B: 249, A: 1 } },
      ContentLoaded: '', GetStringResources: {}
    };
    window.__marks = {};
    window.chrome = { webview: {
      addEventListener: (_type, listener) => listeners.push(listener),
      postMessage: raw => {
        const message = JSON.parse(raw);
        window.__marks[message.name] = true;
        if (message.type === 'invoke') setTimeout(() => deliver(message.id, { code: 0, data: replies[message.name] ?? null }), 0);
      }
    } };
  }, settings);

  await page.goto(`http://127.0.0.1:${server.address().port}/`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded && document.querySelector('.ag-container-preview svg'), { timeout: 30000 });

  await page.evaluate(() => {
    const original = window.mermaid.render.bind(window.mermaid);
    window.__mermaidActive = 0;
    window.__mermaidMaxActive = 0;
    window.__releaseFirstMermaid = null;
    window.__firstMermaidStarted = false;
    window.mermaid.render = async (id, code) => {
      window.__mermaidActive++;
      window.__mermaidMaxActive = Math.max(window.__mermaidMaxActive, window.__mermaidActive);
      try {
        if (code.includes('First')) {
          window.__firstMermaidStarted = true;
          await new Promise(resolve => { window.__releaseFirstMermaid = resolve; });
        }
        return await original(id, code);
      } finally {
        window.__mermaidActive--;
      }
    };

    const state = window.__typedownMuya.contentState;
    const findCode = blocks => {
      for (const block of blocks) {
        if (block.functionType === 'codeContent') return block;
        const nested = findCode(block.children || []);
        if (nested) return nested;
      }
    };
    findCode(state.blocks).text = 'flowchart LR\n  A[First] --> B[End]';
    state.render();
  });
  await page.waitForFunction(() => window.__firstMermaidStarted, { timeout: 10000 });

  await page.evaluate(() => {
    const state = window.__typedownMuya.contentState;
    const findCode = blocks => {
      for (const block of blocks) {
        if (block.functionType === 'codeContent') return block;
        const nested = findCode(block.children || []);
        if (nested) return nested;
      }
    };
    findCode(state.blocks).text = 'flowchart LR\n  A[Latest] --> B[End]';
    state.render();
    window.__releaseFirstMermaid();
  });

  await page.waitForFunction(() => {
    const preview = document.querySelector('.ag-container-preview');
    return preview?.querySelector('svg') && preview.textContent.includes('Latest');
  }, { timeout: 30000 });
  const result = await page.evaluate(() => ({
    maxActive: window.__mermaidMaxActive,
    text: document.querySelector('.ag-container-preview')?.textContent || '',
    loading: document.querySelector('.ag-container-preview')?.textContent === 'Loading...'
  }));
  assert.equal(result.maxActive, 1, 'Mermaid renders must be serialized');
  assert.match(result.text, /Latest/, 'the newest edit must be the rendered diagram');
  assert.equal(result.loading, false, 'the preview must not remain at Loading...');
  console.log('PASS rapid Mermaid edits render the newest diagram without a mode switch');

  await browser.close();
  server.close();
})().catch(error => {
  console.error(error);
  server.close();
  process.exit(1);
});
