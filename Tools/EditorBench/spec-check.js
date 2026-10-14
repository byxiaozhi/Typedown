// Drives every CommonMark 0.31.2 example through the editor and locks what comes back out.
//
// For each example the markdown is imported (full render, the same path a file load takes) and exported
// again; the export is imported and exported a second time. Two things are checked:
//
//   * the second export equals the first — importing what we just wrote must not keep changing the document
//   * the first export equals the recorded baseline — any change in parsing or serialising shows up as a diff
//
// Muya is not a CommonMark renderer and is not expected to become one, so the baseline is a record of what
// this editor does, not of what the spec says. Its value is that a change to the editor cannot alter the
// document model of 652 documents unnoticed.
//
//   node spec-check.js                 # check against spec-known.json
//   node spec-check.js --update        # record the current behaviour as the baseline
//   node spec-check.js --section Table # only examples whose section name contains this
//   node spec-check.js 42 190          # only these example numbers
//
// Exits 1 when anything differs from the baseline.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const baselinePath = path.join(__dirname, 'spec-known.json');
const server = http.createServer((req, res) => { let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html'; const f = path.join(statics, p); if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); } fs.createReadStream(f).pipe(res); });

const args = process.argv.slice(2);
const update = args.includes('--update');
const sectionAt = args.indexOf('--section');
const section = sectionAt >= 0 ? args[sectionAt + 1] : null;
const only = args.filter(a => /^\d+$/.test(a)).map(Number);

let examples = JSON.parse(fs.readFileSync(path.join(__dirname, 'spec-data.json'), 'utf8'));
if (section) examples = examples.filter(e => e.section.toLowerCase().includes(section.toLowerCase()));
if (only.length) examples = examples.filter(e => only.includes(e.n));

(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1200, height: 800 });
  const errors = [];
  page.on('pageerror', e => errors.push(e.message.split('\n')[0]));
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '1000px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, markdown: 'start\n', basePath: '/tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 30000 });
  await new Promise(r => setTimeout(r, 600));

  const results = {};
  const crashed = [];
  for (const e of examples) {
    const before = errors.length;
    let r;
    try {
      r = await page.evaluate((md) => {
        const muya = window.__typedownMuya;
        muya.setMarkdown(md, null, false);
        const first = muya.getMarkdown();
        muya.setMarkdown(first, null, false);
        return { first, second: muya.getMarkdown() };
      }, e.markdown);
    } catch (err) {
      crashed.push({ n: e.n, why: String(err.message).split('\n')[0] });
      continue;
    }
    const entry = { section: e.section, out: r.first };
    if (r.second !== r.first) entry.drift = r.second;              // re-importing our own output changes it
    if (errors.length > before) entry.threw = errors.slice(before)[0];
    results[e.n] = entry;
  }
  await browser.close(); server.close();

  const drifting = Object.entries(results).filter(([, v]) => v.drift);
  const throwing = Object.entries(results).filter(([, v]) => v.threw);

  if (update) {
    const ordered = {};
    for (const k of Object.keys(results).sort((a, b) => a - b)) ordered[k] = results[k];
    fs.writeFileSync(baselinePath, JSON.stringify(ordered, null, 1) + '\n');
    console.log(`recorded ${Object.keys(ordered).length} examples; ${drifting.length} do not survive a second import, ${throwing.length} threw`);
    if (crashed.length) console.log('crashed:', JSON.stringify(crashed));
    return;
  }

  if (!fs.existsSync(baselinePath)) { console.error('no baseline yet — run with --update'); process.exit(1); }
  const baseline = JSON.parse(fs.readFileSync(baselinePath, 'utf8'));
  const changes = [];
  for (const [n, now] of Object.entries(results)) {
    const was = baseline[n];
    if (!was) { changes.push({ n, kind: 'not in baseline' }); continue; }
    if (was.out !== now.out) changes.push({ n, kind: 'export changed', section: now.section, was: was.out, now: now.out });
    else if (!!was.drift !== !!now.drift) changes.push({ n, kind: now.drift ? 'became unstable' : 'became stable', section: now.section });
    else if (!!was.threw !== !!now.threw) changes.push({ n, kind: now.threw ? `started throwing: ${now.threw}` : 'stopped throwing', section: now.section });
  }

  console.log(`${Object.keys(results).length} examples run; ${drifting.length} do not survive a second import, ${throwing.length} threw`);
  for (const c of changes) {
    console.log(`\nexample ${c.n} (${c.section || ''}) — ${c.kind}`);
    if (c.was !== undefined) { console.log('  was:', JSON.stringify(c.was)); console.log('  now:', JSON.stringify(c.now)); }
  }
  if (crashed.length) { console.log('\ncrashed:', JSON.stringify(crashed)); }
  if (changes.length || crashed.length) { console.log(`\n${changes.length + crashed.length} difference(s) from the baseline`); process.exit(1); }
  console.log('no change from the baseline');
})().catch(e => { console.error(e); process.exit(1); });
