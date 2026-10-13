import { toHTML, h } from './snabbdom'

export const PLANTUML_URL = 'https://www.plantuml.com/plantuml'

/**
 * The PlantUML server that draws the diagrams (Settings > Editor > PlantUML server): an http(s) address of a
 * plantuml-server, default plantuml.com. Anything else is the default.
 */
export const plantUmlServer = (value) => {
  const server = (value || '').trim().replace(/\/+$/, '')
  return /^https?:\/\/[^\s]+$/i.test(server) ? server : PLANTUML_URL
}

export default class Diagram {
  encodedInput = ''

  /**
   * Builds a Diagram object storing the encoded input value
   */
  static parse (input) {
    const diagram = new Diagram()
    diagram.encodedInput = Diagram.encode(input)
    return diagram
  }

  /**
   * The diagram's text as PlantUML's hex encoding (https://plantuml.com/text-encoding): "~h" and the UTF-8 bytes in
   * hex. The deflate encoding this used needed Node's zlib, which a web page does not have; with it commented out the
   * function returned nothing, the address ended in "undefined" and plantuml.com drew its "bad URL" error instead.
   */
  static encode (value) {
    return '~h' + [...new TextEncoder().encode(value)].map(b => b.toString(16).padStart(2, '0')).join('')
  }

  insertImgElement (container, server, format = 'svg') {
    const div = typeof container === 'string'
      ? document.getElementById(container)
      : container
    if (div === null || !div.tagName) {
      throw new Error('Invalid container: ' + container)
    }
    const src = `${plantUmlServer(server)}/${format}/${this.encodedInput}`
    const node = h('img', { attrs: { src } })
    div.innerHTML = toHTML(node)
  }
}
