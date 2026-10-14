// Raw HTML and rich renderers belong to Markdown, but they must never become script in the trusted editor page.
// These fixtures go through the complete import/render path rather than testing the libraries in isolation.
const assert = require('assert/strict');
const puppeteer = require('puppeteer-core');
const http = require('http');
const fs = require('fs');
const path = require('path');

const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const htmlFixtures = [
  ['HTML event handler', `<div><img src="missing://image" onerror="window.__typedownXss.push('image')"></div>\n`],
  ['javascript URL', `<div><a href="javascript:window.__typedownXss.push('link')" onclick="window.__typedownXss.push('click')">link</a></div>\n`],
  ['SVG event and URL', `<div><svg onload="window.__typedownXss.push('svg')"><a href="javascript:window.__typedownXss.push('svg-link')"><text>svg link</text></a></svg></div>\n`],
  ['iframe srcdoc', `<div><iframe srcdoc="&lt;script&gt;parent.__typedownXss.push('frame')&lt;/script&gt;"></iframe></div>\n`],
  ['object javascript data', `<div><object data="javascript:window.__typedownXss.push('object')"></object></div>\n`]
];
const rendererFixtures = [
  {
    name: 'KaTeX input',
    markdown: `Safe formula $x^2+y^2$ and untrusted $\\href{javascript:window.__typedownXss.push('katex')}{click}$.\n`,
    ready: '.ag-math-render .katex'
  },
  {
    name: 'Mermaid input',
    markdown: `~~~mermaid\nflowchart LR\n  A["<b>Safe</b>"] --> B[End]\n  click A "javascript:window.__typedownXss.push('mermaid-link')"\n~~~\n`,
    ready: '.ag-container-preview svg'
  },
  {
    name: 'Invalid Mermaid cleanup',
    markdown: `Before\n\n~~~mermaid\ngraph TD\n  A[broken\n~~~\n\nAfter\n`,
    ready: '.ag-math-error',
    exerciseCleanup: true
  },
  {
    name: 'Vega-Lite input',
    markdown: `~~~vega-lite\n{"$schema":"https://vega.github.io/schema/vega-lite/v6.json","data":{"values":[{"category":"A","value":3},{"category":"B","value":5}]},"title":"<img src=x onerror=window.__typedownXss.push('vega')>","mark":"bar","encoding":{"x":{"field":"category","type":"nominal"},"y":{"field":"value","type":"quantitative"}}}\n~~~\n`,
    ready: '.ag-container-preview svg'
  },
  {
    name: 'flowchart.js input',
    markdown: `~~~flowchart\nstart=>start: <img src=x onerror=window.__typedownXss.push('flowchart')>\nfinish=>end: End\nstart->finish\n~~~\n`,
    ready: '.ag-container-preview svg'
  },
  {
    name: 'sequence diagram input',
    markdown: `~~~sequence\nAlice->Bob: <img src=x onerror=window.__typedownXss.push('sequence')>\n~~~\n`,
    ready: '.ag-container-preview svg'
  }
];

const server = http.createServer((req, res) => {
  let requestPath = decodeURIComponent(req.url.split('?')[0]);
  if (requestPath === '/') requestPath = '/index.html';
  const file = path.join(statics, requestPath);
  if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(file).pipe(res);
});

async function openEditor(browser, markdown) {
  const page = await browser.newPage();
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: true, markdown, basePath: '/tmp', loadId: 1 };
  await page.evaluateOnNewDocument(settings => {
    window.__typedownXss = [];
    window.__marks = {};
    const listeners = [];
    const deliver = (eventName, args) => listeners.forEach(listener => listener({ data: JSON.stringify({ name: eventName, args }) }));
    window.__deliver = deliver;
    const replies = { GetSettings: settings, GetCurrentTheme: { theme: 'Light', accentColor: { r: 0, g: 120, b: 212, a: 1 }, background: { R: 249, G: 249, B: 249, A: 1 } }, ContentLoaded: '', GetStringResources: {} };
    window.chrome = { webview: { addEventListener: (_type, listener) => listeners.push(listener), postMessage: raw => {
      const message = JSON.parse(raw);
      window.__marks[message.name] = true;
      if (message.name === 'UnhandledException') window.__unhandled = message.args;
      if (message.type === 'invoke') setTimeout(() => deliver(message.id, { code: 0, data: message.name in replies ? replies[message.name] : null }), 0);
    } } };
  }, settings);
  page.on('pageerror', error => { page.evaluate(message => { window.__pageError = message; }, error.message).catch(() => {}); });
  await page.goto(`http://127.0.0.1:${server.address().port}/`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 30000 });
  return page;
}

const inspect = (page, selector) => page.evaluate(selector => {
  const root = document.querySelector(selector);
  root?.querySelector('a')?.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
  const dangerousAttributes = root ? [...root.querySelectorAll('*')].flatMap(element =>
    [...element.attributes]
      .filter(attribute => /^on/i.test(attribute.name) || (/^(?:href|src|data|xlink:href)$/i.test(attribute.name) && /^\s*javascript:/i.test(attribute.value)))
      .map(attribute => `${element.tagName}.${attribute.name}=${attribute.value}`)
  ) : [];
  return {
    found: !!root,
    executed: window.__typedownXss,
    dangerousAttributes,
    activeTags: root ? [...root.querySelectorAll('script,iframe,object,embed')].map(element => element.tagName) : [],
    leakedMermaidDiagnostics: [...document.querySelectorAll('[id^="dmermaid-render-"]')].map(element => element.id),
    unhandled: window.__unhandled,
    pageError: window.__pageError
  };
}, selector);

(async () => {
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  try {
    for (const [name, markdown] of htmlFixtures) {
      const page = await openEditor(browser, markdown);
      await new Promise(resolve => setTimeout(resolve, 350));
      const result = await inspect(page, '#ag-editor-id .ag-html-preview');

      assert.equal(result.unhandled, undefined, `${name}: rendering must not crash`);
      assert.equal(result.pageError, undefined, `${name}: page must not throw`);
      assert.equal(result.found, true, `${name}: fixture must reach the raw HTML preview`);
      assert.deepEqual(result.executed, [], `${name}: sanitized Markdown HTML must not execute`);
      assert.deepEqual(result.dangerousAttributes, [], `${name}: active attributes and javascript: URLs must be removed`);
      assert.deepEqual(result.activeTags, [], `${name}: active embedded content must be removed`);
      console.log(`PASS ${name}`);
      await page.close();
    }
    console.log('PASS: raw Markdown HTML is inert after the full editor render path');

    for (const { name, markdown, ready, exerciseCleanup } of rendererFixtures) {
      const page = await openEditor(browser, markdown);
      await page.waitForSelector(ready, { timeout: 30000 });
      await new Promise(resolve => setTimeout(resolve, 350));
      const result = await inspect(page, '#ag-editor-id');

      assert.equal(result.unhandled, undefined, `${name}: rendering must not crash`);
      assert.equal(result.pageError, undefined, `${name}: page must not throw`);
      assert.equal(result.found, true, `${name}: editor root must remain mounted`);
      assert.deepEqual(result.executed, [], `${name}: renderer input must not execute`);
      assert.deepEqual(result.dangerousAttributes, [], `${name}: renderer output must not contain active attributes or javascript: URLs`);
      assert.deepEqual(result.activeTags, [], `${name}: renderer output must not contain active embedded content`);
      assert.deepEqual(result.leakedMermaidDiagnostics, [], `${name}: Mermaid diagnostics must not leak below the document`);
      if (exerciseCleanup) {
        await page.evaluate(() => window.__deliver('SettingsChanged', { sourceCode: true }));
        await page.waitForSelector('.CodeMirror');
        assert.deepEqual((await inspect(page, 'body')).leakedMermaidDiagnostics, [], `${name}: source mode must not retain Mermaid diagnostics`);

        await page.evaluate(() => window.__deliver('SettingsChanged', { sourceCode: false }));
        await page.waitForSelector('.ag-math-error');
        assert.deepEqual((await inspect(page, 'body')).leakedMermaidDiagnostics, [], `${name}: returning to visual mode must not accumulate diagnostics`);

        await page.evaluate(() => {
          delete window.__marks.FileLoaded;
          window.__deliver('LoadFile', { text: '# Clean document\n', basePath: '/tmp', loadId: 2 });
        });
        await page.waitForFunction(() => window.__marks.FileLoaded && document.querySelector('#ag-editor-id')?.textContent.includes('Clean document'));
        const cleaned = await inspect(page, 'body');
        assert.deepEqual(cleaned.leakedMermaidDiagnostics, [], `${name}: removing the invalid block must leave no diagnostics`);
        assert.equal(await page.$('.ag-math-error'), null, `${name}: removing the invalid block must remove its inline error`);
      }
      console.log(`PASS ${name}`);
      await page.close();
    }
    console.log('PASS: formula and diagram renderers do not expose active content');
  } finally {
    await browser.close();
    server.close();
  }
})().catch(error => { console.error(error); server.close(); process.exitCode = 1; });
