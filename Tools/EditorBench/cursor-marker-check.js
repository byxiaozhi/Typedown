// A whole reload puts the caret back by writing a marker into the text before parsing it (addCursorToMarkdown). On
// a line of marker characters only - a front matter fence, a thematic break, a setext underline - the marker made
// the line ordinary text: a YAML front matter came back as a paragraph and a rule, and the next edit saved it so
// (found by E2E F01: an automation write while the caret was on line 1). This check switches from source to visual
// mode with the caret on each such line, and writes over a document whose caret is on line 1, and requires every
// block to come back as what it was, with the text unchanged.
//
//   node cursor-marker-check.js
const crypto = require('crypto');
const { openEditor } = require('./harness');

const sha = text => crypto.createHash('sha256').update(text, 'utf8').digest('hex');
const yaml = '---\ntitle: kept\n---\n\n# Doc\n\nbody\n';
// [label, text, caret line, caret ch, the first blocks visual mode must show]
const switches = [
  ['yaml front matter, opening fence, column 0', yaml, 0, 0, 'pre:frontmatter,h1:'],
  ['yaml front matter, opening fence, end', yaml, 0, 3, 'pre:frontmatter,h1:'],
  ['yaml front matter, content', yaml, 1, 3, 'pre:frontmatter,h1:'],
  ['yaml front matter, closing fence', yaml, 2, 3, 'pre:frontmatter,h1:'],
  ['toml front matter, opening fence', '+++\nt = 1\n+++\n\n# D\n', 0, 3, 'pre:frontmatter,h1:'],
  ['json front matter, opening brace', '{\n"t": 1\n}\n\n# D\n', 0, 1, 'pre:frontmatter,h1:'],
  ['json front matter, closing brace', '{\n"t": 1\n}\n\n# D\n', 2, 1, 'pre:frontmatter,h1:'],
  ['thematic break ---', 'a\n\n---\n\nb\n', 2, 3, 'p:,hr:,p:'],
  ['thematic break ***', 'a\n\n***\n\nb\n', 2, 1, 'p:,hr:,p:'],
  ['setext === underline', 'Title\n=====\n\nb\n', 1, 5, 'h1:,p:'],
  ['setext --- underline', 'Title\n-----\n\nb\n', 1, 2, 'h2:,p:'],
];

(async () => {
  const failures = [];
  const blocksOf = page => page.evaluate(() => window.__typedownMuya.contentState.blocks.map(b => b.type + ':' + (b.functionType || '')).join(','));
  for (const [label, text, line, ch, expected] of switches) {
    const { page, errors, mode, load, pause, close } = await openEditor();
    try {
      await mode(true, false);
      await load(text);
      await page.evaluate(({ line, ch }) => { const cm = document.querySelector('.CodeMirror').CodeMirror; cm.focus(); cm.setCursor({ line, ch }); }, { line, ch });
      await pause(200);
      await mode(false, false);
      await pause(300);
      const blocks = await blocksOf(page);
      const exported = await page.evaluate(() => window.__typedownMuya.getMarkdown());
      const ok = blocks.startsWith(expected) && exported === text && !errors.length;
      console.log(`${ok ? 'PASS' : 'FAIL'} ${label}: ${blocks}${exported === text ? '' : ' export ' + JSON.stringify(exported)}${errors.length ? ' errors ' + errors.join(' | ') : ''}`);
      if (!ok) failures.push(label);
    } finally {
      await close();
    }
  }

  // An automation write that replaces the whole document while the reader's caret is on line 1 (E2E F01).
  {
    const { page, errors, mode, load, pause, close } = await openEditor();
    try {
      // The host sends the document's remembered caret with its load; a whole reload puts it back from there.
      await mode(false, false);
      await page.evaluate(() => {
        delete window.__last.FileLoaded;
        window.__deliver('LoadFile', { text: '# F01\n', loadId: 50, basePath: '/tmp', cursor: { anchor: { line: 0, ch: 5 }, focus: { line: 0, ch: 5 } } });
      });
      await page.waitForFunction(() => window.__last.FileLoaded?.loadId === 50, { timeout: 30000 });
      await pause(200);
      await page.evaluate(({ baseContentHash, text }) => {
        delete window.__last.DocumentEditApplied;
        window.__deliver('ApplyDocumentEdit', { operationId: 'op1', baseContentHash, text });
      }, { baseContentHash: sha('# F01\n'), text: yaml });
      // A build without the automation API (main before it is merged) never answers: nothing to check there.
      const answered = await page.waitForFunction(() => window.__last.DocumentEditApplied?.operationId === 'op1', { timeout: 5000 }).then(() => true, () => false);
      if (!answered) console.log('SKIP a write over a document with the caret on line 1: this build has no automation writes');
      else {
        await pause(300);
        const blocks = await blocksOf(page);
        const exported = await page.evaluate(() => window.__typedownMuya.getMarkdown());
        const ok = blocks.startsWith('pre:frontmatter,h1:') && exported === yaml && !errors.length;
        console.log(`${ok ? 'PASS' : 'FAIL'} a write over a document with the caret on line 1: ${blocks}${exported === yaml ? '' : ' export ' + JSON.stringify(exported)}`);
        if (!ok) failures.push('write with the caret on line 1');
      }
    } finally {
      await close();
    }
  }
  console.log(failures.length ? `${failures.length} failed` : 'all passed');
  process.exit(failures.length ? 1 : 0);
})();
