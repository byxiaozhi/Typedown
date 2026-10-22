import loadRenderer from '../components/Muya/lib/renderers'

// The diagrams of a document (mermaid, flowchart, sequence, vega-lite) as pictures, for File > Export > Word. The host sends the source
// of each diagram in the order they are in the document; every one is drawn by the same renderer the editor uses, taken as an SVG and
// painted onto a canvas (twice the size, on white), and the PNG comes back as base64 with its scale. A diagram that cannot be drawn comes back as null
// and stays a block of code in the Word file. Nothing leaves the PC (PlantUML needs a server, so it is not drawn).
const SCALE = 2
const LARGEST = 8000
const MOST_PIXELS = 16000000

export const DIAGRAM_TYPES = ['mermaid', 'flowchart', 'sequence', 'vega-lite']

// The size an SVG is drawn at: what the page gave it, or its view box.
const sizeOf = svg => {
  const box = svg.getBoundingClientRect()
  let width = box.width
  let height = box.height
  if (!width || !height) {
    const view = svg.viewBox && svg.viewBox.baseVal
    if (view && view.width && view.height) {
      width = view.width
      height = view.height
    }
  }
  return { width: Math.ceil(width), height: Math.ceil(height) }
}

const svgToPng = svg => new Promise(resolve => {
  const { width, height } = sizeOf(svg)
  if (!width || !height) {
    resolve(null)
    return
  }
  // no side over LARGEST and no more than MOST_PIXELS in all: a huge diagram is drawn smaller (the scale goes back to the host)
  const scale = Math.min(SCALE, LARGEST / width, LARGEST / height, Math.sqrt(MOST_PIXELS / (width * height)))
  const clone = svg.cloneNode(true)
  clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg')
  clone.setAttribute('xmlns:xlink', 'http://www.w3.org/1999/xlink')
  clone.setAttribute('width', String(width))
  clone.setAttribute('height', String(height))
  clone.style.maxWidth = 'none'
  const url = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(new XMLSerializer().serializeToString(clone))
  const image = new Image()
  image.onload = () => {
    try {
      const canvas = document.createElement('canvas')
      canvas.width = Math.ceil(width * scale)
      canvas.height = Math.ceil(height * scale)
      const context = canvas.getContext('2d')
      context.fillStyle = '#ffffff'
      context.fillRect(0, 0, canvas.width, canvas.height)
      context.scale(scale, scale)
      context.drawImage(image, 0, 0, width, height)
      resolve({ png: canvas.toDataURL('image/png').split(',')[1], scale })
    } catch (err) {
      resolve(null)
    }
  }
  image.onerror = () => resolve(null)
  image.src = url
})

const draw = async (type, code, host, n) => {
  if (type === 'mermaid') {
    const mermaid = await loadRenderer('mermaid')
    mermaid.initialize({ startOnLoad: false, securityLevel: 'strict', theme: 'default', flowchart: { htmlLabels: false } })
    let markup = null
    mermaid.mermaidAPI.render('caret-diagram-' + n, code, svg => { markup = svg }, host)
    if (markup) host.innerHTML = markup
  } else if (type === 'flowchart') {
    const flowchart = await loadRenderer('flowchart')
    flowchart.parse(code).drawSVG(host, {})
  } else if (type === 'sequence') {
    const sequence = await loadRenderer('sequence')
    sequence.parse(code).drawSVG(host, { theme: 'simple' })
  } else if (type === 'vega-lite') {
    const embed = await loadRenderer('vega-lite')
    await embed(host, JSON.parse(code), { actions: false, tooltip: false, renderer: 'svg', theme: 'latimes' })
  }
  return host.querySelector('svg')
}

// items: [{ type, code }]; the result has one { png (base64), scale }, or null, for each.
export const renderDiagramsToPng = async items => {
  const host = document.createElement('div')
  host.style.cssText = 'position:fixed;left:-20000px;top:0;background:#fff;'
  document.body.appendChild(host)
  const images = []
  try {
    for (let n = 0; n < items.length; n++) {
      host.innerHTML = ''
      try {
        const svg = DIAGRAM_TYPES.includes(items[n].type) ? await draw(items[n].type, items[n].code, host, n) : null
        images.push(svg ? await svgToPng(svg) : null)
      } catch (err) {
        images.push(null)
      }
    }
  } finally {
    host.remove()
  }
  return images
}
