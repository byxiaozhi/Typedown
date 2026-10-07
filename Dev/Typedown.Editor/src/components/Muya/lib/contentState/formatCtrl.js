import selection from '../selection'
import { tokenizer, generator } from '../parser/'
import { FORMAT_MARKER_MAP, FORMAT_TYPES } from '../config'
import { getImageInfo } from '../utils/getImageInfo'

const getOffset = (offset, { range: { start, end }, type, tag, anchor, alt }) => {
  const dis = offset - start
  const len = end - start
  switch (type) {
    case 'strong':
    case 'del':
    case 'em':
    case 'inline_code':
    case 'inline_math': {
      const MARKER_LEN = (type === 'strong' || type === 'del') ? 2 : 1
      if (dis < 0) return 0
      if (dis >= 0 && dis < MARKER_LEN) return -dis
      if (dis >= MARKER_LEN && dis <= len - MARKER_LEN) return -MARKER_LEN
      if (dis > len - MARKER_LEN && dis <= len) return len - dis - 2 * MARKER_LEN
      if (dis > len) return -2 * MARKER_LEN
      break
    }
    case 'html_tag': { // handle underline, sup, sub
      const OPEN_MARKER_LEN = FORMAT_MARKER_MAP[tag].open.length
      const CLOSE_MARKER_LEN = FORMAT_MARKER_MAP[tag].close.length
      if (dis < 0) return 0
      if (dis >= 0 && dis < OPEN_MARKER_LEN) return -dis
      if (dis >= OPEN_MARKER_LEN && dis <= len - CLOSE_MARKER_LEN) return -OPEN_MARKER_LEN
      if (dis > len - CLOSE_MARKER_LEN && dis <= len) return len - dis - OPEN_MARKER_LEN - CLOSE_MARKER_LEN
      if (dis > len) return -OPEN_MARKER_LEN - CLOSE_MARKER_LEN
      break
    }
    case 'link': {
      const MARKER_LEN = 1
      if (dis < MARKER_LEN) return 0
      if (dis >= MARKER_LEN && dis <= MARKER_LEN + anchor.length) return -1
      if (dis > MARKER_LEN + anchor.length) return anchor.length - dis
      break
    }
    case 'image': {
      const MARKER_LEN = 1
      if (dis < MARKER_LEN) return 0
      if (dis >= MARKER_LEN && dis < MARKER_LEN * 2) return -1
      if (dis >= MARKER_LEN * 2 && dis <= MARKER_LEN * 2 + alt.length) return -2
      if (dis > MARKER_LEN * 2 + alt.length) return alt.length - dis
      break
    }
  }
}

const clearFormat = (token, { start, end }) => {
  if (start) {
    const deltaStart = getOffset(start.offset, token)
    start.delata += deltaStart
  }
  if (end) {
    const delataEnd = getOffset(end.offset, token)
    end.delata += delataEnd
  }
  switch (token.type) {
    case 'strong':
    case 'del':
    case 'em':
    case 'link':
    case 'html_tag': { // underline, sub, sup
      const { parent } = token
      const index = parent.indexOf(token)
      parent.splice(index, 1, ...token.children)
      break
    }
    case 'image': {
      token.type = 'text'
      token.raw = token.alt
      delete token.marker
      delete token.src
      break
    }
    case 'inline_math':
    case 'inline_code': {
      token.type = 'text'
      token.raw = token.content
      delete token.marker
      break
    }
  }
}

// The text of a line is written from the `raw` of its tokens. A token inside another one (the `*` of `***text***`)
// cannot be taken out without the outer `raw` following, so the part around the inner text of each token is kept
// after tokenizing and the text is written again from the tokens as they are now.
const keepFrames = tokens => {
  for (const token of tokens) {
    if (!Array.isArray(token.children) || !token.children.length) continue
    const inner = generator(token.children)
    const prefix = token.children[0].range.start - token.range.start
    if (prefix >= 0 && token.raw.substr(prefix, inner.length) === inner) {
      token.frame = { prefix: token.raw.substring(0, prefix), suffix: token.raw.substring(prefix + inner.length) }
    }
    keepFrames(token.children)
  }
}

const regenerate = tokens => tokens.map(token => {
  return token.frame && Array.isArray(token.children)
    ? token.frame.prefix + regenerate(token.children) + token.frame.suffix
    : token.raw
}).join('')

const addFormat = (type, block, { start, end }) => {
  if (
    block.type !== 'span' ||
    (block.type === 'span' && !/paragraphContent|cellContent|atxLine/.test(block.functionType))
  ) {
    return false
  }
  switch (type) {
    case 'em':
    case 'del':
    case 'inline_code':
    case 'strong':
    case 'inline_math': {
      const MARKER = FORMAT_MARKER_MAP[type]
      const oldText = block.text
      block.text = oldText.substring(0, start.offset) +
        MARKER + oldText.substring(start.offset, end.offset) +
        MARKER + oldText.substring(end.offset)
      start.offset += MARKER.length
      end.offset += MARKER.length
      break
    }
    case 'sub':
    case 'sup':
    case 'mark':
    case 'u': {
      const MARKER = FORMAT_MARKER_MAP[type]
      const oldText = block.text
      block.text = oldText.substring(0, start.offset) +
        MARKER.open + oldText.substring(start.offset, end.offset) +
        MARKER.close + oldText.substring(end.offset)
      start.offset += MARKER.open.length
      end.offset += MARKER.open.length
      break
    }
    case 'link':
    case 'image': {
      const oldText = block.text
      const anchorTextLen = end.offset - start.offset
      block.text = oldText.substring(0, start.offset) +
        (type === 'link' ? '[' : '![') +
        oldText.substring(start.offset, end.offset) + ']()' +
        oldText.substring(end.offset)
      // put cursor between `()`
      start.offset += type === 'link' ? 3 + anchorTextLen : 4 + anchorTextLen
      end.offset = start.offset
      break
    }
  }
}

// The part of the line between the two offsets without the spaces at its edges and without the `##` of a heading:
// a format around them would not show (`** word **` is no bold text).
const trimToText = (block, from, to) => {
  const { text } = block
  const prefix = block.functionType === 'atxLine' ? /^ {0,3}#{1,6}[ \u00A0]*/.exec(text) : null
  from = Math.max(from, prefix ? prefix[0].length : 0)
  to = Math.min(to, text.length)
  while (from < to && /\s/.test(text[from])) from++
  while (to > from && /\s/.test(text[to - 1])) to--
  // the closing hashes of `# Title #` are a marker too (the parser's `tail_header`), not text to format
  const tail = block.functionType === 'atxLine' ? /\s+#+\s*$/.exec(text) : null
  if (tail) to = Math.min(to, tail.index)
  return [from, to]
}

const checkTokenIsInlineFormat = token => {
  const { type, tag } = token
  if (FORMAT_TYPES.includes(type)) return true
  if (type === 'html_tag' && /^(?:u|sub|sup|mark|img)$/i.test(tag)) return true
  return false
}

// When a format starts or ends exactly at the edge of the range (a whole line of `**text**`, or of `***text***`), the
// range is moved in to the text inside it, so that the formats around count as covering the range.
const insideFormats = (tokens, from, to) => {
  const all = []
  ;(function collect (list) {
    for (const token of list) {
      all.push(token)
      if (Array.isArray(token.children)) collect(token.children)
    }
  })(tokens)
  let moved = true
  while (moved) {
    moved = false
    for (const token of all) {
      if (!token.frame || !checkTokenIsInlineFormat(token)) continue
      const innerStart = token.range.start + token.frame.prefix.length
      const innerEnd = token.range.end - token.frame.suffix.length
      if (token.range.start === from && innerStart < to) {
        from = innerStart
        moved = true
      }
      if (token.range.end === to && innerEnd > from) {
        to = innerEnd
        moved = true
      }
    }
  }
  return [from, to]
}

const formatCtrl = ContentState => {
  ContentState.prototype.selectionFormats = function ({ start, end } = selection.getCursorRange()) {
    if (!start || !end) {
      return { formats: [], tokens: [], neighbors: [] }
    }

    const startBlock = this.getBlock(start.key)
    const formats = []
    const neighbors = []
    let tokens = []
    if (start.key === end.key) {
      const { text } = startBlock
      tokens = tokenizer(text, {
        options: this.muya.options
      })
      keepFrames(tokens)
      ;(function iterator (tks) {
        for (const token of tks) {
          if (
            checkTokenIsInlineFormat(token) &&
            start.offset >= token.range.start &&
            end.offset <= token.range.end
          ) {
            formats.push(token)
          }
          if (
            checkTokenIsInlineFormat(token) &&
            ((start.offset >= token.range.start && start.offset <= token.range.end) ||
            (end.offset >= token.range.start && end.offset <= token.range.end) ||
            (start.offset <= token.range.start && token.range.end <= end.offset))
          ) {
            neighbors.push(token)
          }
          if (token.children && token.children.length) {
            iterator(token.children)
          }
        }
      })(tokens)
    }
    
    return { formats, tokens, neighbors }
  }

  ContentState.prototype.clearBlockFormat = function (block, { start, end } = selection.getCursorRange(), type) {
    if (!start || !end) {
      return
    }
    if (block.type === 'pre') return false
    const { key } = block
    let tokens
    let neighbors
    if (start.key === end.key && start.key === key) {
      ({ tokens, neighbors } = this.selectionFormats({ start, end }))
    } else if (start.key !== end.key && start.key === key) {
      ({ tokens, neighbors } = this.selectionFormats({ start, end: { key: start.key, offset: block.text.length } }))
    } else if (start.key !== end.key && end.key === key) {
      ({ tokens, neighbors } = this.selectionFormats({
        start: {
          key: end.key,
          offset: 0
        },
        end
      }))
    } else {
      ({ tokens, neighbors } = this.selectionFormats({
        start: {
          key,
          offset: 0
        },
        end: {
          key,
          offset: block.text.length
        }
      }))
    }

    neighbors = type
      ? neighbors.filter(n => {
        return n.type === type ||
        n.type === 'html_tag' && n.tag === type
      })
      : neighbors

    // inner formats first: a format taken out of a token that is gone already is not taken out of the line
    for (const neighbor of [...neighbors].reverse()) {
      clearFormat(neighbor, { start, end })
    }
    start.offset += start.delata
    end.offset += end.delata
    block.text = regenerate(tokens)
  }

  ContentState.prototype.format = function (type) {
    const { start, end } = selection.getCursorRange()
    if (!start || !end) {
      return
    }

    const startBlock = this.getBlock(start.key)
    const endBlock = this.getBlock(end.key)
    start.delata = end.delata = 0
    if (start.key === end.key) {
      const { formats, tokens, neighbors } = this.selectionFormats()
      const currentFormats = formats.filter(format => {
        return format.type === type ||
          format.type === 'html_tag' && format.tag === type
      }).reverse()
      const currentNeightbors = neighbors.filter(format => {
        return format.type === type ||
        format.type === 'html_tag' && format.tag === type
      }).reverse()
      // cache delata
      if (type === 'clear') {
        // inner formats first, see clearBlockFormat
        for (const neighbor of [...neighbors].reverse()) {
          clearFormat(neighbor, { start, end })
        }
        start.offset += start.delata
        end.offset += end.delata
        startBlock.text = regenerate(tokens)
      } else if (currentFormats.length) {
        for (const token of currentFormats) {
          clearFormat(token, { start, end })
        }
        start.offset += start.delata
        end.offset += end.delata
        startBlock.text = regenerate(tokens)
      } else {
        if (currentNeightbors.length) {
          for (const neighbor of currentNeightbors) {
            clearFormat(neighbor, { start, end })
          }
        }
        start.offset += start.delata
        end.offset += end.delata
        startBlock.text = regenerate(tokens)
        addFormat(type, startBlock, { start, end })
        if (type === 'image') {
          // Show image selector when create a inline image by menu/shortcut/or just input `![]()`
          requestAnimationFrame(() => {
            const startNode = selection.getSelectionStart()
            if (startNode) {
              const imageWrapper = startNode.closest('.ag-inline-image')
              if (imageWrapper && imageWrapper.classList.contains('ag-empty-image')) {
                const imageInfo = getImageInfo(imageWrapper)
                this.muya.eventCenter.dispatch('muya-image-selector', {
                  reference: imageWrapper,
                  imageInfo,
                  cb: () => {}
                })
              }
            }
          })
        }
      }
      this.cursor = { start, end }
      this.partialRender()
      // the host keeps its own copy of the text: without this it has the old text until the next click or key
      this.muya.dispatchChange()
    } else {
      // The selection covers several blocks (lines). Every line takes the same steps as a selection inside one block,
      // on its own part of the selection: from the start to the end of the first line, whole lines in between, and the
      // beginning of the last line up to the end.
      const parts = []
      for (let next = startBlock; next; next = this.findNextBlockInLocation(next)) {
        if (next.type === 'span' && /paragraphContent|cellContent|atxLine/.test(next.functionType)) {
          const [from, to] = trimToText(next, next === startBlock ? start.offset : 0, next === endBlock ? end.offset : next.text.length)
          // empty lines and lines of spaces are left alone, they would only get the bare markers
          if (from < to) {
            const edges = { start: { key: next.key, offset: from, delata: 0 }, end: { key: next.key, offset: to, delata: 0 } }
            const [innerFrom, innerTo] = insideFormats(this.selectionFormats(edges).tokens, from, to)
            const range = { start: { key: next.key, offset: innerFrom, delata: 0 }, end: { key: next.key, offset: innerTo, delata: 0 } }
            parts.push({ block: next, ...range, ...this.selectionFormats(range) })
          }
        }
        if (next === endBlock) break
      }
      if (!parts.length) {
        return
      }

      const isType = format => format.type === type || (format.type === 'html_tag' && format.tag === type)
      // like inside one block, a format that already covers every line is taken off, otherwise it is put on every line
      const remove = type !== 'clear' && parts.every(part => part.formats.some(isType))
      for (const { block, start: from, end: to, formats, tokens, neighbors } of parts) {
        // inner formats first, see clearBlockFormat
        const affected = (type === 'clear' ? [...neighbors] : (remove ? formats : neighbors).filter(isType)).reverse()
        for (const token of affected) {
          clearFormat(token, { start: from, end: to })
        }
        from.offset += from.delata
        to.offset += to.delata
        block.text = regenerate(tokens)
        if (type !== 'clear' && !remove) {
          addFormat(type, block, { start: from, end: to })
        }
      }

      const first = parts[0]
      const last = parts[parts.length - 1]
      // drawn with the cursor over all the lines changed, so that every one of them is drawn again
      this.cursor = { start: first.start, end: last.end }
      this.partialRender()
      if (type === 'link' || type === 'image') {
        // a link or an image leaves the cursor between the `()` of the last one; the lines were all drawn just now
        // and the next range comes from the cursor of that render
        this.cursor = { start: last.end, end: last.end }
        this.partialRender()
      }
      this.muya.dispatchChange()
    }
  }
}

export default formatCtrl
