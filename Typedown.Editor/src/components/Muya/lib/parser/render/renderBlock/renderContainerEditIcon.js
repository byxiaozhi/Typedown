import { h } from '../snabbdom'
import { CLASS_OR_ID } from '../../../config'

export const renderEditIcon = () => {
  const selector = `a.${CLASS_OR_ID.AG_CONTAINER_ICON}`

  const iconVnode = h(`span.icon`, {
    style: {
      'font-family': '"Segoe Fluent Icons", "Segoe MDL2 Assets"',
      'margin-top':'2px',
      'font-size': '8px',
    }
  }, String.fromCharCode(parseInt('E70F', 16)))

  return h(selector, {
    attrs: {
      contenteditable: 'false'
    }
  }, iconVnode)
}
