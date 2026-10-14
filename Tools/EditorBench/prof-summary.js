// Aggregate self time by (function, source location) from a .cpuprofile, resolving through the source map.
const fs = require('fs'); const path = require('path'); const { SourceMapConsumer } = require('source-map');
(async () => {
  const prof = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
  const mapFile = process.argv[3]; const consumer = mapFile ? await new SourceMapConsumer(JSON.parse(fs.readFileSync(mapFile, 'utf8'))) : null;
  const byId = new Map(prof.nodes.map(n => [n.id, n]));
  const self = new Map(); const total = prof.samples.length;
  const dt = prof.timeDeltas; let sumUs = 0; for (const d of dt) sumUs += d;
  const counts = new Map(); for (const s of prof.samples) counts.set(s, (counts.get(s) || 0) + 1);
  const parent = new Map(); for (const n of prof.nodes) for (const c of (n.children || [])) parent.set(c, n.id);
  const name = (n) => {
    const cf = n.callFrame; let fn = cf.functionName || '(anon)'; let loc = '';
    if (consumer && cf.url && cf.url.includes('main.') && cf.lineNumber >= 0) {
      const o = consumer.originalPositionFor({ line: cf.lineNumber + 1, column: cf.columnNumber });
      if (o.source) { loc = `${o.source.replace(/^.*\/src\//, 'src/')}:${o.line}`; if (o.name) fn = o.name; }
    } else if (cf.url) loc = path.basename(cf.url) + ':' + cf.lineNumber;
    else loc = cf.url || '';
    return `${fn}  ${loc}`;
  };
  const selfBy = new Map();
  for (const [id, c] of counts) { const n = byId.get(id); const k = name(n); selfBy.set(k, (selfBy.get(k) || 0) + c); }
  // inclusive time: walk up
  const inclBy = new Map();
  for (const [id, c] of counts) { const seen = new Set(); let cur = id; while (cur != null) { const k = name(byId.get(cur)); if (!seen.has(k)) { inclBy.set(k, (inclBy.get(k) || 0) + c); seen.add(k); } cur = parent.get(cur); } }
  const msPer = (sumUs / 1000) / total;
  console.log(`samples ${total}, ${(sumUs / 1000).toFixed(0)} ms total`);
  console.log('--- top self time ---');
  [...selfBy].sort((a, b) => b[1] - a[1]).slice(0, 25).forEach(([k, c]) => console.log(`${(c * msPer).toFixed(0).padStart(7)} ms ${(100 * c / total).toFixed(1).padStart(5)}%  ${k}`));
  console.log('--- top inclusive (src/ only) ---');
  [...inclBy].filter(([k]) => k.includes('src/')).sort((a, b) => b[1] - a[1]).slice(0, 40).forEach(([k, c]) => console.log(`${(c * msPer).toFixed(0).padStart(7)} ms ${(100 * c / total).toFixed(1).padStart(5)}%  ${k}`));
})();
