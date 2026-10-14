// Chemistry written with mhchem's \ce, inline and as a display block, in the editor and as the exports render it
// (RenderXhtml). The extension was loaded through its UMD file, which registers \ce on the CommonJS copy of KaTeX;
// the editor and the export render with the ES module copy, so every \ce was "Invalid Mathematical Formula" and an
// "invalid math" span in the export.
const { openEditor } = require('./harness');

const doc = 'Water: $\\ce{2H2 + O2 -> 2H2O}$ and a plain formula $x^2$.\n\n$$\n\\ce{CO2 + C -> 2CO}\n$$\n\nend\n';

(async () => {
  const { page, mode, load, flush, pause, close } = await openEditor();
  let ok = true;
  const check = (c, label) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}`); ok = ok && c; };
  try {
    await mode(false, false);
    await load(doc);
    await pause(1000);
    const editor = await page.evaluate(() => {
      const state = el => el.classList.contains('ag-math-error') ? 'failed' : el.querySelector('.katex') ? 'rendered' : 'empty';
      return {
        inline: [...document.querySelectorAll('#ag-editor-id span.ag-math-render')].map(state),
        display: [...document.querySelectorAll('#ag-editor-id figure[data-role]')].filter(f => f.dataset.role.toLowerCase() === 'multiplemath')
          .map(f => f.querySelector('.ag-container-preview')).map(p => p ? state(p) : 'no preview'),
      };
    });
    check(editor.inline.length === 2 && editor.inline.every(s => s === 'rendered'), `inline \\ce and the plain formula render in the editor (${editor.inline.join(', ')})`);
    check(editor.display.length === 1 && editor.display[0] === 'rendered', `the display \\ce block renders in the editor (${editor.display.join(', ')})`);

    await page.evaluate(() => { delete window.__last.RenderedXhtml; window.__deliver('RenderXhtml', { token: 7, diagramsAsPictures: true }); });
    await page.waitForFunction(() => window.__last.RenderedXhtml, { timeout: 30000 });
    const reply = await page.evaluate(() => window.__last.RenderedXhtml);
    const xhtml = reply.xhtml || '';
    check(!reply.error && typeof reply.xhtml === 'string', `the document renders for export${reply.error ? ` (error ${reply.error})` : ''}`);
    check(!/class="(inline-math|multiple-math) invalid"/.test(xhtml), 'no formula is left invalid in the export');
    check((xhtml.match(/class="katex"/g) || []).length === 3, `the export has the three formulas drawn by KaTeX (${(xhtml.match(/class="katex"/g) || []).length})`);
    check(/class="katex-display"/.test(xhtml), 'the display \\ce is a display formula in the export');
    check((await flush()).replace(/\r\n/g, '\n') === doc, 'the document itself is as it was');
  } finally {
    await close();
  }
  if (!ok) process.exit(1);
  console.log('all passed');
})();
