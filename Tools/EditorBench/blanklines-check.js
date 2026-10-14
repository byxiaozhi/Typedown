// Blank lines survive the visual editor: N blank lines in the file are N blank lines after the first visual edit.
//
// Extra blank lines show as empty paragraphs, fewer of them than there are lines (an empty paragraph is written back
// as two lines). Before the exact counts were kept (Muya/lib/utils/blankLines.js), 2 blank lines came back as 1 and
// 4 as 3: an even run lost a line as soon as the reader typed anything in visual mode.
//
//   1. model: every pair of block kinds (paragraph, heading, lists, quote, code, table, maths, HTML, rule) with 1-6
//      blank lines between them, the same runs at the start and end of the document, inside a quotation and inside
//      a list item, imported and exported - the export must be the document, byte for byte;
//   2. a real keystroke in visual mode on a sample of those: the flushed text is the document plus that keystroke;
//   3. what an edit of the blank lines themselves saves: Enter on a line adds an empty paragraph, Backspace in an
//      empty paragraph removes one, and the run is then written the ordinary way (each empty paragraph two lines).
//
//   node blanklines-check.js [--show]
const { firstDifference, openEditor } = require('./harness');

const show = process.argv.includes('--show');
const kinds = {
  paragraph: 'Some text',
  heading: '# Heading',
  list: '- one\n- two',
  ordered: '1. first\n2. second',
  task: '- [ ] task',
  quote: '> quoted',
  fence: '```js\nlet a = 1\n```',
  table: '| a | b |\n| --- | --- |\n| 1 | 2 |',
  maths: '$$\nx^2\n$$',
  html: '<div>html</div>',
  rule: '***', // not '---': two of them at the top of a document are front matter
};
const runs = [1, 2, 3, 4, 5, 6];
const gap = n => '\n'.repeat(n);

function documents() {
  const docs = [];
  const names = Object.keys(kinds);
  for (const a of names) for (const b of names) for (const n of runs) {
    // Two lists of the same kind with blank lines between them are one loose list, not two blocks.
    if (a === b && ['list', 'ordered', 'task'].includes(a)) continue;
    docs.push([`${a} ${n} ${b}`, `${kinds[a]}\n${gap(n)}${kinds[b]}\n`, 'middle']);
  }
  for (const a of names) for (const n of runs) {
    docs.push([`start ${n} ${a}`, `${gap(n)}${kinds[a]}\n`, 'start']);
    docs.push([`${a} ${n} end`, `${kinds[a]}\n${gap(n)}`, 'end']);
  }
  for (const n of runs) {
    docs.push([`quote: a ${n} b`, `> a\n${'>\n'.repeat(n)}> b\n`, 'middle']);
    docs.push([`list item: a ${n} b`, `- a\n${gap(n)}  b\n`, 'middle']);
  }
  return docs;
}

(async () => {
  const { page, errors, mode, flush, load, pause, close } = await openEditor();
  const failures = [];
  let checked = 0;
  const exportOf = md => page.evaluate(md => {
    const muya = window.__typedownMuya;
    muya.contentState.blocks = muya.contentState.markdownToState(md);
    return muya.getMarkdown();
  }, md);
  try {
    await mode(false, false);

    // 1. Model: import and export.
    const docs = documents();
    for (const [name, md] of docs) {
      const out = await exportOf(md);
      checked++;
      if (out !== md) failures.push(`${name}: ${firstDifference(md, out)}`);
      if (show) console.log(name.padEnd(28), JSON.stringify(md), out === md ? 'ok' : `-> ${JSON.stringify(out)}`);
    }

    // 2. A real keystroke in visual mode, in a paragraph away from the blank lines.
    const anchor = 'EDITHERE';
    const typeAtAnchor = async () => {
      const point = await page.evaluate(anchor => {
        const walker = document.createTreeWalker(document.querySelector('#ag-editor-id'), NodeFilter.SHOW_TEXT);
        for (let node; (node = walker.nextNode());) {
          const at = node.data.indexOf(anchor);
          if (at < 0) continue;
          node.parentElement.scrollIntoView({ block: 'center' });
          const range = document.createRange();
          range.setStart(node, at + anchor.length - 1);
          range.setEnd(node, at + anchor.length);
          const rect = range.getBoundingClientRect();
          return { x: rect.right - 1, y: rect.top + rect.height / 2 };
        }
        return null;
      }, anchor);
      if (!point) return false;
      await page.mouse.click(point.x, point.y);
      await pause(200);
      await page.keyboard.press('End');
      await page.keyboard.type('X');
      await pause();
      return true;
    };
    const sample = docs.filter(([name]) => /^(paragraph|heading|list|fence|table|start|quote:|list item:)/.test(name) &&
      (/^(start|quote:|list item:)/.test(name) || / (paragraph|heading|table|end)$/.test(name)));
    for (const [name, md, position] of sample) {
      const loaded = position === 'start' ? `${md}\n${anchor}\n` : `${anchor}\n\n${md}`;
      await mode(true, false);
      await load(loaded);
      await mode(false, false);
      checked++;
      if (!await typeAtAnchor()) { failures.push(`${name} (typed): "${anchor}" is not on the page`); continue; }
      const actual = await flush();
      const expected = loaded.replace(anchor, anchor + 'X');
      if (actual !== expected) failures.push(`${name} (typed): ${firstDifference(expected, actual)}`);
    }

    // 3. Editing the blank lines themselves: the counts read with the file no longer apply, and the run is written
    //    the ordinary way - each empty paragraph as two lines, one blank line where there is none.
    const caretIn = async (index, atEnd) => {
      await page.evaluate((index, atEnd) => {
        const state = window.__typedownMuya.contentState;
        const block = state.blocks[index].children[0];
        const position = { key: block.key, offset: atEnd ? block.text.length : 0 };
        state.cursor = { start: position, end: position, anchor: position, focus: position };
        state.render();
      }, index, atEnd);
      await pause(200);
    };
    const edits = [
      // [what, document, block index, caret at end, key, expected]
      ['Enter after a paragraph followed by 3 blank lines', 'a\n\n\n\nb\n', 0, true, 'Enter', 'a\n\n\n\n\n\nb\n'],
      ['Enter after a paragraph followed by 2 blank lines', 'a\n\n\nb\n', 0, true, 'Enter', 'a\n\n\n\nb\n'],
      ['Backspace in the empty paragraph of 4 blank lines', 'a\n\n\n\n\nb\n', 1, false, 'Backspace', 'a\n\nb\n'],
    ];
    for (const [what, md, index, atEnd, key, expected] of edits) {
      await mode(true, false);
      await load(md);
      await mode(false, false);
      await caretIn(index, atEnd);
      await page.keyboard.press(key);
      await pause();
      checked++;
      const actual = await flush();
      if (actual !== expected) failures.push(`${what}: ${firstDifference(expected, actual)}`);
    }
    if (errors.length) failures.push(`page errors: ${errors.join(' | ')}`);
  } finally {
    await close();
  }
  if (failures.length) {
    console.log(`FAIL: ${failures.length} of ${checked} documents changed their blank lines\n  ` + failures.slice(0, 40).join('\n  ') +
      (failures.length > 40 ? `\n  ... ${failures.length - 40} more` : ''));
    process.exit(1);
  }
  console.log(`PASS: ${checked} documents kept their blank lines exactly`);
})().catch(e => { console.error(e); process.exit(1); });
