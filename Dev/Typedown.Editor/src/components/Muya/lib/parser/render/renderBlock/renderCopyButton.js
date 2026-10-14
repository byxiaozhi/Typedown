import { h } from '../snabbdom'
import { svgIcon } from './renderSvgIcon'

const renderCopyButton = () => {
  const selector = 'a.ag-code-copy'

  return h(selector, {
    attrs: {
      'data-tooltip': 'CopyContent',
      contenteditable: 'false'
    }
  }, svgIcon('copy', 16))
}

export default renderCopyButton
