// Colours written the way MathJax (any CSS colour) and LaTeX's xcolor take them - \color{rgb(0,0,0)},
// \color[RGB]{0,0,255} - in the editor and as the exports render them (RenderXhtml). KaTeX reads names and #hex only and
// refused the whole formula ("Invalid Mathematical Formula"); the document must keep what was typed.
const { openEditor } = require('./harness');

const doc = 'Love: $\\color{Pink} {我}\\color{rgb(0,0,0)} {爱}\\color{#008000} {你}$ and $\\textcolor[RGB]{0,0,255}{x}$.\n\n$$\n\\colorbox{rgba(255,255,0,0.5)}{y}\n$$\n\nend\n';

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
        html: document.querySelector('#ag-editor-id').innerHTML,
      };
    });
    check(editor.inline.length === 2 && editor.inline.every(s => s === 'rendered'), `the inline formulas render in the editor (${editor.inline.join(', ')})`);
    check(editor.display.length === 1 && editor.display[0] === 'rendered', `the display formula renders in the editor (${editor.display.join(', ')})`);
    check(['color:#000000', 'color:#0000ff', 'background-color:#ffff0080'].every(c => editor.html.includes(c)), 'the colours are drawn as written');

    await page.evaluate(() => { delete window.__last.RenderedXhtml; window.__deliver('RenderXhtml', { token: 7, diagramsAsPictures: true }); });
    await page.waitForFunction(() => window.__last.RenderedXhtml, { timeout: 30000 });
    const reply = await page.evaluate(() => window.__last.RenderedXhtml);
    const xhtml = reply.xhtml || '';
    check(!reply.error && typeof reply.xhtml === 'string', `the document renders for export${reply.error ? ` (error ${reply.error})` : ''}`);
    check(!/class="(inline-math|multiple-math) invalid"/.test(xhtml), 'no formula is left invalid in the export');
    check(xhtml.includes('color:#000000'), 'the export draws rgb(0,0,0)');
    check((await flush()).replace(/\r\n/g, '\n') === doc, 'the document itself is as it was');
  } finally {
    await close();
  }
  if (!ok) process.exit(1);
  console.log('all passed');
})();
