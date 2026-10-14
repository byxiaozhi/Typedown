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
  // Every kind of paragraph command on a selection made with the keyboard, the block after it left as it was (and
  // drawn once).
  { name: 'quote on two paragraphs selected with Shift+Down (Chinese text)',
    doc: '第一段：要加引用。\n\n第二段：也要加引用。\n\n第三段：不该被改。\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Control+Home', 'Shift+ArrowDown', 'Shift+ArrowDown'],
    cmd: 'blockquote', expect: '> 第一段：要加引用。\n> \n> 第二段：也要加引用。\n\n第三段：不该被改。\n' },
  { name: 'ordered list on a paragraph and a list, a quote after them',
    doc: '前言\n\n- 苹果\n- 香蕉\n\n> 后面的引用：不该变\n', click: '#ag-editor-id > p.ag-paragraph', keys: ['Control+Home', 'Shift+ArrowDown', 'Shift+ArrowDown', 'Shift+End'],
    cmd: 'ol-order', expect: '1. 前言\n2. 苹果\n3. 香蕉\n\n> 后面的引用：不该变\n' },
  { name: 'bullet list on two paragraphs selected with the keyboard',
    doc: 'one\n\ntwo\n\nthree\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Control+Home', 'Shift+ArrowDown', 'Shift+End'],
    cmd: 'ul-bullet', expect: '- one\n\n- two\n\nthree\n' },
  { name: 'task list on two paragraphs selected with the keyboard',
    doc: 'one\n\ntwo\n\nthree\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Control+Home', 'Shift+ArrowDown', 'Shift+End'],
    cmd: 'ul-task', expect: '- [ ] one\n\n- [ ] two\n\nthree\n' },
  { name: 'code block on two paragraphs selected with the keyboard',
    doc: 'one\n\ntwo\n\nthree\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Control+Home', 'Shift+ArrowDown', 'Shift+End'],
    cmd: 'pre', expect: '```\none\n\ntwo\n\n```\n\nthree\n' },
  { name: 'heading on a paragraph selected with Shift+Down, two paragraphs after it',
    doc: 'one\n\ntwo\n\nthree\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Control+Home', 'Shift+ArrowDown'],
    cmd: 'heading 2', expect: '## one\n\ntwo\n\nthree\n' },
  // Not a paragraph command but the same render: Enter over a selection of whole paragraphs made with the keyboard.
  { name: 'Enter over three paragraphs selected with the keyboard',
    doc: 'one\n\ntwo\n\nthree\n\nfour\n', click: '#ag-editor-id p.ag-paragraph', keys: ['Control+Home', 'Shift+ArrowDown', 'Shift+ArrowDown', 'Shift+End', 'Enter'],
    cmd: null, expectEnd: '\n\nfour\n' },
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
      if (c.cmd) await page.evaluate(cmd => window.__deliver('UpdateParagraph', cmd), c.cmd);
      await pause(400);
      const text = await flush();
      let pass = c.expect !== undefined ? text === c.expect : text.endsWith(c.expectEnd);
      // The page shows exactly the document's blocks: the same top-level blocks, each once, in the model's order.
      // A partial render that replaced the page only up to a stale key left the block after the selection drawn
      // twice while the Markdown was right.
      const page_ = await page.evaluate(() => {
        const dom = [...document.querySelectorAll('#ag-editor-id > *')].map(e => e.id);
        const model = window.__typedownMuya.contentState.blocks.map(b => b.key);
        return { dom, model };
      });
      const sameBlocks = JSON.stringify(page_.dom) === JSON.stringify(page_.model);
      // Typing into the last block on the page (after the selection) edits the document's last block.
      const at = await page.evaluate(() => {
        const last = document.querySelector('#ag-editor-id > :last-child');
        const walker = document.createTreeWalker(last, NodeFilter.SHOW_TEXT, { acceptNode: n => n.data.trim() ? 1 : 3 });
        let node = null, n;
        while ((n = walker.nextNode())) node = n;
        if (!node) return null;
        const r = document.createRange(); r.selectNodeContents(node);
        const box = [...r.getClientRects()].pop();
        return { x: box.right - 1, y: box.top + box.height / 2 };
      });
      let typed = null;
      if (at) {
        await page.mouse.click(at.x, at.y); await pause(100);
        await page.keyboard.press('End');
        await page.keyboard.type('Z');
        await pause(300);
        typed = await flush();
      }
      const typedOk = typed === text.replace(/\n$/, 'Z\n');
      pass = pass && sameBlocks && typedOk;
      console.log(`${pass ? 'PASS' : 'FAIL'} ${c.name}${pass ? '' : `\n     expected ${JSON.stringify(c.expect ?? '...' + c.expectEnd)}\n     got      ${JSON.stringify(text)}` +
        (sameBlocks ? '' : `\n     page blocks ${JSON.stringify(page_.dom)} but the document's are ${JSON.stringify(page_.model)}`) +
        (typedOk ? '' : `\n     Z typed at the end of the last block on the page gave ${JSON.stringify(typed)}`)}`);
      ok = ok && pass;
    }
    if (errors.length) { console.log('page errors:', errors); ok = false; }
  } finally { await close(); }
  console.log(ok ? '\nPASS' : '\nFAIL');
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error('check failed:', e.message); process.exit(1); });
