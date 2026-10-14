import { normal, gfm, pedantic } from './blockRules'
import options from './options'
import { splitCells, rtrim, getUniqueId } from './utils'
import { visibleEmptyParagraphs } from '../../utils/blankLines'

/**
 * Splits the body rows of a table token into cells. A row with more cells than the header widens the table: the
 * header gets empty cells and the new columns no alignment, so the surplus cells stay in the document. GFM drops
 * them when rendering, and the editor, which writes the table back from its cells, deleted their text with them.
 * Empty cells past the header's width (a trailing pipe of a table without leading pipes) are dropped as before.
 */
const splitTableRows = (item, rows) => {
  const cells = rows.map(row => splitCells(row))
  let width = item.header.length
  for (const row of cells) {
    let used = row.length
    while (used > width && row[used - 1] === '') used--
    width = Math.max(width, used)
  }
  while (item.header.length < width) {
    item.header.push('')
    item.align.push(null)
  }
  item.cells = cells.map(row => {
    row.splice(width)
    while (row.length < width) row.push('')
    return row
  })
}

/**
 * Block Lexer
 */

function Lexer(opts) {
  this.tokens = []
  this.tokens.links = Object.create(null)
  this.tokens.footnotes = Object.create(null)
  this.footnoteOrder = 0
  this.options = Object.assign({}, options, opts)
  this.rules = normal

  if (this.options.pedantic) {
    this.rules = pedantic
  } else if (this.options.gfm) {
    this.rules = gfm
  }
}

/**
 * Preprocessing
 */

// Tabs are expanded (to the next multiple of four columns) only where the block structure depends on them: the
// indentation and marker prefix of a line (list and quote markers, heading hashes). Everywhere else a tab is text and
// stays one - replacing every tab made the first visual edit turn the tabs of Go code, Makefiles and prose into
// spaces. Inside a fenced code block only the fence's own indentation is expanded.
const BLOCK_PREFIX = /^(?:[ \t]*(?:[-*+]|\d{1,9}[.)]|>|#{1,6})(?=[ \t]))*[ \t]*/
const FENCE_OPEN = /^( *)(`{3,}|~{3,})([^`]*)$/

const expandColumns = (text, startColumn, stopAt = Infinity) => {
  let column = startColumn
  let out = ''
  let i = 0
  for (; i < text.length && column < stopAt; i++) {
    const ch = text[i]
    if (ch === '\t') {
      const next = column + 4 - (column % 4)
      out += ' '.repeat(next - column)
      column = next
    } else {
      out += ch
      column++
    }
  }
  return { out, rest: text.slice(i) }
}

export const expandStructuralTabs = src => {
  let fence = null
  return src.split('\n').map(line => {
    if (line.indexOf('\t') === -1 && !fence) {
      const open = FENCE_OPEN.exec(line)
      if (open && open[1].length <= 3) fence = { marker: open[2], indent: open[1].length }
      return line
    }
    if (fence) {
      const closing = new RegExp(`^ {0,3}${fence.marker[0] === '`' ? '`' : '~'}{${fence.marker.length},}[ \\t]*$`)
      if (closing.test(line.replace(/^\t+/, m => ' '.repeat(4 * m.length)))) {
        fence = null
        return line.replace(/^[ \t]+/, ws => expandColumns(ws, 0).out)
      }
      const leading = /^[ \t]*/.exec(line)[0]
      const { out, rest } = expandColumns(leading, 0, fence.indent)
      return out + rest + line.slice(leading.length)
    }
    const prefix = BLOCK_PREFIX.exec(line)[0]
    const expanded = expandColumns(prefix, 0).out + line.slice(prefix.length)
    const open = FENCE_OPEN.exec(expanded)
    if (open && open[1].length <= 3) fence = { marker: open[2], indent: open[1].length }
    return expanded
  }).join('\n')
}

Lexer.prototype.lex = function (src) {
  src = expandStructuralTabs(src.replace(/\r\n|\r/g, '\n'))
  this.checkFrontmatter = true
  this.footnoteOrder = 0
  this.token(src, true)

  // Move footnote token to the end of tokens.
  const { tokens } = this
  const hasNoFootnoteTokens = []
  const footnoteTokens = []
  let isInFootnote = false
  for (const token of tokens) {
    const { type } = token
    if (type === 'footnote_start') {
      isInFootnote = true
      // A definition is moved to the end of the document, away from the blank lines its counts describe.
      delete token.blankLinesBefore
      delete token.blankLinesAfter
      footnoteTokens.push(token)
    } else if (type === 'footnote_end') {
      isInFootnote = false
      footnoteTokens.push(token)
    } else if (isInFootnote) {
      footnoteTokens.push(token)
    } else {
      hasNoFootnoteTokens.push(token)
    }
  }

  const result = [...hasNoFootnoteTokens, ...footnoteTokens]
  result.links = tokens.links
  result.footnotes = tokens.footnotes
  return result
}

/**
 * Lexing
 */

// `container` is 'item' for the content of a list item: the blank lines at its start belong to the marker line and
// the ones at its end are the gap to the next item, which decides whether the list is loose; neither is recorded as
// a blank-line count (utils/blankLines.js).
Lexer.prototype.token = function (src, top, container) {
  const {
    footnote,
    frontMatter,
    isGitlabCompatibilityEnabled,
    math
  } = this.options

  // 移除首行缩进
  src = src.replace(/^ +$/gm, '')
  const tokensAtStart = this.tokens.length

  let loose
  let cap
  let bull
  let b
  let item
  let space
  let i
  let tag
  let l

  // Only check front matter at the begining of a markdown file.
  // Please see note in "blockquote" why we need "checkFrontmatter" and "top".
  if (frontMatter) {
    cap = this.rules.frontmatter.exec(src)
    if (this.checkFrontmatter && top && cap) {
      src = src.substring(cap[0].length)
      let lang
      let style
      let text
      if (cap[1]) {
        lang = 'yaml'
        style = '-'
        text = cap[1]
      } else if (cap[2]) {
        lang = 'toml'
        style = '+'
        text = cap[2]
      } else if (cap[3] || cap[4]) {
        lang = 'json'
        style = cap[3] ? ';' : '{'
        text = cap[3] || cap[4]
      }
      this.tokens.push({
        type: 'frontmatter',
        text,
        style,
        lang
      })
    }
    this.checkFrontmatter = false
  }

  // Blank lines between blocks (utils/blankLines.js). Extra blank lines become empty paragraphs so that they show
  // in the editor; the exact count goes on the token of the block after them (`blankLinesBefore`), or, at the end
  // of the text, on the token of the last block (`blankLinesAfter`). Rules that take the line ending and the blank
  // lines after their block with them (lists, HTML blocks) are covered by counting the newlines the previous step
  // ended with as well as the ones the newline rule takes.
  let hadBlock = this.tokens.length > tokensAtStart // the front matter
  let lastBlockStart = hadBlock ? tokensAtStart : -1
  let pendingBefore = null
  let stepStart = null
  let stepIndex = -1
  const settleStep = () => {
    if (stepStart === null) return 0
    if (pendingBefore && this.tokens[pendingBefore.index]) this.tokens[pendingBefore.index].blankLinesBefore = pendingBefore.n
    pendingBefore = null
    if (this.tokens.length > stepIndex) lastBlockStart = stepIndex
    hadBlock = true
    const consumed = stepStart.slice(0, stepStart.length - src.length)
    const last = this.tokens[this.tokens.length - 1]
    // An unclosed fence runs to the end of the text, its blank lines included: they are code, not a gap.
    if (/^\n*$/.test(consumed) || (last && last.type === 'code' && last.codeBlockStyle === 'fenced')) return 0
    return /\n*$/.exec(consumed)[0].length
  }
  // A run of `newlines` newlines; between blocks (and after the last one) the first of them ends the line of the
  // block before.
  const newlineRun = (newlines, atEnd) => {
    const position = !hadBlock ? 'start' : atEnd ? 'end' : 'middle'
    blankRun(position === 'start' ? newlines : newlines - 1, position)
  }
  const blankRun = (n, position) => {
    if (n <= 0) return
    for (let i = visibleEmptyParagraphs(n, position); i > 0; i--) {
      this.tokens.push({
        type: 'paragraph',
        text: ''
      })
    }
    // No 'space' token is pushed (that is what the empty paragraphs above are for), so anything that looks
    // back at the previous token has to be told that a blank line stood between them.
    const lastToken = this.tokens[this.tokens.length - 1]
    if (lastToken) lastToken.followedByBlankLine = true
    // A list item's own first and last lines are its marker line and the gap to the next item; only the blank
    // lines between its blocks are counted.
    if (container === 'item' && position !== 'middle') return
    if (position === 'end') {
      if (lastBlockStart >= 0) this.tokens[lastBlockStart].blankLinesAfter = n
    } else {
      pendingBefore = { index: this.tokens.length, n }
    }
  }

  while (src) {
    let rejectedTable = false

    const carried = settleStep()
    stepStart = src

    // newline
    cap = this.rules.newline.exec(src)
    if (cap) src = src.substring(cap[0].length)
    if (cap || carried) newlineRun(carried + (cap ? cap[0].length : 0), !src)
    stepIndex = this.tokens.length

    // code
    // An indented code block cannot interrupt a paragraph.
    cap = this.rules.code.exec(src)
    if (cap) {
      const lastToken = this.tokens[this.tokens.length - 1]
      src = src.substring(cap[0].length)
      if (lastToken && lastToken.type === 'paragraph') {
        lastToken.text += `\n${cap[0].trimRight()}`
      } else {
        cap = cap[0].replace(/^ {4}/gm, '')
        this.tokens.push({
          type: 'code',
          codeBlockStyle: 'indented',
          text: !this.options.pedantic
            ? rtrim(cap, '\n')
            : cap
        })
      }
      continue
    }

    // multiple line math
    if (math) {
      cap = this.rules.multiplemath.exec(src)
      if (cap) {
        src = src.substring(cap[0].length)
        this.tokens.push({
          type: 'multiplemath',
          text: cap[1],
          mathStyle: ''
        })
        continue
      }

      cap = this.rules.multiplemathSingleLine.exec(src)
      if (cap) {
        src = src.substring(cap[0].length)
        this.tokens.push({
          type: 'multiplemath',
          text: cap[1].trim(),
          mathStyle: 'singleline'
        })
        continue
      }

      // match GitLab display math blocks (```math)
      if (isGitlabCompatibilityEnabled) {
        cap = this.rules.multiplemathGitlab.exec(src)
        if (cap) {
          src = src.substring(cap[0].length)
          this.tokens.push({
            type: 'multiplemath',
            text: cap[2] || '',
            mathStyle: 'gitlab'
          })
          continue
        }
      }
    }

    // footnote
    if (footnote) {
      cap = this.rules.footnote.exec(src)
      if (top && cap) {
        src = src.substring(cap[0].length)
        const identifier = cap[1]
        this.tokens.push({
          type: 'footnote_start',
          identifier
        })

        // NOTE: Order is wrong if footnote identifier 1 is behind footnote identifier 2 in text.
        this.tokens.footnotes[identifier] = {
          order: ++this.footnoteOrder,
          identifier,
          footnoteId: getUniqueId()
        }

        /* eslint-disable no-useless-escape */
        // Remove the footnote identifer prefix. eg: `[^identifier]: `.
        cap = cap[0].replace(/^\[\^[^\^\[\]\s]+?(?<!\\)\]:\s*/gm, '')
        // Remove the four whitespace before each block of footnote.
        cap = cap.replace(/\n {4}(?=[^\s])/g, '\n')
        /* eslint-enable no-useless-escape */

        this.token(cap, top)

        this.tokens.push({
          type: 'footnote_end'
        })

        continue
      }
    }

    // fences
    cap = this.rules.fences.exec(src)
    if (cap) {
      src = src.substring(cap[0].length)
      const raw = cap[0]
      const text = indentCodeCompensation(raw, cap[3] || '')
      this.tokens.push({
        type: 'code',
        codeBlockStyle: 'fenced',
        lang: cap[2] ? cap[2].trim() : cap[2],
        text
      })
      continue
    }

    // heading
    cap = this.rules.heading.exec(src)
    if (cap) {
      src = src.substring(cap[0].length)
      let text = cap[2] ? cap[2].trim() : ''

      if (text.endsWith('#')) {
        let trimmed = rtrim(text, '#')

        if (this.options.pedantic) {
          text = trimmed.trim()
        } else if (!trimmed || trimmed.endsWith(' ')) {
          // CommonMark requires space before trailing #s
          text = trimmed.trim()
        }
      }

      this.tokens.push({
        type: 'heading',
        headingStyle: 'atx',
        depth: cap[1].length,
        text
      })
      continue
    }

    // table no leading pipe (gfm)
    cap = this.rules.nptable.exec(src)
    if (cap) {
      item = {
        type: 'table',
        header: splitCells(cap[1].replace(/^ *| *\| *$/g, '')),
        align: cap[2].replace(/^ *|\| *$/g, '').split(/ *\| */),
        cells: cap[3] ? cap[3].replace(/\n$/, '').split('\n') : []
      }

      if (item.header.length === item.align.length) {
        src = src.substring(cap[0].replace(/\n*$/g, '').length)

        for (i = 0; i < item.align.length; i++) {
          if (/^ *-+: *$/.test(item.align[i])) {
            item.align[i] = 'right'
          } else if (/^ *:-+: *$/.test(item.align[i])) {
            item.align[i] = 'center'
          } else if (/^ *:-+ *$/.test(item.align[i])) {
            item.align[i] = 'left'
          } else {
            item.align[i] = null
          }
        }

        splitTableRows(item, item.cells)

        this.tokens.push(item)

        continue
      }

      rejectedTable = true
    }

    // hr
    cap = this.rules.hr.exec(src)
    if (cap) {
      const marker = cap[0].replace(/\n*$/, '')
      src = src.substring(cap[0].length)
      this.tokens.push({
        type: 'hr',
        marker
      })
      continue
    }

    // blockquote
    cap = this.rules.blockquote.exec(src)
    if (cap) {
      src = src.substring(cap[0].length)

      this.tokens.push({
        type: 'blockquote_start'
      })

      cap = cap[0].replace(/^ *> ?/gm, '')

      // Pass `top` to keep the current
      // "toplevel" state. This is exactly
      // how markdown.pl works.
      this.token(cap, top)

      this.tokens.push({
        type: 'blockquote_end'
      })

      continue
    }

    // NOTE: Complete list lexer part is a custom implementation based on an older marked.js version.

    // list
    cap = this.rules.list.exec(src)
    if (cap) {
      // The legacy list regex accepts lazy continuation lines too broadly. A heading or
      // fence outside the item's indentation starts a new block, even without a blank
      // line. Split before matching items, otherwise '* text' inside that fence becomes
      // another list item and the code block is destroyed.
      if (!this.options.pedantic) {
        const baseIndent = cap[1].length
        let contentIndent = baseIndent + cap[2].length + 1
        let offset = 0
        for (const line of cap[0].split('\n')) {
          const marker = /^( *)([*+-]|\d{1,9}[.)])( +)/.exec(line)
          if (marker && marker[1].length <= baseIndent) {
            const padding = marker[3].length > 4 ? 1 : marker[3].length
            contentIndent = marker[1].length + marker[2].length + padding
          } else if (offset > 0) {
            const indent = /^ */.exec(line)[0].length
            if (indent < contentIndent && /^ {0,3}(?:#{1,6}(?:[ \t]|$)|`{3,}[^`]*$|~{3,})/.test(line)) {
              cap[0] = cap[0].slice(0, offset)
              break
            }
          }
          offset += line.length + 1
        }
      }
      // The blank lines after the list are a gap to the next block, not part of the last item (where they made an
      // empty paragraph of their own): they are left for the newline rule.
      cap[0] = cap[0].replace(/\n+$/, '')
      let checked
      src = src.substring(cap[0].length)
      bull = cap[2]
      let isOrdered = bull.length > 1
      this.tokens.push({
        type: 'list_start',
        ordered: isOrdered,
        listType: bull.length > 1 ? 'order' : (/^( {0,3})([-*+]) \[[xX ]\]/.test(cap[0]) ? 'task' : 'bullet'),
        start: isOrdered ? +(bull.slice(0, -1)) : ''
      })

      let next = false
      let prevNext = true
      let listItemIndices = []
      let isTaskList = false

      // Get each top-level item.
      cap = cap[0].match(this.rules.item)
      l = cap.length
      i = 0

      // Whether item `k + 1` starts a new list (the conditions below, where an item of another marker, delimiter or
      // task state starts one; ordered and bullet items are split apart further down, by sending the rest back).
      const kindOf = text => {
        const marker = /^ *([*+-]|\d+[.)])/.exec(text)[1]
        const ordered = marker.length > 1
        const task = !ordered && this.options.gfm && /^ *[*+-] {1,4}\[[ xX]\] +/.test(text)
        return { mark: ordered ? marker.slice(-1) : marker, ordered, task }
      }
      const startsNewList = k => {
        if (k + 1 >= l) return false
        const a = kindOf(cap[k])
        const b = kindOf(cap[k + 1])
        return a.ordered === b.ordered && (a.mark !== b.mark || a.task !== b.task)
      }
      // Blank lines after the last item of a list that is followed by another list; they go between the two lists
      // as a gap of their own instead of into the item.
      let gapToNextList = 0
      let switchedList = false

      for (; i < l; i++) {
        const index = i
        const itemWithBullet = cap[i]
        item = itemWithBullet
        let newIsTaskListItem = false
        switchedList = false

        // Remove the list item's bullet so it is seen as the next token.
        space = item.length
        let newBull
        item = item.replace(/^ *([*+-]|\d+(?:\.|\))) {0,4}/, function (m, p1) {
          // Get and remove list item bullet
          newBull = p1 || bull
          return ''
        })

        const newIsOrdered = bull.length > 1 && /\d{1,9}/.test(newBull)
        if (!newIsOrdered && this.options.gfm) {
          checked = this.rules.checkbox.exec(item)
          if (checked) {
            checked = checked[1] === 'x' || checked[1] === 'X'
            newIsTaskListItem = true

            // Remove the list item's checkbox and adjust indentation by removing checkbox length.
            item = item.replace(this.rules.checkbox, '')
            space -= 4
          } else {
            checked = undefined
          }
        }

        if (i === 0) {
          isTaskList = newIsTaskListItem
        } else if (
          // Changing the bullet or ordered list delimiter starts a new list (CommonMark 264 and 265)
          //   - unordered, unordered --> bull !== newBull --> new list (e.g "-" --> "*")
          //   - ordered, ordered --> lastChar !== lastChar --> new list (e.g "." --> ")")
          //   - else --> new list (e.g. ordered --> unordered)
          i !== 0 &&
          (
            (!isOrdered && !newIsOrdered && bull !== newBull) ||
            (isOrdered && newIsOrdered && bull.slice(-1) !== newBull.slice(-1)) ||
            (isOrdered !== newIsOrdered) ||
            // Changing to/from task list item from/to bullet, starts a new list(work for marktext issue #870)
            // Because we distinguish between task list and bullet list in MarkText,
            // the parsing here is somewhat different from the commonmark Spec,
            // and the task list needs to be a separate list.
            (isTaskList !== newIsTaskListItem)
          )
        ) {
          this.tokens.push({
            type: 'list_end'
          })
          blankRun(gapToNextList, 'middle')
          gapToNextList = 0

          // Start a new list
          bull = newBull
          isOrdered = newIsOrdered
          isTaskList = newIsTaskListItem
          stepIndex = this.tokens.length
          this.tokens.push({
            type: 'list_start',
            ordered: isOrdered,
            listType: bull.length > 1 ? 'order' : (/^( {0,3})([-*+]) \[[xX ]\]/.test(itemWithBullet) ? 'task' : 'bullet'),
            start: isOrdered ? +(bull.slice(0, -1)) : ''
          })
          if (pendingBefore) {
            this.tokens[pendingBefore.index].blankLinesBefore = pendingBefore.n
            pendingBefore = null
          }
          // The new list is loose or tight by its own items.
          next = false
          prevNext = true
          listItemIndices = []
          switchedList = true
        }

        // Outdent whatever the
        // list item contains. Hacky.
        if (~item.indexOf('\n ')) {
          space -= item.length
          item = !this.options.pedantic
            ? item.replace(new RegExp('^ {1,' + space + '}', 'gm'), '')
            : item.replace(/^ {1,4}/gm, '')
        }

        // Determine whether the next list item belongs here.
        // Backpedal if it does not belong in this list.
        if (i !== l - 1) {
          b = this.rules.bullet.exec(cap[i + 1])[0]
          if (bull.length > 1
            ? b.length === 1
            : (b.length > 1 || (this.options.smartLists && b !== bull))) {
            // The blank lines at the end of this item stand between this list and the next one: they go back
            // with the rest, for the newline rule.
            const trailing = /\n*$/.exec(item)[0].length
            item = item.slice(0, item.length - trailing)
            src = '\n'.repeat(trailing + 1) + cap.slice(i + 1).join('\n') + src
            i = l - 1
          } else if (startsNewList(i)) {
            const trailing = /\n*$/.exec(item)[0].length
            item = item.slice(0, item.length - trailing)
            gapToNextList = trailing
          }
        }

        // `index`, not `i`: sending the rest back above has moved `i` to the end.
        let prevItem = ''
        if (index === 0 || switchedList) {
          prevItem = item
        } else {
          prevItem = cap[index - 1]
        }

        // Determine whether item is loose or not. If previous item is loose
        // this item is also loose.
        // A list is loose if any of its constituent list items are separated by blank lines,
        // or if any of its constituent list items directly contain two block-level elements with a blank line between them.
        // loose = next = next || /^ *([*+-]|\d{1,9}(?:\.|\)))( +\S+\n\n(?!\s*$)|\n\n(?!\s*$))/.test(itemWithBullet)
        loose = next = next || /\n\n(?!\s*$)/.test(item)
        // Check if previous line ends with a new line.
        if (!loose && (i !== 0 || l > 1) && prevItem.length !== 0 && prevItem.charAt(prevItem.length - 1) === '\n') {
          loose = next = true
        }

        // A list is either loose or tight, so update previous list items but not nested list items.
        if (next && prevNext !== next) {
          for (const index of listItemIndices) {
            this.tokens[index].type = 'loose_item_start'
          }
          listItemIndices = []
        }
        prevNext = next

        if (!loose) {
          listItemIndices.push(this.tokens.length)
        }

        const isOrderedListItem = /\d/.test(bull)
        this.tokens.push({
          checked,
          listItemType: bull.length > 1 ? 'order' : (isTaskList ? 'task' : 'bullet'),
          bulletMarkerOrDelimiter: isOrderedListItem ? bull.slice(-1) : bull.charAt(0),
          type: loose ? 'loose_item_start' : 'list_item_start'
        })

        if (/^\s*$/.test(item)) {
          this.tokens.push({
            type: 'text',
            text: ''
          })
        } else {
          // Recurse.
          this.token(item, false, 'item')
        }

        this.tokens.push({
          type: 'list_item_end'
        })
      }

      this.tokens.push({
        type: 'list_end'
      })
      continue
    }

    // html
    cap = this.rules.html.exec(src)
    if (cap) {
      src = src.substring(cap[0].length)
      this.tokens.push({
        type: this.options.sanitize
          ? 'paragraph'
          : 'html',
        pre: !this.options.sanitizer &&
          (cap[1] === 'pre' || cap[1] === 'script' || cap[1] === 'style'),
        text: this.options.sanitize ? (this.options.sanitizer ? this.options.sanitizer(cap[0]) : escape(cap[0])) : cap[0]
      })
      continue
    }

    // def
    cap = this.rules.def.exec(src)
    if (top && cap) {
      let text = ''
      do {
        src = src.substring(cap[0].length)
        if (cap[3]) cap[3] = cap[3].substring(1, cap[3].length - 1)
        tag = cap[1].toLowerCase().replace(/\s+/g, ' ')
        if (!this.tokens.links[tag]) {
          this.tokens.links[tag] = {
            href: cap[2],
            title: cap[3]
          }
        }

        text += cap[0]
        if (cap[0].endsWith('\n\n')) break
        cap = this.rules.def.exec(src)
      } while (cap)

      if (this.options.disableInline) {
        this.tokens.push({
          type: 'paragraph',
          text: text.replace(/\n*$/, '')
        })
      }
      continue
    }

    // table (gfm)
    cap = this.rules.table.exec(src)
    if (cap) {
      item = {
        type: 'table',
        header: splitCells(cap[1].replace(/^ *| *\| *$/g, '')),
        align: cap[2].replace(/^ *|\| *$/g, '').split(/ *\| */),
        cells: cap[3] ? cap[3].replace(/\n$/, '').split('\n') : []
      }

      if (item.header.length === item.align.length) {
        src = src.substring(cap[0].replace(/\n*$/g, '').length)

        for (i = 0; i < item.align.length; i++) {
          if (/^ *-+: *$/.test(item.align[i])) {
            item.align[i] = 'right'
          } else if (/^ *:-+: *$/.test(item.align[i])) {
            item.align[i] = 'center'
          } else if (/^ *:-+ *$/.test(item.align[i])) {
            item.align[i] = 'left'
          } else {
            item.align[i] = null
          }
        }

        splitTableRows(item, item.cells.map(row => row.replace(/^ *\| *| *\| *$/g, '')))

        this.tokens.push(item)

        continue
      }

      rejectedTable = true
    }

    // lheading
    cap = this.rules.lheading.exec(src)
    if (cap) {
      const precededToken = this.tokens[this.tokens.length - 1]
      const chops = cap[0].trim().split(/\n/)
      const marker = chops[chops.length - 1]
      src = src.substring(cap[0].length)

      // A paragraph on the line right above belongs to the heading (a setext heading may span lines); one
      // separated by a blank line is its own paragraph and must not be swallowed.
      if (precededToken && precededToken.type === 'paragraph' && !precededToken.followedByBlankLine) {
        this.tokens.pop()
        this.tokens.push({
          type: 'heading',
          headingStyle: 'setext',
          depth: cap[2].charAt(0) === '=' ? 1 : 2,
          text: precededToken.text + '\n' + cap[1],
          marker,
          blankLinesBefore: precededToken.blankLinesBefore
        })
      } else {
        this.tokens.push({
          type: 'heading',
          headingStyle: 'setext',
          depth: cap[2].charAt(0) === '=' ? 1 : 2,
          text: cap[1],
          marker
        })
      }
      continue
    }

    // top-level paragraph
    cap = this.rules.paragraph.exec(src)
    if (top && cap) {
      src = src.substring(cap[0].length)

      if (/^\[toc\]\n?$/i.test(cap[1])) {
        this.tokens.push({ type: 'toc', text: '[TOC]' })
        continue
      }

      const text = cap[1].charAt(cap[1].length - 1) === '\n'
        ? cap[1].slice(0, -1)
        : cap[1]
      const previous = this.tokens[this.tokens.length - 1]
      // A rejected GFM table candidate clipped the paragraph before its header.
      // Join only that candidate back; merging every adjacent paragraph would
      // also change link definitions emitted as paragraphs by disableInline.
      if (rejectedTable && previous && previous.type === 'paragraph' && !previous.followedByBlankLine) {
        previous.text += `\n${text}`
      } else {
        this.tokens.push({ type: 'paragraph', text })
      }
      continue
    }

    // text
    cap = this.rules.text.exec(src)
    if (cap) {
      // Top-level should never reach here.
      src = src.substring(cap[0].length)
      this.tokens.push({
        type: 'text',
        text: cap[0]
      })
      continue
    }

    if (src) {
      throw new Error('Infinite loop on byte: ' + src.charCodeAt(0))
    }
  }
  // The last block took the blank lines at the end of the text with it.
  const carried = settleStep()
  if (carried) newlineRun(carried, true)
  if (pendingBefore && this.tokens[pendingBefore.index]) this.tokens[pendingBefore.index].blankLinesBefore = pendingBefore.n
}

function indentCodeCompensation(raw, text) {
  const matchIndentToCode = raw.match(/^(\s+)(?:```)/)

  if (matchIndentToCode === null) {
    return text
  }

  const indentToCode = matchIndentToCode[1]

  return text
    .split('\n')
    .map(node => {
      const matchIndentInNode = node.match(/^\s+/)
      if (matchIndentInNode === null) {
        return node
      }

      const [indentInNode] = matchIndentInNode

      if (indentInNode.length >= indentToCode.length) {
        return node.slice(indentToCode.length)
      }

      return node
    })
    .join('\n')
}

export default Lexer
