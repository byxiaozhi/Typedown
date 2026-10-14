// Diagrams rendered as pictures (RenderXhtml with diagramsAsPictures, what a host turning the document into another
// format asks for): a big diagram still becomes a picture - drawn at twice its size it went past the browser's canvas
// limits and came out empty ("data:,") - and each picture's text says what it was: the diagram's kind and the start of
// its source, not just "diagram".
const { openEditor } = require('./harness');

const big = JSON.stringify({ $schema: 'https://vega.github.io/schema/vega-lite/v5.json', width: 9000, height: 9000, data: { values: [{ a: 1, b: 2 }, { a: 2, b: 3 }] }, mark: 'point', encoding: { x: { field: 'a', type: 'quantitative' }, y: { field: 'b', type: 'quantitative' } } });
const doc = '# T\n\n```mermaid\ngraph TD\n  A --> B\n```\n\n```vega-lite\n' + big + '\n```\n\nend\n';

(async () => {
  const { page, mode, load, pause, close } = await openEditor();
  let ok = true;
  const check = (c, label) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}`); ok = ok && c; };
  try {
    await mode(true, false);
    await load(doc);
    await pause(500);
    await page.evaluate(() => { delete window.__last.RenderedXhtml; window.__deliver('RenderXhtml', { token: 1, diagramsAsPictures: true }); });
    await page.waitForFunction(() => window.__last.RenderedXhtml, { timeout: 90000 });
    const { xhtml, error } = await page.evaluate(() => window.__last.RenderedXhtml);
    check(!error, `rendered${error ? ` (error ${error})` : ''}`);
    const pictures = [...(xhtml || '').matchAll(/<img\b[^>]*>/g)].map(m => ({
      src: (m[0].match(/src="([^"]{0,40})/) || [])[1] || '',
      alt: ((m[0].match(/alt="([^"]*)"/) || [])[1] || '').replace(/&quot;/g, '"').replace(/&gt;/g, '>'),
    }));
    console.log(pictures.map(p => `${p.src.slice(0, 22)} | ${p.alt.slice(0, 60)}`).join('\n'));
    check(pictures.length === 2, `two pictures (${pictures.length})`);
    check(pictures.every(p => p.src.startsWith('data:image/png;base64,')), 'each diagram is a picture with something in it, the 9000x9000 one too');
    check(/^Mermaid: graph TD/.test(pictures[0]?.alt || ''), 'the Mermaid picture says so, with its source');
    check(/^Vega-Lite: \{/.test(pictures[1]?.alt || '') && pictures[1].alt.length <= 250, 'the Vega-Lite picture says so, its source cut at 250 characters');
    check(!/data-diagram-source/.test(xhtml || ''), 'no marks left behind on the blocks');
  } finally { await close(); }
  console.log(ok ? '\nPASS' : '\nFAIL');
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error('check failed:', e.message); process.exit(1); });
