// A picture inside a link ([![alt](src)](url)) is drawn inside the link, in its own box. Its icons were <a> elements
// inside the link's <a>; a page built from the HTML closes the link at the first of them, so the box came out empty
// and the picture landed after the link. A small document is built that way (a big one is drawn in pieces and was
// spared, which is why showcase-check passed).
//
//   node link-image-check.js
const fs = require('fs');
const path = require('path');
const { openEditor } = require('./harness');

const docDir = path.join(__dirname, 'fixtures/showcase');
const doc = '# P\n\nAn image inside a link: [![checker as a link](images/checker.jpg)](https://example.com/gallery)\n\nAfter.\n';

(async () => {
  const { page, mode, pause, close } = await openEditor();
  let failed = false;
  try {
    await page.setRequestInterception(true);
    page.on('request', req => {
      const p = decodeURIComponent(new URL(req.url()).pathname);
      if (p.startsWith(docDir) && fs.existsSync(p)) return req.respond({ status: 200, contentType: 'image/jpeg', body: fs.readFileSync(p) });
      req.continue();
    });
    await mode(false, false);
    await page.evaluate(({ text, basePath }) => { window.__deliver('LoadFile', { text, loadId: 99, basePath }); }, { text: doc, basePath: docDir });
    await page.waitForFunction(() => document.querySelector('#ag-editor-id .ag-inline-image.ag-image-success'), { timeout: 10000, polling: 100 }).catch(() => {});
    await pause(500);
    const seen = await page.evaluate(() => {
      const link = document.querySelector('#ag-editor-id a.ag-inline-rule');
      const box = document.querySelector('#ag-editor-id .ag-inline-image');
      const img = box && box.querySelector('.ag-image-container img');
      return {
        link: !!link,
        box: box ? box.className : null,
        boxInLink: !!(link && box && link.contains(box)),
        imgInBox: !!img,
        drawn: !!(img && img.complete && img.naturalWidth > 0),
        anchorsInLink: link ? link.querySelectorAll('a').length : 0,
      };
    });
    console.log(JSON.stringify(seen));
    const problems = [];
    if (!seen.link) problems.push('no link on the page');
    if (!seen.boxInLink) problems.push('the picture box is not inside the link');
    if (!seen.imgInBox) problems.push('the picture box holds no picture');
    else if (!seen.drawn) problems.push('the picture is not drawn');
    if (seen.anchorsInLink) problems.push(`${seen.anchorsInLink} <a> inside the link's <a>`);
    for (const p of problems) console.log('FAIL ' + p);
    failed = problems.length > 0;
    console.log(failed ? '\nFAIL' : '\nPASS');
  } finally { await close(); }
  process.exit(failed ? 1 : 0);
})().catch(e => { console.error('check failed:', e.message); process.exit(1); });
