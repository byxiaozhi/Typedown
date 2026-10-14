// fixtures/showcase/showcase.md in the built editor: loads it in visual mode, waits for maths and diagrams to be
// drawn and reports
//   - errors the page threw (pageerror) and console errors;
//   - per kind, how many diagrams / maths blocks rendered, failed, are off (PlantUML is off by default) or never
//     finished; the one Mermaid block marked BROKEN-ON-PURPOSE must fail, every other one must render;
//   - whether the Markdown Muya gives back (window.__typedownMuya.getMarkdown(), what the first edit would write)
//     differs from the file, as a short line diff. Normalisation (list markers, table padding, blank lines) is
//     shown, not failed; a word of the file missing from the export is lost content and fails.
//   - the text the page flushes without an edit must be the file byte for byte (source-stability-check.js).
//
//   node showcase-check.js [path/to/doc.md] [--full-diff]
const fs = require('fs');
const path = require('path');
const { openEditor } = require('./harness');

const args = process.argv.slice(2);
const fullDiff = args.includes('--full-diff');
const file = path.resolve(args.find(a => !a.startsWith('--')) || path.join(__dirname, 'fixtures/showcase/showcase.md'));
const source = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
const BROKEN = 'BROKEN-ON-PURPOSE';
// Blocks that fail in this editor today for a reason outside the document: reported, not failed. If one starts
// rendering, the check says so, so the entry can go.
// Each entry: { match: 'text in the block', why: 'reason' }.
const KNOWN = [];
// Words the export drops for a reason the check accepts, with the reason: reported, not failed.
// Each entry: { words: ['word', ...], why: 'reason' }.
const KNOWN_LOSS = [];

/** Line diff (LCS) as unified-style hunks with one line of context. */
function lineDiff(a, b) {
  const A = a.split('\n'), B = b.split('\n');
  const n = A.length, m = B.length;
  const dp = Array.from({ length: n + 1 }, () => new Uint32Array(m + 1));
  for (let i = n - 1; i >= 0; i--) for (let j = m - 1; j >= 0; j--) dp[i][j] = A[i] === B[j] ? dp[i + 1][j + 1] + 1 : Math.max(dp[i + 1][j], dp[i][j + 1]);
  const ops = [];
  let i = 0, j = 0;
  while (i < n || j < m) {
    if (i < n && j < m && A[i] === B[j]) { ops.push([' ', A[i], i + 1]); i++; j++; }
    else if (j < m && (i === n || dp[i][j + 1] >= dp[i + 1][j])) { ops.push(['+', B[j], i + 1]); j++; }
    else { ops.push(['-', A[i], i + 1]); i++; }
  }
  const hunks = [];
  for (let k = 0; k < ops.length; k++) {
    if (ops[k][0] === ' ') continue;
    let end = k;
    while (end + 1 < ops.length && (ops[end + 1][0] !== ' ' || (end + 2 < ops.length && ops[end + 2][0] !== ' '))) end++;
    const from = Math.max(0, k - 1), to = Math.min(ops.length - 1, end + 1);
    hunks.push({ line: ops[k][2], lines: ops.slice(from, to + 1).map(([t, s]) => `${t} ${JSON.stringify(s).slice(1, -1)}`) });
    k = end;
  }
  return hunks;
}

/** Words (letters/digits runs, so markers and padding do not count) as a multiset. */
const words = text => {
  const counts = new Map();
  // List numbers and task boxes are the editor's to renumber and spell ("1. 1. 1." -> "1. 2. 3.", "[X]" -> "[x]").
  text = text.replace(/^((?:\s*>)*\s*)(?:[-*+]|\d{1,9}[.)])(?=\s)(\s+\[[ xX]\](?=\s))?/gm, '$1');
  for (const w of text.match(/[\p{L}\p{N}\p{M}]+/gu) || []) counts.set(w, (counts.get(w) || 0) + 1);
  return counts;
};

(async () => {
  const { page, errors, mode, flush, pause, close } = await openEditor();
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text().split('\n')[0]); });
  const failures = [], notes = new Set(), missingFiles = [];
  page.on('response', r => { if (r.status() === 404) missingFiles.push(decodeURIComponent(new URL(r.url()).pathname)); });
  try {
    await page.setViewport({ width: 1200, height: 900 });
    // The stub server serves the editor only; pictures resolve against the document's folder (LoadFile basePath),
    // so serve that folder's files from disk as the WebView2 host would.
    const docDir = path.dirname(file) + path.sep;
    const types = { '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.svg': 'image/svg+xml', '.gif': 'image/gif', '.webp': 'image/webp' };
    await page.setRequestInterception(true);
    page.on('request', req => {
      const p = decodeURIComponent(new URL(req.url()).pathname);
      if (p.startsWith(docDir) && fs.existsSync(p) && fs.statSync(p).isFile()) {
        return req.respond({ status: 200, contentType: types[path.extname(p).toLowerCase()] || 'application/octet-stream', body: fs.readFileSync(p) });
      }
      req.continue();
    });
    await mode(false, false);
    await page.evaluate(({ text, basePath }) => {
      delete window.__last.FileLoaded;
      window.__deliver('LoadFile', { text, loadId: 99, basePath });
    }, { text: source, basePath: path.dirname(file) });
    await page.waitForFunction(() => window.__last.FileLoaded?.loadId === 99, { timeout: 60000 });

    // Diagrams draw asynchronously (Mermaid is loaded on demand); wait until no preview says "Loading..." any more.
    const started = Date.now();
    await page.waitForFunction(() => {
      // Drawn, failed or switched off; an empty preview is a renderer still at work (js-sequence clears it first).
      const previews = [...document.querySelectorAll('#ag-editor-id figure .ag-container-preview')];
      return previews.length && previews.every(p => p.classList.contains('ag-math-error') || p.classList.contains('ag-plantuml-off') || p.classList.contains('ag-empty') ||
        p.querySelector('svg, .katex, img, canvas'));
    }, { timeout: 45000, polling: 250 }).catch(() => {});
    await pause(1000);
    const waited = Date.now() - started;

    const blocks = await page.evaluate((broken, known) => {
      const out = [];
      for (const fig of document.querySelectorAll('#ag-editor-id figure[data-role]')) {
        const kind = fig.dataset.role.toLowerCase();
        if (!/multiplemath|mermaid|flowchart|sequence|plantuml|vega-lite/.test(kind)) continue;
        const preview = fig.querySelector('.ag-container-preview');
        const text = preview ? preview.textContent.trim() : '';
        let state;
        if (!preview) state = 'no preview';
        else if (preview.classList.contains('ag-plantuml-off')) state = 'off';
        else if (preview.classList.contains('ag-empty')) state = 'empty block';
        else if (preview.classList.contains('ag-math-error') || /^< Invalid .* >$/.test(text)) state = 'failed';
        else if (text === 'Loading...') state = 'pending';
        else if (preview.querySelector('svg, .katex, img, canvas')) state = 'rendered';
        else state = `empty (${text.slice(0, 40)})`;
        out.push({ kind, state, broken: fig.textContent.includes(broken), known: known.find(k => fig.textContent.includes(k)) });
      }
      for (const span of document.querySelectorAll('#ag-editor-id span.ag-math-render')) {
        const source = (span.closest('.ag-math') || span.parentElement).textContent;
        out.push({ kind: 'inline math', state: span.classList.contains('ag-math-error') ? 'failed' : span.querySelector('.katex') ? 'rendered' : 'empty', broken: source.includes(broken), known: known.find(k => source.includes(k)) });
      }
      return out;
    }, BROKEN, KNOWN.map(k => k.match));

    const table = {};
    for (const b of blocks) {
      const row = table[b.kind] || (table[b.kind] = {});
      const known = b.known && KNOWN.find(k => k.match === b.known);
      const key = b.broken ? `${b.state} (on purpose)` : known && b.state === 'failed' ? `${b.state} (known issue)` : b.state;
      row[key] = (row[key] || 0) + 1;
      if (b.broken && b.state !== 'failed') failures.push(`the deliberately broken ${b.kind} block is ${b.state}, not failed`);
      else if (known && b.state === 'failed') notes.add(`known issue, not failed: ${known.why}`);
      else if (known && b.state === 'rendered') notes.add(`a ${b.kind} block listed as a known issue (${known.match}) renders now: drop it from KNOWN`);
      else if (!b.broken && !['rendered', 'empty block'].includes(b.state) && !(b.kind === 'plantuml' && b.state === 'off')) failures.push(`a ${b.kind} block is ${b.state}`);
    }
    if (!blocks.some(b => b.broken)) failures.push(`no block marked ${BROKEN} found`);
    console.log(`rendering (waited ${waited} ms):`);
    for (const [kind, row] of Object.entries(table)) console.log(`  ${kind.padEnd(12)} ${Object.entries(row).map(([k, v]) => `${v} ${k}`).join(', ')}`);
    for (const n of notes) console.log(`  ${n}`);

    // Pictures: every local one loads except the ones named does-not-exist on purpose.
    // Muya shows a picture that failed to load as a placeholder (span.ag-image-fail with the source in data-raw), not an <img>.
    // A reference-style picture is its source text (span.ag-image-marked-text) with the <img> after it once loaded.
    // The pictures load while the page is still busy drawing; wait until each one has loaded or failed.
    await page.waitForFunction(() => !document.querySelector('#ag-editor-id .ag-image-loading') &&
      [...document.querySelectorAll('#ag-editor-id .ag-image-marked-text')].every(e => e.classList.contains('ag-image-fail') || e.nextElementSibling?.tagName === 'IMG'),
    { timeout: 30000, polling: 250 }).catch(() => {});
    const images = await page.evaluate(() => [
      ...[...document.querySelectorAll('#ag-editor-id img')].map(i => ({ src: decodeURIComponent(i.getAttribute('src') || ''), ok: i.complete && i.naturalWidth > 0 })),
      ...[...document.querySelectorAll('#ag-editor-id .ag-image-fail')].map(e => ({ src: e.dataset.raw || e.textContent, ok: false })),
      ...[...document.querySelectorAll('#ag-editor-id .ag-image-loading')].map(e => ({ src: e.dataset.raw, ok: false, pending: true })),
      ...[...document.querySelectorAll('#ag-editor-id .ag-image-marked-text')].filter(e => !e.classList.contains('ag-image-fail') && e.nextElementSibling?.tagName !== 'IMG')
        .map(e => ({ src: e.textContent, ok: false, pending: true })),
    ]);
    const missingOnPurpose = i => /does-not-exist/.test(i.src);
    const brokenImages = images.filter(i => !i.ok && !missingOnPurpose(i));
    console.log(`\nimages: ${images.filter(i => i.ok).length} loaded, ${images.filter(i => !i.ok && missingOnPurpose(i)).length} missing on purpose, ${brokenImages.length} broken`);
    for (const i of brokenImages) failures.push(`image ${i.pending ? 'never finished loading' : 'did not load'}: ${i.src}`);
    for (const i of images.filter(i => i.ok && missingOnPurpose(i))) failures.push(`an image missing on purpose loaded: ${i.src}`);

    // Section 22's "should not run" HTML: a script, an onerror and an onclick that would each leave a mark.
    const ran = await page.evaluate(async () => {
      const link = [...document.querySelectorAll('#ag-editor-id a')].find(a => a.textContent === 'a javascript: link');
      if (link) link.click();
      await new Promise(r => setTimeout(r, 300));
      return { script: window.__showcaseScriptRan === true, onerror: document.body.dataset.showcaseOnerror === 'ran', onclick: document.body.dataset.showcaseOnclick === 'ran', iframes: document.querySelectorAll('#ag-editor-id iframe').length, foundLink: !!link };
    });
    console.log(`\nhostile HTML: script ran ${ran.script}, onerror ran ${ran.onerror}, onclick ran ${ran.onclick} (link ${ran.foundLink ? 'clicked' : 'not found'}), iframes ${ran.iframes}`);
    for (const k of ['script', 'onerror', 'onclick']) if (ran[k]) failures.push(`the ${k} in section 22 ran`);

    // Muya's export: what the document becomes once edited.
    const exported = (await page.evaluate(() => window.__typedownMuya.getMarkdown())).replace(/\r\n/g, '\n');
    const hunks = lineDiff(source, exported);
    console.log(`\nexport vs file: ${hunks.length ? `${hunks.length} differing hunk(s)` : 'identical'}`);
    for (const h of fullDiff ? hunks : hunks.slice(0, 40)) console.log(`@@ file line ${h.line}\n${h.lines.join('\n')}`);
    if (!fullDiff && hunks.length > 40) console.log(`... ${hunks.length - 40} more (--full-diff)`);

    // Invisible and control characters (section 3 and 22.7) are text too; a word count does not see them.
    for (const c of ['\u200b', '\u200d', '\u00a0', '\u00ad', '\ufeff', '\ufffd', '\u202e', '\u202c']) {
      const inSource = source.split(c).length - 1, inExport = exported.split(c).length - 1;
      if (inExport < inSource) failures.push(`U+${c.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')} lost in the export (${inSource} -> ${inExport})`);
    }

    const src = words(source), exp = words(exported);
    const lost = [...src].filter(([w, c]) => (exp.get(w) || 0) < c).map(([w, c]) => `${w} (${c} -> ${exp.get(w) || 0})`);
    const gained = [...exp].filter(([w, c]) => (src.get(w) || 0) < c).map(([w]) => w);
    const accepted = KNOWN_LOSS.filter(k => k.words.some(w => lost.some(l => l.startsWith(`${w} (`))));
    for (const k of accepted) console.log(`known loss, not failed: ${k.words.join(', ')}: ${k.why}`);
    for (const k of KNOWN_LOSS) if (!accepted.includes(k)) console.log(`listed as a known loss but kept now: ${k.words.join(', ')}: drop it from KNOWN_LOSS`);
    const unexpected = lost.filter(l => !KNOWN_LOSS.some(k => k.words.some(w => l.startsWith(`${w} (`))));
    if (unexpected.length) failures.push(`content lost in the export: ${unexpected.join(', ')}`);
    if (gained.length) console.log(`words the export adds: ${gained.join(', ')}`);

    // Without an edit the page hands back the file untouched.
    const flushed = (await flush()).replace(/\r\n/g, '\n');
    if (flushed !== source) failures.push('the unedited flush differs from the file');

    if (errors.length) failures.push(`page errors: ${errors.join(' | ')}`);
    console.log(`\npage errors: ${errors.length}${errors.length ? '\n  ' + errors.join('\n  ') : ''}`);
    // The pictures missing on purpose answer 404 and Chrome logs each; anything else the console reports is shown.
    const other = consoleErrors.filter(e => !/^Failed to load resource: .*404/.test(e));
    const unexpected404 = missingFiles.filter(f => !/does-not-exist/.test(f));
    console.log(`files not found: ${missingFiles.length ? missingFiles.map(f => path.basename(f)).join(', ') : 'none'}`);
    for (const f of unexpected404) failures.push(`not found: ${f}`);
    console.log(`other console errors: ${other.length}${other.length ? '\n  ' + other.join('\n  ') : ''}`);
  } finally {
    await close();
  }
  console.log(failures.length ? `\nFAIL\n  ${failures.join('\n  ')}` : '\nPASS');
  process.exit(failures.length ? 1 : 0);
})().catch(e => { console.error(e); process.exit(2); });
