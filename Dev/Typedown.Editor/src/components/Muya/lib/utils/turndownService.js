import TurndownService from 'turndown'
import { identity } from './index'

const turndownPluginGfm = require('joplin-turndown-plugin-gfm')

export const usePluginAddRules = (turndownService, keeps) => {
  // Use the gfm plugin
  const { gfm } = turndownPluginGfm
  turndownService.use(gfm)

  // The plugin's table row writes the header's border without the cells' alignment: its test for whether a cell is
  // there is the wrong way round (only a row with fewer cells than columns would read one). The border is written again
  // here from the header cells' align attribute (a pasted table's right-aligned column stayed left-aligned).
  const gfmRow = turndownService.rules.array.find(rule => rule.filter === 'tr')
  if (gfmRow) {
    turndownService.addRule('tableRowAligned', {
      filter: 'tr',
      replacement (content, node, options) {
        const out = gfmRow.replacement(content, node, options)
        const cells = Array.from(node.children).filter(c => c.nodeName === 'TH' || c.nodeName === 'TD')
        const aligns = cells.map(c => (c.getAttribute('align') || '').toLowerCase())
        if (!aligns.some(Boolean)) return out
        const lines = out.split('\n')
        const last = lines.length - 1
        if (!/^\|(\s*:?-+:?\s*\|)+$/.test(lines[last])) return out
        const borders = lines[last].split('|').slice(1, -1)
        lines[last] = '|' + borders.map((b, i) => {
          const mark = { left: ':--', right: '--:', center: ':-:' }[aligns[i]]
          return mark ? b.replace(/:?-+:?/, mark) : b
        }).join('|') + '|'
        return lines.join('\n')
      }
    })
  }

  // We need a extra strikethrough rule because the strikethrough rule in gfm is single `~`.
  turndownService.addRule('strikethrough', {
    filter: ['del', 's', 'strike'],
    replacement (content) {
      return '~~' + content + '~~'
    }
  })

  turndownService.addRule('mark', {
    filter: ['mark'],
    replacement (content) {
      return '==' + content + '=='
    }
  })

  turndownService.addRule('paragraph', {
    filter: 'p',

    replacement: function (content, node) {
      const isTaskListItemParagraph = node.previousElementSibling && node.previousElementSibling.tagName === 'INPUT'

      return isTaskListItemParagraph ? content + '\n\n' : '\n\n' + content + '\n\n'
    }
  })

  turndownService.addRule('listItem', {
    filter: 'li',

    replacement: function (content, node, options) {
      content = content
        .replace(/^\n+/, '') // remove leading newlines
        .replace(/\n+$/, '\n') // replace trailing newlines with just a single one
        .replace(/\n/gm, '\n  ') // indent

      let prefix = options.bulletListMarker + ' '
      const parent = node.parentNode
      if (parent.nodeName === 'OL') {
        const start = parent.getAttribute('start')
        const index = Array.prototype.indexOf.call(parent.children, node)
        prefix = (start ? Number(start) + index : index + 1) + '. '
      }
      return (
        prefix + content + (node.nextSibling && !/\n$/.test(content) ? '\n' : '')
      )
    }
  })

  // Handle multiple math lines
  turndownService.addRule('multiplemath', {
    filter (node, options) {
      return node.nodeName === 'PRE' && node.classList.contains('multiple-math')
    },
    replacement (content, node, options) {
      return `$$\n${content}\n$$`
    }
  })

  // A picture whose address has a space or brackets in it is written ![alt](<a b.png>): turndown's own rule wrote it
  // bare, which is not a picture in Markdown (a copy of ![pic](<图 片.png>) came out as text).
  turndownService.addRule('image', {
    filter: 'img',
    replacement (content, node) {
      const alt = (node.getAttribute('alt') || '').replace(/([\[\]\\])/g, '\\$1')
      const src = node.getAttribute('src') || ''
      const title = node.getAttribute('title')
      const address = /[\s()<>]/.test(src) ? `<${src.replace(/[<>]/g, encodeURIComponent)}>` : src
      return src ? `![${alt}](${address}${title ? ` "${title.replace(/"/g, '\\"')}"` : ''})` : ''
    }
  })

  turndownService.escape = identity
  turndownService.keep(keeps)
}

export default TurndownService
