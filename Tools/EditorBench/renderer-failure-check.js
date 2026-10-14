// What the page does when a part of it fails: a renderer that cannot be loaded (Mermaid's script refused), and an error
// nothing catches. Mermaid not loading stopped every export of a document with a diagram, and the library was never
// asked for again; an error in a listener or a promise went only to the console.
const { openEditor } = require('./harness');

const doc = '# T\n\n```mermaid\ngraph TD\n  A --> B\n```\n\nend\n';

(async () => {
  const { page, mode, load, pause, close } = await openEditor();
  let ok = true;
  const check = (c, label) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}`); ok = ok && c; };
  const render = async token => {
    await page.evaluate(token => { delete window.__last.RenderedXhtml; window.__deliver('RenderXhtml', { token, diagramsAsPictures: true }); }, token);
    await page.waitForFunction(() => window.__last.RenderedXhtml, { timeout: 60000 });
    return page.evaluate(() => window.__last.RenderedXhtml);
  };
  try {
    let refuse = true;
    await page.setRequestInterception(true);
    page.on('request', request => {
      if (refuse && request.url().includes('mermaid.min.js')) request.abort();
      else request.continue();
    });
    await mode(true, false); // source mode: the live preview does not ask for Mermaid first
    await load(doc);
    await pause(500);

    const refused = await render(1);
    check(!refused.error && /language-mermaid[^>]*>[\s\S]*A --&gt; B/.test(refused.xhtml || ''), `Mermaid not loading: the document still renders, the diagram as its source${refused.error ? ` (error ${refused.error})` : ''}`);

    refuse = false;
    const loaded = await render(2);
    check(!loaded.error && /<img[^>]+src="data:image\/png/.test(loaded.xhtml || ''), 'Mermaid asked for again once it can load: the diagram is drawn');

    // From a script of the page's own (a rejection made by the test's evaluate is the debugger's, not the page's).
    const inPage = code => page.evaluate(code => { const s = document.createElement('script'); s.textContent = code; document.head.appendChild(s); }, code);
    await page.evaluate(() => { delete window.__last.PageError; });
    await inPage("Promise.reject(new Error('nobody caught this'))");
    await pause(500);
    const reported = await page.evaluate(() => window.__last.PageError);
    check(reported && /nobody caught this/.test(reported.message), `an error nothing catches is told to the host (${reported ? reported.message : 'nothing'})`);
    await page.evaluate(() => { delete window.__last.PageError; });
    await inPage("Promise.reject({ str: 'Parse error on line 2' })");
    await pause(500);
    const objectError = await page.evaluate(() => window.__last.PageError);
    check(objectError && objectError.message === 'Parse error on line 2', `an object thrown is told readably (${objectError ? objectError.message : 'nothing'})`);
  } finally {
    await close();
  }
  if (!ok) process.exit(1);
  console.log('all passed');
})();
