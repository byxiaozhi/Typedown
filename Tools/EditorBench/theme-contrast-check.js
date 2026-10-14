// Inline code, table borders and dividers have to be visible against the page in every bundled theme.
// Two Store reviews: in the light theme inline code had a background indistinguishable from the page; in
// the dark theme table borders and dividers were as good as invisible. The colours are composited the way
// the browser paints them (alpha over the editor background) and compared as WCAG contrast ratios.
//
//   node theme-contrast-check.js
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const themes = ['light', 'dark', 'black'].map(name => ({ name, css: fs.readFileSync(path.join(statics, 'theme/editor', `${name}.theme.css`), 'utf8') }));
const markdown = '# Title\n\nSome `inline code` in a line.\n\n---\n\n| a | b |\n|---|---|\n| 1 | 2 |\n';
const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});
const MIN = { code: 1.15, border: 1.4, hr: 1.4 }; // ratios: 1.0 is invisible; GitHub's light inline code is about 1.17
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox'] });
  let ok = true;
  for (const theme of themes) {
    const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
    const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: true, markdown, basePath: '/tmp', loadId: 1, themeCss: theme.css };
    await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};
      window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
      const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'${theme.name === 'light' ? 'Light' : 'Dark'}',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
      window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;
        if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
    await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
    await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
    await new Promise(r => setTimeout(r, 800));
    const r = await page.evaluate(() => {
      const parse = (s) => { const m = s.match(/rgba?\(([^)]+)\)/); if (!m) return null; const p = m[1].split(',').map(Number); return { r: p[0], g: p[1], b: p[2], a: p.length > 3 ? p[3] : 1 }; };
      const over = (c, bg) => ({ r: c.r * c.a + bg.r * (1 - c.a), g: c.g * c.a + bg.g * (1 - c.a), b: c.b * c.a + bg.b * (1 - c.a), a: 1 });
      const lum = (c) => { const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }; return 0.2126 * f(c.r) + 0.7152 * f(c.g) + 0.0722 * f(c.b); };
      const ratio = (a, b) => { const la = lum(a), lb = lum(b); return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05); };
      const root = document.getElementById('ag-editor-id');
      const bg = parse(getComputedStyle(document.body).backgroundColor);
      const pageBg = bg && bg.a > 0 ? bg : { r: 255, g: 255, b: 255, a: 1 };
      const code = root.querySelector('code'); const codeBg = code ? parse(getComputedStyle(code).backgroundColor) : null;
      const hr = root.querySelector('p[data-role="hr"]'); const hrBg = hr ? parse(getComputedStyle(hr, '::before').borderTopColor) : null;
      const cell = root.querySelector('td'); const cellBorder = cell ? parse(getComputedStyle(cell, '::before').borderTopColor) : null;
      const fmt = (c) => c ? `rgba(${Math.round(c.r)},${Math.round(c.g)},${Math.round(c.b)},${c.a})` : 'none';
      return {
        page: fmt(pageBg),
        code: codeBg ? { ratio: ratio(over(codeBg, pageBg), pageBg), color: fmt(codeBg) } : null,
        hr: hrBg ? { ratio: ratio(over(hrBg, pageBg), pageBg), color: fmt(hrBg) } : null,
        border: cellBorder ? { ratio: ratio(over(cellBorder, pageBg), pageBg), color: fmt(cellBorder) } : null,
      };
    });
    const line = (k, v) => `${k} ${v ? `${v.ratio.toFixed(2)} (${v.color})` : 'missing'}${!v || v.ratio < MIN[k] ? '  <- too faint' : ''}`;
    const good = r.code && r.hr && r.border && r.code.ratio >= MIN.code && r.hr.ratio >= MIN.hr && r.border.ratio >= MIN.border;
    ok = ok && good;
    console.log(`  ${theme.name.padEnd(5)} page ${r.page}: ${line('code', r.code)}; ${line('hr', r.hr)}; ${line('border', r.border)}`);
    await page.close();
  }
  console.log(ok ? 'OK: inline code, dividers and table borders are visible in every theme' : 'FAIL: something is too faint to see');
  await browser.close(); server.close(); process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
