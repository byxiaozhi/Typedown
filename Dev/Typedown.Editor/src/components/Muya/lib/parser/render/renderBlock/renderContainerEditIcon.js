import { h } from '../snabbdom'
import { CLASS_OR_ID } from '../../../config'
import { svgIcon } from './renderSvgIcon'

export const renderEditIcon = () => h(`a.${CLASS_OR_ID.AG_CONTAINER_ICON}`, {
  attrs: {
    contenteditable: 'false'
  }
}, svgIcon('edit', 12))
