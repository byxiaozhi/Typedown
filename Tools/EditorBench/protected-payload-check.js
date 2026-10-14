// Protected payloads through the real editor (docs/automation-fixtures/protected-payload.json, "preserve").
// Each document gets a paragraph appended and one real keystroke in visual mode. Whatever Muya then writes back,
// no payload may be lost without warning: if the written text lost one of the fixture's mustKeep strings, the editor
// must have predicted "unsafe" before the edit - which is what makes an automation write of that text fail with
// content_not_roundtrippable instead of succeeding. A loss is judged by plain substring search, not by the editor's
// classifier: judged by the classifier, a blind spot in it would hide itself (removing fence protection from the
// classifier used to leave this check passing). Each document is also written through ApplyDocumentEdit; its verdict
// must equal the prediction made on load.
const crypto = require('crypto');
const path = require('path');
const { openEditor } = require('./harness');

const fixtures = require(path.join(__dirname, '../../docs/automation-fixtures/protected-payload.json')).preserve;
const only = process.argv.includes('--only') ? process.argv[process.argv.indexOf('--only') + 1] : null;
const sha = t => crypto.createHash('sha256').update(t, 'utf8').digest('hex');
const anchor = 'EDITHERE';

(async () => {
  const { page, errors, mode, flush, load, pause, close } = await openEditor();
  const failures = [], findings = [];
  try {
    for (const { category, name, markdown, mustKeep } of fixtures.filter(f => !only || f.name === only)) {
      const fail = m => failures.push(`${name}: ${m}`);
      const loaded = markdown.replace(/\n*$/, '') + '\n\n' + anchor + '\n';
      await mode(true, false);
      await load(loaded);
      await mode(false, false);
      const prediction = await page.evaluate(() => window.__typedownPendingNormalization());

      // One real keystroke at the end of the anchor paragraph.
      const point = await page.evaluate(anchor => {
        const w = document.createTreeWalker(document.querySelector('#ag-editor-id'), NodeFilter.SHOW_TEXT);
        for (let n; (n = w.nextNode());) {
          const at = n.data.indexOf(anchor);
          if (at < 0) continue;
          n.parentElement.scrollIntoView({ block: 'center' });
          const r = document.createRange(); r.setStart(n, at + anchor.length - 1); r.setEnd(n, at + anchor.length);
          const b = r.getBoundingClientRect();
          return { x: b.right - 1, y: (b.top + b.bottom) / 2 };
        }
      }, anchor);
      if (!point) { fail('anchor not on the page'); continue; }
      await page.mouse.click(point.x, point.y);
      await pause(200);
      await page.keyboard.press('End');
      await page.keyboard.type('X');
      await pause(300);
      const actual = await flush();
      const lost = mustKeep.filter(k => !actual.includes(k));
      if (lost.length) {
        findings.push(`${category}/${name}: the first edit loses ${lost.map(k => JSON.stringify(k)).join(', ')}`);
        if (prediction.pendingNormalization !== 'unsafe') fail(`lost ${lost.map(k => JSON.stringify(k)).join(', ')} but the prediction was ${prediction.pendingNormalization}`);
      }
      if (!actual.includes(anchor + 'X')) fail('the keystroke did not land in the anchor paragraph');

      // The write path: the same document as an automation candidate.
      await mode(true, false);
      await load('# base\n');
      await mode(false, false);
      const reply = await page.evaluate(({ base, text }) => {
        delete window.__last.DocumentEditApplied;
        window.__deliver('ApplyDocumentEdit', { operationId: 'p', baseContentHash: base, text });
        return null;
      }, { base: sha('# base\n'), text: loaded });
      await page.waitForFunction(() => window.__last.DocumentEditApplied?.operationId === 'p', { timeout: 30000 });
      const applied = await page.evaluate(() => window.__last.DocumentEditApplied);
      if (applied.outcome !== 'applied') fail(`ApplyDocumentEdit ${applied.outcome}`);
      else if (applied.normalization.pendingNormalization !== prediction.pendingNormalization)
        fail(`write verdict ${applied.normalization.pendingNormalization} differs from the load prediction ${prediction.pendingNormalization}`);
      process.stdout.write(failures.some(f => f.startsWith(name + ':')) ? 'F' : '.');
    }
    process.stdout.write('\n');
    if (errors.length) failures.push(`page errors: ${errors.join(' | ')}`);
  } finally {
    await close();
  }
  if (findings.length) console.log(`Muya loses on the first edit (predicted, so writes of these are refused):\n  ${findings.join('\n  ')}`);
  if (failures.length) { console.log(`FAIL: ${failures.length}\n  ${failures.join('\n  ')}`); process.exit(1); }
  console.log(`PASS: no protected payload lost without an unsafe prediction (${fixtures.length} documents)`);
})().catch(e => { console.error(e); process.exit(1); });
