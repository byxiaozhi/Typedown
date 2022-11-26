import { h } from '../snabbdom'

const renderCopyButton = () => {
  const selector = 'a.ag-code-copy'

  const iconVnode = h(`span.icon`, {
    style: {
      'font-family': '"Segoe Fluent Icons", "Segoe MDL2 Assets"',
      'font-size': '16px'
    }
  }, String.fromCharCode(parseInt('E8C8', 16)))

  return h(selector, {
    attrs: {
      'data-tooltip': 'CopyContent',
      contenteditable: 'false'
    }
  }, iconVnode)
}

export default renderCopyButton
