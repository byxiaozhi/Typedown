// ApplyDocumentEdit, the page's half of an automation write (docs/automation-api-spec.md, section 2.2): the page
// refuses when its live text is not the base the host checked, otherwise applies the text exactly and replies with
// the source hash and what the first visual edit would do to it. Applying is not an edit: no MarkdownChange.
const crypto = require('crypto');
const { openEditor } = require('./harness');

const sha = text => crypto.createHash('sha256').update(text, 'utf8').digest('hex');

(async () => {
  const { page, errors, mode, flush, load, pause, close } = await openEditor();
  let ok = true;
  const check = (c, label) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}`); ok = ok && c; };
  let op = 0;
  const apply = async (baseText, text) => {
    const operationId = `op${++op}`;
    await page.evaluate(({ operationId, baseContentHash, text }) => {
      delete window.__last.DocumentEditApplied;
      delete window.__last.MarkdownChange;
      window.__deliver('ApplyDocumentEdit', { operationId, baseContentHash, text });
    }, { operationId, baseContentHash: sha(baseText), text });
    await page.waitForFunction(id => window.__last.DocumentEditApplied?.operationId === id, { timeout: 30000 }, operationId);
    await pause(500);
    return page.evaluate(() => ({ reply: window.__last.DocumentEditApplied, change: window.__last.MarkdownChange }));
  };
  try {
    const base = '# Title\n\nplain\n';
    await mode(false, false);
    await load(base);

    let { reply, change } = await apply(base, '# Title\n\nwritten by a program\n');
    check(reply.outcome === 'applied' && reply.sourceHash === sha('# Title\n\nwritten by a program\n'), 'visual mode: applied, the reply hashes the exact text');
    check(reply.normalization.pendingNormalization === 'none' && reply.normalization.normalizedHash === reply.sourceHash, `plain text normalizes to itself (${reply.normalization.pendingNormalization})`);
    check(!change, 'applying reported no MarkdownChange');
    check(await flush() === '# Title\n\nwritten by a program\n', 'flush returns the written text');
    check(await page.evaluate(() => document.querySelector('#ag-editor-id').innerText.includes('written by a program')), 'the page shows it');

    ({ reply } = await apply('# stale base\n', 'never\n'));
    check(reply.outcome === 'conflict', `a stale base is refused (${reply.outcome})`);
    check(await flush() === '# Title\n\nwritten by a program\n', 'nothing changed on conflict');

    // The reader types, and the host has not heard of it yet: the page takes the typing in and refuses.
    const point = await page.evaluate(() => { const w = document.createTreeWalker(document.querySelector('#ag-editor-id'), NodeFilter.SHOW_TEXT); for (let n; (n = w.nextNode());) if (n.data.includes('program')) { const r = document.createRange(); r.selectNodeContents(n); const b = r.getBoundingClientRect(); return { x: b.right - 1, y: (b.top + b.bottom) / 2 }; } });
    await page.mouse.click(point.x, point.y);
    await page.keyboard.press('End');
    await page.keyboard.type('!');
    ({ reply } = await apply('# Title\n\nwritten by a program\n', 'overwrite\n'));
    check(reply.outcome === 'conflict', `typing not yet reported makes the base stale (${reply.outcome})`);
    const typed = await flush();
    check(typed === '# Title\n\nwritten by a program!\n', 'the typing survives');

    ({ reply } = await apply(typed, '|a|b|\n|-|-|\n|1|2|\n'));
    check(reply.outcome === 'applied' && reply.normalization.pendingNormalization === 'unknown', `a formatting difference is unknown (${reply.normalization.pendingNormalization})`);
    check(await flush() === '|a|b|\n|-|-|\n|1|2|\n', 'the source is kept exactly, not the normalized form');
    const report = await page.evaluate(() => { delete window.__last.NormalizationReport; window.__deliver('QueryNormalization', { token: 7 }); return window.__last.NormalizationReport; });
    check(report?.token === 7 && report.normalization.pendingNormalization === reply.normalization.pendingNormalization && report.sourceHash === reply.sourceHash,
      `QueryNormalization reports what the write reported (${report?.normalization?.pendingNormalization})`);

    ({ reply } = await apply('|a|b|\n|-|-|\n|1|2|\n', '```ts {title="x.ts"}\nconst a = 1;\n```\n'));
    check(reply.normalization.pendingNormalization === 'none', `fence attributes are kept, so the text normalizes to itself (${reply.normalization.pendingNormalization})`);
    // Muya still decodes entities in an HTML block (CommonMark example 31): text it would change is unsafe.
    ({ reply } = await apply('```ts {title="x.ts"}\nconst a = 1;\n```\n', '<a href="&ouml;&ouml;.html">\n'));
    check(reply.normalization.pendingNormalization === 'unsafe' && reply.normalization.reasons.includes('html-lost'), `an HTML entity Muya would decode is unsafe (${reply.normalization.reasons})`);

    // Visual mode: a write that changes a few blocks renders only those. The blocks around them keep their DOM, the
    // reader's cursor and what is on screen; the result is the same page a whole load of the new text draws.
    // The page without ids, and without the top-level block at skip (the one the reader clicked, drawn active).
    const shape = skip => page.evaluate(skip => {
      const root = document.querySelector('#ag-editor-id').cloneNode(true);
      if (skip >= 0) root.children[skip].remove();
      root.querySelectorAll('[id]').forEach(e => e.removeAttribute('id'));
      root.querySelectorAll('[data-key], [key]').forEach(e => { e.removeAttribute('data-key'); e.removeAttribute('key'); });
      root.querySelectorAll('.ag-active, .ag-cursor, .td-external-change').forEach(e => { e.classList.remove('ag-active'); e.classList.remove('ag-cursor'); e.classList.remove('td-external-change'); });
      return root.innerHTML.replace(/ag-[a-z-]*\d+/g, 'ag-N');
    }, skip);
    const paragraphs = Array.from({ length: 120 }, (_, i) => `Paragraph ${i} with *emphasis* and a [link](https://example.com/${i}).`);
    const visualDocs = [
      ['paragraphs', '# Visual\n\n' + paragraphs.join('\n\n') + '\n', t => t.replace('Paragraph 3 with', 'Paragraph three with')],
      ['insert a block', '# Visual\n\n' + paragraphs.join('\n\n') + '\n', t => t.replace('Paragraph 5 with', '## New heading\n\nParagraph 5 with')],
      ['delete a block', '# Visual\n\n' + paragraphs.join('\n\n') + '\n', t => t.replace('Paragraph 4 with *emphasis* and a [link](https://example.com/4).\n\n', '')],
      ['list and table', '# T\n\n- one\n- two\n\n| a | b |\n| - | - |\n| 1 | 2 |\n\n' + paragraphs.slice(0, 60).join('\n\n') + '\n', t => t.replace('- two', '- two\n- three')],
      ['code and math', '# C\n\n```js\nlet a = 1\n```\n\n$$\nx^2\n$$\n\n' + paragraphs.slice(0, 60).join('\n\n') + '\n', t => t.replace('let a = 1', 'let a = 2')],
    ];
    for (const [name, before, edit] of visualDocs) {
      await load(before);
      // The reader's cursor near the end, scrolled there.
      await page.evaluate(() => {
        const blocks = document.querySelectorAll('#ag-editor-id > *');
        const target = blocks[blocks.length - 3];
        target.scrollIntoView({ block: 'center' });
        window.__watched = target;
        target.__benchMark = true;
      });
      const last = await page.evaluate(() => { const r = window.__watched.getBoundingClientRect(); return { x: r.left + 40, y: r.top + r.height / 2 }; });
      await page.mouse.click(last.x, last.y);
      await pause(300);
      // Clicking makes the block active, which renders it again: watch the element it has now.
      await page.evaluate(() => {
        const blocks = document.querySelectorAll('#ag-editor-id > *');
        window.__watched = blocks[blocks.length - 3];
        window.__watched.__benchMark = true;
      });
      const topBefore = await page.evaluate(() => window.__watched.getBoundingClientRect().top);
      const after = edit(before);
      const shown = await flush();
      ({ reply } = await apply(shown, after));
      const state = await page.evaluate(() => ({
        how: window.__typedownLastApply,
        kept: document.contains(window.__watched) && window.__watched.__benchMark === true,
        top: window.__watched.getBoundingClientRect().top,
        caretInside: window.__watched.contains(window.getSelection().anchorNode),
      }));
      check(reply.outcome === 'applied' && await flush() === after, `visual ${name}: applied exactly`);
      check(state.how === 'local', `visual ${name}: only the changed blocks were rendered (${state.how})`);
      check(state.kept, `visual ${name}: an untouched block keeps its DOM`);
      check(Math.abs(state.top - topBefore) < 2, `visual ${name}: what the reader looks at stays in place (${topBefore} -> ${state.top})`);
      check(state.caretInside, `visual ${name}: the reader's caret stays in its block`);
      if (name === 'paragraphs') {
        const lit = await page.evaluate(() => Array.from(document.querySelectorAll('#ag-editor-id > .td-external-change')).map(e => e.textContent));
        check(lit.length === 1 && lit[0].includes('Paragraph three'), `visual: the changed block is highlighted, nothing else (${JSON.stringify(lit)})`);
        await pause(2300);
        check(await page.evaluate(() => !document.querySelector('.td-external-change')), 'visual: the highlight goes away');
      }
      const skip = await page.evaluate(() => Array.from(document.querySelector('#ag-editor-id').children).indexOf(window.__watched));
      const localShape = await shape(skip);
      await load('# other\n');
      await load(after);
      const wholeShape = await shape(skip);
      let at = 0;
      while (at < localShape.length && localShape[at] === wholeShape[at]) at++;
      check(wholeShape === localShape, `visual ${name}: the page is the one a whole load draws${wholeShape === localShape ? '' : ` (first difference at ${at}: local ${JSON.stringify(localShape.slice(Math.max(0, at - 80), at + 80))} whole ${JSON.stringify(wholeShape.slice(Math.max(0, at - 80), at + 80))})`}`);
    }

    // Cases that must fall back to the whole path: the caret in a changed block, a reference definition changed.
    {
      const before = '# R\n\nSee [the site][s].\n\n' + paragraphs.slice(0, 30).join('\n\n') + '\n\n[s]: https://a.example\n';
      await load(before);
      const after = before.replace('https://a.example', 'https://b.example');
      ({ reply } = await apply(await flush(), after));
      check(reply.outcome === 'applied' && await flush() === after, 'a changed reference definition applies exactly');
      check(await page.evaluate(() => window.__typedownLastApply) === 'whole', 'a changed reference definition renders the whole document (other blocks use it)');
      check(await page.evaluate(() => document.querySelector('#ag-editor-id').innerHTML.includes('b.example')), 'the link now points to the new target');
    }
    {
      const before = '# Caret\n\nfirst paragraph\n\nsecond paragraph\n';
      await load(before);
      const point = await page.evaluate(() => { const w = document.createTreeWalker(document.querySelector('#ag-editor-id'), NodeFilter.SHOW_TEXT); for (let n; (n = w.nextNode());) if (n.data.includes('first')) { const r = document.createRange(); r.selectNodeContents(n); const b = r.getBoundingClientRect(); return { x: b.left + 5, y: (b.top + b.bottom) / 2 }; } });
      await page.mouse.click(point.x, point.y);
      await pause(300);
      const after = before.replace('first paragraph', 'first paragraph, rewritten');
      ({ reply } = await apply(await flush(), after));
      check(reply.outcome === 'applied' && await flush() === after, 'an edit of the block holding the caret applies exactly');
      check(await page.evaluate(() => window.__typedownLastApply) === 'whole', 'an edit of the block holding the caret takes the whole path');
    }

    await mode(true, false);
    const current = await flush();
    ({ reply, change } = await apply(current, '# source mode\n'));
    check(reply.outcome === 'applied' && reply.normalization.pendingNormalization === 'unknown' && reply.normalization.reasons[0] === 'notEvaluated' && reply.normalization.normalizedHash === null,
      `source mode: applied, normalization not evaluated (${JSON.stringify(reply.normalization.reasons)})`);
    check(await page.evaluate(() => document.querySelector('.CodeMirror').CodeMirror.getValue()) === '# source mode\n', 'CodeMirror holds the exact text');
    check(!change, 'source mode: no MarkdownChange');

    // A short edit of a long document in source mode replaces only the changed word: the reader's cursor, the scroll
    // position and a mark in untouched text all stay (a whole-text setValue loses the mark and moves the cursor).
    const long = '# Long\n\n' + Array.from({ length: 400 }, (_, i) => `line ${i} of the document`).join('\n') + '\n';
    await load(long);
    await page.evaluate(() => {
      const cm = document.querySelector('.CodeMirror').CodeMirror;
      cm.focus();
      cm.setCursor({ line: 300, ch: 5 });
      cm.scrollIntoView({ line: 300, ch: 5 });
      window.__mark = cm.markText({ line: 250, ch: 0 }, { line: 250, ch: 4 }, { className: 'bench-mark' });
    });
    await pause(300);
    const placeBefore = await page.evaluate(() => ({ y: window.scrollY, cursor: document.querySelector('.CodeMirror').CodeMirror.getCursor() }));
    const edited = long.replace('line 10 of', 'line ten of');
    ({ reply } = await apply(long, edited));
    const placeAfter = await page.evaluate(() => {
      const cm = document.querySelector('.CodeMirror').CodeMirror;
      return { y: window.scrollY, cursor: cm.getCursor(), mark: window.__mark.find() ? true : false, text: cm.getValue() };
    });
    check(reply.outcome === 'applied' && placeAfter.text === edited, 'source mode: a short edit of a long document applies exactly');
    check(placeAfter.cursor.line === 300 && placeAfter.cursor.ch === 5, `the reader's cursor stays (${JSON.stringify(placeAfter.cursor)})`);
    check(Math.abs(placeAfter.y - placeBefore.y) < 2 && placeBefore.y > 0, `the scroll position stays (${placeBefore.y} -> ${placeAfter.y})`);
    check(placeAfter.mark, 'a mark in untouched text survives: only the changed part was replaced');
    const litSource = await page.evaluate(() => Array.from(document.querySelectorAll('.CodeMirror .td-external-change')).map(e => e.textContent).join(''));
    check(litSource === 'ten', `source mode: the changed text is highlighted (${JSON.stringify(litSource)})`);
    // The reader turned the highlight off: nothing is lit.
    await page.evaluate(() => window.__deliver('SettingsChanged', { highlightAutomationChanges: false }));
    await pause(300);
    const shownNow = await flush();
    ({ reply } = await apply(shownNow, shownNow.replace('line 20 of', 'line twenty of')));
    const litOff = await page.evaluate(() => Array.from(document.querySelectorAll('.CodeMirror .td-external-change')).map(e => e.textContent).join(''));
    check(reply.outcome === 'applied' && !litOff.includes('twenty'), `source mode: with the setting off the change is not highlighted (${JSON.stringify(litOff)})`);
    await page.evaluate(() => window.__deliver('SettingsChanged', { highlightAutomationChanges: true }));
    await pause(300);
    await load('# source mode\n');

    await mode(false, true);
    ({ reply } = await apply('# source mode\n', '# reading\n\ntext\n'));
    check(reply.outcome === 'applied' && reply.normalization.pendingNormalization === 'none', `reading mode accepts writes (${reply.outcome}/${reply.normalization.pendingNormalization} ${JSON.stringify(reply.normalization.reasons)})`);
    // A load arriving while an edit is being applied (a tab switch, the host restoring) supersedes it.
    const shown = await flush();
    const superseded = await page.evaluate(({ baseContentHash }) => {
      delete window.__last.DocumentEditApplied;
      window.__deliver('ApplyDocumentEdit', { operationId: 'late', baseContentHash, text: 'never shown\n' });
      window.__deliver('LoadFile', { text: '# other tab\n', loadId: 900, basePath: '/tmp' });
      return window.__last.DocumentEditApplied;
    }, { baseContentHash: sha(shown) });
    check(superseded?.operationId === 'late' && superseded.outcome === 'failed' && superseded.reason === 'superseded', `a load supersedes an edit in flight (${JSON.stringify(superseded)})`);
    await page.waitForFunction(() => window.__last.FileLoaded?.loadId === 900, { timeout: 30000 });
    check(await flush() === '# other tab\n', 'the load wins');
    check(!errors.length, `no page errors${errors.length ? `: ${errors.join(' | ')}` : ''}`);
  } finally {
    await close();
  }
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
