const rendererCache = new Map()

// Wait for a global injected by a <script> tag to become available.
const waitForGlobal = (prop, timeout = 15000) => new Promise((resolve, reject) => {
  if (window[prop]) {
    resolve(window[prop])
    return
  }
  const start = Date.now()
  const timer = setInterval(() => {
    if (window[prop]) {
      clearInterval(timer)
      resolve(window[prop])
    } else if (Date.now() - start > timeout) {
      clearInterval(timer)
      reject(new Error(`Global "${prop}" was not loaded`))
    }
  }, 50)
})

/**
 *
 * @param {string} name the renderer name: katex, sequence, plantuml, flowchart, mermaid, vega-lite
 */
const loadRenderer = async (name) => {
  if (!rendererCache.has(name)) {
    let m
    switch (name) {
      case 'sequence':
        m = await import('../parser/render/sequence')
        rendererCache.set(name, m.default)
        break
      case 'plantuml':
        m = await import('../parser/render/plantuml')
        rendererCache.set(name, m.default)
        break
      case 'flowchart':
        m = await import('flowchart.js')
        rendererCache.set(name, m.default)
        break
      case 'mermaid':
        // Mermaid v11 is loaded as a global UMD script (see public/index.html)
        // because bundling its UMD build through webpack fails to evaluate over file://.
        rendererCache.set(name, await waitForGlobal('mermaid'))
        break
      case 'vega-lite':
        m = await import('vega-embed')
        rendererCache.set(name, m.default)
        break
      default:
        throw new Error(`Unknown diagram name ${name}`)
    }
  }

  return rendererCache.get(name)
}

export default loadRenderer
