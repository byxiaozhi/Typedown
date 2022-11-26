// used for render table tookbar or others.
import { h } from '../snabbdom'
import { CLASS_OR_ID } from '../../../config'

export const TABLE_TOOLS = Object.freeze([{
  label: 'table',
  title: 'ResizeTable',
  icon: 'E9E9'
}, {
  label: 'left',
  title: 'AlignLeft',
  icon: 'E8E4'
}, {
  label: 'center',
  title: 'AlignCenter',
  icon: 'E8E3'
}, {
  label: 'right',
  title: 'AlignRight',
  icon: 'E8E2'
}, {
  label: 'delete',
  title: 'DeleteTable',
  icon: 'E74D'
}])

const renderToolBar = (type, tools, activeBlocks) => {
  const children = tools.map(tool => {
    const { label, title, icon } = tool
    const { align } = activeBlocks[1] // activeBlocks[0] is span block. cell content.
    let selector = 'li'
    if (align && label === align) {
      selector += '.active'
    }
    const iconVnode = h(`span.icon`, {
      style: {
        'font-family': '"Segoe Fluent Icons", "Segoe MDL2 Assets"',
      }
    }, String.fromCharCode(parseInt(icon, 16)))
    return h(selector, {
      dataset: {
        label,
        tooltip: title
      }
    }, iconVnode)
  })
  const selector = `div.ag-tool-${type}.${CLASS_OR_ID.AG_TOOL_BAR}`

  return h(selector, {
    attrs: {
      contenteditable: false
    }
  }, h('ul', children))
}

export const renderTableTools = (activeBlocks) => {
  return renderToolBar('table', TABLE_TOOLS, activeBlocks)
}
