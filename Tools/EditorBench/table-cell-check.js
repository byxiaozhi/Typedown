// Two Store reviews about table cells: "if all the text in a cell is deleted the cell takes no input until you click
// elsewhere and back" (1.2.18), and "a cell cannot hold more than one line - could Alt+Enter break the line, as in
// Excel?" (1.2.18). A cell emptied by selecting its text and deleting it, or by typing over the selection, takes
// what is typed next; Shift+Enter and Alt+Enter put a line break (<br/>) in the cell, which shows as a second line
// and survives a load. Backspace in an emptied cell is table-backspace-check.js.
//
//   node table-cell-check.js
const { openEditor } = require('./harness');

const doc = '# T\n\n| A | B |\n| --- | --- |\n| abc | d |\n\nafter\n';
const cell = '#ag-editor-id tbody tr:nth-child(1) > td:nth-child(1)';

(async () => {
  const { page, mode, load, flush, pause, close, errors } = await openEditor();
  let ok = true;
  const check = (c, label, text) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}${c ? '' : '\n     ' + JSON.stringify(text)}`); ok = ok && c; };
  const cellLines = () => page.evaluate(s => {
    const td = document.querySelector(s);
    const lineHeight = parseFloat(getComputedStyle(td).lineHeight) || 24;
    const span = td.querySelector('span') || td;
    return Math.round(span.getBoundingClientRect().height / lineHeight);
  }, cell);
  try {
    await mode(false, false);

    // Emptied with the mouse: drag over the text, Delete, type.
    await load(doc); await pause(500);
    const box = await (await page.$(cell)).boundingBox();
    await page.mouse.move(box.x + 4, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width - 4, box.y + box.height / 2, { steps: 8 });
    await page.mouse.up();
    await pause(200);
    await page.keyboard.press('Delete');
    await pause(200);
    await page.keyboard.type('NEW');
    await pause(300);
    let t = await flush();
    check(t.includes('| NEW | d |'), 'a cell emptied by selecting its text and deleting it takes what is typed next', t);

    // Typed over: select the text with the keyboard and type.
    await load(doc); await pause(500);
    await page.click(cell); await page.keyboard.press('End');
    await page.keyboard.down('Shift'); await page.keyboard.press('Home'); await page.keyboard.up('Shift');
    await page.keyboard.type('XY');
    await pause(300);
    t = await flush();
    check(t.includes('| XY | d |'), 'typing over the selected text of a cell replaces it', t);

    for (const key of ['Shift', 'Alt']) {
      await load(doc); await pause(500);
      await page.click(cell); await page.keyboard.press('End');
      const before = await cellLines();
      await page.keyboard.down(key); await page.keyboard.press('Enter'); await page.keyboard.up(key);
      await page.keyboard.type('two');
      await pause(300);
      t = await flush();
      check(t.includes('| abc<br/>two | d |') && t.includes('after'), `${key}+Enter puts a line break in the cell and stays in it`, t);
      // Shown as a second line once the caret has left the cell (inside it the <br/> is shown as source).
      await page.click('#ag-editor-id > p.ag-paragraph:last-of-type'); await pause(300);
      const after = await cellLines();
      check(after === before + 1, `${key}+Enter: the cell shows two lines (${before} before, ${after} after)`, t);
    }

    await load('| A | B |\n| --- | --- |\n| one<br/>two | d |\n'); await pause(500);
    check(await cellLines() === 2, 'a cell loaded with <br/> shows two lines', '');
    t = await flush();
    check(t === '| A | B |\n| --- | --- |\n| one<br/>two | d |\n', 'and is written back unchanged', t);

    if (errors.length) { console.log('page errors:', errors); ok = false; }
  } finally { await close(); }
  console.log(ok ? '\nPASS' : '\nFAIL');
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error('check failed:', e.message); process.exit(1); });
