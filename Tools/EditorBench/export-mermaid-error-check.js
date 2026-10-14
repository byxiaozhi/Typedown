// A document with a Mermaid diagram whose text Mermaid cannot read, rendered as the exports render it (RenderXhtml, the
// path of the PDF, HTML and other exports). The broken diagram threw an object, not an Error: the render stopped - the
// export failed, saying "[object Object]" - and its container stayed at the bottom of the page, the rendered document
// showing under the editor in every mode until the page was made again.
const { openEditor } = require('./harness');

const doc = '# T\n\ngood:\n\n```mermaid\ngraph TD\n  A --> B\n```\n\nbad:\n\n```mermaid\ngraph TD\n  A --> \n  ((( nonsense\n```\n\nend\n';

(async () => {
  const { page, mode, load, flush, pause, close } = await openEditor();
  let ok = true;
  const check = (c, label) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}`); ok = ok && c; };
  try {
    await mode(false, false);
    await load(doc);
    await pause(1500);
    await page.evaluate(() => { delete window.__last.RenderedXhtml; window.__deliver('RenderXhtml', { token: 7, diagramsAsPictures: true }); });
    await page.waitForFunction(() => window.__last.RenderedXhtml, { timeout: 30000 });
    const reply = await page.evaluate(() => window.__last.RenderedXhtml);
    check(!reply.error && typeof reply.xhtml === 'string', `the document renders with a diagram Mermaid cannot read${reply.error ? ` (error ${reply.error})` : ''}`);
    const xhtml = reply.xhtml || '';
    check(/<img[^>]+src="data:image\/png/.test(xhtml), 'the readable diagram is drawn (a picture)');
    check(/language-mermaid[^>]*>[\s\S]*nonsense/.test(xhtml), 'the unreadable one is kept as its source, in a code block');
    const left = await page.evaluate(() => document.querySelectorAll('.ag-render-container, body > [id^="dmermaid"]').length);
    check(left === 0, `nothing of the render is left on the page (${left} left)`);
    check((await flush()).replace(/\r\n/g, '\n') === doc, 'the document itself is as it was');
  } finally {
    await close();
  }
  if (!ok) process.exit(1);
  console.log('all passed');
})();
