import { h } from '../snabbdom'
import { CLASS_OR_ID } from '../../../config'

const fontCode = code => String.fromCharCode(parseInt(code, 16))

const FUNCTION_TYPE_HASH = {
  mermaid: fontCode('EF90'),
  flowchart: fontCode('EF90'),
  sequence: fontCode('E943'),
  plantuml: fontCode('E943'),
  'vega-lite': fontCode('E943'),
  table: fontCode('F0E2'),
  html: fontCode('E943'),
  multiplemath: h(`span.icon`, {
    style: {
      'font-family': 'Times New Roman',
      'font-style': 'italic',
      'font-size': '12px',
    }
  }, 'f'),
  fencecode: fontCode('E943'),
  indentcode: fontCode('E943'),
  frontmatter: fontCode('E943'),
  footnote: fontCode('E943')
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
      if (listType === 'task') {
        icon = fontCode('E9D5', 16)
      } else {
        icon = fontCode('E8FD', 16)
      }
      break
    }
    case 'ol': {
      icon = fontCode('E8FD', 16)
      break
    }
    case 'blockquote': {
      icon = fontCode('E9B2') + fontCode('E9B1')
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

  const iconVnode = h(`span.icon`, {
    style: {
      'font-family': '"Segoe Fluent Icons", "Segoe MDL2 Assets"',
      'font-size': '12px',
    }
  }, icon)

  return h(selector, {
    attrs: {
      contenteditable: 'false'
    }
  }, iconVnode)
}
