import { h } from '../snabbdom'
import { CLASS_OR_ID } from '../../../config'
import { svgIcon } from './renderSvgIcon'

const FUNCTION_TYPE_HASH = {
  mermaid: { svg: 'diagram' },
  flowchart: { svg: 'diagram' },
  sequence: { svg: 'code' },
  plantuml: { svg: 'code' },
  'vega-lite': { svg: 'code' },
  table: { svg: 'table' },
  html: { svg: 'code' },
  multiplemath: { text: 'f', serifItalic: true },
  fencecode: { svg: 'code' },
  indentcode: { svg: 'code' },
  frontmatter: { svg: 'code' },
  footnote: { svg: 'code' }
}

export default function renderIcon (block) {
  if (block.parent) {
    console.error('Only top most block can render front icon button.')
  }
  const { type, functionType, listType } = block
  const selector = `a.${CLASS_OR_ID.AG_FRONT_ICON}`
  let icon = null

  switch (type) {
    case 'p': {
      icon = 'P'
      break
    }
    case 'figure':
    case 'pre': {
      icon = FUNCTION_TYPE_HASH[functionType]
      if (!icon) {
        console.warn(`Unhandled functionType ${functionType}`)
        icon = 'P'
      }
      break
    }
    case 'ul': {
      icon = listType === 'task' ? { svg: 'taskList' } : { svg: 'bulletList' }
      break
    }
    case 'ol': {
      icon = { svg: 'numberList' }
      break
    }
    case 'blockquote': {
      icon = { svg: 'quote' }
      break
    }
    case 'h1': {
      icon = 'H₁'
      break
    }
    case 'h2': {
      icon = 'H₂'
      break
    }
    case 'h3': {
      icon = 'H₃'
      break
    }
    case 'h4': {
      icon = 'H₄'
      break
    }
    case 'h5': {
      icon = 'H₅'
      break
    }
    case 'h6': {
      icon = 'H₆'
      break
    }
    case 'hr': {
      icon = 'HR'
      break
    }
    default:
      icon = 'P'
      break
  }

  // a letter for the plain blocks ("P", "H1"…), an SVG for the rest. The icon is built here rather than kept
  // in the table above: a snabbdom vnode belongs to one patch and cannot be shared between renders.
  const iconVnode = typeof icon === 'string'
    ? h(`span.icon`, { style: { 'font-size': '12px' } }, icon)
    : icon.svg
      ? svgIcon(icon.svg, 12)
      : h(`span.icon`, { style: { 'font-family': 'Times New Roman', 'font-style': 'italic', 'font-size': '12px' } }, icon.text)

  return h(selector, {
    attrs: {
      contenteditable: 'false'
    }
  }, iconVnode)
}
