import selection from '../selection'
import { CLASS_OR_ID } from '../config'
import { escapeHTML } from '../utils'
import ExportMarkdown from '../utils/exportMarkdown'
import marked from '../parser/marked'
import { sanitize, absoluteImageUrls } from '../utils'
import { svgImagesAsPng } from 'services/localPaths'
import { tokenizer } from '../parser'
import { EXPORT_DOMPURIFY_CONFIG } from '../config'
import remote from 'services/remote/common'
import { selectionToPlainText } from 'services/plainText'

const getSanitizeHtml = (markdown, options) => {
  const html = marked(markdown, options)
  return sanitize(html, EXPORT_DOMPURIFY_CONFIG, false)
}

const copyCutCtrl = ContentState => {
  ContentState.prototype.docCutHandler = function () {
    const { selectedTableCells } = this
    if (selectedTableCells) {
      return this.deleteSelectedTableCells(true)
    }
  }

  ContentState.prototype.cutHandler = function () {
    if (this.selectedTableCells) {
      return
    }
    const { selectedImage } = this
    if (selectedImage) {
      const { key, token } = selectedImage
      this.deleteImage({
        key,
        token
      })
      this.selectedImage = null
      return
    }
    const { start, end } = selection.getCursorRange()
    if (!start || !end) {
      return
    }
    const startBlock = this.getBlock(start.key)
    const endBlock = this.getBlock(end.key)
    startBlock.text = startBlock.text.substring(0, start.offset) + endBlock.text.substring(end.offset)
    if (start.key !== end.key) {
      this.removeBlocks(startBlock, endBlock)
    }
    this.cursor = {
      start,
      end: start
    }
    this.checkInlineUpdate(startBlock)
    this.partialRender()
    this.muya.dispatchChange()
  }

  ContentState.prototype.getClipBoardData = function () {
    const { start, end } = selection.getCursorRange()
    if (!start || !end) {
      // Reading mode's select-all selects the editor's whole content, which is no cursor range Muya knows; the
      // copy below works from the page's selection, so it goes on when there is one in the editor.
      const domSelection = window.getSelection()
      const inEditor = domSelection && !domSelection.isCollapsed && domSelection.rangeCount > 0 &&
        this.muya.container.contains(domSelection.getRangeAt(0).commonAncestorContainer)
      if (!inEditor) {
        return { html: '', text: '', plainText: '' }
      }
    } else if (start.key === end.key) {
      const startBlock = this.getBlock(start.key)
      const { type, text, functionType } = startBlock
      // Fix issue #942
      if (type === 'span' && functionType === 'codeContent') {
        const selectedText = text.substring(start.offset, end.offset)
        return {
          html: marked(selectedText, this.muya.options),
          text: selectedText,
          plainText: selectedText
        }
      }
    }
    const html = selection.getSelectionHtml()
    const wrapper = document.createElement('div')
    wrapper.innerHTML = html
    const removedElements = wrapper.querySelectorAll(
      `.${CLASS_OR_ID.AG_TOOL_BAR},
      .${CLASS_OR_ID.AG_MATH_RENDER},
      .${CLASS_OR_ID.AG_RUBY_RENDER},
      .${CLASS_OR_ID.AG_HTML_PREVIEW},
      .${CLASS_OR_ID.AG_MATH_PREVIEW},
      .${CLASS_OR_ID.AG_COPY_REMOVE},
      .${CLASS_OR_ID.AG_LANGUAGE_INPUT},
      .${CLASS_OR_ID.AG_HTML_TAG} br,
      .${CLASS_OR_ID.AG_FRONT_ICON}`
    )

    for (const e of removedElements) {
      e.remove()
    }

    // Fix #1678 copy task list, and the first list item is not task list item.
    const taskListItems = wrapper.querySelectorAll('li.ag-task-list-item')
    for (const item of taskListItems) {
      const firstChild = item.firstElementChild
      if (firstChild && firstChild.nodeName !== 'INPUT') {
        const originItem = document.querySelector(`#${item.id}`)
        let checked = false
        if (originItem && originItem.firstElementChild && originItem.firstElementChild.nodeName === 'INPUT') {
          checked = originItem.firstElementChild.checked
        }

        const input = document.createElement('input')
        input.setAttribute('type', 'checkbox')
        if (checked) {
          input.setAttribute('checked', true)
        }

        item.insertBefore(input, firstChild)
      }
    }

    // A picture is copied as its Markdown source says, not as drawn: one that did not load has no <img> and the
    // conversion to Markdown dropped it, and a drawn one has the resolved absolute path, which the lookup below did not
    // always map back (the Markdown then held C:/... for a relative picture). Pictures written as HTML keep that way.
    // The source is read by the editor's own parser: a pattern of its own missed an alt text with brackets
    // (![ja-dark[1]](...)) and a path with a space, and those were copied at the page's address (encoded, with a
    // ?msec= cache key), which Word could not load.
    for (const placeholder of wrapper.querySelectorAll('span.ag-inline-image')) {
      const raw = placeholder.getAttribute('data-raw') || ''
      let token = null
      try { token = tokenizer(raw, { options: this.muya.options })[0] } catch (e) { }
      if (!token || token.type !== 'image' || token.raw !== raw || !token.src) continue
      const img = document.createElement('img')
      img.setAttribute('alt', token.alt || '')
      img.setAttribute('src', token.src)
      if (token.title) img.setAttribute('title', token.title)
      placeholder.replaceWith(img)
    }

    const images = wrapper.querySelectorAll('span.ag-inline-image img')
    for (const image of images) {
      const src = image.getAttribute('src')
      let originSrc = null
      for (const [sSrc, tSrc] of this.stateRender.urlMap.entries()) {
        if (tSrc === src) {
          originSrc = sSrc
          break
        }
      }

      if (originSrc) {
        image.setAttribute('src', originSrc)
      }
    }

    const hrs = wrapper.querySelectorAll('[data-role=hr]')
    for (const hr of hrs) {
      hr.replaceWith(document.createElement('hr'))
    }

    const headers = wrapper.querySelectorAll('[data-head]')
    for (const header of headers) {
      const p = document.createElement('p')
      p.textContent = header.textContent
      header.replaceWith(p)
    }

    // replace inline rule element: code, a, strong, em, del, auto_link to span element
    // in order to escape turndown translation

    const inlineRuleElements = wrapper.querySelectorAll(
      `a.${CLASS_OR_ID.AG_INLINE_RULE},
      code.${CLASS_OR_ID.AG_INLINE_RULE},
      strong.${CLASS_OR_ID.AG_INLINE_RULE},
      em.${CLASS_OR_ID.AG_INLINE_RULE},
      del.${CLASS_OR_ID.AG_INLINE_RULE}`
    )
    for (const e of inlineRuleElements) {
      const span = document.createElement('span')
      span.textContent = e.textContent
      e.replaceWith(span)
    }

    const aLinks = wrapper.querySelectorAll(`.${CLASS_OR_ID.AG_A_LINK}`)
    for (const l of aLinks) {
      const span = document.createElement('span')
      span.innerHTML = l.innerHTML
      l.replaceWith(span)
    }

    const codefense = wrapper.querySelectorAll('pre[data-role$=\'code\']')
    for (const cf of codefense) {
      const id = cf.id
      const block = this.getBlock(id)
      const language = block.lang || ''
      const codeContent = cf.querySelector('.ag-code-content')
      const value = escapeHTML(codeContent.textContent)
      cf.innerHTML = `<code class="language-${language}">${value}</code>`
    }

    const tightListItem = wrapper.querySelectorAll('.ag-tight-list-item')
    for (const li of tightListItem) {
      for (const item of li.childNodes) {
        if (item.tagName === 'P' && item.childElementCount === 1 && item.classList.contains('ag-paragraph')) {
          li.replaceChild(item.firstElementChild, item)
        }
      }
    }

    const htmlBlock = wrapper.querySelectorAll('figure[data-role=\'HTML\']')
    for (const hb of htmlBlock) {
      const codeContent = hb.querySelector('.ag-code-content')
      const pre = document.createElement('pre')
      pre.textContent = codeContent.textContent
      hb.replaceWith(pre)
    }

    // Just work for turndown, turndown will add `leading` and `traling` space in line-break.
    const lineBreaks = wrapper.querySelectorAll('span.ag-soft-line-break, span.ag-hard-line-break')
    for (const b of lineBreaks) {
      b.innerHTML = ''
    }

    const mathBlock = wrapper.querySelectorAll('figure.ag-container-block')
    for (const mb of mathBlock) {
      const preElement = mb.querySelector('pre[data-role]')
      const functionType = preElement.getAttribute('data-role')
      const codeContent = mb.querySelector('.ag-code-content')
      const value = codeContent.textContent
      let pre
      switch (functionType) {
        case 'multiplemath':
          pre = document.createElement('pre')
          pre.classList.add('multiple-math')
          pre.textContent = value
          mb.replaceWith(pre)
          break
        case 'mermaid':
        case 'flowchart':
        case 'sequence':
        case 'plantuml':
        case 'vega-lite':
          pre = document.createElement('pre')
          pre.innerHTML = `<code class="language-${functionType}">${value}</code>`
          mb.replaceWith(pre)
          break
      }
    }

    let htmlData = wrapper.innerHTML
    const textData = this.htmlToMarkdown(htmlData)
    htmlData = svgImagesAsPng(absoluteImageUrls(marked(textData)))
    // What the selection reads as, without Markdown (services/plainText).
    const plainText = selectionToPlainText()

    return { html: htmlData, text: textData, plainText }
  }

  ContentState.prototype.docCopyHandler = function () {
    const { selectedTableCells } = this
    if (selectedTableCells) {
      const { row, column, cells } = selectedTableCells
      const tableContents = []
      let i
      let j
      for (i = 0; i < row; i++) {
        const rowWrapper = []
        for (j = 0; j < column; j++) {
          const cell = cells[i * column + j]

          rowWrapper.push({
            text: cell.text,
            align: cell.align
          })
        }
        tableContents.push(rowWrapper)
      }

      if (row === 1 && column === 1) {
        // Copy cells text if only one is selected
        remote.setClipboard({ type: 'text/html', data: '' })
        remote.setClipboard({ type: 'text/plain', data: tableContents[0][0].text })
      } else {
        // Copy as markdown table
        const figureBlock = this.createBlock('figure', {
          functionType: 'table'
        })
        const table = this.createTableInFigure({ rows: row, columns: column }, tableContents)
        this.appendChild(figureBlock, table)
        const { isGitlabCompatibilityEnabled, listIndentation } = this
        try {
          const markdown = new ExportMarkdown([figureBlock], listIndentation, isGitlabCompatibilityEnabled).generate()
          remote.setClipboard({ type: 'text/html', data: '' })
          remote.setClipboard({ type: 'text/plain', data: markdown })
        } catch (e) {
          console.log(e)
        }
      }
    }
  }

  ContentState.prototype.copyHandler = function (type, copyInfo = null) {
    if (this.selectedTableCells) {
      // Hand over to docCopyHandler
      return
    }
    const { selectedImage } = this
    if (selectedImage) {
      // The picture itself for Word and mail (its Markdown source as HTML showed as text there), the Markdown as text.
      const { token } = selectedImage
      remote.setClipboard({ type: 'text/html', data: type === 'normal' ? svgImagesAsPng(absoluteImageUrls(marked(token.raw))) : '' })
      remote.setClipboard({ type: 'text/plain', data: token.raw })
      return
    }

    const { html, text, plainText } = this.getClipBoardData()
    switch (type) {
      case 'normal': {
        remote.setClipboard({ type: 'text/html', data: html })
        remote.setClipboard({ type: 'text/plain', data: text })
        break
      }
      case 'copyAsMarkdown': {
        remote.setClipboard({ type: 'text/html', data: '' })
        remote.setClipboard({ type: 'text/plain', data: text })
        break
      }
      case 'copyAsHtml': {
        remote.setClipboard({ type: 'text/html', data: '' })
        remote.setClipboard({
          type: 'text/plain', data: getSanitizeHtml(text, {
            superSubScript: this.muya.options.superSubScript,
            footnote: this.muya.options.footnote,
            isGitlabCompatibilityEnabled: this.muya.options.isGitlabCompatibilityEnabled
          })
        })
        break
      }
      case 'copyAsPlainText': {
        remote.setClipboard({ type: 'text/html', data: '' })
        remote.setClipboard({ type: 'text/plain', data: plainText })
        break
      }

      case 'copyBlock': {
        const block = typeof copyInfo === 'string' ? this.getBlock(copyInfo) : copyInfo
        if (!block) return
        const anchor = this.getAnchor(block)
        const { isGitlabCompatibilityEnabled, listIndentation } = this
        try {
          const markdown = new ExportMarkdown([anchor], listIndentation, isGitlabCompatibilityEnabled).generate()
          remote.setClipboard({ type: 'text/html', data: '' })
          remote.setClipboard({ type: 'text/plain', data: markdown })
        } catch (e) {
          console.log(e)
        }
        break
      }

      case 'copyCodeContent': {
        const codeContent = copyInfo
        if (typeof codeContent !== 'string') {
          return
        }
        remote.setClipboard({ type: 'text/html', data: '' })
        remote.setClipboard({ type: 'text/plain', data: codeContent })
      }
    }
  }
}

export default copyCutCtrl
