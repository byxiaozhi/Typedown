import { CLASS_OR_ID } from '../../../config'
import { snakeToCamel } from '../../../utils'
import { describe, paceStyle, hueOf, styleOf } from '../../speech'

// Speech marks, drawn (docs/speech-marks-design.md). A single mark ({pause 2s}, {cue: ...}, a definition) is a chip:
// its text stays in the line, hidden, and the chip is drawn from `data-chip` by the style sheet, so it adds nothing
// to the text and the caret offsets stay those of the file. A pair ({slow}...{/slow}) hides its two marks like the
// marks of **bold** and styles the text between them. While the caret is inside a mark the raw text shows in gray.
export default function speech (h, cursor, block, token, outerClass) {
  const className = this.getClassName(outerClass, block, token, cursor)
  const labels = this.muya.options.speechLabels
  const { start, end } = token.range
  const { info } = token
  const text = (from, to) => this.highlight(h, block, from, to, token)

  if (token.role !== 'pair') {
    const { chip, title } = describe(info, labels)
    const kind = token.role === 'define' ? 'define' : info.entry.kind
    const attrs = { spellcheck: 'false' }
    if (title) attrs.title = title
    const dataset = { chip }
    let selector = `span.${className}.${CLASS_OR_ID.AG_REMOVE}.ag-speech.ag-speech-chip.ag-speech-${kind}`
    // a word of the user's own: the colour and the symbol they chose
    if (token.role === 'point' && info.entry.user) {
      const { color, icon } = styleOf(this.muya.options.speechStyles, info.entry.name)
      if (color) selector += `.ag-speech-c-${color}`
      if (icon) dataset.icon = icon
    }
    return [h(selector, { attrs, dataset }, text(start, end))]
  }

  const { entry } = info
  const render = children => children.reduce((acc, to) => {
    const chunk = this[snakeToCamel(to.type)](h, cursor, block, to, className)
    return Array.isArray(chunk) ? [...acc, ...chunk] : [...acc, chunk]
  }, [])
  const marker = (from, to) => h(`span.${className}.${CLASS_OR_ID.AG_REMOVE}`, text(from, to))

  // the words between the marks are spoken words: they stay open to the spell checker
  const data = { dataset: {}, style: {}, attrs: {} }
  let selector = `span.${CLASS_OR_ID.AG_INLINE_RULE}.ag-speech.ag-speech-pair.ag-speech-${entry.user ? 'user' : entry.name}`
  if (entry.kind === 'pace') {
    Object.assign(data.style, paceStyle(entry))
    selector += entry.speed < 1 ? '.ag-speech-slower' : entry.speed > 1 ? '.ag-speech-faster' : ''
  }
  if (entry.name === 'tone') {
    data.dataset.label = info.text
  } else if (entry.user) {
    const { color, icon } = styleOf(this.muya.options.speechStyles, entry.name)
    data.dataset.label = icon ? `${icon} ${entry.name}` : entry.name
    if (color) {
      // the colour the user chose (a pair of theme colours: see the style sheet)
      selector += `.ag-speech-c-${color}`
    } else if (entry.kind === 'span') {
      // no choice: a colour from the name, the same on every PC
      data.style.backgroundColor = `hsla(${hueOf(entry.name)}, 65%, 50%, .2)`
    }
  }
  if (entry.user && entry.meaning) data.attrs.title = entry.meaning
  else if (info.text) data.attrs.title = info.text

  const openEnd = start + token.open.length
  const closeStart = end - token.close.length
  return [
    marker(start, openEnd),
    h(selector, data, render(token.children)),
    ...(token.close ? [marker(closeStart, end)] : [])
  ]
}
