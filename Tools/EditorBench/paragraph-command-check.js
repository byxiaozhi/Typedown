// Paragraph commands (Paragraph menu, its shortcuts, the block menu) change what the reader selected and nothing
// around it. A Store review: "changing selected text from paragraph to heading 2 also changes the text right above
// and below it; only when there are blank lines around it does it change just what I selected".
//
//   node paragraph-command-check.js
const { openEditor } = require('./harness');

const cases = [
  // A paragraph of several lines (single line breaks): the heading is the line the caret is on.
  { name: 'heading on the middle line of a three-line paragraph leaves the lines around it as text',
    doc: 'line one\nline two\nline three\n\nafter\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Control+Home', 'ArrowDown', 'End'],
    cmd: 'heading 2', expect: 'line one\n\n## line two\n\nline three\n\nafter\n' },
  { name: 'heading on the first line of a paragraph',
    doc: 'line one\nline two\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Control+Home'],
    cmd: 'heading 1', expect: '# line one\n\nline two\n' },
  { name: 'heading on the last line of a paragraph',
    doc: 'line one\nline two\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Control+End'],
    cmd: 'heading 3', expect: 'line one\n\n### line two\n' },
  { name: 'raising a paragraph line one heading level makes only that line a heading',
    doc: 'line one\nline two\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Control+Home'],
    cmd: 'upgrade heading', expect: '###### line one\n\nline two\n' },
  { name: 'heading on a paragraph with blank lines around it (always worked)',
    doc: 'one\n\ntwo\n\nthree\n', click: '#ag-editor-id p.ag-paragraph:nth-of-type(2)', keys: [],
    cmd: 'heading 2', expect: 'one\n\n## two\n\nthree\n' },
  // A selection made with Shift+Down ends at the start of the next block, which has nothing selected.
  { name: 'a paragraph selected with Shift+Down becomes the heading (the next one is not selected)',
    doc: 'one\n\ntwo\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Home', 'Shift+ArrowDown'],
    cmd: 'heading 2', expect: '## one\n\ntwo\n' },
  { name: 'quoting two paragraphs selected with Shift+Down leaves the third alone',
    doc: 'one\n\ntwo\n\nthree\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Home', 'Shift+ArrowDown', 'Shift+ArrowDown'],
    cmd: 'blockquote', expect: '> one\n> \n> two\n\nthree\n' },
  { name: 'a selection starting at the very end of a paragraph does not take that paragraph in',
    doc: 'one\n\ntwo\n\nthree\n', click: '#ag-editor-id p.ag-paragraph', keys: ['End', 'Shift+ArrowDown', 'Shift+End'],
    cmd: 'blockquote', expect: 'one\n\n> two\n\nthree\n' },
];

(async () => {
  const { page, mode, load, flush, pause, close, errors } = await openEditor();
  let ok = true;
  const press = async k => {
    const parts = k.split('+');
    for (const p of parts.slice(0, -1)) await page.keyboard.down(p);
    await page.keyboard.press(parts[parts.length - 1]);
    for (const p of parts.slice(0, -1).reverse()) await page.keyboard.up(p);
    await pause(60);
  };
  try {
    await mode(false, false);
    for (const c of cases) {
      await load(c.doc); await pause(400);
      await page.click(c.click); await pause(100);
      for (const k of c.keys) await press(k);
      await pause(150);
      await page.evaluate(cmd => window.__deliver('UpdateParagraph', cmd), c.cmd);
      await pause(400);
      const text = await flush();
      const pass = text === c.expect;
      console.log(`${pass ? 'PASS' : 'FAIL'} ${c.name}${pass ? '' : `\n     expected ${JSON.stringify(c.expect)}\n     got      ${JSON.stringify(text)}`}`);
      ok = ok && pass;
    }
    if (errors.length) { console.log('page errors:', errors); ok = false; }
  } finally { await close(); }
  console.log(ok ? '\nPASS' : '\nFAIL');
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error('check failed:', e.message); process.exit(1); });
