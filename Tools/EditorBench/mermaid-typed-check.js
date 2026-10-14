// A diagram written by hand in visual mode: typing ```mermaid and Enter must give the diagram block with its preview
// (not a plain code block that only became a diagram after a tab or mode switch reloaded the document), and the caret
// must be able to leave it - by clicking the paragraph below, which the floating preview used to cover, by clicking
// above, or with the arrow keys - after which the new diagram is shown.
const { openEditor } = require('./harness');

(async () => {
  const { page, errors, mode, load, pause, close } = await openEditor();
  let ok = true;
  const check = (c, label) => { console.log(`${c ? 'PASS' : 'FAIL'} ${label}`); ok = ok && c; };
  const clickText = async (text) => {
    const p = await page.evaluate(t => {
      const w = document.createTreeWalker(document.querySelector('#ag-editor-id'), NodeFilter.SHOW_TEXT);
      for (let n; (n = w.nextNode());) if (n.data.includes(t)) { const r = document.createRange(); r.selectNodeContents(n); const b = r.getBoundingClientRect(); return { x: b.right - 1, y: (b.top + b.bottom) / 2 }; }
    }, text);
    await page.mouse.click(p.x, p.y);
    await pause(250);
  };
  const figure = () => page.evaluate(() => {
    const f = document.querySelector('figure.ag-container-block');
    const p = f?.querySelector('.ag-container-preview');
    return f ? { active: f.classList.contains('ag-active'), svg: !!p?.querySelector('svg'), text: p?.textContent || '' } : null;
  });
  const write = async () => {
    await mode(true, false);
    await load('before\n\nplaceholder\n\nafter\n');
    await mode(false, false);
    await clickText('placeholder');
    for (let i = 0; i < 11; i++) await page.keyboard.press('Backspace');
    await page.keyboard.type('```mermaid');
    await page.keyboard.press('Enter');
    await pause(300);
    await page.keyboard.type('graph LR');
    await page.keyboard.press('Enter');
    await page.keyboard.type('A[Typed] --> B[Diagram]');
    await pause(800);
  };
  try {
    for (const leave of ['below', 'above', 'arrows']) {
      await write();
      const typed = await figure();
      check(typed?.active, `${leave}: typing \`\`\`mermaid made a diagram block being edited`);
      if (leave === 'below') await clickText('after');
      else if (leave === 'above') await clickText('before');
      else for (let i = 0; i < 4; i++) { await page.keyboard.press('ArrowDown'); await pause(80); }
      await pause(1500);
      const left = await figure();
      check(left && !left.active && left.svg && left.text.includes('Typed'), `${leave}: after leaving, the new diagram is shown (${JSON.stringify(left && { active: left.active, svg: left.svg })})`);
    }
    check(!errors.length, `no page errors${errors.length ? `: ${errors.join(' | ')}` : ''}`);
  } finally {
    await close();
  }
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
