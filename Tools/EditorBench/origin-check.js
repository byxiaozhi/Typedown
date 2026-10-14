// Change origin: every MarkdownChange says whether the reader caused it ("user") or the editor changed the text on
// its own ("editor"; services/changeOrigin.ts). Internal re-renders and normalization must never pass as edits.
const { openEditor } = require('./harness');

(async () => {
  const { page, errors, mode, flush, load, pause, close } = await openEditor();
  let ok = true;
  const check = (c, label) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}`); ok = ok && c; };
  const clear = () => page.evaluate(() => { delete window.__last.MarkdownChange; });
  const change = async () => { await flush(); await pause(200); return page.evaluate(() => window.__last.MarkdownChange); };
  const findBlock = anchor => page.evaluate(anchor => {
    const find = bs => { for (const b of bs) { if (typeof b.text === 'string' && b.text.includes(anchor)) return b.key; const r = find(b.children || []); if (r) return r; } };
    return find(window.__typedownMuya.contentState.blocks);
  }, anchor);
  try {
    const doc = '# Title\n\n* star list\n\n```mermaid\ngraph LR\n  A --> B\n```\n\n$$\nx^2\n$$\n\nplain paragraph\n';
    await mode(true, false);
    await load(doc);
    for (const [s, r] of [[false, true], [false, false], [true, false], [false, false]]) await mode(s, r);
    await pause(1500);
    let c = await change();
    check(!c, `loads, mode switches and late renders report no change${c ? ` (got origin ${c.origin})` : ''}`);

    // The editor changes the text with nothing done by the reader.
    await clear();
    const key = await findBlock('plain paragraph');
    await page.evaluate(key => {
      const state = window.__typedownMuya.contentState;
      const find = bs => { for (const b of bs) { if (b.key === key) return b; const r = find(b.children || []); if (r) return r; } };
      find(state.blocks).text = 'plain paragraph rewritten';
      state.render();
      window.__typedownMuya.dispatchChange();
    }, key);
    c = await change();
    check(c?.origin === 'editor' && c.text.includes('rewritten'), `a change nobody made reports origin "editor" (got ${c?.origin})`);

    // A real click and keystroke in visual mode.
    await clear();
    const point = await page.evaluate(() => {
      const w = document.createTreeWalker(document.querySelector('#ag-editor-id'), NodeFilter.SHOW_TEXT);
      for (let n; (n = w.nextNode());) if (n.data.includes('rewritten')) { const r = document.createRange(); r.selectNodeContents(n); const b = r.getBoundingClientRect(); return { x: b.right - 1, y: (b.top + b.bottom) / 2 }; }
    });
    await page.mouse.click(point.x, point.y);
    await page.keyboard.press('End');
    await page.keyboard.type('Z');
    c = await change();
    check(c?.origin === 'user' && c.text.includes('rewrittenZ'), `typing in visual mode reports origin "user" (got ${c?.origin})`);

    // The action was consumed: the next unprompted change is the editor's again.
    await clear();
    await page.evaluate(() => { const s = window.__typedownMuya.contentState; s.blocks[0].children[0].text = '# Title changed'; s.render(); window.__typedownMuya.dispatchChange(); });
    c = await change();
    check(c?.origin === 'editor', `an action counts for one report only (got ${c?.origin})`);

    // An editing command from the host menus.
    await clear();
    await page.evaluate(() => window.__deliver('InsertParagraph', 'after'));
    c = await change();
    check(c?.origin === 'user', `a host editing command reports origin "user" (got ${c?.origin})`);

    // Source mode: a keystroke in CodeMirror.
    await mode(true, false);
    await clear();
    await page.click('.CodeMirror');
    await page.keyboard.press('End');
    await page.keyboard.type('W');
    c = await change();
    check(c?.origin === 'user', `typing in source mode reports origin "user" (got ${c?.origin})`);

    // After a load the armed action is gone.
    await page.keyboard.press('Shift');
    await load('# fresh\n');
    await clear();
    await page.evaluate(() => { const cm = document.querySelector('.CodeMirror').CodeMirror; cm.setValue('# fresh changed by the editor\n'); });
    c = await change();
    check(c?.origin === 'editor', `a load forgets an earlier key press (got ${c?.origin})`);
    check(!errors.length, `no page errors${errors.length ? `: ${errors.join(' | ')}` : ''}`);
  } finally {
    await close();
  }
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
