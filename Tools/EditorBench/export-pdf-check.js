// The exported HTML has to carry what the PDF is made from: images by absolute path so they survive being
// opened from a temp file, and KaTeX fonts by real address so a formula keeps its font in the PDF. Then a
// PDF is actually produced from that HTML (headless Chrome, the same engine WebView2 uses) with a document
// outline, and the outline is checked to have an entry per heading. Store reviews: PDF loses images, PDF
// has no bookmarks, the formula font changes in the PDF.
const puppeteer = require('puppeteer-core'); const http = require('http'); const fs = require('fs'); const path = require('path'); const os = require('os');
const statics = path.resolve(process.env.STATICS || '../../Dev/Typedown/Resources/Statics');
const imgPath = path.join(os.tmpdir(), 'exp-img.png');
fs.writeFileSync(imgPath, Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQAY3Y2wAAAAAElFTkSuQmCC', 'base64'));
const markdown = `# Report\n\nInline math $E=mc^2$ and a block:\n\n$$\\int_0^1 x^2 dx$$\n\n![pic](${path.basename(imgPath)})\n\n## Second\n\nText.\n\n## Third\n\nMore.\n`;
const server = http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]); if (p === '/') p = '/index.html';
  const f = path.join(statics, p);
  if (!fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.statusCode = 404; return res.end(); }
  fs.createReadStream(f).pipe(res);
});
(async () => {
  await new Promise(r => server.listen(0, r)); const port = server.address().port;
  const browser = await puppeteer.launch({ executablePath: process.env.CHROME || '/opt/google/chrome/chrome', headless: 'new', args: ['--no-sandbox', '--allow-file-access-from-files'] });
  const page = await browser.newPage(); await page.setViewport({ width: 1000, height: 700 });
  const settings = { fontSize: 16, lineHeight: 1.6, editorAreaWidth: '900px', tabSize: 4, textDirection: 'auto', preferLooseListItem: true, listIndentation: '1', tableAlignColumns: false, readOnly: false, markdown, basePath: os.tmpdir(), loadId: 1 };
  await page.evaluateOnNewDocument(`(()=>{const ls=[];window.__marks={};window.__export=null;
    window.__deliver=(n,a)=>ls.forEach(l=>l({data:JSON.stringify({name:n,args:a})}));
    const resp={GetSettings:${JSON.stringify(settings)},GetCurrentTheme:{theme:'Light',accentColor:{r:0,g:120,b:212,a:1},background:{R:249,G:249,B:249,A:1}},ContentLoaded:'',GetStringResources:{}};
    window.chrome={webview:{addEventListener:(t,l)=>ls.push(l),dispatchEvent:(e)=>ls.forEach(l=>l(e)),postMessage:(raw)=>{const m=JSON.parse(raw);window.__marks[m.name]=1;if(m.type==='invoke'&&m.name==='ExportCallback')window.__export=m.args;
      if(m.type==='invoke'){setTimeout(()=>window.__deliver(m.id,{code:0,data:m.name in resp?resp[m.name]:null}),0);}}}}})()`);
  await page.goto(`http://127.0.0.1:${port}/index.html`, { waitUntil: 'load' });
  await page.waitForFunction(() => window.__marks.FileLoaded, { timeout: 60000 });
  await new Promise(r => setTimeout(r, 1000));
  await page.evaluate((base) => window.__deliver('Export', { type: 'html', context: 1, basePath: base, title: 'Report', options: {} }), os.tmpdir());
  await page.waitForFunction(() => window.__export, { timeout: 30000 });
  const html = await page.evaluate(() => typeof window.__export === 'string' ? JSON.parse(window.__export).html : window.__export.html);
  await page.close();

  const katexAbsolute = /url\((?:file:|https?:)[^)]*KaTeX_[^)]*\.woff2\)/.test(html) && !/url\(fonts\/KaTeX_/.test(html);
  const imgAbsolute = new RegExp(`<img[^>]+src="(?:file:///)?${imgPath.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').replace(/\\/g, '[\\\\/]')}"`).test(html) || html.includes(`src="file:///${imgPath.replace(/\\/g, '/')}"`) || html.includes(imgPath.replace(/\\/g, '/'));

  const pdfPage = await browser.newPage();
  await pdfPage.setContent(html.replace(/src="([A-Za-z]:[\\/][^"]*)"/g, (m, p) => `src="file:///${p.replace(/\\/g, '/')}"`), { waitUntil: 'networkidle0' });
  // The exact call the WinUI code makes (FileConverter.HtmlToPdfWithOutline), through CDP, not puppeteer's
  // pdf() helper: Page.printToPDF with generateDocumentOutline + generateTaggedPDF. Same engine WebView2 uses.
  const client = await pdfPage.target().createCDPSession();
  const { data } = await client.send('Page.printToPDF', { generateDocumentOutline: true, generateTaggedPDF: true, printBackground: true });
  const pdfPath = path.join(os.tmpdir(), 'exp.pdf');
  fs.writeFileSync(pdfPath, Buffer.from(data, 'base64'));
  const pdf = Buffer.from(data, 'base64').toString('latin1');
  const outlineCount = (pdf.match(/\/Type\s*\/Outlines/) ? (pdf.match(/\/Title\s*\(/g) || []).length : 0);
  const hasImage = /\/Subtype\s*\/Image/.test(pdf);

  console.log(`  export: KaTeX fonts absolute ${katexAbsolute}; image path absolute ${imgAbsolute}`);
  console.log(`  pdf: ${outlineCount} outline entr${outlineCount === 1 ? 'y' : 'ies'} (want >=3 headings), image embedded ${hasImage}`);
  const ok = katexAbsolute && imgAbsolute && outlineCount >= 3 && hasImage;
  console.log(ok ? 'OK: the export carries its images and fonts, and the PDF has an outline' : 'FAIL');
  await browser.close(); server.close();
  fs.unlinkSync(imgPath); try { fs.unlinkSync(pdfPath); } catch (e) { }
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error(e); process.exit(1); });
