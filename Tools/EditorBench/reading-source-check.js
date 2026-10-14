// Regression (source-text mapping stability): viewing a document must never rewrite its source. Run against a
// freshly built editor. This covers the document from the reading-mode bug fixed in 7c02d10 and a few structural
// assertions; source-stability-check.js runs the wider fixture set. Neither proves that the first real visual
// edit serializes faithfully (first-edit-check.js).
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const http = require('http');
const path = require('path');
const assert = require('assert/strict');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const markdown = '## Repo分支说明\n目前有`master`（默认），`test`2个分支。\n- `master`: 用于分阶段working的代码\n- `test`： 将本地的`local`分支代码提交到这个分支`git push origin local:test`\n## 提交和拉取说明\n```bash\n# git branch\n* test\n# git remote\norigin\n```\n提交：`git push origin test:test`, 或者`git push`\n\n拉取：`git pull `\n';
const tableAfterParagraph = '**改进汇总**：\n| 版本 | 改进焦点 | 核心变化 |\n| --- | --- | --- |\n| v1 | 结构化重构 | 章节重组 |\n';
const server = http.createServer((req, res) => {
 const p = decodeURIComponent(req.url.split('?')[0]);
 const f = path.join(statics, p === '/' ? 'index.html' : p);
 if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
 fs.createReadStream(f).pipe(res);
});
(async () => {
 await new Promise(r => server.listen(0, '127.0.0.1', r));
 const browser = await puppeteer.launch({executablePath:process.env.CHROME || '/opt/google/chrome/chrome',headless:true,args:['--no-sandbox']});
 try {
 const page = await browser.newPage();
 const settings = {markdown, sourceCode:true, readOnly:false, fontSize:16,lineHeight:1.6,editorAreaWidth:'900px',tabSize:4,textDirection:'auto',preferLooseListItem:true,listIndentation:'1',tableAlignColumns:false,basePath:'/tmp',loadId:1};
 await page.evaluateOnNewDocument(settings => {
   const listeners = [], prev = {};
   window.__last = {};
   window.__deliver = (name,args) => listeners.forEach(l=>l({data:JSON.stringify({name,args})}));
   const replies={GetSettings:settings,GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
   window.chrome={webview:{addEventListener:(_t,l)=>listeners.push(l),postMessage:raw=>{
     const m=JSON.parse(raw);
     if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:replies[m.name]??null}),0);return;}
     if(m.type==='diffmsg'){const old=prev[m.name]||'';prev[m.name]=m.diff?old.slice(0,m.start)+m.args+old.slice(m.end):m.args;window.__last[m.name]=JSON.parse(prev[m.name]);}
   }}};
 },settings);
 const errors = [];
 page.on('pageerror', e=>errors.push(e.message));
 await page.goto(`http://127.0.0.1:${server.address().port}/`,{waitUntil:'load'});
 await page.waitForFunction(()=>window.__last.FileLoaded, {timeout:30000});
 const pause = () => new Promise(r => setTimeout(r, 350));
 const mode = async (sourceCode, readOnly) => {
   await page.evaluate(o => window.__deliver('SettingsChanged', o), { sourceCode, readOnly });
   await page.waitForFunction(source => source ? !!document.querySelector('.CodeMirror') : !document.querySelector('.CodeMirror') && !!document.querySelector('#ag-editor-id'), {}, sourceCode);
   await pause();
 };
 const flush = async () => {
   return page.evaluate(() => {
     delete window.__last.ContentFlushed;
     window.__deliver('FlushContent', { token: 42 });
     return window.__last.ContentFlushed.text;
   });
 };
 const source = () => page.evaluate(() => document.querySelector('.CodeMirror').CodeMirror.getValue());
 const fixtures = [markdown, tableAfterParagraph, markdown.replace('说明\n```', '说明\n\n```'), 'Title\n=====\n\n* a   \n* b', '', 'no final newline'];
 let loadId = 1;
 for (const text of fixtures) {
   await page.evaluate(({text, loadId}) => {
     delete window.__last.FileLoaded;
     delete window.__last.MarkdownChange;
     window.__deliver('LoadFile', {text, loadId, basePath:'/tmp'});
   }, {text, loadId: ++loadId});
   await page.waitForFunction(id => window.__last.FileLoaded?.loadId === id, {}, loadId);
   for (let round = 0; round < 2; round++) {
     await mode(false, true);
     assert.equal(await flush(), text, 'reading mode flush preserves source');
     if (text === markdown) {
       const structure = await page.evaluate(() => {
         const blocks = window.__typedownMuya.contentState.blocks;
         return { types: blocks.map(b => b.type), code: blocks.find(b => b.type === 'pre')?.children.find(b => b.type === 'code')?.children.map(b => b.text).join('\n') };
       });
       assert.deepEqual(structure.types, ['h2', 'p', 'ul', 'h2', 'pre', 'p', 'p']);
       assert.equal(structure.code, '# git branch\n* test\n# git remote\norigin');
     }
     if (text === tableAfterParagraph) {
       const structure = await page.evaluate(() => ({
         types: window.__typedownMuya.contentState.blocks.map(b => b.type),
         rows: document.querySelectorAll('#ag-editor-id table tr').length
       }));
       assert.deepEqual(structure.types, ['p', 'figure']);
       assert.equal(structure.rows, 2, 'the table after a paragraph is rendered as a table');
     }
     await mode(false, false);
     assert.equal(await flush(), text, 'entering visual editing without editing preserves source');
     await mode(true, false);
     assert.equal(await source(), text, 'returning to source preserves exact text');
     assert.equal(await page.evaluate(() => window.__last.MarkdownChange), undefined, 'no edit reported for mode switches');
   }
 }

 // A real source-mode edit must become the visual/reading document and remain the saved source afterwards.
 const sourceEdited = tableAfterParagraph.replace('v1', 'v2')
 await page.evaluate(({text, loadId}) => {
   delete window.__last.FileLoaded;
   delete window.__last.MarkdownChange;
   window.__deliver('LoadFile', {text, loadId, basePath:'/tmp'});
 }, {text: tableAfterParagraph, loadId: ++loadId});
 await page.waitForFunction(id => window.__last.FileLoaded?.loadId === id, {}, loadId);
 await page.evaluate(text => document.querySelector('.CodeMirror').CodeMirror.setValue(text), sourceEdited);
 await page.waitForFunction(text => window.__last.MarkdownChange?.text === text, {}, sourceEdited);
 await mode(false, true);
 assert.equal(await flush(), sourceEdited, 'reading mode receives an edit made in source mode');
 await mode(false, false);
 assert.equal(await flush(), sourceEdited, 'visual mode preserves an edit made in source mode');
 await mode(true, false);
 assert.equal(await source(), sourceEdited, 'source edit survives a full source/reading/visual round trip');

 // Loading directly into reading mode must also preserve source in the handshake.
 await mode(false, true);
 await page.evaluate(text => {
   delete window.__last.FileLoaded;
   window.__deliver('LoadFile', {text, loadId:100, basePath:'/tmp'});
 }, markdown);
 await page.waitForFunction(() => window.__last.FileLoaded?.loadId === 100);
 assert.equal(await page.evaluate(() => window.__last.FileLoaded.text), markdown);
 assert.equal(await flush(), markdown);

 // A throttled edit must reach the host before the visual editor is unmounted.
 await mode(false, false);
 const large = 'start\n\n' + 'long paragraph '.repeat(5000);
 await page.evaluate(text => {
   delete window.__last.FileLoaded;
   window.__deliver('LoadFile', {text, loadId:101, basePath:'/tmp'});
 }, large);
 await page.waitForFunction(() => window.__last.FileLoaded?.loadId === 101);
 await page.evaluate(() => {
   const muya = window.__typedownMuya;
   muya.contentState.blocks[0].children[0].text = 'edited before switch';
   muya.lastContentChangeAt = Date.now();
   muya.dispatchChange();
   window.__deliver('SettingsChanged', {sourceCode:true});
 });
 await page.waitForSelector('.CodeMirror');
 assert.ok((await source()).startsWith('edited before switch'));
 assert.equal(await page.evaluate(() => window.__last.MarkdownChange.loadId), 101);
 assert.deepEqual(errors, []);
 console.log('PASS (source mapping stability): original source, source edits, reading structure, repeated mode switches, direct reading loads and pending edits');
 } finally { await browser.close(); server.close(); }
})().catch(e=>{console.error(e);server.close();process.exitCode=1;});
