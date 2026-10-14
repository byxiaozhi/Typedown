// Lays out one document and measures every block, so a change in the stylesheet, the theme or the editor
// markup cannot move the page without somebody noticing.
//
//   node style-check.js                 # compare against style-known.json
//   node style-check.js --update        # record the current layout as the baseline
//   node style-check.js --theme dark    # same, in the dark theme (its own baseline)
//   node style-check.js --against DIR   # compare two editor builds against each other, e.g. the Uno assets
//
// A block matches when it sits within 2px of where it sat and its size is within 1px; its font, weight,
// colour and background have to be identical. Exits 1 when anything differs.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');

const args = process.argv.slice(2);
const update = args.includes('--update');
const themeAt = args.indexOf('--theme');
const theme = themeAt >= 0 ? args[themeAt + 1] : 'light';
const againstAt = args.indexOf('--against');
const against = againstAt >= 0 ? path.resolve(args[againstAt + 1]) : null;
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const baselinePath = path.join(__dirname, theme === 'light' ? 'style-known.json' : `style-known-${theme}.json`);
const markdown = fs.readFileSync(path.join(__dirname, 'style-fixture.md'), 'utf8');

const themes = {
  light: { theme: 'Light', accentColor: { r: 0, g: 120, b: 212, a: 1 }, background: { R: 249, G: 249, B: 249, A: 1 } },
  dark: { theme: 'Dark', accentColor: { r: 0, g: 120, b: 212, a: 1 }, background: { R: 32, G: 32, B: 32, A: 1 } },
};

async function measure(dir) {
  const server = http.createServer((req, res) => { let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html'; const f = path.join(dir, p); if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); } fs.createReadStream(f).pipe(res); });
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox', '--force-device-scale-factor=1', '--font-render-hinting=none'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1200, height: 900, deviceScaleFactor: 1 });
  const problems = [];
  page.on('pageerror', e => problems.push(e.message.split('\n')[0]));
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '860px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, markdown, basePath: '/tmp' };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};const deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:${JSON.stringify(themes[theme] || themes.light)},ContentLoaded:'',GetStringResources:{}};window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'){setTimeout(()=>deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 30000 });
  await page.evaluate(() => document.fonts.ready);
  await new Promise(r => setTimeout(r, 800));               // maths and highlighting settle after the load

  const blocks = await page.evaluate(() => {
    const root = document.querySelector('#ag-editor-id');
    const origin = root.getBoundingClientRect();
    const out = [];
    const walk = (el, trail) => {
      const rect = el.getBoundingClientRect();
      const cs = getComputedStyle(el);
      if (rect.width || rect.height) {
        out.push({
          at: trail,
          tag: el.tagName.toLowerCase(),
          cls: (el.className && el.className.baseVal !== undefined ? el.className.baseVal : el.className || '').toString().split(/\s+/).filter(Boolean).sort().join(' '),
          x: Math.round((rect.left - origin.left) * 2) / 2,
          y: Math.round((rect.top - origin.top) * 2) / 2,
          w: Math.round(rect.width * 2) / 2,
          h: Math.round(rect.height * 2) / 2,
          font: cs.fontFamily.split(',')[0].replace(/["']/g, ''),
          size: cs.fontSize,
          weight: cs.fontWeight,
          colour: cs.color,
          fill: cs.backgroundColor === 'rgba(0, 0, 0, 0)' ? '' : cs.backgroundColor,
        });
      }
      [...el.children].forEach((c, i) => walk(c, `${trail}/${c.tagName.toLowerCase()}[${i}]`));
    };
    [...root.children].forEach((c, i) => walk(c, `${c.tagName.toLowerCase()}[${i}]`));
    return out;
  });

  await browser.close(); server.close();
  return { blocks, problems };
}

const differences = (was, now) => {
  const out = [];
  const count = Math.max(was.length, now.length);
  for (let i = 0; i < count; i++) {
    const a = was[i]; const b = now[i];
    if (!a) { out.push(`+ ${b.at} ${b.tag}.${b.cls} appeared`); continue; }
    if (!b) { out.push(`- ${a.at} ${a.tag}.${a.cls} is gone`); continue; }
    if (a.at !== b.at || a.tag !== b.tag || a.cls !== b.cls) { out.push(`~ ${a.at} ${a.tag}.${a.cls} is now ${b.at} ${b.tag}.${b.cls}`); continue; }
    const moved = Math.abs(a.x - b.x) > 2 || Math.abs(a.y - b.y) > 2;
    const resized = Math.abs(a.w - b.w) > 1 || Math.abs(a.h - b.h) > 1;
    const restyled = ['font', 'size', 'weight', 'colour', 'fill'].filter(k => a[k] !== b[k]);
    if (moved) out.push(`  ${a.at} moved ${a.x},${a.y} -> ${b.x},${b.y}`);
    if (resized) out.push(`  ${a.at} resized ${a.w}x${a.h} -> ${b.w}x${b.h}`);
    if (restyled.length) out.push(`  ${a.at} ${restyled.map(k => `${k} ${a[k]} -> ${b[k]}`).join(', ')}`);
  }
  return out;
};

(async () => {
  const now = await measure(statics);
  if (now.problems.length) console.log('page errors:', now.problems.join(' | '));
  console.log(`${now.blocks.length} elements measured in ${statics} (${theme})`);

  if (against) {
    const other = await measure(against);
    if (other.problems.length) console.log('page errors in the other build:', other.problems.join(' | '));
    console.log(`${other.blocks.length} elements measured in ${against}`);
    const diff = differences(now.blocks, other.blocks);
    diff.slice(0, 80).forEach(l => console.log(l));
    if (diff.length > 80) console.log(`… and ${diff.length - 80} more`);
    if (diff.length) { console.log(`\n${diff.length} difference(s) between the two builds`); process.exit(1); }
    console.log('the two builds lay out identically');
    return;
  }

  if (update) { fs.writeFileSync(baselinePath, JSON.stringify(now.blocks, null, 1) + '\n'); console.log(`recorded ${now.blocks.length} elements to ${path.basename(baselinePath)}`); return; }
  if (!fs.existsSync(baselinePath)) { console.error(`no baseline yet — run with --update`); process.exit(1); }
  const diff = differences(JSON.parse(fs.readFileSync(baselinePath, 'utf8')), now.blocks);
  diff.slice(0, 80).forEach(l => console.log(l));
  if (diff.length > 80) console.log(`… and ${diff.length - 80} more`);
  if (diff.length || now.problems.length) { console.log(`\n${diff.length} difference(s) from the baseline`); process.exit(1); }
  console.log('no change from the baseline');
})().catch(e => { console.error(e); process.exit(1); });
