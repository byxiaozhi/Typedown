// Local image paths: resolving one against the document folder, and the file:/// address a copy gives it.
import path from 'path-browserify'

/**
 * Resolve a relative image path against the document folder. `path-browserify` is POSIX-only, so a Windows base
 * path such as `C:\notes\sub` was treated as a single segment and `../img/a.png` resolved to `/img/a.png`
 * (upstream #17). Handle drive-letter and UNC prefixes explicitly and resolve the rest as POSIX segments.
 */
export const resolveLocalPath = (basePath, src) => {
  const toPosix = p => p.replace(/\\/g, '/')
  let base = toPosix(basePath)
  let prefix = ''
  const drive = base.match(/^([a-zA-Z]:)(\/.*)?$/)
  const unc = base.match(/^(\/\/[^/]+\/[^/]+)(\/.*)?$/)
  if (drive) {
    prefix = drive[1]
    base = drive[2] || '/'
  } else if (unc) {
    prefix = unc[1]
    base = unc[2] || '/'
  }
  return prefix + path.resolve(base, toPosix(src))
}

/**
 * An absolute local path as a file address for Word: "C:\\notes\\流程 图.png" is file:///C:/notes/流程 图.png. The
 * characters stay as they are: Word does not decode %E6%B5%81... back into the name, and showed an empty frame for
 * every picture with Chinese in its path (a space as such works too). Only # and ? are escaped, which would end the
 * path. A UNC path is file://server/...
 */
const toFileUrl = absolute => {
  const posix = absolute.replace(/\\/g, '/').replace(/#/g, '%23').replace(/\?/g, '%3F')
  if (posix.startsWith('//')) return 'file:' + posix
  return 'file://' + (posix.startsWith('/') ? '' : '/') + posix
}

/**
 * The HTML a copy puts on the clipboard, with its local images at absolute file:/// addresses. The HTML is made from
 * the Markdown, where an image is relative to the document ("images/a.png"): Word and mail clients pasting it have no
 * document folder to resolve that against, and showed an empty frame. Web, data and file URLs stay as they are; so do
 * relative ones while the document has no folder (not saved yet).
 */
export const absoluteImageUrls = (html, basePath = window.basePath) => {
  if (!html || !/<img/i.test(html)) return html
  const wrapper = document.createElement('div')
  wrapper.innerHTML = html
  for (const img of Array.from(wrapper.querySelectorAll('img'))) {
    const src = img.getAttribute('src') || ''
    const drive = /^[a-zA-Z]:[\\/]/.test(src)
    // A file address written encoded (file:///C:/%E5%9B%BE...) goes as the path it names: Word does not decode it.
    if (/^file:\/\//i.test(src)) {
      let path
      try { path = decodeURI(src.slice(5)) } catch (e) { continue }
      if (/^\/\/\/[a-zA-Z]:/.test(path)) path = path.slice(3) // file:///C:/... -> C:/...
      else if (path.startsWith('///')) path = path.slice(2) // file:///home/... -> /home/...
      // file://server/share/... stays //server/share/...
      img.setAttribute('src', toFileUrl(path))
      continue
    }
    if (!src || (!drive && /^[a-zA-Z][a-zA-Z0-9+.-]*:/.test(src))) continue
    let local = src
    try { local = decodeURI(src) } catch { /* left as written */ }
    const absolute = /^(?:\/|\\\\|[a-zA-Z]:[\\/])/.test(local) ? local : basePath ? resolveLocalPath(basePath, local) : null
    if (absolute) img.setAttribute('src', toFileUrl(absolute))
  }
  return wrapper.innerHTML
}

const crcTable = Array.from({ length: 256 }, (_, n) => {
  let c = n
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
  return c >>> 0
})
const crc32 = bytes => {
  let c = 0xffffffff
  for (const b of bytes) c = crcTable[(c ^ b) & 0xff] ^ (c >>> 8)
  return (c ^ 0xffffffff) >>> 0
}

/**
 * A PNG data URL with its resolution set to `scale` times 96 dpi (a pHYs chunk after the header): Word sizes a pasted
 * picture by its resolution and not by the HTML's width and height, so a picture drawn at twice its size for
 * sharpness came out twice as large.
 */
export const withResolution = (dataUrl, scale) => {
  const bytes = Uint8Array.from(atob(dataUrl.slice(dataUrl.indexOf(',') + 1)), ch => ch.charCodeAt(0))
  const perMetre = Math.round(scale * 96 / 0.0254)
  const chunk = new Uint8Array(21)
  const view = new DataView(chunk.buffer)
  view.setUint32(0, 9)
  chunk.set([0x70, 0x48, 0x59, 0x73], 4) // pHYs
  view.setUint32(8, perMetre)
  view.setUint32(12, perMetre)
  chunk[16] = 1 // unit: metre
  view.setUint32(17, crc32(chunk.subarray(4, 17)))
  const at = 8 + 25 // signature, then the IHDR chunk (length, type, 13 bytes of data, CRC)
  const out = new Uint8Array(bytes.length + chunk.length)
  out.set(bytes.subarray(0, at))
  out.set(chunk, at)
  out.set(bytes.subarray(at), at + chunk.length)
  let binary = ''
  for (const b of out) binary += String.fromCharCode(b)
  return 'data:image/png;base64,' + btoa(binary)
}

/**
 * SVG pictures in a copy's HTML as PNG, embedded: Word does not show an SVG it is given in pasted HTML, wherever it
 * is, but takes a picture inside the HTML (data:). Each is drawn from the picture on the page (already loaded, so no
 * waiting), at twice its size for sharpness, and keeps the size it is shown at. One that is not on the page, or that
 * the page may not read (another site's), stays as it was.
 */
export const svgImagesAsPng = (html, page = document) => {
  if (!html || !/\.svg/i.test(html)) return html
  const drawn = Array.from(page.querySelectorAll('#ag-editor-id img'))
  // The page loads a local picture with a ?msec= cache key; the copy has the address as written.
  const key = src => { const bare = src.replace(/[?#].*$/, ''); try { return decodeURI(bare).replace(/\\/g, '/').toLowerCase() } catch { return bare.toLowerCase() } }
  const wrapper = page.createElement('div')
  wrapper.innerHTML = html
  for (const img of Array.from(wrapper.querySelectorAll('img'))) {
    const src = img.getAttribute('src') || ''
    if (!/\.svg(?:[?#].*)?$/i.test(src)) continue
    const source = drawn.find(d => d.complete && d.naturalWidth > 0 && key(d.src) === key(src))
    if (!source) continue
    try {
      const rect = source.getBoundingClientRect()
      const width = Math.round(rect.width || source.naturalWidth)
      const height = Math.round(rect.height || source.naturalHeight)
      const canvas = page.createElement('canvas')
      canvas.width = width * 2
      canvas.height = height * 2
      canvas.getContext('2d').drawImage(source, 0, 0, canvas.width, canvas.height)
      img.setAttribute('src', withResolution(canvas.toDataURL('image/png'), 2))
      img.setAttribute('width', String(width))
      img.setAttribute('height', String(height))
    } catch (e) { /* a picture the page may not read stays an address */ }
  }
  return wrapper.innerHTML
}
