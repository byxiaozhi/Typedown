// Backspace in a table cell stays in that cell: one Backspace too many after emptying a cell went to the end of the
// cell before, the next deleted that cell's text and what was typed next landed there - a reader saw "a cell emptied
// takes no typing" and lost its neighbour's text. A table with nothing in it still becomes a paragraph with Backspace
// in its first cell.
//
//   node table-backspace-check.js
const { openEditor } = require('./harness');

const doc = '# T\n\n| A | B |\n| --- | --- |\n| abc | d |\n| e | f |\n\nafter\n';

(async () => {
  const { page, mode, load, flush, pause, close } = await openEditor();
  let ok = true;
  const check = (c, label, text) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}${c ? '' : '\n     ' + JSON.stringify(text)}`); ok = ok && c; };
  const typeIn = async (selector, backspaces, typed) => {
    await page.click(selector);
    await page.keyboard.press('End');
    for (let i = 0; i < backspaces; i++) await page.keyboard.press('Backspace');
    await pause(200);
    if (typed) await page.keyboard.type(typed);
    await pause(300);
    return flush();
  };
  try {
    await mode(false, false);

    await load(doc); await pause(500);
    let t = await typeIn('#ag-editor-id tbody tr:nth-child(1) > td:nth-child(1)', 3, 'XY');
    check(t.includes('| XY | d |') && t.includes('| A | B |'), 'a body cell emptied takes what is typed next', t);

    await load(doc); await pause(500);
    t = await typeIn('#ag-editor-id tbody tr:nth-child(1) > td:nth-child(1)', 6, 'XY');
    check(t.includes('| XY | d |') && t.includes('| A | B |'), 'Backspace past the start of an emptied cell leaves the cells before it alone', t);

    await load(doc); await pause(500);
    t = await typeIn('#ag-editor-id thead th:nth-child(2)', 4, 'HZ');
    check(t.includes('| A | HZ |'), 'the same in a header cell', t);

    await load(doc); await pause(500);
    t = await typeIn('#ag-editor-id tbody tr:nth-child(2) > td:nth-child(2)', 0, null);
    await page.keyboard.press('Home'); await page.keyboard.press('Backspace'); await pause(300);
    t = await flush();
    check(t.includes('| abc | d |') && t.includes('| e | f |'), 'Backspace at the start of a cell with text deletes nothing', t);

    await load('# T\n\n|  |  |\n| --- | --- |\n|  |  |\n\nafter\n'); await pause(500);
    t = await typeIn('#ag-editor-id thead th:nth-child(1)', 1, 'P');
    check(!t.includes('---') && /\nP\n/.test(t), 'a table with nothing in it becomes a paragraph with Backspace in its first cell', t);
  } finally { await close(); }
  console.log(ok ? '\nPASS' : '\nFAIL');
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error('check failed:', e.message); process.exit(1); });
