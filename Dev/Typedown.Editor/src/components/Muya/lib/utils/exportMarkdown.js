/**
 * Hi contributors!
 *
 * Before you edit or update codes in this file,
 * make sure you have read this below:
 * Commonmark Spec: https://spec.commonmark.org/0.29/
 * GitHub Flavored Markdown Spec: https://github.github.com/gfm/
 * Pandoc Markdown: https://pandoc.org/MANUAL.html#pandocs-markdown
 * The output markdown needs to obey the standards of these Spec.
 */

import { CURSOR_ANCHOR_DNA, CURSOR_FOCUS_DNA } from '../config'
import { isEmptyParagraph, visibleEmptyParagraphs } from './blankLines'

class ExportMarkdown {
  constructor(blocks, listIndentation = 1, isGitlabCompatibilityEnabled = false, { alignTableColumns = true } = {}) {
    this.blocks = blocks
    // false: write tables compactly (`| a | b |`) instead of padding every cell to the column width
    this.alignTableColumns = alignTableColumns
    this.listType = [] // 'ul' or 'ol'
    // helper to translate the first tight item in a nested list
    this.isLooseParentList = true
    this.isGitlabCompatibilityEnabled = !!isGitlabCompatibilityEnabled

    // set and validate settings
    this.listIndentation = 'number'
    if (listIndentation === 'dfm') {
      this.listIndentation = 'dfm'
      this.listIndentationCount = 4
    } else if (typeof listIndentation === 'number') {
      this.listIndentationCount = Math.min(Math.max(listIndentation, 1), 4)
    } else {
      this.listIndentationCount = 1
    }
  }

  generate() {
    return this.translateBlocks2Markdown(this.blocks)
  }

  // `tight` is set for the direct children of a tight list item: such an item had no blank line between its
  // blocks in the source (that is what makes a list tight), so writing one back would turn the whole list
  // loose the next time the file is read.
  translateBlocks2Markdown(blocks, indent = '', listIndent = '', tight = false) {
    const result = []
    // Inside a tight list item a blank line is left out wherever the two blocks still read as two blocks
    // without it, because a blank line there makes the whole list loose the next time the file is read.
    // It is kept where dropping it would change the text: two paragraphs would run into one, a rule right
    // under a paragraph would become a setext heading, and an indented code block would become plain text.
    let previous = null
    const separate = (res, ind, block) => {
      const needed = !tight || previous === null || block.type === 'hr' ||
        (block.type === 'p' && previous === 'p') ||
        (block.type === 'pre' && block.functionType !== 'fencecode' && block.functionType !== 'frontmatter')
      if (needed) this.insertLineBreak(res, ind)
    }
    // helper for CommonMark 264
    let lastListBullet = ''
    // Exact blank-line counts read with the document (utils/blankLines.js): where the last block with content
    // ended in `result`, and how many empty paragraphs have followed it.
    let lastContent = null
    let contentEnd = 0
    let emptyRun = 0

    for (const block of blocks) {
      if (block.type !== 'ul' && block.type !== 'ol') {
        lastListBullet = ''
      }

      switch (block.type) {
        case 'p':
        case 'hr': {
          separate(result, indent, block)
          if (result.length && isEmptyParagraph(block)) {
            // A blank line, written like the ones between blocks: no trailing spaces, and only the quotation marker
            // inside a quotation. (The text is empty, or only the caret markers of an export with a cursor.)
            result.push(`${indent.trimEnd()}${block.children[0].text}\n`)
          } else {
            result.push(this.translateBlocks2Markdown(block.children, indent))
          }
          break
        }
        case 'span': {
          result.push(this.normalizeParagraphText(block, indent))
          break
        }
        case 'h1':
        case 'h2':
        case 'h3':
        case 'h4':
        case 'h5':
        case 'h6': {
          separate(result, indent, block)
          result.push(this.normalizeHeaderText(block, indent))
          break
        }
        case 'figure': {
          separate(result, indent, block)
          switch (block.functionType) {
            case 'table': {
              const table = block.children[0]
              result.push(this.normalizeTable(table, indent))
              break
            }
            case 'html': {
              result.push(this.normalizeHTML(block, indent))
              break
            }
            case 'footnote': {
              result.push(this.normalizeFootnote(block, indent))
              break
            }
            case 'multiplemath': {
              result.push(this.normalizeMultipleMath(block, indent))
              break
            }
            case 'mermaid':
            case 'flowchart':
            case 'sequence':
            case 'plantuml':
            case 'vega-lite': {
              result.push(this.normalizeContainer(block, indent))
              break
            }
          }
          break
        }
        case 'li': {
          const insertNewLine = block.isLooseListItem

          // helper variable to correct the first tight item in a nested list
          this.isLooseParentList = insertNewLine
          if (insertNewLine) {
            this.insertLineBreak(result, indent)
          }
          result.push(this.normalizeListItem(block, indent + listIndent, !insertNewLine))
          this.isLooseParentList = true
          break
        }
        case 'ul': {
          let insertNewLine = this.isLooseParentList
          this.isLooseParentList = true

          // Start a new list without separation due changing the bullet or ordered list delimiter starts a new list.
          const { bulletMarkerOrDelimiter } = block.children[0]
          if (lastListBullet && lastListBullet !== bulletMarkerOrDelimiter) {
            insertNewLine = false
          }
          lastListBullet = bulletMarkerOrDelimiter
          if (insertNewLine) {
            this.insertLineBreak(result, indent)
          }

          this.listType.push({ type: 'ul' })
          result.push(this.normalizeList(block, indent, listIndent))
          this.listType.pop()
          break
        }
        case 'ol': {
          let insertNewLine = this.isLooseParentList
          this.isLooseParentList = true

          // Start a new list without separation due changing the bullet or ordered list delimiter starts a new list.
          const { bulletMarkerOrDelimiter } = block.children[0]
          if (lastListBullet && lastListBullet !== bulletMarkerOrDelimiter) {
            insertNewLine = false
          }
          lastListBullet = bulletMarkerOrDelimiter
          if (insertNewLine) {
            this.insertLineBreak(result, indent)
          }
          const listCount = block.start !== undefined ? block.start : 1
          this.listType.push({ type: 'ol', listCount })
          result.push(this.normalizeList(block, indent, listIndent))
          this.listType.pop()
          break
        }
        case 'pre': {
          separate(result, indent, block)
          if (block.functionType === 'frontmatter') {
            result.push(this.normalizeFrontMatter(block, indent))
          } else {
            result.push(this.normalizeCodeBlock(block, indent))
          }
          break
        }
        case 'blockquote': {
          separate(result, indent, block)
          result.push(this.normalizeBlockquote(block, indent))
          break
        }
        default: {
          console.warn('translateBlocks2Markdown: Unknown block type:', block.type)
          break
        }
      }
      previous = block.type
      if (isEmptyParagraph(block)) {
        emptyRun++
      } else {
        const n = block.blankLinesBefore
        if (n != null && block.blankLinesParent === block.parent && emptyRun === visibleEmptyParagraphs(n, lastContent ? 'middle' : 'start')) {
          const lines = result.slice(contentEnd).join('').split('\n')
          let written = 0
          while (written < lines.length - 1 && this.isBlankLine(lines[written], indent)) written++
          this.insertBlankLines(result, contentEnd, n - written, indent)
        }
        lastContent = block
        contentEnd = result.length
        emptyRun = 0
      }
    }
    const n = lastContent && lastContent.blankLinesAfter
    if (n != null && lastContent.blankLinesParent === lastContent.parent && emptyRun === visibleEmptyParagraphs(n, 'end')) {
      this.insertBlankLines(result, contentEnd, n - (result.slice(contentEnd).join('').split('\n').length - 1), indent)
    }
    return result.join('')
  }

  isBlankLine(line, indent) {
    return line.trim() === '' || line.trim() === indent.trim()
  }

  // Blank lines the empty paragraphs and the gaps around them did not write, put back where the run starts.
  insertBlankLines(result, at, count, indent) {
    if (count <= 0) return
    result.splice(at, 0, ...new Array(count).fill(`${indent.trimEnd()}\n`))
  }

  insertLineBreak(result, indent) {
    if (!result.length) return
    // Inside a quotation the marker has to be repeated or the quotation ends; list indentation is only
    // spaces, and a line of trailing spaces is no better than an empty one — it just shows up in diffs.
    result.push(`${indent.trimEnd()}\n`)
  }

  normalizeParagraphText(block, indent) {
    const { text } = block
    const lines = text.split('\n')
    return lines.map(line => `${indent}${line}`).join('\n') + '\n'
  }

  normalizeHeaderText(block, indent) {
    const { headingStyle, marker } = block
    const { text } = block.children[0]
    if (headingStyle === 'atx') {
      const match = text.match(/(#{1,6})(.*)/)
      const atxHeadingText = `${match[1]} ${match[2].trim()}`
      return `${indent}${atxHeadingText}\n`
    } else if (headingStyle === 'setext') {
      const lines = text.trim().split('\n')
      return lines.map(line => `${indent}${line}`).join('\n') + `\n${indent}${marker.trim()}\n`
    }
  }

  normalizeBlockquote(block, indent) {
    const { children } = block
    const newIndent = `${indent}> `
    return this.translateBlocks2Markdown(children, newIndent)
  }

  normalizeFrontMatter(block, indent) { // preBlock
    let startToken
    let endToken
    switch (block.lang) {
      case 'yaml':
        startToken = '---\n'
        endToken = '---\n'
        break
      case 'toml':
        startToken = '+++\n'
        endToken = '+++\n'
        break
      case 'json':
        if (block.style === ';') {
          startToken = ';;;\n'
          endToken = ';;;\n'
        } else {
          startToken = '{\n'
          endToken = '}\n'
        }
        break
    }

    const result = []
    result.push(startToken)
    for (const line of block.children[0].children) {
      result.push(`${line.text}\n`)
    }
    result.push(endToken)
    return result.join('')
  }

  normalizeMultipleMath(block, /* figure */ indent) {
    const { isGitlabCompatibilityEnabled } = this
    let startToken = '$$'
    let endToken = '$$'
    if (isGitlabCompatibilityEnabled && block.mathStyle === 'gitlab') {
      startToken = '```math'
      endToken = '```'
    }

    const lines = block.children[0].children[0].children
    // Keep the author's single-line `$$ ... $$` form as long as the formula still fits on one line.
    if (block.mathStyle === 'singleline' && lines.length === 1 && !lines[0].text.includes('$')) {
      return `${indent}$$${lines[0].text}$$\n`
    }

    const result = []
    result.push(`${indent}${startToken}\n`)
    for (const line of lines) {
      result.push(`${indent}${line.text}\n`)
    }
    result.push(`${indent}${endToken}\n`)
    return result.join('')
  }

  // `mermaid` `flowchart` `sequence` `plantuml` `vega-lite`
  normalizeContainer(block, indent) {
    const result = []
    const diagramType = block.children[0].functionType
    result.push('```' + diagramType + '\n')
    for (const line of block.children[0].children[0].children) {
      result.push(`${line.text}\n`)
    }
    result.push('```\n')
    return result.join('')
  }

  normalizeCodeBlock(block, indent) {
    const result = []
    const codeContent = block.children[1].children[0]
    const textList = codeContent.text.split('\n')
    const { functionType } = block
    if (functionType === 'fencecode') {
      // Code containing a line of backticks needs a longer fence, or the block ends in the middle of itself
      // and everything after it is read as ordinary text.
      const info = `${block.lang || ''}${block.lang ? block.info || '' : ''}`
      // A backtick fence cannot carry an info string with a backtick in it: such a block is written with tildes.
      const char = info.includes('`') ? '~' : '`'
      const run = new RegExp(`^ {0,3}${char === '`' ? '`' : '~'}{3,}`, 'gm')
      const longest = (codeContent.text.match(run) || []).reduce((n, m) => Math.max(n, m.trim().length), 0)
      const fence = char.repeat(Math.max(3, longest + 1))
      result.push(`${indent}${fence}${info}\n`)
      textList.forEach(text => {
        result.push(`${indent}${text}\n`)
      })
      result.push(indent + fence + '\n')
    } else {
      textList.forEach(text => {
        result.push(`${indent}    ${text}\n`)
      })
    }

    return result.join('')
  }

  normalizeHTML(block, indent) { // figure
    const result = []
    const codeContentText = block.children[0].children[0].children[0].text
    const lines = codeContentText.split('\n')
    for (const line of lines) {
      result.push(`${indent}${line}\n`)
    }
    return result.join('')
  }

  /**
   * Widest column, in characters, that still gets its cells padded so the columns line up in the source.
   */
  static MAX_ALIGNED_COLUMN_WIDTH = 60

  normalizeTable(table, indent) {
    const result = []
    const { row, column } = table
    const tableData = []
    const tHeader = table.children[0]
    const tBody = table.children[1]
    const escapeText = str => {
      return str.replace(/([^\\])\|/g, '$1\\|')
    }

    tableData.push(tHeader.children[0].children.map(th => escapeText(th.children[0].text).trim()))
    if (tBody) {
      tBody.children.forEach(bodyRow => {
        tableData.push(bodyRow.children.map(td => escapeText(td.children[0].text).trim()))
      })
    }

    const columnWidth = tHeader.children[0].children.map(th => ({ width: 5, align: th.align }))

    // While the caret is inside a cell the cell text carries the (long) cursor markers, which are stripped
    // again after export. Measure and pad by the visible text only, otherwise the table is written with
    // different column widths whenever the caret sits in it and the document looks modified.
    const visibleLength = cell => cell.replace(CURSOR_ANCHOR_DNA, '').replace(CURSOR_FOCUS_DNA, '').length

    let i
    let j

    if (this.alignTableColumns) {
      for (i = 0; i <= row; i++) {
        for (j = 0; j <= column; j++) {
          columnWidth[j].width = Math.max(columnWidth[j].width, visibleLength(tableData[i][j]) + 2) // add 2, because have two space around text
        }
      }
      // A single long cell would pad every other cell in its column to the same width: a table of test cases
      // with one wide "steps" column grew fifteen-fold that way, and the padding is written to the file. Past
      // this width the column keeps one space on each side, which is what a column that wide looks like anyway.
      columnWidth.forEach(c => {
        if (c.width - 2 > ExportMarkdown.MAX_ALIGNED_COLUMN_WIDTH) c.width = 0
      })
    }
    tableData.forEach((r, i) => {
      const rs = indent + '|' + r.map((cell, j) => {
        if (!this.alignTableColumns || !columnWidth[j].width) return ` ${cell} `
        const width = columnWidth[j].width + (cell.length - visibleLength(cell))
        const raw = ` ${cell + ' '.repeat(width)}`
        return raw.substring(0, width)
      }).join('|') + '|'
      result.push(rs)
      if (i === 0) {
        const cutOff = indent + '|' + columnWidth.map(({ width, align }) => {
          let raw = '-'.repeat(this.alignTableColumns && width ? width - 2 : 3)
          switch (align) {
            case 'left':
              raw = `:${raw} `
              break
            case 'center':
              raw = `:${raw}:`
              break
            case 'right':
              raw = ` ${raw}:`
              break
            default:
              raw = ` ${raw} `
              break
          }
          return raw
        }).join('|') + '|'
        result.push(cutOff)
      }
    })
    return result.join('\n') + '\n'
  }

  normalizeList(block, indent, listIndent) {
    const { children } = block
    return this.translateBlocks2Markdown(children, indent, listIndent)
  }

  normalizeListItem(block, indent, tight = false) {
    const result = []
    const listInfo = this.listType[this.listType.length - 1]
    const isUnorderedList = listInfo.type === 'ul'
    let { children, bulletMarkerOrDelimiter } = block
    let itemMarker

    if (isUnorderedList) {
      itemMarker = bulletMarkerOrDelimiter ? `${bulletMarkerOrDelimiter} ` : '- '
    } else {
      // NOTE: GitHub and Bitbucket limit the list count to 99 but this is nowhere defined.
      //  We limit the number to 99 for Daring Fireball Markdown to prevent indentation issues.
      let n = listInfo.listCount
      if ((this.listIndentation === 'dfm' && n > 99) || n > 999999999) {
        n = 1
      }
      listInfo.listCount++

      const delimiter = bulletMarkerOrDelimiter || '.'
      itemMarker = `${n}${delimiter} `
    }

    // Subsequent paragraph indentation
    const newIndent = indent + ' '.repeat(itemMarker.length)

    // New list indentation. We already added one space to the indentation
    let listIndent = ''
    const { listIndentation } = this
    if (listIndentation === 'dfm') {
      listIndent = ' '.repeat(4 - itemMarker.length)
    } else if (listIndentation === 'number') {
      listIndent = ' '.repeat(this.listIndentationCount - 1)
    }

    // TODO: Indent subsequent paragraphs by one tab. - not important
    //  Problem: "translateBlocks2Markdown" use "indent" in spaces to indent elements. How should
    //  we integrate tabs in blockquotes and subsequent paragraphs and how to combine with spaces?
    //  I don't know how to combine tabs and spaces and it seems not specified, so work for another day.

    if (isUnorderedList && block.listItemType === 'task') {
      const firstChild = children[0]
      itemMarker += firstChild.checked ? '[x] ' : '[ ] '
      children = children.slice(1)
    }

    result.push(`${indent}${itemMarker}`)
    result.push(this.translateBlocks2Markdown(children, newIndent, listIndent, tight).substring(newIndent.length))
    return result.join('')
  }

  normalizeFootnote(block, indent) {
    const result = []
    const identifier = block.children[0].text
    result.push(`${indent}[^${identifier}]:`)
    const hasMultipleBlocks = block.children.length > 2 || block.children[1].type !== 'p'
    if (hasMultipleBlocks) {
      result.push('\n')
      const newIndent = indent + ' '.repeat(4)
      result.push(this.translateBlocks2Markdown(block.children.slice(1), newIndent))
    } else {
      result.push(' ')
      const paragraphContent = block.children[1].children[0]
      result.push(this.normalizeParagraphText(paragraphContent, indent))
    }

    return result.join('')
  }
}

export default ExportMarkdown
