// Pictures on the web (todo #18, "network images do not show"). A picture whose address has no picture extension was
// first asked for with a HEAD request, and drawn only when that answered 200 with one of five exact content types: a
// shields.io badge ("image/svg+xml;charset=utf-8"), a server that refuses HEAD (403, as signed storage links do), a
// BMP or an "application/octet-stream" picture were all marked failed although the browser draws them. The picture
// itself is the test now: a page or a missing file is still marked failed.
//
// The web is stubbed by request interception (no network); the browser runs with the host's switches
// (Config.WebView2Args), as the page does in WebView2.
const fs = require('fs');
const path = require('path');
const { openEditor } = require('./harness');

const png = fs.readFileSync(path.join(__dirname, 'fixtures/showcase/images/landscape.png'));
const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="90" height="20"><rect width="90" height="20" fill="#4c1"/></svg>';
// A 2x2 24-bit BMP.
const bmp = (() => {
  const row = Buffer.from([0, 0, 255, 0, 255, 0, 0, 0]); // two pixels and the padding to four bytes
  const pixels = Buffer.concat([row, row]);
  const header = Buffer.alloc(54);
  header.write('BM', 0);
  header.writeUInt32LE(54 + pixels.length, 2);
  header.writeUInt32LE(54, 10);
  header.writeUInt32LE(40, 14);
  header.writeInt32LE(2, 18);
  header.writeInt32LE(2, 22);
  header.writeUInt16LE(1, 26);
  header.writeUInt16LE(24, 28);
  header.writeUInt32LE(pixels.length, 34);
  return Buffer.concat([header, pixels]);
})();

// address -> what the stub web answers: [status, content type, body] for GET, and for HEAD where it differs.
const web = {
  'https://img.shields.io/badge/build-passing-green': { get: [200, 'image/svg+xml;charset=utf-8', svg] },
  'https://images.example.com/photo-1?w=800': { get: [200, 'image/png', png], head: [403, 'application/xml', ''] },
  'https://cdn.example.com/pic.bmp': { get: [200, 'image/bmp', bmp] },
  'https://bucket.example.com/object-key': { get: [200, 'application/octet-stream', png] },
  'https://cdn.example.com/a.png': { get: [200, 'image/png', png] },
  'http://plain.example.com/b.png': { get: [200, 'image/png', png] },
  'https://short.example.com/r': { redirect: 'https://cdn.example.com/a.png' },
  'https://example.com/page': { get: [200, 'text/html', '<!doctype html><title>a page</title>'] },
  'https://cdn.example.com/missing.png': { get: [404, 'text/html', 'not found'] },
};
const shown = ['img.shields.io', 'images.example.com', 'pic.bmp', 'bucket.example.com', 'cdn.example.com/a.png', 'plain.example.com', 'short.example.com'];
const failed = ['example.com/page', 'missing.png'];

const doc = Object.keys(web).map((url, i) => `![p${i}](${url})`).join('\n\n') + '\n';

(async () => {
  const { page, mode, flush, close } = await openEditor({ browserArgs: ['--disable-web-security', '--allow-file-access-from-files'] });
  let ok = true;
  const check = (c, label) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}`); ok = ok && c; };
  try {
    await page.setRequestInterception(true);
    page.on('request', req => {
      const answer = web[req.url()];
      if (!answer) return req.continue();
      if (answer.redirect) return req.respond({ status: 302, headers: { location: answer.redirect }, body: '' });
      const [status, contentType, body] = (req.method() === 'HEAD' && answer.head) || answer.get;
      req.respond({ status, contentType, body: req.method() === 'HEAD' ? '' : body });
    });
    await mode(false, false);
    await page.evaluate(text => {
      delete window.__last.FileLoaded;
      window.__deliver('LoadFile', { text, loadId: 99, basePath: '/tmp' });
    }, doc);
    await page.waitForFunction(() => window.__last.FileLoaded?.loadId === 99, { timeout: 60000 });
    const settled = await page.waitForFunction(n => {
      const all = [...document.querySelectorAll('#ag-editor-id .ag-inline-image')];
      return all.length === n && all.every(e => e.classList.contains('ag-image-success') || e.classList.contains('ag-image-fail'));
    }, { timeout: 20000, polling: 100 }, Object.keys(web).length).then(() => true, () => false);
    check(settled, 'every picture ends up drawn or marked failed');
    const pictures = await page.evaluate(() => [...document.querySelectorAll('#ag-editor-id .ag-inline-image')].map(e => {
      const img = e.querySelector('img');
      return { raw: e.dataset.raw, failed: e.classList.contains('ag-image-fail'), drawn: !!img && img.complete && img.naturalWidth > 0 };
    }));
    const find = key => pictures.find(p => p.raw && p.raw.includes(key));
    for (const key of shown) {
      const p = find(key);
      check(!!p && p.drawn && !p.failed, `${key}: ${p ? (p.drawn ? 'drawn' : 'not drawn') + (p.failed ? ', marked failed' : '') : 'not on the page'}`);
    }
    for (const key of failed) {
      const p = find(key);
      check(!!p && p.failed && !p.drawn, `${key} (not a picture): ${p ? (p.failed ? 'marked failed' : 'not marked failed') + (p.drawn ? ', drawn' : '') : 'not on the page'}`);
    }
    check((await flush()).replace(/\r\n/g, '\n') === doc, 'the document itself is as it was');
  } finally {
    await close();
  }
  if (!ok) process.exit(1);
  console.log('all passed');
})();
