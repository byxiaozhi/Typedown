// A style setting that redraws the document (font size, line height, text direction) must leave the keyboard where
// it was. The redraw used to blur the editor: the caret stayed on screen and the keys went nowhere until a click
// (E2E F01 after a settings change; a setting changed from another window or through the automation API). It must
// not scroll to the caret either, and an editor that had no focus must not take it.
//
//   node style-focus-check.js
const { openEditor } = require('./harness');

const changes = [{ fontSize: 17 }, { lineHeight: 1.8 }, { textDirection: 'rtl' }, { tabSize: 2 }, { focusMode: true }];
const long = '# Doc\n\n' + Array.from({ length: 200 }, (_, i) => `Paragraph ${i} with some words in it.`).join('\n\n') + '\n';

(async () => {
  const failures = [];
  const check = (ok, label) => { console.log(`${ok ? 'PASS' : 'FAIL'} ${label}`); if (!ok) failures.push(label); };
  const active = page => page.evaluate(() => document.activeElement.id || document.activeElement.tagName);
  const hasQ = page => page.evaluate(() => window.__typedownMuya.getMarkdown().includes('Q'));

  for (const change of changes) {
    const { page, errors, mode, load, pause, close } = await openEditor();
    try {
      await mode(false, false);
      await load('# Doc\n\nbody text\n');
      await page.focus('#ag-editor-id');
      await pause(200);
      await page.evaluate(o => window.__deliver('SettingsChanged', o), change);
      await pause(400);
      const after = await active(page);
      await page.keyboard.type('Q');
      await pause(300);
      check(after === 'editor' && await hasQ(page) && !errors.length, `${JSON.stringify(change)}: the editor keeps the keyboard (${after})`);
    } finally {
      await close();
    }
  }

  // Scrolled away from the caret: the redraw keeps the reader's place instead of jumping to the caret.
  {
    const { page, mode, load, pause, close } = await openEditor();
    try {
      await mode(false, false);
      await load(long);
      await page.focus('#ag-editor-id');
      await pause(200);
      await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight * 0.6));
      await pause(300);
      const before = await page.evaluate(() => window.scrollY);
      await page.evaluate(() => window.__deliver('SettingsChanged', { fontSize: 17 }));
      await pause(500);
      const after = await page.evaluate(() => window.scrollY);
      check(before > 1000 && after > before * 0.8, `a font change far from the caret keeps the scroll position (${Math.round(before)} -> ${Math.round(after)})`);
    } finally {
      await close();
    }
  }

  // An editor without focus stays without it: the redraw must not take the keyboard from elsewhere.
  {
    const { page, mode, load, pause, close } = await openEditor();
    try {
      await mode(false, false);
      await load('# Doc\n\nbody text\n');
      await page.evaluate(() => { const i = document.createElement('input'); i.id = 'elsewhere'; document.body.appendChild(i); i.focus(); });
      await page.evaluate(() => window.__deliver('SettingsChanged', { fontSize: 17 }));
      await pause(400);
      check(await active(page) === 'elsewhere', 'a font change does not take the keyboard from another field');
    } finally {
      await close();
    }
  }

  console.log(failures.length ? `${failures.length} failed` : 'all passed');
  process.exit(failures.length ? 1 : 0);
})();
