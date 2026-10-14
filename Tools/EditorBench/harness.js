// Shared by the source-text checks: the fixture set and an editor page with a stubbed WebView2 host.
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const http = require('http');
const path = require('path');

const statics = path.resolve(process.env.STATICS || path.join(__dirname, '../../Dev/Typedown/Resources/Statics'));

/** [name, markdown] pairs: hand-written cases, a sample (or all, `full`) of the CommonMark examples, and a slice
 *  (or all) of the 300k document. */
function sourceFixtures(full) {
  const fixtures = [
    ['repo-branches', '## Repo分支说明\n目前有`master`（默认），`test`2个分支。\n- `master`: 用于分阶段working的代码\n- `test`： 将本地的`local`分支代码提交到这个分支`git push origin local:test`\n## 提交和拉取说明\n```bash\n# git branch\n* test\n# git remote\norigin\n```\n提交：`git push origin test:test`, 或者`git push`\n\n拉取：`git pull `\n'],
    ['gfm-table', '| 左 | 中 | 右 |\n|:---|:---:|---:|\n| a | **b** | `c` |\n| 1 | 2 | 3 |\n'],
    ['gfm-table-ragged', '|a|b|\n|-|-|\n|1|2|\n|3|4|\n'],
    ['html-block', '<div align="center" class="note" data-x="1">\n  <img src="a.png" width="120" alt="logo">\n</div>\n\n段落\n'],
    ['html-inline', 'Press <kbd>Ctrl</kbd>+<kbd>S</kbd>, then <span style="color:red" title="t">red</span>.\n'],
    ['footnote', 'Text with a note[^1] and another[^long-id].\n\n[^1]: First.\n[^long-id]: Second, with `code`.\n'],
    ['math-inline', 'Energy $E = mc^2$ and $\\sum_{i=1}^{n} i$.\n'],
    ['math-block', '$$\n\\int_0^1 x^2 \\, dx = \\frac{1}{3}\n$$\n'],
    ['mermaid', '```mermaid\ngraph LR\n  A[开始] --> B{判断}\n  B -->|是| C[结束]\n```\n'],
    ['code-fence-lang', '```ts {title="x.ts"}\nconst a: number = 1;\n```\n\n~~~python\nprint("tilde fence")\n~~~\n'],
    ['emoji', '表情 😀 👍🏽 👨‍👩‍👧‍👦 🏳️‍🌈 end\n'],
    ['combining', 'e\u0301 a\u0308 n\u0303 — 한국어 조합 ㅎ\u1161\n'],
    ['no-final-newline', '# Heading\n\nlast line without newline'],
    ['trailing-spaces', 'hard break  \nnext line\n'],
    ['task-list', '- [ ] todo\n- [x] done\n  - [ ] nested\n'],
    ['front-matter', '---\ntitle: 标题\ntags: [a, b]\n---\n\n# Body\n'],
    ['links-images', 'See [site](https://example.com/a_b?x=1&y=2 "Title") and ![alt](./img/a%20b.png "Pic") and <https://auto.link>.\n\n[ref]: https://example.com/ref  "Ref Title"\n'],
    ['nested-lists', '1. one\n   - a\n   - b\n2. two\n\n   para in item\n\n* star\n+ plus\n'],
    ['blockquote', '> quote\n> > nested\n>\n> - list in quote\n'],
    ['setext-and-rules', 'Title\n=====\n\nSub\n---\n\n***\n___\n'],
    ['escapes', '\\*not em\\* \\_x\\_ \\# not heading \\[x\\]\n'],
    ['blank-lines', 'a\n\n\n\nb\n'],
    ['empty', ''],
  ];
  // No CRLF fixture here on purpose: the host turns every line ending into "\n" before the editor sees the text
  // (TextFileFormat.Normalize) and restores the file's own ending on save. CodeMirror would rewrite a "\r\n" it did
  // receive and report that as an edit, so keeping CR out of the editor is a host contract, tested on the host side
  // (the automation API rejects document text containing "\r"; see docs/automation-api-spec.md).

  const spec = require('./spec-data.json');
  for (const e of full ? spec : spec.filter(e => e.n % 10 === 1)) fixtures.push([`commonmark-${e.n}`, e.markdown]);
  const big = fs.readFileSync(path.join(__dirname, 'doc300k.md'), 'utf8');
  fixtures.push(['large-doc', full ? big : big.slice(0, big.indexOf('\n', 50000) + 1)]);
  return fixtures;
}

const firstDifference = (expected, actual) => {
  let i = 0;
  while (i < expected.length && i < actual.length && expected[i] === actual[i]) i++;
  const show = s => JSON.stringify(s.slice(Math.max(0, i - 15), i + 25));
  return `at ${i}: expected ${show(expected)} got ${show(actual)} (lengths ${expected.length}/${actual.length})`;
};

/** Serves the built editor and opens it in headless Chrome behind a stub host. `window.__deliver(name, args)`
 *  sends a host message; `window.__last[name]` holds the latest page message of each kind. `browserArgs`: more
 *  Chrome switches (the host's own, Config.WebView2Args, where a check depends on them). */
async function openEditor({ browserArgs = [] } = {}) {
  const server = http.createServer((req, res) => {
    const p = decodeURIComponent(req.url.split('?')[0]);
    const f = path.join(statics, p === '/' ? 'index.html' : p);
    if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
    fs.createReadStream(f).pipe(res);
  });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: true, args: ['--no-sandbox', ...browserArgs] });
  const close = async () => { await browser.close(); server.close(); };
  const page = await browser.newPage();
  const settings = { markdown: '', sourceCode: true, readOnly: false, fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, basePath: '/tmp', loadId: 1 };
  await page.evaluateOnNewDocument(settings => {
    const listeners = [], prev = {};
    window.__last = {};
    window.__deliver = (name, args) => listeners.forEach(l => l({ data: JSON.stringify({ name, args }) }));
    const replies = { GetSettings: settings, GetCurrentTheme: { theme: 'Light', accentColor: { r: 0, g: 120, b: 212, a: 1 }, background: { R: 249, G: 249, B: 249, A: 1 } }, ContentLoaded: '', GetStringResources: {} };
    window.chrome = { webview: { addEventListener: (_t, l) => listeners.push(l), postMessage: raw => {
      const m = JSON.parse(raw);
      if (m.type === 'invoke') { setTimeout(() => window.__deliver(m.id, { code: 0, data: replies[m.name] ?? null }), 0); return; }
      if (m.type === 'diffmsg') { const old = prev[m.name] || ''; prev[m.name] = m.diff ? old.slice(0, m.start) + m.args + old.slice(m.end) : m.args; window.__last[m.name] = JSON.parse(prev[m.name]); }
    } } };
  }, settings);
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  try {
    await page.goto(`http://127.0.0.1:${server.address().port}/`, { waitUntil: 'load' });
    await page.waitForFunction(() => window.__last.FileLoaded, { timeout: 30000 });
  } catch (e) { await close(); throw e; }

  const pause = (ms = 350) => new Promise(r => setTimeout(r, ms));
  /** Switches to source (sourceCode), reading (readOnly) or visual mode and waits for that editor. */
  const mode = async (sourceCode, readOnly) => {
    await page.evaluate(o => window.__deliver('SettingsChanged', o), { sourceCode, readOnly });
    await page.waitForFunction(source => source ? !!document.querySelector('.CodeMirror') : !document.querySelector('.CodeMirror') && !!document.querySelector('#ag-editor-id'), { timeout: 60000 }, sourceCode);
    await pause();
  };
  const flush = () => page.evaluate(() => {
    delete window.__last.ContentFlushed;
    window.__deliver('FlushContent', { token: 42 });
    return window.__last.ContentFlushed.text;
  });
  let loadId = 1;
  /** LoadFile as the host sends it; resolves with the text FileLoaded reported. */
  const load = async text => {
    const id = ++loadId;
    await page.evaluate(({ text, id }) => {
      delete window.__last.FileLoaded;
      delete window.__last.MarkdownChange;
      window.__deliver('LoadFile', { text, loadId: id, basePath: '/tmp' });
    }, { text, id });
    await page.waitForFunction(id => window.__last.FileLoaded?.loadId === id, { timeout: 60000 }, id);
    return page.evaluate(() => window.__last.FileLoaded.text);
  };
  return { page, errors, mode, flush, load, pause, close };
}

module.exports = { sourceFixtures, firstDifference, openEditor };
