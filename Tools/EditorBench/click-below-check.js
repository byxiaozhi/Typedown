// A click below the last block of the document. After a line of text the caret goes to its end and the document does
// not change - it used to get an empty paragraph, and a click alone marked it unsaved. After a table (a block the
// caret cannot sit at the end of) an empty paragraph is still created, so there is somewhere to write.
//
//   node click-below-check.js
const { openEditor } = require('./harness');

(async () => {
  const failures = [];
  const check = (ok, label) => { console.log(`${ok ? 'PASS' : 'FAIL'} ${label}`); if (!ok) failures.push(label); };
  const clickBelow = async page => {
    const y = await page.evaluate(() => {
      const blocks = document.querySelectorAll('#ag-editor-id > [id^="ag-"]');
      const last = blocks[blocks.length - 1].getBoundingClientRect();
      return last.bottom + 40;
    });
    await page.mouse.click(300, y);
  };
  for (const [label, text, expectSame, typed] of [
    ['after a paragraph', '# Title\n\nSome text\n', true, '# Title\n\nSome textX\n'],
    ['after a heading', '# Title\n\n## Last heading\n', true, '# Title\n\n## Last headingX\n'],
    ['after a table', '# Title\n\n| a | b |\n| - | - |\n| 1 | 2 |\n', false, null],
  ]) {
    const { page, errors, mode, load, flush, pause, close } = await openEditor();
    try {
      await mode(false, false);
      await load(text);
      await page.evaluate(() => { delete window.__last.MarkdownChange; });
      await clickBelow(page);
      await pause(400);
      const after = await page.evaluate(() => window.__typedownMuya.getMarkdown());
      const reported = await page.evaluate(() => !!window.__last.MarkdownChange);
      if (expectSame) {
        check(after === text && !reported, `${label}: the click leaves the document as it was (${JSON.stringify(after)}${reported ? ', a change was reported' : ''})`);
        await page.keyboard.type('X');
        await pause(300);
        await flush();
        const now = await page.evaluate(() => window.__typedownMuya.getMarkdown());
        check(now === typed, `${label}: a letter typed then goes to the end of the last line (${JSON.stringify(now)})`);
      } else {
        await page.keyboard.type('X');
        await pause(300);
        const now = await page.evaluate(() => window.__typedownMuya.getMarkdown());
        check(now.endsWith('\n\nX\n'), `${label}: a new paragraph to write in (${JSON.stringify(now)})`);
      }
      if (errors.length) check(false, `${label}: page errors ${errors.join(' | ')}`);
    } finally {
      await close();
    }
  }
  console.log(failures.length ? `${failures.length} failed` : 'all passed');
  process.exit(failures.length ? 1 : 0);
})();
