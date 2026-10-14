// Source-text mapping stability: a document the reader has not edited must come back byte for byte through
// every view. Each fixture is loaded, then taken through reading -> visual -> source twice; the text flushed in
// reading and visual mode and the text shown in source mode must equal what was loaded, and no MarkdownChange
// may be reported. Run against a freshly built editor (STATICS=<dir>, default the Windows bundle).
//
// What this proves, and what it does not: Muya exports a normalized form of what it imported, and the editor
// maps that normalized form back to the original source (`importedRef` in Muya/index.tsx). This check shows the
// mapping holds across mode switches, asynchronous renders (Mermaid, KaTeX) and flushes. It does NOT prove that
// the first real visual edit serializes the document faithfully - that is first-edit-check.js.
//
//   node source-stability-check.js            # built-in fixtures + a sample of the CommonMark examples
//   node source-stability-check.js --full     # every CommonMark example and the whole 300k document
//   node source-stability-check.js --only gfm-table
const { sourceFixtures, firstDifference, openEditor } = require('./harness');

const args = process.argv.slice(2);
const only = args.includes('--only') ? args[args.indexOf('--only') + 1] : null;
const fixtures = sourceFixtures(args.includes('--full'));
const selected = only ? fixtures.filter(([name]) => name === only) : fixtures;
if (!selected.length) { console.error(`no fixture named ${only}`); process.exit(2); }

(async () => {
  const { page, errors, mode, flush, load, close } = await openEditor();
  const failures = [];
  try {
    const source = () => page.evaluate(() => document.querySelector('.CodeMirror').CodeMirror.getValue());
    for (const [name, text] of selected) {
      const fail = (stage, actual) => failures.push(`${name}: ${stage} ${firstDifference(text, actual)}`);
      const before = failures.length;
      try {
        await mode(true, false);
        const loaded = await load(text);
        if (loaded !== text) fail('FileLoaded handshake', loaded);
        for (let round = 1; round <= 2; round++) {
          await mode(false, true);
          const reading = await flush();
          if (reading !== text) fail(`round ${round} reading flush`, reading);
          await mode(false, false);
          const visual = await flush();
          if (visual !== text) fail(`round ${round} visual flush`, visual);
          await mode(true, false);
          const shown = await source();
          if (shown !== text) fail(`round ${round} source view`, shown);
        }
        const change = await page.evaluate(() => window.__last.MarkdownChange);
        if (change) failures.push(`${name}: a MarkdownChange was reported without an edit (${firstDifference(text, change.text)})`);
      } catch (e) {
        failures.push(`${name}: ${e.message.split('\n')[0]}`);
      }
      process.stdout.write(failures.length === before ? '.' : 'F');
    }
    process.stdout.write('\n');
    if (errors.length) failures.push(`page errors: ${errors.join(' | ')}`);
  } finally {
    await close();
  }
  if (failures.length) {
    console.log(`FAIL: ${failures.length} problem(s) in ${selected.length} fixtures\n  ` + failures.join('\n  '));
    process.exit(1);
  }
  console.log(`PASS: source text mapping stable for ${selected.length} fixtures through reading/visual/source switches and flushes`);
})().catch(e => { console.error(e); process.exit(1); });
