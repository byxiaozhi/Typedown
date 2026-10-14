// First real visual edit: what does Muya's serialization do to a document once the reader types into it?
//
// Until the first edit the editor hands back the loaded source untouched (source-stability-check.js). The first
// real edit makes Muya's whole export the document text. The editor predicts that rewrite before it happens
// (window.__typedownPendingNormalization, services/normalization.ts), and automation writes are gated on the
// prediction. This check holds the prediction to what actually happens:
//
//   1. load the fixture with a paragraph "EDITHERE" appended, in source mode;
//   2. switch to visual mode and read the prediction and Muya's export;
//   3. click at the end of "EDITHERE" and type "X" on the keyboard;
//   4. flush: the text must be the export with "EDITHERE" -> "EDITHEREX", nothing else, and the prediction
//      afterwards must be "none".
//
// The verdict of every fixture (none / unknown / unsafe, with reasons) is kept in first-edit-known.json. A fixture
// whose verdict gets worse fails the check; one that gets better asks for the baseline to be updated. "unsafe"
// entries are real Muya fidelity findings - text a reader would lose by typing one character.
//
//   node first-edit-check.js               # compare with first-edit-known.json
//   node first-edit-check.js --update      # rewrite the baseline
//   node first-edit-check.js --only footnote --show
const fs = require('fs');
const path = require('path');
const { sourceFixtures, firstDifference, openEditor } = require('./harness');

const args = process.argv.slice(2);
const only = args.includes('--only') ? args[args.indexOf('--only') + 1] : null;
const update = args.includes('--update');
const show = args.includes('--show');
const fixtures = sourceFixtures(args.includes('--full'));
const selected = only ? fixtures.filter(([name]) => name === only) : fixtures;
if (!selected.length) { console.error(`no fixture named ${only}`); process.exit(2); }

const knownFile = path.join(__dirname, 'first-edit-known.json');
const known = fs.existsSync(knownFile) ? JSON.parse(fs.readFileSync(knownFile, 'utf8')) : {};
const rank = { none: 0, knownFormatting: 1, unknown: 2, unsafe: 3 };
const anchor = 'EDITHERE';

(async () => {
  const { page, errors, mode, flush, load, pause, close } = await openEditor();
  const failures = [], improved = [], results = {};
  try {
    for (const [name, text] of selected) {
      const before = failures.length;
      const fail = message => failures.push(`${name}: ${message}`);
      try {
        const loaded = text.replace(/\n*$/, '') + (text.trim() ? '\n\n' : '') + anchor + '\n';
        await mode(true, false);
        await load(loaded);
        await mode(false, false);
        const { prediction, exported } = await page.evaluate(() => ({ prediction: window.__typedownPendingNormalization(), exported: window.__typedownMuya.getMarkdown() }));
        if (!prediction) { fail('no prediction after the load'); continue; }
        // The notice that the first edit may rewrite part of the document shows exactly when that is predicted.
        const warned = await page.evaluate(() => !!document.querySelector('.td-first-edit-warning'));
        if (warned !== (prediction.pendingNormalization === 'unsafe')) fail(`first-edit notice ${warned ? 'shown' : 'missing'} for a ${prediction.pendingNormalization} prediction`);
        if (exported.split(anchor).length !== 2) { fail(`the export does not hold "${anchor}" exactly once`); continue; }

        // A real click and keystroke, so the edit goes through Muya's own input handling. The caret may start in
        // a block that expands while edited (maths, diagrams); the click collapses it and the page moves, so the
        // click is repeated until the caret is in the anchor's text.
        const locate = () => page.evaluate(anchor => {
          const walker = document.createTreeWalker(document.querySelector('#ag-editor-id'), NodeFilter.SHOW_TEXT);
          for (let node; (node = walker.nextNode());) {
            const at = node.data.indexOf(anchor);
            if (at < 0) continue;
            const selection = window.getSelection();
            if (selection.anchorNode === node) return { inside: true };
            node.parentElement.scrollIntoView({ block: 'center' });
            const range = document.createRange();
            range.setStart(node, at + anchor.length - 1);
            range.setEnd(node, at + anchor.length);
            const rect = range.getBoundingClientRect();
            return { x: rect.right - 1, y: rect.top + rect.height / 2 };
          }
          return null;
        }, anchor);
        let point = await locate();
        for (let tries = 0; point && !point.inside && tries < 3; tries++) {
          await page.mouse.click(point.x, point.y);
          await pause(250);
          point = await locate();
        }
        if (point && !point.inside) {
          // Something covers the anchor (the floating preview of a maths or diagram block being edited): put
          // Muya's caret there directly. The keystroke below is still real.
          await page.evaluate(anchor => {
            const state = window.__typedownMuya.contentState;
            const find = blocks => { for (const b of blocks) { if (typeof b.text === 'string' && b.text.includes(anchor)) return b; const r = find(b.children || []); if (r) return r; } };
            const block = find(state.blocks);
            const position = { key: block.key, offset: block.text.indexOf(anchor) + anchor.length };
            state.cursor = { start: position, end: position, anchor: position, focus: position };
            state.render();
          }, anchor);
          await pause(250);
          point = await locate();
        }
        if (!point?.inside) { fail(point ? `could not put the caret into "${anchor}"` : `"${anchor}" is not on the page`); continue; }
        await page.keyboard.press('End');
        await page.keyboard.type('X');
        await pause();

        const actual = await flush();
        const expected = exported.replace(anchor, anchor + 'X');
        if (actual !== expected) fail(`the edit did more than the prediction said: ${firstDifference(expected, actual)}`);
        const after = await page.evaluate(() => window.__typedownPendingNormalization());
        if (after?.pendingNormalization !== 'none') fail(`after the edit the prediction is ${after?.pendingNormalization}, not none`);

        const verdict = { pendingNormalization: prediction.pendingNormalization, reasons: prediction.reasons };
        results[name] = verdict;
        if (show) console.log(`\n--- ${name}: ${verdict.pendingNormalization} ${verdict.reasons.join(',')}\n${JSON.stringify(loaded)}\n${JSON.stringify(exported)}`);
        const was = known[name];
        if (!update && was) {
          if (rank[verdict.pendingNormalization] > rank[was.pendingNormalization])
            fail(`${was.pendingNormalization} -> ${verdict.pendingNormalization} (${verdict.reasons.join(',')})`);
          else if (rank[verdict.pendingNormalization] < rank[was.pendingNormalization])
            improved.push(`${name}: ${was.pendingNormalization} -> ${verdict.pendingNormalization}`);
        } else if (!update && !only) {
          fail(`not in first-edit-known.json (${verdict.pendingNormalization}); run with --update`);
        }
      } catch (e) {
        fail(e.message.split('\n')[0]);
      } finally {
        process.stdout.write(failures.length === before ? '.' : 'F');
      }
    }
    process.stdout.write('\n');
    if (errors.length) failures.push(`page errors: ${errors.join(' | ')}`);
  } finally {
    await close();
  }

  const counts = {};
  for (const r of Object.values(results)) counts[r.pendingNormalization] = (counts[r.pendingNormalization] || 0) + 1;
  const unsafe = Object.entries(results).filter(([, r]) => r.pendingNormalization === 'unsafe');
  console.log(`verdicts: ${JSON.stringify(counts)}`);
  if (unsafe.length) console.log(`unsafe (a first keystroke would lose text):\n  ${unsafe.map(([n, r]) => `${n}: ${r.reasons.join(',')}`).join('\n  ')}`);
  if (update && !failures.length) {
    fs.writeFileSync(knownFile, JSON.stringify(only ? { ...known, ...results } : results, null, 2) + '\n');
    console.log(`wrote ${path.basename(knownFile)}`);
  }
  if (improved.length) console.log(`better than the baseline, run with --update:\n  ${improved.join('\n  ')}`);
  if (failures.length) {
    console.log(`FAIL: ${failures.length} problem(s) in ${selected.length} fixtures\n  ` + failures.join('\n  '));
    process.exit(1);
  }
  console.log(`PASS: the first visual edit did exactly what was predicted for ${selected.length} fixtures`);
})().catch(e => { console.error(e); process.exit(1); });
