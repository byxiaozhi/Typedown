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
  // A store review: "changing the list format easily turns the text after it into a quote". The selection took in the
  // block after the one meant (above), and a list in a selection became one item holding the whole list.
  { name: 'a list selected and made ordered leaves the paragraph after it alone',
    doc: '- a\n- b\n\nafter\n', click: '#ag-editor-id li p', keys: ['Control+Home', 'Shift+ArrowDown', 'Shift+End'],
    cmd: 'ol-order', expect: '1. a\n2. b\n\nafter\n' },
  { name: 'a list and the paragraph after it made ordered become one list, not a list inside an item',
    doc: '- a\n- b\n\nafter\n\nlast\n', click: '#ag-editor-id li p', keys: ['Control+Home', 'Shift+ArrowDown', 'Shift+ArrowDown', 'Shift+End'],
    cmd: 'ol-order', expect: '1. a\n2. b\n3. after\n\nlast\n' },
  { name: 'a paragraph and the list after it made a bullet list become one list',
    doc: 'before\n\n- a\n- b\n\nlast\n', click: '#ag-editor-id > p.ag-paragraph', keys: ['Control+Home', 'Shift+ArrowDown', 'Shift+End'],
    cmd: 'ul-bullet', expect: '- before\n- a\n- b\n\nlast\n' },
  { name: 'two bullet lists selected and made a bullet list again stop being lists',
    doc: '- a\n\n* b\n\nlast\n', click: '#ag-editor-id li p', keys: ['Control+Home', 'Shift+ArrowDown', 'Shift+End'],
    cmd: 'ul-bullet', expect: 'a\n\nb\n\nlast\n' },
  { name: 'two lists and the paragraph between them made a bullet list become one list',
    doc: '- a\n- b\n\npara\n\n- c\n\nlast\n', click: '#ag-editor-id li p', keys: ['Control+Home', 'Shift+ArrowDown', 'Shift+ArrowDown', 'Shift+ArrowDown', 'Shift+End'],
    cmd: 'ul-bullet', expect: '- a\n- b\n- para\n- c\n\nlast\n' },
  { name: 'a task list and a paragraph made a task list keep one checkbox each',
    doc: '- [x] a\n\nafter\n', click: '#ag-editor-id li p', keys: ['drag:#ag-editor-id li p|#ag-editor-id > p.ag-paragraph'],
    cmd: 'ul-task', expect: '- [x] a\n- [ ] after\n' },
];

(async () => {
  const { page, mode, load, flush, pause, close, errors } = await openEditor();
  let ok = true;
  const press = async k => {
    // 'drag:<from>|<to>' selects with the mouse from the start of one element's text to the end of another's.
    if (k.startsWith('drag:')) {
      const [from, to] = k.slice(5).split('|');
      const a = await page.$eval(from, e => { const r = document.createRange(); r.selectNodeContents(e); const b = r.getClientRects()[0]; return { x: b.left + 1, y: b.top + b.height / 2 }; });
      const b = await page.$eval(to, e => { const r = document.createRange(); r.selectNodeContents(e); const l = [...r.getClientRects()].pop(); return { x: l.right - 1, y: l.top + l.height / 2 }; });
      await page.mouse.move(a.x, a.y); await page.mouse.down();
      await page.mouse.move(b.x, b.y, { steps: 10 }); await page.mouse.up();
      await pause(150);
      return;
    }
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
