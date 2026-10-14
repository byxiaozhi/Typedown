// Reference-style pictures (![alt][ref] with [ref]: path elsewhere) as a document opens. The source text of one was
// marked failed on its first render, before its picture had loaded, and kept that mark after the picture was drawn
// beside it: with the caret in its paragraph the text showed the broken-picture icon, and a check of the page saw a
// failed picture. A reference to a missing file must still end up marked failed.
const fs = require('fs');
const path = require('path');
const { openEditor } = require('./harness');

const docDir = path.join(__dirname, 'fixtures/showcase');
const doc = 'Inline ![inline](images/landscape.png)\n\nBy reference: ![landscape][pic] and ![checker][jpeg]\n\nMissing: ![gone][missing]\n\n' +
  '[pic]: images/landscape.png "Reference image"\n[jpeg]: ./images/checker.jpg\n[missing]: images/does-not-exist.png\n';

(async () => {
  const { page, mode, flush, close } = await openEditor();
  let ok = true;
  const check = (c, label) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}`); ok = ok && c; };
  try {
    // The stub server serves the editor only; pictures resolve against the document's folder (LoadFile basePath).
    const types = { '.png': 'image/png', '.jpg': 'image/jpeg' };
    await page.setRequestInterception(true);
    page.on('request', req => {
      const p = decodeURIComponent(new URL(req.url()).pathname);
      if (p.startsWith(docDir + path.sep) && fs.existsSync(p)) return req.respond({ status: 200, contentType: types[path.extname(p)], body: fs.readFileSync(p) });
      if (p.startsWith(docDir + path.sep)) return req.respond({ status: 404, body: '' });
      req.continue();
    });
    await mode(false, false);
    await page.evaluate(({ text, basePath }) => {
      delete window.__last.FileLoaded;
      window.__deliver('LoadFile', { text, loadId: 99, basePath });
    }, { text: doc, basePath: docDir });
    await page.waitForFunction(() => window.__last.FileLoaded?.loadId === 99, { timeout: 60000 });
    // Settled: each reference has its picture beside it or is marked failed.
    await page.waitForFunction(() => {
      const marks = [...document.querySelectorAll('#ag-editor-id .ag-image-marked-text')];
      return marks.length === 3 && marks.every(e => e.nextElementSibling?.tagName === 'IMG' || e.classList.contains('ag-image-fail'));
    }, { timeout: 20000, polling: 100 }).catch(() => {});
    const marks = await page.evaluate(() => [...document.querySelectorAll('#ag-editor-id .ag-image-marked-text')].map(e => {
      const img = e.nextElementSibling?.tagName === 'IMG' ? e.nextElementSibling : null;
      return { text: e.textContent, failed: e.classList.contains('ag-image-fail'), loaded: !!img && img.complete && img.naturalWidth > 0 };
    }));
    const show = m => `${m.text}: ${m.loaded ? 'picture drawn' : 'no picture'}, ${m.failed ? 'marked failed' : 'not marked failed'}`;
    for (const m of marks.filter(m => m.text !== '![gone][missing]')) check(m.loaded && !m.failed, show(m));
    const missing = marks.find(m => m.text === '![gone][missing]');
    check(!!missing && missing.failed && !missing.loaded, missing ? show(missing) : 'the reference to a missing file is on the page');
    check((await flush()).replace(/\r\n/g, '\n') === doc, 'the document itself is as it was');
  } finally {
    await close();
  }
  if (!ok) process.exit(1);
  console.log('all passed');
})();
