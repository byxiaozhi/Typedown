import { h } from '../snabbdom'
import { svgIcon } from './renderSvgIcon'

export const footnoteJumpIcon = () => h('span.ag-footnote-backlink', {}, svgIcon('back', 12))
