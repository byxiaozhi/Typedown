// Copies Mermaid's self-contained UMD build from node_modules into public/ so it
// can be loaded as a global <script> (see public/index.html) instead of being
// bundled by webpack. Mermaid v11's webpack-bundled build fails to evaluate over
// the file:// protocol the WebView2 app runs on, so the UMD global is the only
// reliable load path. Run automatically via the prestart/prebuild npm hooks; the
// copied file is gitignored, keeping the library out of source control.
const fs = require('fs')
const path = require('path')

const src = require.resolve('mermaid/dist/mermaid.min.js')
const dest = path.join(__dirname, '..', 'public', 'mermaid.min.js')
fs.copyFileSync(src, dest)
console.log(`Copied ${src} -> ${dest}`)
