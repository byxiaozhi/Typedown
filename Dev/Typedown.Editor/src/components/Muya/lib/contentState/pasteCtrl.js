
import { PARAGRAPH_TYPES, PREVIEW_DOMPURIFY_CONFIG, HAS_TEXT_BLOCK_REG, IMAGE_EXT_REG, URL_REG } from '../config'
import { sanitize, getUniqueId, getImageInfo as getImageSrc, getPageTitle } from '../utils'
import { getImageInfo } from '../utils/getImageInfo'
import { dedentPastedText } from '../utils/pastedText'

const LIST_REG = /ul|ol/
const LINE_BREAKS_REG = /\n/

const pasteCtrl = ContentState => {
  // check paste type: `MERGE` or `NEWLINE`
  ContentState.prototype.checkPasteType = function (start, fragment, fragmentCount = 1) {
    const fragmentType = fragment.type
    const parent = this.getParent(start)

    if (fragmentType === 'p') {
      // Several blocks (a web page) pasted on a heading's line start below it: merged, the page's first paragraph or
      // picture became part of the heading ("# Title![picture](...)"). A few words pasted into a heading still join it.
      if (fragmentCount > 1 && parent && /^h\d$/.test(parent.type)) return 'NEWLINE'
      return 'MERGE'
    } else if (/^h\d/.test(fragmentType)) {
      // A heading is a block of its own: merged into the line the cursor is on, its marker came in as text ("# Title##
      // Pasted"). The text before the cursor stays; the text after it follows the pasted blocks.
      return 'NEWLINE'
    } else if (LIST_REG.test(fragmentType)) {
      const listItem = this.getParent(parent)
      const list = listItem && listItem.type === 'li' ? this.getParent(listItem) : null
      if (list) {
        if (
          list.listType === fragment.listType &&
          listItem.bulletMarkerOrDelimiter === fragment.children[0].bulletMarkerOrDelimiter
        ) {
          return 'MERGE'
        } else {
          return 'NEWLINE'
        }
      } else {
        return 'NEWLINE'
      }
    } else {
      return 'NEWLINE'
    }
  }

  // Try to identify the data type.
  ContentState.prototype.checkCopyType = function (html, rawText) {
    let type = 'normal'
    if (!html && rawText) {
      type = 'copyAsMarkdown'
      const match = /^<([a-zA-Z\d-]+)(?=\s|>).*?>[\s\S]+?<\/([a-zA-Z\d-]+)>$/.exec(rawText.trim())
      if (match && match[1]) {
        const tag = match[1]
        if (tag === 'table' && match.length === 3 && match[2] === 'table') {
          // Try to import a single table
          const tmp = document.createElement('table')
          tmp.innerHTML = sanitize(rawText, PREVIEW_DOMPURIFY_CONFIG, false)
          if (tmp.childElementCount === 1) {
            return 'htmlToMd'
          }
        }

        // TODO: We could try to import HTML elements such as headings, text and lists to markdown for better UX.
        type = PARAGRAPH_TYPES.find(type => type === tag) ? 'copyAsHtml' : type
      }
    }
    return type
  }

  ContentState.prototype.standardizeHTML = async function (rawHtml) {
    // The page the HTML was copied from: Windows puts it in the clipboard's HTML header (CF_HTML's SourceURL).
    const sourceMatch = /^SourceURL:(\S+)/m.exec(rawHtml || '')
    const sourceUrl = sourceMatch ? sourceMatch[1] : null

    // Only extract the `body.innerHTML` when the `html` is a full HTML Document.
    if (/<body[^>]*>[\s\S]*<\/body>/.test(rawHtml)) {
      const match = /<body[^>]*>([\s\S]*)<\/body>/.exec(rawHtml)
      if (match && typeof match[1] === 'string') {
        rawHtml = match[1]
      }
    }

    // What web pages put around their content, made plain before it is sanitized (which drops the attributes this
    // reads). Parsed inert: nothing in it runs or loads.
    rawHtml = tidyWebHtml(rawHtml, sourceUrl)

    // Prevent XSS and sanitize HTML.
    const sanitizedHtml = sanitize(rawHtml, PREVIEW_DOMPURIFY_CONFIG, false)
    const tempWrapper = document.createElement('div')
    tempWrapper.innerHTML = sanitizedHtml

    // Special process for turndown.js, needed for Number app on macOS.
    const tables = Array.from(tempWrapper.querySelectorAll('table'))
    for (const table of tables) {
      const row = table.querySelector('tr')
      if (row.firstElementChild.tagName !== 'TH') {
        [...row.children].forEach(cell => {
          const th = document.createElement('th')
          th.innerHTML = cell.innerHTML
          cell.replaceWith(th)
        })
      }
      const paragraphs = Array.from(table.querySelectorAll('p'))
      for (const p of paragraphs) {
        const span = document.createElement('span')
        span.innerHTML = p.innerHTML
        p.replaceWith(span)
      }

      const tds = table.querySelectorAll('td')
      for (const td of tds) {
        const tableDataHtml  = td.innerHTML
        if (/<br>/.test(tableDataHtml)) {
          td.innerHTML = tableDataHtml.replace(/<br>/g, '&lt;br&gt;')
        }
      }
    }

    // Prevent it parse into a link if copy a url.
    const links = Array.from(tempWrapper.querySelectorAll('a'))
    for (const link of links) {
      const href = link.getAttribute('href')
      const text = link.textContent

      if (URL_REG.test(href) && href === text) {
        const title = await getPageTitle(href)
        if (title) {
          link.innerHTML = sanitize(title, PREVIEW_DOMPURIFY_CONFIG, true)
        } else {
          const span = document.createElement('span')
          span.innerHTML = text
          link.replaceWith(span)
        }
      }
    }
    return tempWrapper.innerHTML
  }

  ContentState.prototype.pasteImage = async function (event) {
    // Try to guess the clipboard file path.
    const imagePath = this.muya.options.clipboardFilePath()
    if (imagePath && typeof imagePath === 'string' && IMAGE_EXT_REG.test(imagePath)) {
      const id = `loading-${getUniqueId()}`
      if (this.selectedImage) {
        this.replaceImage(this.selectedImage, {
          alt: id,
          src: imagePath
        })
      } else {
        this.insertImage({
          alt: id,
          src: imagePath
        })
      }

      let newSrc = null
      try {
        newSrc = await this.muya.options.imageAction(imagePath, id)
      } catch (error) {
        // TODO: Notify user about an error.
        console.error('Unexpected error on image action:', error)
        return null
      }

      const { src } = getImageSrc(imagePath)
      if (src) {
        this.stateRender.urlMap.set(newSrc, src)
      }

      const imageWrapper = this.muya.container.querySelector(`span[data-id=${id}]`)

      if (imageWrapper) {
        const imageInfo = getImageInfo(imageWrapper)
        this.replaceImage(imageInfo, {
          src: newSrc
        })
      }
      return imagePath
    }

    const items = event?.clipboardData && event?.clipboardData.items
    let file = null
    if (items && items.length) {
      for (let i = 0; i < items.length; i++) {
        if (items[i].type.indexOf('image') !== -1) {
          file = items[i].getAsFile()
          break
        }
      }
    }

    // handle paste to create inline image
    if (file) {
      const id = `loading-${getUniqueId()}`
      if (this.selectedImage) {
        this.replaceImage(this.selectedImage, {
          alt: id,
          src: ''
        })
      } else {
        this.insertImage({
          alt: id,
          src: ''
        })
      }

      const reader = new FileReader()
      reader.onload = event => {
        const base64 = event?.target.result
        const imageWrapper = this.muya.container.querySelector(`span[data-id=${id}]`)
        const imageContainer = this.muya.container.querySelector(`span[data-id=${id}] .ag-image-container`)
        this.stateRender.urlMap.set(id, base64)
        if (imageContainer) {
          imageWrapper.classList.remove('ag-empty-image')
          imageWrapper.classList.add('ag-image-success')
          const image = document.createElement('img')
          image.src = base64
          imageContainer.appendChild(image)
        }
      }
      reader.readAsDataURL(file)

      let newSrc = null
      try {
        newSrc = await this.muya.options.imageAction(file, id)
      } catch (error) {
        // TODO: Notify user about an error.
        console.error('Unexpected error on image action:', error)
        return null
      }

      const base64 = this.stateRender.urlMap.get(id)
      if (base64) {
        this.stateRender.urlMap.set(newSrc, base64)
        this.stateRender.urlMap.delete(id)
      }
      const imageWrapper = this.muya.container.querySelector(`span[data-id=${id}]`)

      if (imageWrapper) {
        const imageInfo = getImageInfo(imageWrapper)
        this.replaceImage(imageInfo, {
          src: newSrc
        })
      }
      return file
    }
    return null
  }

  // Handle global events.
  ContentState.prototype.docPasteHandler = async function (event) {
    // TODO: Pasting into CodeMirror will not work for special data like images
    // or tables (HTML) because it's not handled.

    // const file = await this.pasteImage(event)
    // if (file) {
    //   return event?.preventDefault()
    // }

    // if (this.selectedTableCells) {
    //   const { start } = this.cursor
    //   const startBlock = this.getBlock(start.key)
    //   const { selectedTableCells: stc } = this

    //   // Exactly one table cell is selected. Replace the cells text via default handler.
    //   if (startBlock && startBlock.functionType === 'cellContent' && stc.row === 1 && stc.column === 1) {
    //     this.pasteHandler(event)
    //     return event?.preventDefault()
    //   }
    // }
  }

  // Handle `normal` and `pasteAsPlainText` paste for preview mode.
  ContentState.prototype.pasteHandler = async function (type = 'normal', text, html) {

    // Support pasted URLs from Firefox.
    if (URL_REG.test(text) && !/\s/.test(text) && !html) {
      html = `<a href="${text}">${text}</a>`
    }
    if (!text && !html) {
      return
    }

    // Remove crap from HTML such as meta data and styles and sanitize HTML,
    // but `text` may still contain dangerous HTML.
    html = await this.standardizeHTML(html)

    let copyType = this.checkCopyType(html, text)
    const { start, end } = this.cursor
    const startBlock = this.getBlock(start.key)
    const endBlock = this.getBlock(end.key)
    const parent = this.getParent(startBlock)

    if (copyType === 'htmlToMd') {
      html = sanitize(text, PREVIEW_DOMPURIFY_CONFIG, false)
      copyType = 'normal'
    }

    if (start.key !== end.key) {
      this.cutHandler()
      return this.pasteHandler(type, text, html)
    }

    // NOTE: We should parse HTML if we can and use it instead the image (see GH#1271).
    // if (!html) {
    //   const file = await this.pasteImage(event)
    //   if (file) {
    //     return
    //   }
    // }

    const appendHtml = (text) => {
      startBlock.text = startBlock.text.substring(0, start.offset) + text + startBlock.text.substring(start.offset)
      const { key } = start
      const offset = start.offset + text.length
      this.cursor = {
        start: { key, offset },
        end: { key, offset }
      }
    }

    // Prepare paste
    if (startBlock.type === 'span' && startBlock.functionType === 'languageInput') {
      // Extract the first line from the language identifier (GH#553)
      let language = text.trim().match(/^.*$/m)[0] || ''
      const oldLanguageLength = startBlock.text.length
      let offset = 0
      if (start.offset !== 0 || end.offset !== oldLanguageLength) {
        const prePartText = startBlock.text.substring(0, start.offset)
        const postPartText = startBlock.text.substring(end.offset)

        // Expect that the language doesn't contain new lines
        language = prePartText + language + postPartText
        offset = prePartText.length + language.length
      } else {
        offset = language.length
      }

      startBlock.text = language

      const key = startBlock.key
      this.cursor = {
        start: { key, offset },
        end: { key, offset }
      }

      // Hide code picker float box
      const { eventCenter } = this.muya
      eventCenter.dispatch('muya-code-picker', { reference: null })

      // Update code block language and render
      this.updateCodeLanguage(startBlock, language)
      return
    }

    if (startBlock.type === 'span' && startBlock.functionType === 'codeContent') {
      const blockText = startBlock.text
      const prePartText = blockText.substring(0, start.offset)
      const postPartText = blockText.substring(end.offset)
      startBlock.text = prePartText + text + postPartText
      const { key } = startBlock
      const offset = start.offset + text.length
      this.cursor = {
        start: { key, offset },
        end: { key, offset }
      }

      return this.partialRender()
    }

    if (startBlock.functionType === 'cellContent') {
      let isOneCellSelected = false
      if (this.selectedTableCells) {
        const { selectedTableCells: stc } = this
        // Replace cells text when one cell is selected.
        if (stc.row === 1 && stc.column === 1) {
          isOneCellSelected = true
        } else {
          // Cancel event, multiple cells are selected.
          return this.partialRender()
        }
      }

      const { key } = startBlock
      const pendingText = text.trim().replace(/\n/g, '<br/>')
      let offset = pendingText.length
      if (isOneCellSelected) {
        // Replace text and deselect cell.
        startBlock.text = pendingText
        this.selectedTableCells = null
      } else {
        offset += start.offset
        startBlock.text = startBlock.text.substring(0, start.offset) + pendingText + startBlock.text.substring(end.offset)
      }

      this.cursor = {
        start: { key, offset },
        end: { key, offset }
      }
      return this.partialRender()
    }

    // Handle paste event and transform data into internal block structure.
    if (copyType === 'copyAsHtml') {
      switch (type) {
        case 'normal': {
          const htmlBlock = this.createBlockP(text.trim())
          this.insertAfter(htmlBlock, parent)
          this.removeBlock(parent)
          // handler heading
          this.insertHtmlBlock(htmlBlock)
          break
        }
        case 'pasteAsPlainText': {
          const lines = text.trim().split(LINE_BREAKS_REG)
          let htmlBlock = null

          if (!startBlock.text || lines.length > 1) {
            htmlBlock = this.createBlockP((startBlock.text ? lines.slice(1) : lines).join('\n'))
          }
          if (htmlBlock) {
            this.insertAfter(htmlBlock, parent)
            this.insertHtmlBlock(htmlBlock)
          }
          if (startBlock.text) {
            appendHtml(lines[0])
          } else {
            this.removeBlock(parent)
          }
          break
        }
      }
      return this.partialRender()
    }

    const stateFragments = type === 'pasteAsPlainText' || copyType === 'copyAsMarkdown'
      ? this.markdownToState(dedentPastedText(text))
      : this.html2State(html)

    if (stateFragments.length <= 0) {
      return
    }

    // Step 1: if select content, cut the content, and chop the block text into two part by the cursor.
    const cacheText = endBlock.text.substring(end.offset)
    startBlock.text = startBlock.text.substring(0, start.offset)

    // Step 2: when insert the fragments, check begin a new block, or insert into pre block.
    const firstFragment = stateFragments[0]
    const tailFragments = stateFragments.slice(1)
    const pasteType = this.checkPasteType(startBlock, firstFragment, stateFragments.length)

    const getLastBlock = blocks => {
      const len = blocks.length
      const lastBlock = blocks[len - 1]

      if (lastBlock.children.length === 0 && HAS_TEXT_BLOCK_REG.test(lastBlock.type)) {
        return lastBlock
      } else {
        if (lastBlock.editable === false) {
          return getLastBlock(blocks[len - 2].children)
        } else {
          return getLastBlock(lastBlock.children)
        }
      }
    }

    const lastBlock = getLastBlock(stateFragments)
    let key = lastBlock.key
    let offset = lastBlock.text.length
    lastBlock.text += cacheText

    switch (pasteType) {
      case 'MERGE': {
        if (LIST_REG.test(firstFragment.type)) {
          const listItems = firstFragment.children
          const firstListItem = listItems[0]
          const liChildren = firstListItem.children
          const originListItem = this.getParent(parent)
          const originList = this.getParent(originListItem)
          const targetListType = firstFragment.children[0].isLooseListItem
          const originListType = originList.children[0].isLooseListItem
          // No matter copy loose list to tight list or vice versa, the result is one loose list.
          if (targetListType !== originListType) {
            if (!targetListType) {
              firstFragment.children.forEach(item => (item.isLooseListItem = true))
            } else {
              originList.children.forEach(item => (item.isLooseListItem = true))
            }
          }

          if (liChildren[0].type === 'p') {
            // TODO @JOCS
            startBlock.text += liChildren[0].children[0].text
            const tail = liChildren.slice(1)
            if (tail.length) {
              tail.forEach(t => {
                this.appendChild(originListItem, t)
              })
            }
            const firstFragmentTail = listItems.slice(1)
            if (firstFragmentTail.length) {
              firstFragmentTail.forEach(t => {
                this.appendChild(originList, t)
              })
            }
          } else {
            listItems.forEach(c => {
              this.appendChild(originList, c)
            })
          }
          let target = originList
          tailFragments.forEach(block => {
            this.insertAfter(block, target)
            target = block
          })
        } else if (firstFragment.type === 'p' || /^h\d/.test(firstFragment.type)) {
          const text = firstFragment.children[0].text
          const lines = text.split('\n')
          let target = parent
          if (parent.headingStyle === 'atx') {
            startBlock.text += lines[0]
            if (lines.length > 1) {
              const pBlock = this.createBlockP(lines.slice(1).join('\n'))
              this.insertAfter(parent, pBlock)
              target = pBlock
            }
          } else {
            startBlock.text += text
          }

          tailFragments.forEach(block => {
            this.insertAfter(block, target)
            target = block
          })
        }
        break
      }
      case 'NEWLINE': {
        let target = parent
        stateFragments.forEach(block => {
          this.insertAfter(block, target)
          target = block
        })
        if (startBlock.text.length === 0) {
          this.removeBlock(parent)
        }
        break
      }
      default: {
        throw new Error('unknown paste type')
      }
    }

    // Step 3: set cursor and render
    let cursorBlock = this.getBlock(key)
    if (!cursorBlock) {
      key = startBlock.key
      offset = startBlock.text.length - cacheText.length
      cursorBlock = startBlock
    }

    this.cursor = {
      start: {
        key, offset
      },
      end: {
        key, offset
      }
    }
    this.checkInlineUpdate(cursorBlock)
    this.partialRender()
    return this.muya.dispatchChange()
  }
}

export default pasteCtrl

// ---- pasted web pages ----

// A picture's address in a page that loads its pictures late: the real one is in an attribute, the src a placeholder.
const LAZY_SRC_ATTRIBUTES = ['data-original', 'data-actualsrc', 'data-src', 'data-lazy-src', 'data-original-src', 'data-croporisrc']
const isPlaceholderSrc = src => !src || /^data:image\/(svg\+xml|gif)/i.test(src) || /(^|\/)(blank|placeholder|loading|spacer|pixel)\.(gif|png|svg)$/i.test(src)

// The language of a code block, from the class names pages use: language-x, lang-x, brush: x, highlight-source-x.
const codeLanguage = element => {
  for (const node of [element, ...element.querySelectorAll('code')]) {
    const names = `${node.getAttribute('class') || ''} ${node.getAttribute('data-lang') || ''}`
    const m = /(?:^|\s)(?:language|lang)-([\w#+-]+)/.exec(names) || /brush:\s*([\w#+-]+)/.exec(names) ||
      /highlight-source-([\w#+-]+)/.exec(names) || /(?:^|\s)data-lang\s*([\w#+-]+)/.exec(names)
    if (m) return m[1].toLowerCase()
    const data = node.getAttribute('data-lang')
    if (data) return data.toLowerCase()
  }
  return ''
}

/**
 * HTML copied from a web page or an online editor, made into what the Markdown conversion understands:
 * - Google Docs: the <b style="font-weight:normal"> around everything unwrapped; bold, italic and struck-out text given
 *   by style made <strong>, <em>, <del>.
 * - pictures loaded late: the real address instead of the placeholder (data-original, data-src..., srcset, <noscript>).
 * - code blocks: their line numbers taken out (a list or a table column beside the code), the language kept, a block of
 *   lines one <pre><code> however the page split it.
 * - tables: a cell's alignment given by style made the align attribute the conversion reads.
 * - addresses relative to the page (/wiki/..., ../img.png) made absolute with the page's own address.
 */
export const tidyWebHtml = (html, sourceUrl) => {
  if (!html) return html
  const doc = new DOMParser().parseFromString(`<!DOCTYPE html><html><body>${html}</body></html>`, 'text/html')
  const body = doc.body

  // Google Docs.
  for (const wrapper of Array.from(body.querySelectorAll('b[id^="docs-internal-guid"]'))) {
    if (/font-weight:\s*normal/i.test(wrapper.getAttribute('style') || '')) wrapper.replaceWith(...wrapper.childNodes)
  }
  for (const span of Array.from(body.querySelectorAll('span[style]'))) {
    const style = span.getAttribute('style')
    let node = span
    const wrap = tag => {
      const outer = doc.createElement(tag)
      node.replaceWith(outer)
      outer.appendChild(node)
      node = outer
    }
    if (/font-weight:\s*(bold|[6-9]00)/i.test(style)) wrap('strong')
    if (/font-style:\s*italic/i.test(style)) wrap('em')
    if (/text-decoration[^;]*line-through/i.test(style)) wrap('del')
  }

  // Pictures loaded late.
  for (const img of Array.from(body.querySelectorAll('img'))) {
    const src = img.getAttribute('src')
    if (!isPlaceholderSrc(src)) continue
    let real = LAZY_SRC_ATTRIBUTES.map(a => img.getAttribute(a)).find(Boolean)
    if (!real) {
      const set = img.getAttribute('data-srcset') || img.getAttribute('srcset')
      if (set) real = set.split(',').map(s => s.trim().split(/\s+/)[0]).filter(Boolean).pop()
    }
    if (real) img.setAttribute('src', real)
  }
  for (const noscript of Array.from(body.querySelectorAll('noscript'))) {
    // The picture a page shows without scripts: kept when the one beside it is a placeholder, else dropped.
    const inner = new DOMParser().parseFromString(noscript.textContent || noscript.innerHTML, 'text/html').querySelector('img')
    const sibling = noscript.parentElement && noscript.parentElement.querySelector('img')
    if (inner && (!sibling || isPlaceholderSrc(sibling.getAttribute('src')))) {
      if (sibling) sibling.remove()
      noscript.replaceWith(doc.importNode(inner, true))
    } else {
      noscript.remove()
    }
  }

  // Code blocks: line numbers out, one <pre><code class="language-x"> each.
  for (const numbers of Array.from(body.querySelectorAll('.pre-numbering, .line-numbers-rows, .hljs-ln-numbers, .linenodiv, td.gutter, .gutter, .lineno, .line-numbers'))) {
    if (numbers.closest('pre') || numbers.closest('table')) numbers.remove()
  }
  for (const pre of Array.from(body.querySelectorAll('pre'))) {
    const language = codeLanguage(pre) || (pre.parentElement ? codeLanguage(pre.parentElement) : '')
    // A table of lines (hljs-ln, some blogs): each row's code cell is a line.
    const rows = pre.querySelectorAll('tr')
    let text
    if (rows.length) {
      text = Array.from(rows).map(row => (row.querySelector('.hljs-ln-code, td.code, td:last-child') || row).textContent).join('\n')
    } else {
      // A <br> or a block per line is a line break.
      for (const br of Array.from(pre.querySelectorAll('br'))) br.replaceWith('\n')
      for (const line of Array.from(pre.querySelectorAll('div, p, li'))) line.append('\n')
      text = pre.textContent.replace(/\n$/, '')
    }
    const fresh = doc.createElement('pre')
    const code = doc.createElement('code')
    if (language) code.className = `language-${language}`
    code.textContent = text
    fresh.appendChild(code)
    pre.replaceWith(fresh)
  }

  // Table cells aligned by style.
  for (const cell of Array.from(body.querySelectorAll('th, td'))) {
    const m = /text-align:\s*(left|center|right)/i.exec(cell.getAttribute('style') || '')
    if (m && !cell.getAttribute('align')) cell.setAttribute('align', m[1].toLowerCase())
  }

  // Addresses relative to the page.
  if (sourceUrl && /^https?:/i.test(sourceUrl)) {
    for (const [selector, attribute] of [['a[href]', 'href'], ['img[src]', 'src']]) {
      for (const element of Array.from(body.querySelectorAll(selector))) {
        const value = element.getAttribute(attribute)
        if (!value || /^(#|[a-z][a-z0-9+.-]*:)/i.test(value)) continue
        try { element.setAttribute(attribute, new URL(value, sourceUrl).href) } catch (e) { /* left as it is */ }
      }
    }
  }
  return body.innerHTML
}
