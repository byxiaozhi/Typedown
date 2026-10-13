// The document rendered as XHTML for another format (RenderXhtml) carries its footnotes as section.footnotes and
// a.footnote-ref, as the HTML export does: the host sends no footnote option, and the export uses the editor's own
// default (footnotes on) - [^1] stayed literal Markdown there.
const { openEditor } = require('./harness');

const doc = 'Markdown was created in 2004[^1]. Footnotes can have names instead of numbers[^named-note], and can hold code and several paragraphs[^long]. The first footnote can be referenced again[^1].\n\n' +
  '[^1]: The original description was published together with a Perl script.\n\n' +
  '[^named-note]: A named footnote with `inline code` and **bold** text.\n\n' +
  '[^long]: The first paragraph of a longer footnote.\n\n' +
  '    The second paragraph, indented by four spaces so it belongs to the same footnote.\n';

(async () => {
  const { page, mode, load, close } = await openEditor();
  let ok = true;
  const check = (condition, label) => { console.log(`${condition ? 'PASS' : 'FAIL'} ${label}`); ok = ok && condition; };
  try {
    await mode(true, false);
    await load(doc);
    await page.evaluate(() => { delete window.__last.RenderedXhtml; window.__deliver('RenderXhtml', { token: 1, diagramsAsPictures: true, mathAsTex: true }); });
    await page.waitForFunction(() => window.__last.RenderedXhtml, { timeout: 60000 });
    const result = await page.evaluate(() => {
      const { xhtml, error } = window.__last.RenderedXhtml;
      if (error) return { error };
      const body = new DOMParser().parseFromString(xhtml, 'application/xml').documentElement;
      const refs = [...body.querySelectorAll('a.footnote-ref')];
      const notes = [...body.querySelectorAll('section.footnotes li')];
      return {
        rawMarkdown: body.textContent.includes('[^'),
        refs: refs.map(ref => ref.getAttribute('href')),
        ids: notes.map(note => '#' + note.id),
        first: notes[0]?.textContent,
        namedCode: notes[1]?.querySelector('code')?.textContent,
        namedBold: notes[1]?.querySelector('strong')?.textContent,
        longParagraphs: notes[2]?.querySelectorAll('p').length,
        longText: notes[2]?.textContent
      };
    });
    check(!result.error, `rendered${result.error ? ` (${result.error})` : ''}`);
    check(!result.rawMarkdown, 'footnote Markdown is parsed');
    check(result.refs?.length === 4 && result.ids?.length === 3 && result.refs.every(ref => result.ids.includes(ref)), 'four references resolve to three notes');
    check(result.refs?.length === 4 && result.refs[0] === result.refs[3], 'repeated reference points to the same note');
    check(result.first?.includes('original description') && result.namedCode === 'inline code' && result.namedBold === 'bold', 'note text and inline formatting are kept');
    check(result.longParagraphs === 2 && result.longText?.includes('The second paragraph'), 'the indented second paragraph belongs to the long note');
  } finally { await close(); }
  console.log(ok ? '\nPASS' : '\nFAIL');
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error('check failed:', e.message); process.exit(1); });
