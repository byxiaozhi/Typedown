// The word count is the same in the visual editor and in source mode: source mode counted every character and split
// words at spaces only, so a mode switch moved the count with nothing written, and what follows the count's changes
// as edits took the jump for writing.
const { openEditor } = require('./harness');

(async () => {
  const { page, mode, load, pause, close } = await openEditor();
  let ok = true;
  const check = (c, label) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}`); ok = ok && c; };
  // The count the page last reported (StateChange carries it a level or two down).
  const count = () => page.evaluate(() => {
    const find = o => { if (!o || typeof o !== 'object') return null; if (o.wordCount) return o.wordCount; for (const x of Object.values(o)) { const r = find(x); if (r) return r; } return null; };
    const w = find(window.__last.StateChange);
    return w ? { word: w.word, character: w.character } : null;
  });
  try {
    await mode(false, false);
    await load('# 标题\n\n这是一段中文，用来测试字数。Some English words here too.\n\n第二段内容。\n');
    await pause(1500);
    const visual = await count();
    await mode(true, false); await pause(1500);
    const source = await count();
    console.log('visual', JSON.stringify(visual), 'source', JSON.stringify(source));
    check(visual && source && visual.word === source.word && visual.character === source.character, 'source mode counts as the visual editor does');
  } finally { await close(); }
  console.log(ok ? '\nPASS' : '\nFAIL');
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error('check failed:', e.message); process.exit(1); });
