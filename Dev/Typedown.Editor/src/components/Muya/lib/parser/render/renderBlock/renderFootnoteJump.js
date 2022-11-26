import { h } from '../snabbdom'

export const footnoteJumpIcon = () => {
  return h('span.ag-footnote-backlink', {
    style: {
      'font-family': '"Segoe Fluent Icons", "Segoe MDL2 Assets"',
      'font-size': '12px',
    }
  }, String.fromCharCode(parseInt('EB97', 16)))
}
