// HTML pasted from web pages and online editors, as Windows hands it to the page (Paste: text and HTML; HTML may carry
// the clipboard's CF_HTML header with the page's SourceURL), through the real paste path: tidied, sanitized, turned into
// Markdown in the editor's own style. Each sample is what a site of that kind puts on the clipboard.
const { openEditor } = require('./harness');

const samples = [
  {
    name: 'GitHub README: heading, inline code, highlighted block, nested list, right-aligned column, task',
    html: `<h2 dir="auto">Install</h2><p>Run <code>npm i foo</code> then see <a href="https://example.com/docs">the docs</a>.</p><div class="highlight highlight-source-shell"><pre><span class="pl-c1">npm</span> install foo
<span class="pl-c1">foo</span> --help</pre></div><ul><li><strong>Fast</strong>: really</li><li>Small<ul><li>nested</li></ul></li></ul><table><thead><tr><th>Option</th><th align="right">Default</th></tr></thead><tbody><tr><td><code>--x</code></td><td align="right">1</td></tr></tbody></table><ul class="contains-task-list"><li class="task-list-item"><input type="checkbox" checked disabled> done</li></ul>`,
    expect: ['## Install', 'Run `npm i foo` then see [the docs](https://example.com/docs).', '```shell\nnpm install foo\nfoo --help\n```', '- **Fast**: really', '  - nested', /\|\s*-+\s*\|\s*-+:\s*\|/, '- [x] done'],
    absent: ['-------', '* Small'],
  },
  {
    name: 'CSDN-style code with line numbers beside it',
    html: `<p>示例代码如下：</p><pre class="prettyprint"><code class="prism language-python has-numbering"><span class="token keyword">def</span> hello():
    print("hi")
</code><ul class="pre-numbering"><li>1</li><li>2</li></ul></pre><p>运行结果：<strong>hi</strong></p>`,
    expect: ['示例代码如下：\n\n```python\ndef hello():\n    print("hi")\n```', '运行结果：**hi**'],
    absent: ['    def hello', '- 1'],
  },
  {
    name: 'highlight.js line-number table',
    html: `<pre><code class="hljs language-js"><table class="hljs-ln"><tbody><tr><td class="hljs-ln-numbers"><div class="hljs-ln-n" data-line-number="1"></div></td><td class="hljs-ln-code"><span class="hljs-keyword">let</span> a = 1</td></tr><tr><td class="hljs-ln-numbers"><div class="hljs-ln-n" data-line-number="2"></div></td><td class="hljs-ln-code">console.log(a)</td></tr></tbody></table></code></pre>`,
    expect: ['```js\nlet a = 1\nconsole.log(a)\n```'],
  },
  {
    name: 'Google Docs: the normal-weight wrapper, bold and italic by style',
    html: `<meta charset="utf-8"><b style="font-weight:normal;" id="docs-internal-guid-1"><p dir="ltr"><span style="font-weight:400;">Plain text and </span><span style="font-weight:700;">bold</span><span style="font-style:italic;font-weight:400;"> italic</span><span style="text-decoration:line-through;"> gone</span></p><h2 dir="ltr"><span style="font-weight:400;">A heading</span></h2><ul><li dir="ltr"><p dir="ltr"><span style="font-weight:400;">item one</span></p></li></ul></b>`,
    expect: ['Plain text and **bold** *italic* ~~gone~~', '## A heading', '- item one'],
    absent: ['\n**\n'],
  },
  {
    name: 'Zhihu-style late-loaded picture with a placeholder and <noscript>',
    html: `<p>正文。</p><figure><noscript><img src="https://pic1.zhimg.com/real.jpg" width="720"></noscript><img src="data:image/svg+xml;utf8,&lt;svg xmlns='http://www.w3.org/2000/svg' width='720' height='480'&gt;&lt;/svg&gt;" data-original="https://pic1.zhimg.com/v2-real_r.jpg" class="lazy"></figure>`,
    expect: ['![](https://pic1.zhimg.com/v2-real_r.jpg)'],
    absent: ['data:image/svg', 'real.jpg'],
  },
  {
    name: 'srcset-only late-loaded picture',
    html: `<p><img src="data:image/gif;base64,R0lGODlhAQABAAAAACw=" data-srcset="https://example.com/a-480.jpg 480w, https://example.com/a-960.jpg 960w" alt="photo"></p>`,
    expect: ['![photo](https://example.com/a-960.jpg)'],
  },
  {
    name: 'CF_HTML with SourceURL: relative link and picture made absolute, an anchor kept',
    html: "Version:0.9\r\nStartHTML:00000097\r\nEndHTML:00000400\r\nStartFragment:00000133\r\nEndFragment:00000364\r\nSourceURL:https://en.wikipedia.org/wiki/Markdown\r\n<html><body><!--StartFragment--><p>See <a href=\"/wiki/John_Gruber\">Gruber</a>, <img src=\"../static/logo.png\" alt=\"logo\"> and <a href=\"#History\">History</a>.</p><!--EndFragment--></body></html>",
    expect: ['[Gruber](https://en.wikipedia.org/wiki/John_Gruber)', '![logo](https://en.wikipedia.org/static/logo.png)', '[History](#History)'],
  },
  {
    name: 'table aligned by style',
    html: `<table><tr><th style="text-align:left">A</th><th style="text-align:center">B</th><th style="text-align:right">C</th></tr><tr><td>1</td><td>2</td><td>3</td></tr></table>`,
    expect: [/\|\s*:-+\s*\|\s*:-+:\s*\|\s*-+:\s*\|/],
  },
  {
    name: 'a heading pasted at the end of a heading line is a block of its own',
    start: '# Title\n',
    html: `<h2>Pasted</h2><p>after</p>`,
    expect: ['# Title\n\n## Pasted\n\nafter'],
    absent: ['Title##'],
  },
  {
    name: 'a page whose first block is a picture, pasted at the end of a heading line, starts below the heading',
    start: '# Title\n',
    html: `<p><img src="https://example.com/a.png" alt="a"></p><p>after</p>`,
    expect: ['# Title\n\n![a](https://example.com/a.png)\n\nafter'],
    absent: ['Title!['],
  },
  {
    name: 'two paragraphs pasted at the end of a heading line start below it',
    start: '# Title\n',
    html: `<p>first</p><p>second</p>`,
    expect: ['# Title\n\nfirst\n\nsecond'],
    absent: ['Titlefirst'],
  },
  {
    name: 'a few words pasted at the end of a heading line still join the heading',
    start: '# Title\n',
    html: `<span>More</span>`,
    expect: ['# TitleMore'],
    absent: ['# Title\n\nMore'],
  },
  // Plain text (no HTML on the clipboard: Notepad, a chat window) is read as Markdown. Indentation that every line shares
  // is not structure: a passage whose paragraphs are indented came in as an indented code block.
  {
    name: 'plain text with tab-indented paragraphs is text, not a code block',
    text: '\t第一段文字，开头缩进。\r\n\r\n\t第二段文字。\r\n',
    expect: ['第一段文字，开头缩进。\n\n第二段文字。'],
    absent: ['```', '    第一段'],
  },
  {
    name: 'plain text indented by four spaces, pasted as plain text, is text',
    type: 'pasteAsPlainText',
    text: '    The first paragraph.\n\n    The second one.',
    expect: ['The first paragraph.\n\nThe second one.'],
    absent: ['```', '    The first'],
  },
  {
    name: 'plain Markdown whose fenced code is indented inside keeps it',
    text: 'Run this:\n\n```\n    x()\n```\n',
    expect: ['```\n    x()\n```'],
  },
  {
    name: 'a javascript: address in a late-load attribute is not made a picture address',
    html: `<p>x</p><p><img src="data:image/gif;base64,R0lGODlhAQABAAAAACw=" data-original="javascript:window.__typedownXss.push('lazy')" alt="bad"></p><p><img src="missing://x" onerror="window.__typedownXss.push('onerror')" alt="ev"></p>`,
    absent: ['javascript:'],
  },
];

(async () => {
  const { page, mode, load, flush, pause, close } = await openEditor();
  let failed = 0;
  try {
    await page.evaluate(() => { window.__typedownXss = []; });
    await mode(false, false);
    for (const sample of samples) {
      await load(sample.start || '');
      await pause(300);
      if (sample.start) {
        // The cursor at the end of the document's first line, as a person puts it there.
        const box = await page.evaluate(() => { const r = document.querySelector('#ag-editor-id > *').getBoundingClientRect(); return { x: r.right - 4, y: r.top + r.height / 2 }; });
        await page.mouse.click(box.x, box.y);
        await page.keyboard.press('End');
        await pause(200);
      }
      await page.evaluate(s => window.__deliver('Paste', { type: s.type || 'normal', text: s.text || '', html: s.html || '' }), sample);
      await pause(1200);
      const md = (await flush()).replace(/\r\n/g, '\n');
      // A string is looked for as it is; a pattern where only the meaning is fixed (a table's padding is the editor's).
      const has = e => (e instanceof RegExp ? e.test(md) : md.includes(e));
      const missing = (sample.expect || []).filter(e => !has(e));
      const unwanted = (sample.absent || []).filter(e => md.includes(e));
      const ok = missing.length === 0 && unwanted.length === 0;
      if (!ok) failed++;
      console.log(`${ok ? 'PASS' : 'FAIL'} ${sample.name}`);
      if (!ok) {
        for (const m of missing) console.log(`  missing: ${m instanceof RegExp ? m : JSON.stringify(m)}`);
        for (const u of unwanted) console.log(`  unwanted: ${JSON.stringify(u)}`);
        console.log(`  got: ${JSON.stringify(md)}`);
      }
    }
    await pause(500);
    const xss = await page.evaluate(() => window.__typedownXss);
    const safe = xss.length === 0;
    if (!safe) failed++;
    console.log(`${safe ? 'PASS' : 'FAIL'} nothing in pasted HTML ran${safe ? '' : `: ${xss.join(', ')}`}`);
  } finally {
    await close();
  }
  if (failed) { console.log(`${failed} failed`); process.exit(1); }
  console.log('all passed');
})();
