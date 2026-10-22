import { stripMarks } from '../parser/speech'

const tocCtrl = ContentState => {
  ContentState.prototype.getTOC = function () {
    const { blocks } = this
    const toc = []
    let cur = null

    const start = this.findOutMostBlock(this.getBlock(this.cursor.start.key))
    const end = this.findOutMostBlock(this.getBlock(this.cursor.end.key))

    for (const block of blocks) {
      if (/^h\d$/.test(block.type)) {
        const { headingStyle, key, type } = block
        const { text } = block.children[0]
        let content = headingStyle === 'setext' ? text.trim() : text.replace(/^\s*#{1,6}\s{1,}/, '').trim()
        // in Speech mode the marks ({budget 3m}, {pause}) are not part of the title
        if (this.muya.options.speechMode) {
          content = stripMarks(content, this.muya.options.speechDefs).replace(/\s+/g, ' ').trim()
        }
        const lvl = +type.substring(1)
        const slug = key
        toc.push({
          content,
          lvl,
          slug
        })
      }
      if (toc.length > 0) {
        if (block == start) {
          cur = toc[toc.length - 1]
        } else if (block == end && cur != toc[toc.length - 1]) {
          cur = null
        }
      }
    }
    
    return { toc, cur }
  }
}

export default tocCtrl
