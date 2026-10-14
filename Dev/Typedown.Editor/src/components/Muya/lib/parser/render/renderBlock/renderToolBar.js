// used for render table tookbar or others.
import { h } from '../snabbdom'
import { CLASS_OR_ID } from '../../../config'
import { svgIcon } from './renderSvgIcon'

export const TABLE_TOOLS = Object.freeze([{
  label: 'table',
  title: 'ResizeTable',
  icon: 'resize'
}, {
  label: 'left',
  title: 'AlignLeft',
  icon: 'alignLeft'
}, {
  label: 'center',
  title: 'AlignCenter',
  icon: 'alignCenter'
}, {
  label: 'right',
  title: 'AlignRight',
  icon: 'alignRight'
}, {
  label: 'delete',
  title: 'DeleteTable',
  icon: 'trash'
}])

const renderToolBar = (type, tools, activeBlocks) => {
  const children = tools.map(tool => {
    const { label, title, icon } = tool
    const { align } = activeBlocks[1] // activeBlocks[0] is span block. cell content.
    let selector = 'li'
    if (align && label === align) {
      selector += '.active'
    }
    const iconVnode = svgIcon(icon, 14)
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
